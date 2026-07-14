namespace LivingWorld.Core;

public enum TravelingGroupReservationStatus
{
    Success,
    InvalidRequest,
    UnknownSettlement,
    InsufficientCitizens,
    Failed
}

public sealed record TravelingGroupReservationRequest(
    EntityId SourceSettlementId,
    MaterializationPurpose Purpose,
    string PurposeKey,
    int CitizenCount,
    int LifetimeTicks,
    IReadOnlyDictionary<string, int> RequestedResources,
    int RequestedAnimals,
    int Tick);

public sealed record TravelingGroupReservationResult(
    TravelingGroupReservationStatus Status,
    string Reason,
    IReadOnlyList<MaterializationLease> CitizenLeases,
    IReadOnlyList<ResourceStack> Resources,
    IReadOnlyList<MaterializedAnimalStack> Animals,
    EntityId? ResourceOwnerId);

public sealed record TravelingGroupRollbackResult(
    int ReleasedCitizens,
    int ReturnedResources,
    int ReturnedAnimals);

/// <summary>
/// Atomically reserves the physical payload of a neutral group before world travel begins. The first
/// citizen lease owns cargo while the group is in transit; cancellation releases that lease and returns
/// every reserved stack through the normal materialization resource-return path.
/// </summary>
public static class TravelingGroupReservationService
{
    public static TravelingGroupReservationResult Reserve(
        WorldState state,
        TravelingGroupReservationRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.CitizenCount <= 0
            || request.LifetimeTicks <= 0
            || request.Tick < 0
            || string.IsNullOrWhiteSpace(request.PurposeKey))
        {
            return Failed(
                TravelingGroupReservationStatus.InvalidRequest,
                "Traveling group reservation requires citizens, lifetime, tick and purpose key.");
        }

        var settlement = state.GetSettlement(request.SourceSettlementId);
        if (settlement is not { IsActive: true })
        {
            return Failed(
                TravelingGroupReservationStatus.UnknownSettlement,
                $"Settlement {request.SourceSettlementId} does not exist or is inactive.");
        }

        var activeCitizenIds = new HashSet<EntityId>(state.MaterializationLeases
            .Where(lease => lease.IsActive)
            .Select(lease => lease.CitizenId));
        var citizens = state.Citizens
            .Where(citizen => citizen.Status == CitizenStatus.Alive
                && citizen.IsAdult
                && state.GetOwner(citizen.Id) == request.SourceSettlementId
                && !activeCitizenIds.Contains(citizen.Id))
            .OrderBy(citizen => citizen.Id.Value)
            .Take(request.CitizenCount)
            .ToList();
        if (citizens.Count < request.CitizenCount)
        {
            return Failed(
                TravelingGroupReservationStatus.InsufficientCitizens,
                $"Only {citizens.Count} adult citizens are available to travel from {request.SourceSettlementId}.");
        }

        var leases = new List<MaterializationLease>(citizens.Count);
        try
        {
            foreach (var citizen in citizens)
            {
                leases.Add(state.CreateMaterializationLease(
                    citizen.Id,
                    request.SourceSettlementId,
                    request.SourceSettlementId,
                    request.Purpose,
                    request.PurposeKey,
                    request.LifetimeTicks));
            }
        }
        catch (Exception ex)
        {
            foreach (var lease in leases)
            {
                if (state.GetMaterializationLease(lease.Id)?.IsActive == true)
                {
                    MaterializationLeaseService.Release(state, lease.Id, "neutral group citizen reservation failed");
                }
            }

            return Failed(TravelingGroupReservationStatus.Failed, ex.Message);
        }

        var resourceOwnerId = leases[0].Id;
        var reservedResources = new List<ResourceStack>();
        IReadOnlyList<MaterializedAnimalStack> reservedAnimals = Array.Empty<MaterializedAnimalStack>();
        try
        {
            foreach (var pair in (request.RequestedResources ?? new Dictionary<string, int>())
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
                .OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var available = state.GetOwnedResourceQuantity(request.SourceSettlementId, pair.Key);
                var quantity = Math.Min(available, pair.Value);
                if (quantity <= 0)
                {
                    continue;
                }

                var transfer = state.TransferResource(
                    request.SourceSettlementId,
                    resourceOwnerId,
                    pair.Key,
                    quantity,
                    "neutral group cargo reserved before departure");
                if (transfer.Status != OwnershipTransferStatus.Success)
                {
                    throw new InvalidOperationException(transfer.Reason);
                }

                reservedResources.Add(new ResourceStack(resourceOwnerId, pair.Key, quantity));
            }

            if (request.RequestedAnimals > 0)
            {
                var animalResult = AnimalMapMaterializationService.WithdrawForSettlementMap(
                    state,
                    new AnimalMapMaterializationRequest(
                        request.SourceSettlementId,
                        request.RequestedAnimals,
                        request.Tick,
                        request.PurposeKey));
                if (animalResult.Status == AnimalMapMaterializationStatus.Success)
                {
                    reservedAnimals = animalResult.Animals;
                }
                else if (animalResult.Status is not AnimalMapMaterializationStatus.NoAnimals)
                {
                    throw new InvalidOperationException(animalResult.Reason);
                }
            }

            return new TravelingGroupReservationResult(
                TravelingGroupReservationStatus.Success,
                $"Reserved {leases.Count} citizen(s), {reservedResources.Sum(stack => stack.Quantity)} cargo and {reservedAnimals.Sum(stack => stack.Count)} animal(s).",
                leases,
                reservedResources,
                reservedAnimals,
                resourceOwnerId);
        }
        catch (Exception ex)
        {
            if (reservedAnimals.Count > 0)
            {
                AnimalMapMaterializationService.ReturnToCohorts(
                    state,
                    reservedAnimals,
                    request.Tick,
                    "neutral group reservation failed");
            }

            foreach (var lease in leases.OrderBy(lease => lease.Id.Value))
            {
                if (state.GetMaterializationLease(lease.Id)?.IsActive == true)
                {
                    MaterializationLeaseService.Release(state, lease.Id, "neutral group reservation failed");
                }
            }

            return Failed(TravelingGroupReservationStatus.Failed, ex.Message);
        }
    }

    public static TravelingGroupRollbackResult Rollback(
        WorldState state,
        string purposeKey,
        IReadOnlyList<MaterializedAnimalStack> animals,
        int tick,
        string reason)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(purposeKey) || string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Traveling group rollback requires a purpose key and reason.");
        }

        var activeLeases = state.MaterializationLeases
            .Where(lease => lease.IsActive && string.Equals(lease.PurposeKey, purposeKey, StringComparison.Ordinal))
            .OrderBy(lease => lease.Id.Value)
            .ToList();
        var resources = 0;
        foreach (var lease in activeLeases)
        {
            var release = MaterializationLeaseService.Release(state, lease.Id, reason);
            if (release.Status == MaterializationLeaseResolveStatus.Success)
            {
                resources += release.ReturnedResources.Sum(stack => stack.Quantity);
            }
        }

        var returnedAnimals = AnimalMapMaterializationService.ReturnToCohorts(
            state,
            animals ?? Array.Empty<MaterializedAnimalStack>(),
            tick,
            reason);
        return new TravelingGroupRollbackResult(activeLeases.Count, resources, returnedAnimals);
    }

    private static TravelingGroupReservationResult Failed(
        TravelingGroupReservationStatus status,
        string reason)
    {
        return new TravelingGroupReservationResult(
            status,
            reason,
            Array.Empty<MaterializationLease>(),
            Array.Empty<ResourceStack>(),
            Array.Empty<MaterializedAnimalStack>(),
            null);
    }
}
