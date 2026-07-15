namespace LivingWorld.Core;

public enum MaterializationLeaseStatus
{
    Success,
    InvalidRequest,
    UnknownOwner,
    InsufficientCitizens
}

public enum MaterializationLeaseBindStatus
{
    Success,
    UnknownLease,
    AlreadyResolved,
    InvalidPawn,
    PawnAlreadyLeased
}

public enum MaterializationLeaseResolveStatus
{
    Success,
    UnknownLease,
    AlreadyResolved,
    InvalidRequest
}

public sealed record MaterializationLeaseRequest(
    EntityId SourceOwnerId,
    EntityId ReturnOwnerId,
    MaterializationPurpose Purpose,
    string PurposeKey,
    int Count,
    int LifetimeTicks);

public sealed record MaterializationLeaseResult(
    MaterializationLeaseStatus Status,
    string Reason,
    IReadOnlyList<MaterializationLease> Leases);

public sealed record MaterializationLeaseBindResult(
    MaterializationLeaseBindStatus Status,
    string Reason,
    MaterializationLease? Lease);

public sealed record MaterializationLeaseResolveRequest(
    EntityId LeaseId,
    PawnFateKind Fate,
    string Reason);

public sealed record MaterializationLeaseResolveResult(
    MaterializationLeaseResolveStatus Status,
    string Reason,
    MaterializationLease? Lease);

public sealed record MaterializationLeaseReleaseResult(
    MaterializationLeaseResolveStatus Status,
    string Reason,
    MaterializationLease? Lease,
    IReadOnlyList<ResourceStack> ReturnedResources);

public static class MaterializationLeaseService
{
    public static int ReleaseExpiredLeases(WorldState state, int currentTick)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        state.AdvanceToTick(currentTick);
        var expired = state.MaterializationLeases
            .Where(lease =>
                lease.Lifecycle == MaterializationLeaseLifecycle.Reserved
                && lease.ExpiresTick < currentTick)
            .OrderBy(lease => lease.Id.Value)
            .ToList();

        foreach (var lease in expired)
        {
            Release(state, lease.Id, "materialization reservation expired before a pawn was bound");
        }

