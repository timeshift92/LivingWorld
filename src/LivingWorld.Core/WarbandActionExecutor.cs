namespace LivingWorld.Core;

internal static class WarbandActionExecutor
{
    public static bool Execute(WorldState state, FactionActionPlan plan, WorldWarRequest request)
    {
        if (!plan.TargetSettlementId.HasValue
            || FactionHasArmyInFlight(state, plan.FactionId)
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
            plan.TargetSettlementId.Value,
            request.Tick + (Math.Max(0, request.TravelDays) * 60_000));
        return true;
    }

    private static bool FactionHasArmyInFlight(WorldState state, string factionId)
    {
        return state.ArmyMovements.Any(movement =>
            movement.Status == ArmyMovementStatus.Traveling
            && string.Equals(state.GetArmy(movement.ArmyId)?.FactionId, factionId, StringComparison.Ordinal));
    }

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
