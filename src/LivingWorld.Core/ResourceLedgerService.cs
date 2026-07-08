namespace LivingWorld.Core;

public static class ResourceLedgerService
{
    public static void AddResource(WorldState state, EntityId ownerId, string resourceKey, int quantity)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        ThrowIfNullOrWhiteSpace(resourceKey, nameof(resourceKey));

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Resource quantity must be positive.");
        }

        state.EnsureOwnerExistsForLedger(ownerId);

        var current = state.GetResourceQuantityForLedger(ownerId, resourceKey);
        state.SetResourceQuantityForLedger(ownerId, resourceKey, current + quantity);

        state.RecordEvent(WorldEventKind.ResourceAdded, ownerId, $"{quantity} {resourceKey} added to {ownerId}.");
    }

    public static void AddResource(WorldState state, EntityId ownerId, WorldResourceKey resourceKey, int quantity)
    {
        if (resourceKey == null)
        {
            throw new ArgumentNullException(nameof(resourceKey));
        }

        AddResource(state, ownerId, resourceKey.DefName, quantity);
    }

    public static int GetQuantity(WorldState state, EntityId ownerId, string resourceKey)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        ThrowIfNullOrWhiteSpace(resourceKey, nameof(resourceKey));

        return state.GetResourceQuantityForLedger(ownerId, resourceKey);
    }

    public static int GetQuantity(WorldState state, EntityId ownerId, WorldResourceKey resourceKey)
    {
        if (resourceKey == null)
        {
            throw new ArgumentNullException(nameof(resourceKey));
        }

        return GetQuantity(state, ownerId, resourceKey.DefName);
    }

    public static IReadOnlyList<ResourceStack> GetResources(WorldState state, EntityId ownerId)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        return state.ResourcesForOwner(ownerId);
    }

    public static int ConsumeResource(
        WorldState state,
        EntityId ownerId,
        string resourceKey,
        int requestedQuantity,
        string reason)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        ThrowIfNullOrWhiteSpace(resourceKey, nameof(resourceKey));
        ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (requestedQuantity <= 0)
        {
            return 0;
        }

        state.EnsureOwnerExistsForLedger(ownerId);

        var available = state.GetResourceQuantityForLedger(ownerId, resourceKey);
        var consumed = Math.Min(available, requestedQuantity);
        if (consumed == 0)
        {
            return 0;
        }

        state.SetResourceQuantityForLedger(ownerId, resourceKey, available - consumed);
        state.RecordEvent(
            WorldEventKind.ResourceConsumed,
            ownerId,
            $"{consumed} {resourceKey} consumed by {ownerId}: {reason}.");

        return consumed;
    }

    public static int ConsumeResource(
        WorldState state,
        EntityId ownerId,
        WorldResourceKey resourceKey,
        int requestedQuantity,
        string reason)
    {
        if (resourceKey == null)
        {
            throw new ArgumentNullException(nameof(resourceKey));
        }

        return ConsumeResource(state, ownerId, resourceKey.DefName, requestedQuantity, reason);
    }

    internal static void TransferQuantity(
        WorldState state,
        EntityId fromOwnerId,
        EntityId toOwnerId,
        string resourceKey,
        int quantity)
    {
        var available = state.GetResourceQuantityForLedger(fromOwnerId, resourceKey);
        state.SetResourceQuantityForLedger(fromOwnerId, resourceKey, available - quantity);
        var targetCurrent = state.GetResourceQuantityForLedger(toOwnerId, resourceKey);
        state.SetResourceQuantityForLedger(toOwnerId, resourceKey, targetCurrent + quantity);
    }

    private static void ThrowIfNullOrWhiteSpace(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", parameterName);
        }
    }
}
