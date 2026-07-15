namespace LivingWorld.Core;

/// <summary>
/// Resolves every terminal world-army path through one conservation boundary. Living citizens and
/// every resource stack move together; when no valid destination exists, citizens become Missing
/// and cargo is explicitly lost instead of remaining on a terminal army owner.
/// </summary>
public static class ArmyTerminalResolutionService
{
    public static void ReturnToFaction(
        WorldState state,
        EntityId armyId,
        ArmyMovementStatus status,
        string reason)
    {
        var army = state.GetArmy(armyId);
        if (army == null)
        {
            if (state.GetArmyMovement(armyId) != null)
            {
                state.SetArmyMovementStatus(armyId, status);
            }

            return;
        }

        var destination = state.GetSettlement(army.SourceSettlementId);
        if (destination?.IsActive != true
            || !string.Equals(destination.FactionId, army.FactionId, StringComparison.Ordinal))
        {
            destination = state.Settlements
                .Where(settlement => settlement.IsActive)
                .Where(settlement => string.Equals(settlement.FactionId, army.FactionId, StringComparison.Ordinal))
                .OrderBy(settlement => settlement.Id.Value)
                .FirstOrDefault();
        }

        if (destination == null)
        {
            LoseArmyAssets(state, armyId, reason);
        }
        else
        {
            TransferArmyAssets(state, armyId, destination.Id, reason);
        }

        ReleaseDeadOwnership(state, armyId);

        state.SetArmyMovementStatus(armyId, status);
    }

    public static void ResolveAtSettlement(
        WorldState state,
        EntityId armyId,
        EntityId settlementId,
        ArmyMovementStatus status,
        string reason)
    {
        var destination = state.GetSettlement(settlementId);
        if (destination?.IsActive != true)
        {
            ReturnToFaction(state, armyId, status, reason);
            return;
        }

        TransferArmyAssets(state, armyId, settlementId, reason);
        ReleaseDeadOwnership(state, armyId);
        state.SetArmyMovementStatus(armyId, status);
    }

    public static void CaptureCargo(WorldState state, EntityId defeatedArmyId, EntityId victorArmyId, string reason)
    {
        foreach (var resource in state.ResourcesForOwner(defeatedArmyId).ToList())
        {
            var transfer = state.TransferResource(
                defeatedArmyId,
                victorArmyId,
                resource.ResourceKey,
                resource.Quantity,
                reason);
            if (transfer.Status != OwnershipTransferStatus.Success)
            {
                throw new InvalidOperationException(transfer.Reason);
            }
        }
    }

    public static int LoseCargo(WorldState state, EntityId armyId, string reason)
    {
        var lost = 0;
        foreach (var resource in state.ResourcesForOwner(armyId).ToList())
        {
            var consumed = state.ConsumeResource(armyId, resource.ResourceKey, resource.Quantity, reason);
            if (consumed != resource.Quantity)
            {
                throw new InvalidOperationException(
                    $"Army {armyId} lost {consumed}/{resource.Quantity} {resource.ResourceKey}.");
            }

            lost += consumed;
        }

        return lost;
    }

    private static void TransferArmyAssets(WorldState state, EntityId armyId, EntityId destinationId, string reason)
    {
        foreach (var citizen in state.GetCitizensOwnedBy(armyId)
            .Where(candidate => candidate.Status == CitizenStatus.Alive)
            .OrderBy(candidate => candidate.Id.Value)
            .ToList())
        {
            var transfer = state.TransferAsset(citizen.Id, armyId, destinationId, reason);
            if (transfer.Status != OwnershipTransferStatus.Success)
            {
                throw new InvalidOperationException(transfer.Reason);
            }

            if (citizen.SettlementId != destinationId)
            {
                state.ReplaceCitizenForSimulation(citizen with { SettlementId = destinationId });
            }
        }

        foreach (var resource in state.ResourcesForOwner(armyId).ToList())
        {
            var transfer = state.TransferResource(
                armyId,
                destinationId,
                resource.ResourceKey,
                resource.Quantity,
                reason);
            if (transfer.Status != OwnershipTransferStatus.Success)
            {
                throw new InvalidOperationException(transfer.Reason);
            }
        }
    }

    private static void LoseArmyAssets(WorldState state, EntityId armyId, string reason)
    {
        foreach (var citizen in state.GetCitizensOwnedBy(armyId)
            .Where(candidate => candidate.Status == CitizenStatus.Alive)
            .OrderBy(candidate => candidate.Id.Value)
            .ToList())
        {
            state.ReplaceCitizenForSimulation(citizen with { Status = CitizenStatus.Missing });
            state.SetOwnerForLedger(citizen.Id, citizen.Id);
            state.RecordEvent(WorldEventKind.RaidPawnMissing, citizen.Id, $"Army member {citizen.Id} missing: {reason}.");
        }

        foreach (var resource in state.ResourcesForOwner(armyId).ToList())
        {
            state.SetResourceQuantityForLedger(armyId, resource.ResourceKey, 0);
        }
    }

    private static void ReleaseDeadOwnership(WorldState state, EntityId armyId)
    {
        foreach (var citizen in state.GetCitizensOwnedBy(armyId)
            .Where(candidate => candidate.Status == CitizenStatus.Dead)
            .OrderBy(candidate => candidate.Id.Value)
            .ToList())
        {
            state.SetOwnerForLedger(citizen.Id, citizen.Id);
        }
    }
}
