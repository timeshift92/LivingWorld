namespace LivingWorld.Core;

public static class OwnershipService
{
    public static OwnershipTransferResult TransferResource(
        WorldState state,
        EntityId fromOwnerId,
        EntityId toOwnerId,
        string resourceKey,
        int quantity,
        string reason)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        ThrowIfNullOrWhiteSpace(resourceKey, nameof(resourceKey));
        ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (quantity <= 0)
        {
            return new OwnershipTransferResult(
                OwnershipTransferStatus.InvalidQuantity,
                "Resource transfer quantity must be positive.");
        }

        if (!state.OwnerExistsForLedger(fromOwnerId))
        {
            return new OwnershipTransferResult(
                OwnershipTransferStatus.UnknownOwner,
                $"Source owner {fromOwnerId} does not exist.");
        }

        if (!state.OwnerExistsForLedger(toOwnerId))
        {
            return new OwnershipTransferResult(
                OwnershipTransferStatus.UnknownOwner,
                $"Target owner {toOwnerId} does not exist.");
        }

        var available = state.GetResourceQuantityForLedger(fromOwnerId, resourceKey);
        if (available < quantity)
        {
            return new OwnershipTransferResult(
                OwnershipTransferStatus.InsufficientOwnedAssets,
                $"Owner {fromOwnerId} has {available} {resourceKey} but transfer requested {quantity}.");
        }

        ResourceLedgerService.TransferQuantity(state, fromOwnerId, toOwnerId, resourceKey, quantity);
        state.RecordEvent(
            WorldEventKind.OwnershipTransferred,
            fromOwnerId,
            $"{quantity} {resourceKey} transferred from {fromOwnerId} to {toOwnerId}: {reason}.");

        return OwnershipTransferResult.Completed(
            $"Transferred {quantity} {resourceKey} from {fromOwnerId} to {toOwnerId}.");
    }

    public static OwnershipTransferResult TransferAsset(
        WorldState state,
        EntityId assetId,
        EntityId fromOwnerId,
        EntityId toOwnerId,
        string reason)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        var currentOwnerId = state.GetOwner(assetId);
        if (!currentOwnerId.HasValue)
        {
            return new OwnershipTransferResult(
                OwnershipTransferStatus.OwnerMismatch,
                $"Asset {assetId} does not have an owner.");
        }

        if (currentOwnerId.Value != fromOwnerId)
        {
            return new OwnershipTransferResult(
                OwnershipTransferStatus.OwnerMismatch,
                $"Asset {assetId} is owned by {currentOwnerId.Value}, not {fromOwnerId}.");
        }

        if (assetId.Kind == EntityKind.Citizen && state.HasActiveMaterializationLease(assetId))
        {
            return new OwnershipTransferResult(
                OwnershipTransferStatus.AssetMaterialized,
                $"Citizen {assetId} has an active materialization lease and cannot change ledger owner.");
        }

        if (!state.OwnerExistsForLedger(toOwnerId))
        {
            return new OwnershipTransferResult(
                OwnershipTransferStatus.UnknownOwner,
                $"Target owner {toOwnerId} does not exist.");
        }

        state.SetOwnerForLedger(assetId, toOwnerId);
        state.RecordEvent(
            WorldEventKind.OwnershipTransferred,
            assetId,
            $"Asset {assetId} transferred from {fromOwnerId} to {toOwnerId}: {reason}.");

        return OwnershipTransferResult.Completed(
            $"Transferred {assetId} from {fromOwnerId} to {toOwnerId}.");
    }

    private static void ThrowIfNullOrWhiteSpace(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", parameterName);
        }
    }
}
