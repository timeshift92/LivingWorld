namespace LivingWorld.Core;

public enum SettlementMapLayoutStatus
{
    Success,
    InvalidRequest,
    UnknownSettlement,
    NoFacilities
}

public sealed record SettlementMapLayoutRequest(
    EntityId SettlementId,
    int CenterX,
    int CenterZ,
    int MaxFacilities);

public sealed record SettlementMapFacilityFeature(
    EntityId FacilityId,
    SettlementFacilityKind Kind,
    int Level,
    int ConditionPercent,
    string PrimaryThingDefName,
    int AnchorX,
    int AnchorZ);

public sealed record SettlementMapLayoutResult(
    SettlementMapLayoutStatus Status,
    string Reason,
    IReadOnlyList<SettlementMapFacilityFeature> Facilities);

public static class SettlementMapLayoutService
{
    public static SettlementMapLayoutResult BuildFacilityLayout(
        WorldState state,
        SettlementMapLayoutRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.MaxFacilities <= 0)
        {
            return new SettlementMapLayoutResult(
                SettlementMapLayoutStatus.InvalidRequest,
                "Settlement map layout requires a positive facility cap.",
                Array.Empty<SettlementMapFacilityFeature>());
        }

        var settlement = state.GetSettlement(request.SettlementId);
        if (settlement == null || !settlement.IsActive)
        {
            return new SettlementMapLayoutResult(
                SettlementMapLayoutStatus.UnknownSettlement,
                $"Settlement {request.SettlementId} does not exist or is inactive.",
                Array.Empty<SettlementMapFacilityFeature>());
        }

        var facilities = state.GetSettlementFacilities(request.SettlementId)
            .Where(facility => facility.ConditionPercent > 0)
            .OrderBy(facility => FacilitySortRank(facility.Kind))
            .ThenByDescending(facility => facility.Level)
            .ThenBy(facility => facility.Id.Value)
            .Take(request.MaxFacilities)
            .Select((facility, index) => ToFeature(facility, request.CenterX, request.CenterZ, index))
            .ToList();

        if (facilities.Count == 0)
        {
            return new SettlementMapLayoutResult(
                SettlementMapLayoutStatus.NoFacilities,
                $"Settlement {request.SettlementId} has no active facilities to materialize.",
                Array.Empty<SettlementMapFacilityFeature>());
        }

        return new SettlementMapLayoutResult(
            SettlementMapLayoutStatus.Success,
            $"Prepared {facilities.Count} settlement facility feature(s).",
            facilities);
    }

    private static SettlementMapFacilityFeature ToFeature(
        SettlementFacility facility,
        int centerX,
        int centerZ,
        int index)
    {
        var ring = index / 4 + 1;
        var slot = index % 4;
        var offset = 6 * ring;
        var (dx, dz) = slot switch
        {
            0 => (-offset, -offset),
            1 => (offset, -offset),
            2 => (-offset, offset),
            _ => (offset, offset)
        };

        return new SettlementMapFacilityFeature(
            facility.Id,
            facility.Kind,
            facility.Level,
            Math.Max(0, Math.Min(100, facility.ConditionPercent)),
            PrimaryThingDefName(facility.Kind),
            centerX + dx,
            centerZ + dz);
    }

    private static int FacilitySortRank(SettlementFacilityKind kind)
    {
        return kind switch
        {
            SettlementFacilityKind.Storage => 0,
            SettlementFacilityKind.Workshop => 1,
            SettlementFacilityKind.Clinic => 2,
            SettlementFacilityKind.Farm => 3,
            SettlementFacilityKind.PowerPlant => 4,
            _ => 10
        };
    }

    private static string PrimaryThingDefName(SettlementFacilityKind kind)
    {
        return kind switch
        {
            SettlementFacilityKind.Farm => "HydroponicsBasin",
            SettlementFacilityKind.Workshop => "TableMachining",
            SettlementFacilityKind.Clinic => "HospitalBed",
            SettlementFacilityKind.PowerPlant => "Battery",
            SettlementFacilityKind.Storage => "Shelf",
            _ => "TableShort"
        };
    }
}

public enum AnimalMapFateKind
{
    Returned,
    Dead,
    Missing
}

public enum AnimalMapFateSyncStatus
{
    Success,
    InvalidRequest,
    UnknownCohort
}

public sealed record AnimalMapFateSyncRequest(
    EntityId CohortId,
    string AnimalKind,
    AnimalCohortType Type,
    int Count,
    AnimalMapFateKind Fate,
    int Tick,
    string Reason);

public sealed record AnimalMapFateSyncResult(
    AnimalMapFateSyncStatus Status,
    string Reason,
    int ReturnedCount);

public static class AnimalMapFateSyncService
{
    public static AnimalMapFateSyncResult Resolve(WorldState state, AnimalMapFateSyncRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.CohortId.Kind != EntityKind.Animal
            || request.Count <= 0
            || request.Tick < 0
            || string.IsNullOrWhiteSpace(request.AnimalKind)
            || string.IsNullOrWhiteSpace(request.Reason))
        {
            return new AnimalMapFateSyncResult(
                AnimalMapFateSyncStatus.InvalidRequest,
                "Animal map fate sync requires a cohort, positive count, tick, animal kind and reason.",
                0);
        }

        var cohort = state.GetAnimalCohort(request.CohortId);
        if (cohort == null)
        {
            return new AnimalMapFateSyncResult(
                AnimalMapFateSyncStatus.UnknownCohort,
                $"Animal cohort {request.CohortId} does not exist.",
                0);
        }

        if (request.Fate == AnimalMapFateKind.Returned)
        {
            var returned = AnimalMapMaterializationService.ReturnToCohorts(
                state,
                new[]
                {
                    new MaterializedAnimalStack(
                        request.CohortId,
                        request.AnimalKind,
                        request.Type,
                        request.Count)
                },
                request.Tick,
                request.Reason);

            return new AnimalMapFateSyncResult(
                AnimalMapFateSyncStatus.Success,
                $"Returned {returned} animal(s) to cohort {request.CohortId}.",
                returned);
        }

        state.RecordEvent(
            WorldEventKind.AnimalCohortDeclined,
            request.CohortId,
            $"Animal cohort {request.CohortId} lost {request.Count} {request.AnimalKind} on settlement map: {request.Reason}.");
        return new AnimalMapFateSyncResult(
            AnimalMapFateSyncStatus.Success,
            $"Recorded {request.Count} animal(s) as {request.Fate}.",
            0);
    }
}
