using System;
using System.Linq;

namespace LivingWorld.Core;

public sealed record WorldWarRequest(
    int Tick,
    int TravelDays,
    int RaidCombatants,
    int WarbandCooldownDays = 0,
    int SettlerCount = 4,
    string CaravanResourceKey = "Steel",
    int CaravanQuantity = 10,
    int DiplomatGoodwill = 5,
    int ResolvedMovementRetentionDays = 30);

public sealed record WorldWarResult(
    int PlansConsidered,
    int WarbandsLaunched,
    int BattlesResolved,
    int SettlementsCaptured,
    int ColoniesFounded,
    int CaravansCompleted = 0,
    int ScoutingReports = 0,
    int DiplomaticMissions = 0);

/// <summary>
/// Runs one day of the ledger world war by orchestrating the phase services in order: advance
/// travelling armies, resolve the battles of any that arrived (dropping the aggressor's standing
/// with the defender), then let each faction's <see cref="FactionActionPlanner"/> plan launch a
/// fresh action — reserving real citizens for warbands, founding colonies from real adults,
/// moving owned goods by caravan, writing scout intel, or adjusting the diplomacy ledger.
///
/// One warband per faction is in flight at a time, so a warmonger cannot spam limitless armies.
/// This is the executable heart of the absorption; the RimWorld layer only calls it once per day
/// behind the Rim-War-exclusion flag.
/// </summary>
public static class WorldWarService
{
    public const int AggressionSeverity = 20;
    public const int MinSettlersRemaining = 8;

    public static WorldWarResult SimulateDay(WorldState state, WorldWarRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        state.AdvanceToTick(request.Tick);

        // 1. Advance travelling armies so any that reached their ETA are ready to fight.
        ArmyMovementService.SimulateDay(state, new ArmyMovementRequest(request.Tick));

        // 2. Resolve the battles of arrived armies; each attack sours relations.
        var battles = 0;
        var captured = 0;
        foreach (var movement in state.ArmyMovements
            .Where(candidate => candidate.Status == ArmyMovementStatus.Arrived)
            .OrderBy(candidate => candidate.ArmyId.Value)
            .ToList())
        {
            var attackerFaction = state.GetArmy(movement.ArmyId)?.FactionId;
            var defenderFaction = state.GetSettlement(movement.TargetSettlementId)?.FactionId;

            var battle = WorldBattleService.TryResolve(state, movement.ArmyId);
            if (battle.Status == BattleResolutionStatus.BlockedPlayerSettlement)
            {
                continue;
            }

            var outcome = battle.Outcome!;
            battles++;
            if (outcome.Captured)
            {
                captured++;
            }

            if (attackerFaction != null && defenderFaction != null
                && !string.Equals(attackerFaction, defenderFaction, StringComparison.Ordinal))
            {
                DiplomacyService.RecordAggression(state, attackerFaction, defenderFaction, AggressionSeverity);
            }
        }

        // 3. Execute each faction's plan through the ledger. These are still cheap world-level
        // effects; materializing pawns/world objects remains the RimWorld layer's job.
        var launched = 0;
        var founded = 0;
        var caravans = 0;
        var scoutingReports = 0;
        var diplomaticMissions = 0;
        var plans = FactionActionPlanner.PlanDay(state, request.Tick);
        foreach (var plan in plans)
        {
            if (plan.Action == WarAction.Warband && plan.TargetSettlementId.HasValue)
            {
                if (TryLaunchWarband(state, plan, request))
                {
                    launched++;
                }
            }
            else if (plan.Action == WarAction.Settler)
            {
                if (TryFoundColony(state, plan.FactionId, request.SettlerCount))
                {
                    founded++;
                }
            }
            else if (plan.Action == WarAction.Caravan)
            {
                if (TryRunCaravan(state, plan.FactionId, request))
                {
                    caravans++;
                }
            }
            else if (plan.Action == WarAction.ScoutingParty)
            {
                if (TryRunScoutingParty(state, plan.FactionId))
                {
                    scoutingReports++;
                }
            }
            else if (plan.Action == WarAction.Diplomat)
            {
                if (TryRunDiplomat(state, plan.FactionId, request.DiplomatGoodwill))
                {
                    diplomaticMissions++;
                }
            }
        }

        ArmyMovementPruneService.Prune(
            state,
            new ArmyMovementPruneRequest(request.Tick, request.ResolvedMovementRetentionDays));

        return new WorldWarResult(
            plans.Count,
            launched,
            battles,
            captured,
            founded,
            caravans,
            scoutingReports,
            diplomaticMissions);
    }

    private static bool TryLaunchWarband(WorldState state, FactionActionPlan plan, WorldWarRequest request)
    {
        if (FactionHasArmyInFlight(state, plan.FactionId)
            || FactionOnWarbandCooldown(state, plan.FactionId, request.Tick, request.WarbandCooldownDays))
        {
            return false;
        }

        var reservation = RaidPopulationAllocator.ReserveForRaid(
            state,
            new RaidPopulationAllocationRequest(
                plan.FactionId,
                $"{plan.FactionId} warband",
                Math.Max(1, request.RaidCombatants),
                FoodPerCitizen: 0));

        if (reservation.Army == null)
        {
            return false;
        }

        state.DispatchArmy(
            reservation.Army.Id,
            plan.TargetSettlementId!.Value,
            request.Tick + (Math.Max(0, request.TravelDays) * 60_000));
        return true;
    }

    // An expansionist founds a colony from its most populous settlement, but only if that
    // settlement can spare the settlers and still stay viable at home.
    private static bool TryFoundColony(WorldState state, string factionId, int settlerCount)
    {
        var settlers = Math.Max(1, settlerCount);
        var source = state.Settlements
            .Where(settlement => string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal))
            .OrderByDescending(settlement => state.GetSettlementPopulation(settlement.Id).Adults)
            .ThenBy(settlement => settlement.Id.Value)
            .FirstOrDefault();

