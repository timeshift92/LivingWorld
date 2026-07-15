namespace LivingWorld.Core;

public enum RaidPreparationStatus
{
    Ready,
    Released,
    Launched,
    Failed
}

public sealed record RaidPreparation(
    EntityId Id,
    string FactionId,
    EntityId SourceSettlementId,
    EntityId ArmyId,
    EntityId? IntelFactId,
    RaidIntentReason Reason,
    RaidPreparationStatus Status,
    int ReservedCombatants,
    string SupplyResourceKey,
    int ReservedSupplies,
    int CreatedTick,
    int ExpiresTick,
    string Summary)
{
    public RaidPreparation Release()
    {
        return this with { Status = RaidPreparationStatus.Released };
    }

    public RaidPreparation Launch()
    {
        return this with { Status = RaidPreparationStatus.Launched };
    }
}

public sealed record RaidPreparationRequest(
    RaidIntent Intent,
    string SupplyResourceKey,
    int SupplyPerCombatant,
    int LifetimeTicks);

public static class RaidPreparationService
{
    public static RaidPreparation PrepareRaid(
        WorldState state,
        RaidPreparationRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.Intent == null)
        {
            throw new ArgumentNullException(nameof(request.Intent));
        }

        if (string.IsNullOrWhiteSpace(request.SupplyResourceKey))
        {
            throw new ArgumentException("Supply resource key cannot be empty.", nameof(request));
        }

        var desiredCombatants = Math.Max(1, request.Intent.DesiredCombatants);
        var allocation = RaidPopulationAllocator.ReserveForRaid(
            state,
            new RaidPopulationAllocationRequest(
                request.Intent.FactionId,
                $"Prepared raid: {request.Intent.FactionId}",
                desiredCombatants,
                request.SupplyResourceKey,
                Math.Max(0, request.SupplyPerCombatant)));

        if (allocation.Status != RaidPopulationAllocationStatus.Success || allocation.Army == null || allocation.SourceSettlement == null)
        {
            throw new InvalidOperationException(allocation.Reason);
        }

        if (allocation.ReservedCombatants < desiredCombatants)
        {
            RaidReconciliationService.ReleaseUndeployedReserves(state, allocation.Army.Id);
            throw new InvalidOperationException(
                $"Raid preparation required {desiredCombatants} combatants but only {allocation.ReservedCombatants} could be supplied.");
        }

        var supplies = Math.Max(0, allocation.ReservedCombatants * Math.Max(0, request.SupplyPerCombatant));
        if (supplies > 0)
        {
            var transfer = state.TransferResource(
                allocation.SourceSettlement.Id,
                allocation.Army.Id,
                request.SupplyResourceKey,
                supplies,
                "raid preparation supplies reserved");
            if (transfer.Status != OwnershipTransferStatus.Success)
            {
                RaidReconciliationService.ReleaseUndeployedReserves(state, allocation.Army.Id);
                throw new InvalidOperationException(transfer.Reason);
            }
        }

        return state.CreateRaidPreparation(
            request.Intent.FactionId,
            allocation.SourceSettlement.Id,
            allocation.Army.Id,
            request.Intent.IntelFactId,
            request.Intent.Reason,
            RaidPreparationStatus.Ready,
            allocation.ReservedCombatants,
            request.SupplyResourceKey,
            supplies,
            state.CurrentTick + Math.Max(0, request.LifetimeTicks),
            request.Intent.Summary);
    }

    public static int ReleaseExpiredPreparations(WorldState state, int currentTick)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        state.AdvanceToTick(currentTick);
        var expired = state.RaidPreparations
            .Where(preparation =>
                preparation.Status == RaidPreparationStatus.Ready
                && preparation.ExpiresTick < currentTick)
            .OrderBy(preparation => preparation.Id.Value)
            .ToList();

        foreach (var preparation in expired)
        {
            ReleasePreparation(state, preparation.Id, "expired raid preparation released");
        }

        return expired.Count;
    }

    public static RaidPreparation ReleasePreparation(WorldState state, EntityId preparationId, string reason)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var preparation = state.GetRaidPreparation(preparationId)
            ?? throw new InvalidOperationException($"Raid preparation {preparationId} does not exist.");
        if (preparation.Status != RaidPreparationStatus.Ready)
        {
            return preparation;
        }

        var availableSupplies = state.GetOwnedResourceQuantity(preparation.ArmyId, preparation.SupplyResourceKey);
        var suppliesToReturn = Math.Min(preparation.ReservedSupplies, availableSupplies);
        if (suppliesToReturn > 0)
        {
            state.TransferResource(
                preparation.ArmyId,
                preparation.SourceSettlementId,
                preparation.SupplyResourceKey,
                suppliesToReturn,
                reason);
        }

        RaidReconciliationService.ReleaseUndeployedReserves(state, preparation.ArmyId);
        return state.ReleaseRaidPreparation(preparation.Id);
    }

    public static RaidPreparation LaunchPreparedRaid(WorldState state, EntityId preparationId)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var preparation = state.GetRaidPreparation(preparationId)
            ?? throw new InvalidOperationException($"Raid preparation {preparationId} does not exist.");
        if (preparation.Status != RaidPreparationStatus.Ready)
        {
            return preparation;
        }

        var supplies = Math.Min(
            preparation.ReservedSupplies,
            state.GetOwnedResourceQuantity(preparation.ArmyId, preparation.SupplyResourceKey));
        if (supplies > 0)
        {
            state.ConsumeResource(
                preparation.ArmyId,
                preparation.SupplyResourceKey,
                supplies,
                "raid travel supplies consumed");
        }

        return state.LaunchRaidPreparation(preparation.Id);
    }
}
