namespace LivingWorld.Core;

public enum SettlementResidentMaterializationStatus
{
    Success,
    InvalidRequest,
    UnknownSettlement,
    NoResidents
}

public sealed record SettlementResidentMaterializationRequest(
    EntityId SettlementId,
    int LifetimeTicks,
    string PurposeKey);

public sealed record SettlementResidentMaterializationResult(
    SettlementResidentMaterializationStatus Status,
    string Reason,
    IReadOnlyList<MaterializationLease> Leases);

/// <summary>
/// Leases every currently available resident of a settlement for an active settlement map.
/// The ledger remains authoritative; the leases are the only source for generated resident pawns.
/// </summary>
public static class SettlementResidentMaterializationService
{
    public static SettlementResidentMaterializationResult PrepareResidents(
        WorldState state,
        SettlementResidentMaterializationRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.LifetimeTicks <= 0 || string.IsNullOrWhiteSpace(request.PurposeKey))
        {
            return new SettlementResidentMaterializationResult(
                SettlementResidentMaterializationStatus.InvalidRequest,
                "Settlement resident materialization requires a lifetime and purpose key.",
                Array.Empty<MaterializationLease>());
        }

        var settlement = state.GetSettlement(request.SettlementId);
        if (settlement == null || !settlement.IsActive)
        {
            return new SettlementResidentMaterializationResult(
                SettlementResidentMaterializationStatus.UnknownSettlement,
                $"Settlement {request.SettlementId} does not exist or is inactive.",
                Array.Empty<MaterializationLease>());
        }

        var activeCitizenIds = new HashSet<EntityId>(
            state.MaterializationLeases
                .Where(lease => lease.IsActive)
                .Select(lease => lease.CitizenId));
        var residents = state.Citizens
            .Where(citizen =>
                citizen.Status == CitizenStatus.Alive
                && state.GetOwner(citizen.Id) == request.SettlementId
                && !activeCitizenIds.Contains(citizen.Id))
            .OrderBy(citizen => citizen.Id.Value)
            .ToList();

        if (residents.Count == 0)
        {
            return new SettlementResidentMaterializationResult(
                SettlementResidentMaterializationStatus.NoResidents,
                $"Settlement {request.SettlementId} has no available residents.",
                Array.Empty<MaterializationLease>());
        }

        var leases = residents
            .Select(citizen => state.CreateMaterializationLease(
                citizen.Id,
                request.SettlementId,
                request.SettlementId,
                MaterializationPurpose.SettlementVisit,
                request.PurposeKey,
                request.LifetimeTicks))
            .ToList();

        return new SettlementResidentMaterializationResult(
            SettlementResidentMaterializationStatus.Success,
            $"Prepared {leases.Count} resident(s) from {request.SettlementId}.",
            leases);
    }

    public static int ReleaseUnmaterialized(WorldState state, string purposeKey)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var leases = state.MaterializationLeases
            .Where(lease =>
                lease.Lifecycle == MaterializationLeaseLifecycle.Reserved
                && lease.Purpose == MaterializationPurpose.SettlementVisit
                && string.Equals(lease.PurposeKey, purposeKey, StringComparison.Ordinal))
            .OrderBy(lease => lease.Id.Value)
            .ToList();
        foreach (var lease in leases)
        {
            state.ReleaseMaterializationLease(lease.Id);
        }

        return leases.Count;
    }
}