        if (source == null || state.GetSettlementPopulation(source.Id).Adults < settlers + MinSettlersRemaining)
        {
            return false;
        }

        var ordinal = state.Settlements.Count + 1;
        var slug = $"{factionId}-colony-{ordinal}";
        while (state.Settlements.Any(settlement => string.Equals(settlement.Slug, slug, StringComparison.Ordinal)))
        {
            ordinal++;
            slug = $"{factionId}-colony-{ordinal}";
        }

        state.ExpandSettlement(source.Id, slug, $"{factionId} colony {ordinal}", settlers);
        return true;
    }

    private static bool TryRunCaravan(WorldState state, string factionId, WorldWarRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CaravanResourceKey) || request.CaravanQuantity <= 0)
        {
            return false;
        }

        var source = state.Settlements
            .Where(settlement => string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal))
            .Where(settlement => state.GetSettlementPopulation(settlement.Id).Adults > 0)
            .Where(settlement => state.GetOwnedResourceQuantity(settlement.Id, request.CaravanResourceKey) > 0)
            .OrderByDescending(settlement => state.GetOwnedResourceQuantity(settlement.Id, request.CaravanResourceKey))
            .ThenBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
        var target = FindTradeTarget(state, factionId);
        if (source == null || target == null)
        {
            return false;
        }

        var quantity = Math.Min(
            request.CaravanQuantity,
            state.GetOwnedResourceQuantity(source.Id, request.CaravanResourceKey));
        var transfer = state.TransferResource(
            source.Id,
            target.Id,
            request.CaravanResourceKey,
            quantity,
            $"world-war caravan from {source.Id} to {target.Id}");
        if (transfer.Status != OwnershipTransferStatus.Success)
        {
            return false;
        }

        state.RecordEvent(
            WorldEventKind.SettlementTradeRecorded,
            source.Id,
            $"Caravan from {source.Id} delivered {quantity} {request.CaravanResourceKey} to {target.Id}.");
        return true;
    }

    private static bool TryRunScoutingParty(WorldState state, string factionId)
    {
        var source = FindReadySourceSettlement(state, factionId);
        var target = FindScoutingTarget(state, factionId);
        if (source == null || target == null)
        {
            return false;
        }

        var summary = $"Scouts from {source.Id} surveyed {target.Id}.";
        state.RecordIntelReport(IntelSourceKind.Scout, factionId, valueScore: 100, summary);
        PlayerKnowledgeService.RecordScoutSettlementInfo(state, target.Id, summary);
        return true;
    }

    private static bool TryRunDiplomat(WorldState state, string factionId, int goodwill)
    {
        var source = FindReadySourceSettlement(state, factionId);
        var targetFaction = FindDiplomacyTargetFaction(state, factionId);
        var delta = Math.Abs(goodwill);
        if (source == null || targetFaction == null || delta <= 0)
        {
            return false;
        }

        var before = DiplomacyService.GetGoodwill(state, factionId, targetFaction);
        var after = DiplomacyService.AdjustGoodwill(state, factionId, targetFaction, delta);
        if (after == before)
        {
            return false;
        }

        state.RecordEvent(
            WorldEventKind.DiplomaticMissionSent,
            source.Id,
            $"Diplomats from {source.Id} improved relations between {factionId} and {targetFaction} to {after}.");
        return true;
    }

    private static WorldSettlement? FindReadySourceSettlement(WorldState state, string factionId)
    {
        return state.Settlements
            .Where(settlement => string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal))
            .Where(settlement => state.GetSettlementPopulation(settlement.Id).Adults > 0)
            .OrderBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
    }

    private static WorldSettlement? FindTradeTarget(WorldState state, string factionId)
    {
        return state.Settlements
            .Where(settlement => !string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal))
            .Where(settlement => DiplomacyService.GetStance(state, factionId, settlement.FactionId) != RelationStance.Hostile)
            .OrderBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
    }

    private static WorldSettlement? FindScoutingTarget(WorldState state, string factionId)
    {
        return state.Settlements
            .Where(settlement => !string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal))
            .OrderBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
    }

    private static string? FindDiplomacyTargetFaction(WorldState state, string factionId)
    {
        return state.Settlements
            .OrderBy(settlement => settlement.Id.Value)
            .Select(settlement => settlement.FactionId)
            .Where(targetFaction => !string.Equals(targetFaction, factionId, StringComparison.Ordinal))
            .Where(targetFaction => !state.IsFactionIrreconcilable(targetFaction))
            .Where(targetFaction => !state.IsFactionIrreconcilable(factionId))
            .Distinct(StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool FactionHasArmyInFlight(WorldState state, string factionId)
    {
        return state.ArmyMovements.Any(movement =>
            movement.Status == ArmyMovementStatus.Traveling
            && string.Equals(state.GetArmy(movement.ArmyId)?.FactionId, factionId, StringComparison.Ordinal));
    }

    // After a warband sets out the faction waits out a cooldown before launching another, so a
    // warmonger paces its attacks instead of firing one off every single day.
    private static bool FactionOnWarbandCooldown(WorldState state, string factionId, int tick, int cooldownDays)
    {
        if (cooldownDays <= 0)
        {
            return false;
        }

        var factionMovements = state.ArmyMovements
            .Where(movement => string.Equals(state.GetArmy(movement.ArmyId)?.FactionId, factionId, StringComparison.Ordinal))
            .ToList();

        if (factionMovements.Count == 0)
        {
            return false;
        }

        var lastDepartTick = factionMovements.Max(movement => movement.DepartTick);
        return tick - lastDepartTick < cooldownDays * 60_000;
    }
}
