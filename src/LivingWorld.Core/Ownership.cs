namespace LivingWorld.Core;

public enum OwnershipTransferStatus
{
    Success,
    InvalidQuantity,
    UnknownOwner,
    OwnerMismatch,
    InsufficientOwnedAssets
}

public sealed record OwnershipTransferResult(
    OwnershipTransferStatus Status,
    string Reason)
{
    public static OwnershipTransferResult Completed(string reason)
    {
        return new OwnershipTransferResult(OwnershipTransferStatus.Success, reason);
    }
}

public sealed record ResourceStack(
    EntityId OwnerId,
    string ResourceKey,
    int Quantity);

public sealed record OwnershipRecord(
    EntityId AssetId,
    EntityId OwnerId);
