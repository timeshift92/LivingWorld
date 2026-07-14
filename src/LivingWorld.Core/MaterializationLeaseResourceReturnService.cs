namespace LivingWorld.Core;

internal static class MaterializationLeaseResourceReturnService
{
    public static IReadOnlyList<ResourceStack> ReturnOwnedResources(
        WorldState state,
        MaterializationLease lease,
        string reason)
    {
        var resources = ResourceLedgerService.GetResources(state, lease.Id)
            .Where(resource => resource.Quantity > 0)
            .OrderBy(resource => resource.ResourceKey, StringComparer.Ordinal)
            .ToList();
        if (resources.Count == 0)
        {
            return Array.Empty<ResourceStack>();
        }

        var returnOwnerId = ResolveReturnOwner(state, lease);
        var returned = new List<ResourceStack>(resources.Count);
        foreach (var resource in resources)
        {
            var transfer = state.TransferResource(
                lease.Id,
                returnOwnerId,
                resource.ResourceKey,
                resource.Quantity,
                reason);
            if (transfer.Status != OwnershipTransferStatus.Success)
            {
                throw new InvalidOperationException(
                    $"Could not return {resource.Quantity} {resource.ResourceKey} from materialization lease {lease.Id}: {transfer.Reason}");
            }

            returned.Add(new ResourceStack(returnOwnerId, resource.ResourceKey, resource.Quantity));
        }

        return returned;
    }

    private static EntityId ResolveReturnOwner(WorldState state, MaterializationLease lease)
    {
        if (state.OwnerExistsForLedger(lease.ReturnOwnerId))
        {
            return lease.ReturnOwnerId;
        }

        if (state.OwnerExistsForLedger(lease.SourceOwnerId))
        {
            return lease.SourceOwnerId;
        }

        throw new InvalidOperationException(
            $"Materialization lease {lease.Id} has no valid return owner; resources remain owned by the active lease.");
    }
}