        return expired.Count;
    }

    public static MaterializationLeaseResult CreateLeases(WorldState state, MaterializationLeaseRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.Count <= 0 || request.LifetimeTicks <= 0 || string.IsNullOrWhiteSpace(request.PurposeKey))
        {
            return new MaterializationLeaseResult(
                MaterializationLeaseStatus.InvalidRequest,
                "Materialization lease request must include a positive count, lifetime and purpose key.",
                Array.Empty<MaterializationLease>());
        }

        if (!state.OwnerExistsForLedger(request.SourceOwnerId) || !state.OwnerExistsForLedger(request.ReturnOwnerId))
        {
            return new MaterializationLeaseResult(
                MaterializationLeaseStatus.UnknownOwner,
                "Source and return owners must exist in the ledger.",
                Array.Empty<MaterializationLease>());
        }

        var activeCitizenIds = new HashSet<EntityId>(
            state.MaterializationLeases
                .Where(lease => lease.IsActive)
                .Select(lease => lease.CitizenId));
        var candidates = state.Citizens
            .Where(citizen =>
                citizen.Status == CitizenStatus.Alive
                && state.GetOwner(citizen.Id) == request.SourceOwnerId
                && !activeCitizenIds.Contains(citizen.Id))
            .OrderBy(citizen => citizen.Id.Value)
            .Take(request.Count)
            .ToList();

        if (candidates.Count < request.Count)
        {
            return new MaterializationLeaseResult(
                MaterializationLeaseStatus.InsufficientCitizens,
                $"Only {candidates.Count} citizens are available to lease from {request.SourceOwnerId}.",
                Array.Empty<MaterializationLease>());
        }

        var leases = candidates
            .Select(citizen => state.CreateMaterializationLease(
                citizen.Id,
                request.SourceOwnerId,
                request.ReturnOwnerId,
                request.Purpose,
                request.PurposeKey,
                request.LifetimeTicks))
            .ToList();

        return new MaterializationLeaseResult(
            MaterializationLeaseStatus.Success,
            $"Created {leases.Count} materialization lease(s).",
            leases);
    }

    public static MaterializationLeaseBindResult BindPawn(WorldState state, EntityId leaseId, int pawnThingId)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (pawnThingId <= 0)
        {
            return new MaterializationLeaseBindResult(
                MaterializationLeaseBindStatus.InvalidPawn,
                "Pawn thing id must be positive.",
                null);
        }

        var lease = state.GetMaterializationLease(leaseId);
        if (lease == null)
        {
            return new MaterializationLeaseBindResult(
                MaterializationLeaseBindStatus.UnknownLease,
                $"Materialization lease {leaseId} does not exist.",
                null);
        }

        if (!lease.IsActive)
        {
            return new MaterializationLeaseBindResult(
                MaterializationLeaseBindStatus.AlreadyResolved,
                $"Materialization lease {leaseId} is already {lease.Lifecycle}.",
                lease);
        }

        if (state.MaterializationLeases.Any(existing =>
            existing.Id != leaseId
            && existing.IsActive
            && existing.PawnThingId == pawnThingId))
        {
            return new MaterializationLeaseBindResult(
                MaterializationLeaseBindStatus.PawnAlreadyLeased,
                $"Pawn {pawnThingId} is already bound to an active materialization lease.",
                lease);
        }

        var updated = state.BindMaterializationLeasePawn(leaseId, pawnThingId);
        return new MaterializationLeaseBindResult(
            MaterializationLeaseBindStatus.Success,
            $"Pawn {pawnThingId} bound to materialization lease {leaseId}.",
            updated);
    }

    public static MaterializationLeaseResolveResult Resolve(WorldState state, MaterializationLeaseResolveRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return new MaterializationLeaseResolveResult(
                MaterializationLeaseResolveStatus.InvalidRequest,
                "Materialization lease resolve reason cannot be empty.",
                null);
        }

        var lease = state.GetMaterializationLease(request.LeaseId);
        if (lease == null)
        {
            return new MaterializationLeaseResolveResult(
                MaterializationLeaseResolveStatus.UnknownLease,
                $"Materialization lease {request.LeaseId} does not exist.",
                null);
        }

        if (!lease.IsActive)
        {
            return new MaterializationLeaseResolveResult(
                MaterializationLeaseResolveStatus.AlreadyResolved,
                $"Materialization lease {request.LeaseId} is already {lease.Lifecycle}.",
                lease);
        }

        var updated = state.ResolveMaterializationLease(request.LeaseId, request.Fate, request.Reason);
        return new MaterializationLeaseResolveResult(
            MaterializationLeaseResolveStatus.Success,
            $"Materialization lease {request.LeaseId} resolved as {updated.Lifecycle}.",
            updated);
    }

    public static MaterializationLeaseReleaseResult Release(
        WorldState state,
        EntityId leaseId,
        string reason)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return new MaterializationLeaseReleaseResult(
                MaterializationLeaseResolveStatus.InvalidRequest,
                "Materialization lease release reason cannot be empty.",
                null,
                Array.Empty<ResourceStack>());
        }

        var lease = state.GetMaterializationLease(leaseId);
        if (lease == null)
        {
            return new MaterializationLeaseReleaseResult(
                MaterializationLeaseResolveStatus.UnknownLease,
                $"Materialization lease {leaseId} does not exist.",
                null,
                Array.Empty<ResourceStack>());
        }

        if (!lease.IsActive)
        {
            return new MaterializationLeaseReleaseResult(
                MaterializationLeaseResolveStatus.AlreadyResolved,
                $"Materialization lease {leaseId} is already {lease.Lifecycle}.",
                lease,
                Array.Empty<ResourceStack>());
        }

        var returnedResources = MaterializationLeaseResourceReturnService.ReturnOwnedResources(
            state,
            lease,
            reason);
        var released = state.ReleaseMaterializationLease(leaseId);

        return new MaterializationLeaseReleaseResult(
            MaterializationLeaseResolveStatus.Success,
            $"Materialization lease {leaseId} released.",
            released,
            returnedResources);
    }
}
