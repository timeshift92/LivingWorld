using System;
using System.Linq;

namespace LivingWorld.Core;

public sealed record WorldWarRequest(int Tick, int TravelDays, int RaidCombatants, int WarbandCooldownDays = 0);

public sealed record WorldWarResult(
    int PlansConsidered,
    int WarbandsLaunched,
    int BattlesResolved,
    int SettlementsCaptured);

/// <summary>
/// Runs one day of the ledger world war by orchestrating the phase services in order: advance
/// travelling armies, resolve the battles of any that arrived (dropping the aggressor's standing
/// with the defender), then let each faction's <see cref="FactionActionPlanner"/> plan launch a
/// fresh warband — reserving real citizens and dispatching them at the target.
///
/// One warband per faction is in flight at a time, so a warmonger cannot spam limitless armies.
/// Non-warband actions (settle/trade/scout/diplomacy) are planned here but executed by their own
/// existing services in a later integration step. This is the executable heart of the absorption;
/// the RimWorld layer only calls it once per day behind the Rim-War-exclusion flag.
/// </summary>
public static class WorldWarService
{
    public const int AggressionSeverity = 20;

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

            var outcome = WorldBattleService.Resolve(state, movement.ArmyId);
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

        // 3. Let factions plan and launch new warbands (one in flight per faction).
        var launched = 0;
        var plans = FactionActionPlanner.PlanDay(state, request.Tick);
        foreach (var plan in plans)
        {
            if (plan.Action != WarAction.Warband || !plan.TargetSettlementId.HasValue)
            {
                continue;
            }

            if (FactionHasArmyInFlight(state, plan.FactionId)
                || FactionOnWarbandCooldown(state, plan.FactionId, request.Tick, request.WarbandCooldownDays))
            {
                continue;
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
                continue;
            }

            state.DispatchArmy(
                reservation.Army.Id,
                plan.TargetSettlementId.Value,
                request.Tick + (Math.Max(0, request.TravelDays) * 60_000));
            launched++;
        }

        return new WorldWarResult(plans.Count, launched, battles, captured);
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
