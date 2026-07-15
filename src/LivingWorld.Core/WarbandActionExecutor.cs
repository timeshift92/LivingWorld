namespace LivingWorld.Core;

internal static class WarbandActionExecutor
{
    public static ActionAttemptResult Execute(WorldState state, FactionActionPlan plan, WorldWarRequest request)
    {
        if (!plan.TargetSettlementId.HasValue)
        {
            return ActionAttemptResult.Failed(ActionAttemptReason.NoTarget, "warband has no target settlement");
        }

        if (FactionHasArmyInFlight(state, plan.FactionId))
        {
            return ActionAttemptResult.Failed(ActionAttemptReason.AlreadyInFlight, "faction already has a warband in flight");
        }

        if (FactionOnWarbandCooldown(state, plan.FactionId, request.Tick, request.WarbandCooldownDays))
        {
            return ActionAttemptResult.Failed(ActionAttemptReason.Cooldown, "warband cooldown is active");
        }

        var target = state.GetSettlement(plan.TargetSettlementId.Value);
        if (target?.IsActive != true
            || string.Equals(target.FactionId, plan.FactionId, StringComparison.Ordinal))
        {
            return ActionAttemptResult.Failed(ActionAttemptReason.NoTarget, "warband target is unavailable");
        }

        if (ConflictService.IsTruceActive(state, plan.FactionId, target.FactionId, request.Tick))
        {
            return ActionAttemptResult.Failed(ActionAttemptReason.Truce, "an active truce blocks the warband");
        }

        var travelDays = Math.Max(1, request.TravelDays);
        var reservation = RaidPopulationAllocator.ReserveForRaid(
            state,
            new RaidPopulationAllocationRequest(
                plan.FactionId,
                $"{plan.FactionId} warband",
                Math.Max(1, request.RaidCombatants),
                FoodPerCitizen: travelDays,
                TransferFoodToArmy: true,
                EquipmentResourceKey: request.WarbandEquipmentResourceKey,
                EquipmentPerCombatant: Math.Max(0, request.WarbandEquipmentPerCombatant)));

        if (reservation.Army == null)
        {
            var reason = reservation.Status == RaidPopulationAllocationStatus.NoAvailableCombatants
                ? ActionAttemptReason.InsufficientPopulation
                : ActionAttemptReason.InsufficientSupplies;
            return ActionAttemptResult.Failed(reason, reservation.Reason);
        }

        try
        {
            state.DispatchArmy(
                reservation.Army.Id,
                plan.TargetSettlementId.Value,
                request.Tick + (travelDays * 60_000),
                requiresHostileRelation: true,
                supplyResourceKey: "PackagedSurvivalMeal",
                supplyPerCitizenPerDay: 1);
            FactionConflictPolicy.DeclareWar(state, plan.FactionId, target.FactionId, request.Tick);
            ArmyReservationPurposeService.Mark(
                state,
                reservation.Army.Id,
                ArmyReservationPurpose.WorldWarMovement);
        }
        catch
        {
            RaidReconciliationService.ReleaseUndeployedReserves(state, reservation.Army.Id);
            throw;
        }

        return ActionAttemptResult.Success("warband launched with conserved citizens and food");
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
