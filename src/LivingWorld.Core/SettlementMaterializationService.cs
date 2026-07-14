namespace LivingWorld.Core;

public enum SettlementDefenseMaterializationStatus
{
    Success,
    InvalidRequest,
    UnknownSettlement,
    NoDefenders
}

public sealed record SettlementDefenseMaterializationRequest(
    EntityId SettlementId,
    int RequestedDefenders,
    int LifetimeTicks,
    IReadOnlyDictionary<string, int> RequestedResources,
    string PurposeKey);

public sealed record SettlementDefenseMaterializationResult(
    SettlementDefenseMaterializationStatus Status,
    string Reason,
    IReadOnlyList<MaterializationLease> DefenderLeases,
    IReadOnlyList<ResourceStack> Resources,
    EntityId? ResourceOwnerId);

public sealed record SettlementDefenseAbortResult(
    int ReleasedDefenders,
    IReadOnlyList<ResourceStack> ReturnedResources);

public static class SettlementMaterializationService
{
    public static SettlementDefenseMaterializationResult PrepareDefense(
        WorldState state,
        SettlementDefenseMaterializationRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.RequestedDefenders <= 0
            || request.LifetimeTicks <= 0
            || string.IsNullOrWhiteSpace(request.PurposeKey))
        {
            return new SettlementDefenseMaterializationResult(
                SettlementDefenseMaterializationStatus.InvalidRequest,
                "Settlement defense materialization requires defenders, lifetime and purpose key.",
                Array.Empty<MaterializationLease>(),
                Array.Empty<ResourceStack>(),
                null);
        }

        var settlement = state.GetSettlement(request.SettlementId);
        if (settlement == null || !settlement.IsActive)
        {
            return new SettlementDefenseMaterializationResult(
                SettlementDefenseMaterializationStatus.UnknownSettlement,
                $"Settlement {request.SettlementId} does not exist or is inactive.",
                Array.Empty<MaterializationLease>(),
                Array.Empty<ResourceStack>(),
                null);
        }

        var activeCitizenIds = new HashSet<EntityId>(
            state.MaterializationLeases
                .Where(lease => lease.IsActive)
                .Select(lease => lease.CitizenId));
        var defenders = state.Citizens
            .Where(citizen =>
                citizen.Status == CitizenStatus.Alive
                && citizen.IsAdult
                && state.GetOwner(citizen.Id) == request.SettlementId
                && !activeCitizenIds.Contains(citizen.Id))
            .OrderBy(citizen => DefenseProfessionRank(citizen.Profession))
            .ThenBy(citizen => citizen.Id.Value)
            .Take(request.RequestedDefenders)
            .ToList();

        if (defenders.Count == 0)
        {
            return new SettlementDefenseMaterializationResult(
                SettlementDefenseMaterializationStatus.NoDefenders,
                $"Settlement {request.SettlementId} has no adult defenders available.",
                Array.Empty<MaterializationLease>(),
                Array.Empty<ResourceStack>(),
                null);
        }

        var leases = defenders
            .Select(defender => state.CreateMaterializationLease(
                defender.Id,
                request.SettlementId,
                request.SettlementId,
                MaterializationPurpose.SettlementDefense,
                request.PurposeKey,
                request.LifetimeTicks))
            .ToList();

        var resourceOwnerId = leases[0].Id;
        var resources = ReserveResources(state, request.SettlementId, resourceOwnerId, request.RequestedResources);

        return new SettlementDefenseMaterializationResult(
            SettlementDefenseMaterializationStatus.Success,
            $"Prepared {leases.Count} defender(s) from {request.SettlementId}.",
            leases,
            resources,
            resourceOwnerId);
    }

    public static SettlementDefenseAbortResult AbortDefense(WorldState state, string purposeKey, string reason)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(purposeKey))
        {
            throw new ArgumentException("Purpose key cannot be empty.", nameof(purposeKey));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason cannot be empty.", nameof(reason));
        }

        var activeLeases = state.MaterializationLeases
            .Where(lease =>
                lease.IsActive
                && lease.Purpose == MaterializationPurpose.SettlementDefense
                && string.Equals(lease.PurposeKey, purposeKey, StringComparison.Ordinal))
            .OrderBy(lease => lease.Id.Value)
            .ToList();
        var returned = new List<ResourceStack>();

        foreach (var lease in activeLeases)
        {
            var release = MaterializationLeaseService.Release(state, lease.Id, reason);
            if (release.Status != MaterializationLeaseResolveStatus.Success)
            {
                throw new InvalidOperationException(release.Reason);
            }

            returned.AddRange(release.ReturnedResources);
        }

        return new SettlementDefenseAbortResult(activeLeases.Count, returned);
    }

    private static IReadOnlyList<ResourceStack> ReserveResources(
        WorldState state,
        EntityId settlementId,
        EntityId resourceOwnerId,
        IReadOnlyDictionary<string, int> requestedResources)
    {
        if (requestedResources == null || requestedResources.Count == 0)
        {
            return Array.Empty<ResourceStack>();
        }

        var reserved = new List<ResourceStack>();
        foreach (var pair in requestedResources
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var available = ResourceLedgerService.GetQuantity(state, settlementId, pair.Key);
            var quantity = Math.Min(available, pair.Value);
            if (quantity <= 0)
            {
                continue;
            }

            var transfer = state.TransferResource(
                settlementId,
                resourceOwnerId,
                pair.Key,
                quantity,
                "settlement defense materialization");
            if (transfer.Status != OwnershipTransferStatus.Success)
            {
                throw new InvalidOperationException(transfer.Reason);
            }

            reserved.Add(new ResourceStack(resourceOwnerId, pair.Key, quantity));
        }

        return reserved;
    }

    private static int DefenseProfessionRank(string profession)
    {
        return profession?.ToLowerInvariant() switch
        {
            "soldier" => 0,
            "guard" => 1,
            "warrior" => 2,
            "hunter" => 3,
            _ => 10
        };
    }
}
