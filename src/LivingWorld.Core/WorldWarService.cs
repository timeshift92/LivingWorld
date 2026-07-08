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
    int ResolvedMovementRetentionDays = 30)
{
    public string DevelopmentFoodResourceKey { get; init; } = "PackagedSurvivalMeal";

    public string DevelopmentSilverResourceKey { get; init; } = "Silver";

    public int DevelopmentHousingHeadroom { get; init; } = 12;

    public int DevelopmentStep { get; init; } = 5;

    public int DevelopmentMaxHousing { get; init; } = 120;

    public int DevelopmentSilverCost { get; init; } = 0;

    public int DevelopmentSpecialistGrowthStep { get; init; } = 0;
}

public sealed record WorldWarResult(
    int PlansConsidered,
    int WarbandsLaunched,
    int BattlesResolved,
    int SettlementsCaptured,
    int ColoniesFounded,
    int CaravansCompleted = 0,
    int ScoutingReports = 0,
    int DiplomaticMissions = 0)
{
    public int DevelopmentsCompleted { get; init; }
}

/// <summary>
/// Runs one day of the ledger world war by orchestrating phase services. It advances movements,
/// resolves arrived battles, dispatches faction action plans to focused executors, then prunes
/// stale resolved movement state. RimWorld integration remains a thin caller around this Core API.
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

        ArmyMovementService.SimulateDay(state, new ArmyMovementRequest(request.Tick));
        var battleResult = ResolveArrivedBattles(state);
        var plans = FactionActionPlanner.PlanDay(state, request.Tick);
        var actionResult = WorldWarActionDispatcher.Execute(state, request, plans);

        ArmyMovementPruneService.Prune(
            state,
            new ArmyMovementPruneRequest(request.Tick, request.ResolvedMovementRetentionDays));

        return new WorldWarResult(
            plans.Count,
            actionResult.WarbandsLaunched,
            battleResult.BattlesResolved,
            battleResult.SettlementsCaptured,
            actionResult.ColoniesFounded,
            actionResult.CaravansCompleted,
            actionResult.ScoutingReports,
            actionResult.DiplomaticMissions)
        {
            DevelopmentsCompleted = actionResult.DevelopmentsCompleted
        };
    }

    private static WorldWarBattlePhaseResult ResolveArrivedBattles(WorldState state)
    {
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

        return new WorldWarBattlePhaseResult(battles, captured);
    }
}

internal sealed record WorldWarBattlePhaseResult(int BattlesResolved, int SettlementsCaptured);
