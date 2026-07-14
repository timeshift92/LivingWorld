namespace LivingWorld.Core;

public sealed record SettlementTradeStockRequest(
    string ResourceKey,
    int RequestedQuantity,
    SettlementTradeDirection Direction,
    bool RequireLedgerStock);

public sealed record SettlementTradeStockAllocation(
    string ResourceKey,
    int RequestedQuantity,
    int AllowedQuantity,
    SettlementTradeDirection Direction,
    bool LedgerBacked,
    int AvailableBefore);

/// <summary>
/// Produces a non-mutating stock plan for a vanilla trade. The caller can clamp the vanilla deal
/// before it transfers any Things, then apply only the final successful quantities to the ledger.
/// </summary>
public static class SettlementTradeReconciliationService
{
    public static bool IsLedgerTrackedResource(
        WorldState state,
        EntityId settlementId,
        string resourceKey)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return false;
        }

        if (state.GetOwnedResourceQuantity(settlementId, resourceKey) > 0)
        {
            return true;
        }

        return state.Events.Any(worldEvent =>
            worldEvent.SubjectId == settlementId
            && worldEvent.Kind is WorldEventKind.ResourceAdded
                or WorldEventKind.ResourceConsumed
                or WorldEventKind.SettlementTradeRecorded
            && worldEvent.Summary.IndexOf(resourceKey, StringComparison.Ordinal) >= 0);
    }

    public static IReadOnlyList<SettlementTradeStockAllocation> Plan(
        WorldState state,
        EntityId settlementId,
        IReadOnlyList<SettlementTradeStockRequest> requests)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (requests == null)
        {
            throw new ArgumentNullException(nameof(requests));
        }

        var settlement = state.GetSettlement(settlementId);
        if (settlement is not { IsActive: true })
        {
            return Array.Empty<SettlementTradeStockAllocation>();
        }

        var remaining = new Dictionary<string, int>(StringComparer.Ordinal);
        var allocations = new List<SettlementTradeStockAllocation>(requests.Count);
        foreach (var request in requests)
        {
            var resourceKey = request.ResourceKey ?? string.Empty;
            if (string.IsNullOrWhiteSpace(resourceKey) || request.RequestedQuantity <= 0)
            {
                allocations.Add(new SettlementTradeStockAllocation(
                    resourceKey,
                    request.RequestedQuantity,
                    0,
                    request.Direction,
                    false,
                    0));
                continue;
            }

            if (request.Direction == SettlementTradeDirection.SettlementReceives)
            {
                allocations.Add(new SettlementTradeStockAllocation(
                    resourceKey,
                    request.RequestedQuantity,
                    request.RequestedQuantity,
                    request.Direction,
                    true,
                    0));
                continue;
            }

            if (!remaining.TryGetValue(resourceKey, out var available))
            {
                available = state.GetOwnedResourceQuantity(settlementId, resourceKey);
            }

            var ledgerBacked = request.RequireLedgerStock || available > 0;
            var allowed = ledgerBacked
                ? Math.Min(available, request.RequestedQuantity)
                : request.RequestedQuantity;
            remaining[resourceKey] = Math.Max(0, available - allowed);

            allocations.Add(new SettlementTradeStockAllocation(
                resourceKey,
                request.RequestedQuantity,
                allowed,
                request.Direction,
                ledgerBacked,
                available));
        }

        return allocations;
    }
}
