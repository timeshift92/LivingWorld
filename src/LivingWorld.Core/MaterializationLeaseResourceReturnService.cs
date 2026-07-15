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
            if (!returnOwnerId.HasValue)
            {
                ResourceLedgerService.ConsumeResource(
                    state,
                    lease.Id,
                    resource.ResourceKey,
                    resource.Quantity,
                    $"{reason}; no same-faction return settlement survived");
                continue;
            }

            var transfer = state.TransferResource(
                lease.Id,
                returnOwnerId.Value,
                resource.ResourceKey,
                resource.Quantity,
                reason);
            if (transfer.Status != OwnershipTransferStatus.Success)
            {
                throw new InvalidOperationException(
                    $"Could not return {resource.Quantity} {resource.ResourceKey} from materialization lease {lease.Id}: {transfer.Reason}");
            }

            returned.Add(new ResourceStack(returnOwnerId.Value, resource.ResourceKey, resource.Quantity));
        }

        return returned;
    }

    private static EntityId? ResolveReturnOwner(WorldState state, MaterializationLease lease)
    {
        if (!string.IsNullOrWhiteSpace(lease.ReturnFactionId))
        {
            return FactionReturnService.Resolve(state, lease.ReturnFactionId, lease.ReturnOwnerId)?.Id;
        }

        // Legacy saves predate ReturnFactionId. Preserve their previous behavior when the
        // original owner still exists; all newly-created leases use faction-safe routing.
        if (state.OwnerExistsForLedger(lease.ReturnOwnerId))
        {
            return lease.ReturnOwnerId;
        }
        if (state.OwnerExistsForLedger(lease.SourceOwnerId))
        {
            return lease.SourceOwnerId;
        }

        return null;
    }
}
