namespace LivingWorld.Core;

public enum AnimalMapMaterializationStatus
{
    Success,
    InvalidRequest,
    UnknownSettlement,
    NoAnimals
}

public sealed record AnimalMapMaterializationRequest(
    EntityId SettlementId,
    int MaxAnimals,
    int Tick,
    string PurposeKey);

public sealed record MaterializedAnimalStack(
    EntityId CohortId,
    string AnimalKind,
    AnimalCohortType Type,
    int Count);

public sealed record AnimalMapMaterializationResult(
    AnimalMapMaterializationStatus Status,
    string Reason,
    IReadOnlyList<MaterializedAnimalStack> Animals);

public static class AnimalMapMaterializationService
{
    public static AnimalMapMaterializationResult WithdrawForSettlementMap(
        WorldState state,
        AnimalMapMaterializationRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.MaxAnimals <= 0 || request.Tick < 0 || string.IsNullOrWhiteSpace(request.PurposeKey))
        {
            return new AnimalMapMaterializationResult(
                AnimalMapMaterializationStatus.InvalidRequest,
                "Animal map materialization requires a positive animal cap, non-negative tick and purpose key.",
                Array.Empty<MaterializedAnimalStack>());
        }

        var settlement = state.GetSettlement(request.SettlementId);
        if (settlement == null || !settlement.IsActive)
        {
            return new AnimalMapMaterializationResult(
                AnimalMapMaterializationStatus.UnknownSettlement,
                $"Settlement {request.SettlementId} does not exist or is inactive.",
                Array.Empty<MaterializedAnimalStack>());
        }

        var remaining = request.MaxAnimals;
        var withdrawn = new List<MaterializedAnimalStack>();
        foreach (var cohort in state.GetAnimalCohorts(request.SettlementId)
            .Where(cohort => cohort.Count > 0)
            .OrderBy(cohort => cohort.Type == AnimalCohortType.Domesticated ? 0 : 1)
            .ThenBy(cohort => cohort.AnimalKind, StringComparer.Ordinal)
            .ThenBy(cohort => cohort.Id.Value))
        {
            if (remaining <= 0)
            {
                break;
            }

            var count = Math.Min(remaining, cohort.Count);
            state.RecordAnimalCohortForSimulation(cohort with
            {
                Count = cohort.Count - count,
                LastUpdatedTick = request.Tick
            });
            state.RecordEvent(
                WorldEventKind.AnimalCohortDeclined,
                cohort.Id,
                $"Animal cohort {cohort.Id} materialized {count} {cohort.AnimalKind} on settlement map: {request.PurposeKey}.");
            withdrawn.Add(new MaterializedAnimalStack(cohort.Id, cohort.AnimalKind, cohort.Type, count));
            remaining -= count;
        }

        if (withdrawn.Count == 0)
        {
            return new AnimalMapMaterializationResult(
                AnimalMapMaterializationStatus.NoAnimals,
                $"Settlement {request.SettlementId} has no animals available for map materialization.",
                Array.Empty<MaterializedAnimalStack>());
        }

        return new AnimalMapMaterializationResult(
            AnimalMapMaterializationStatus.Success,
            $"Withdrew {withdrawn.Sum(stack => stack.Count)} animal(s) for settlement map.",
            withdrawn);
    }

    public static int ReturnToCohorts(
        WorldState state,
        IReadOnlyList<MaterializedAnimalStack> animals,
        int tick,
        string reason)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (animals == null || animals.Count == 0)
        {
            return 0;
        }

        if (tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tick), "Animal materialization return tick cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Return reason cannot be empty.", nameof(reason));
        }

        var returned = 0;
        foreach (var animal in animals.OrderBy(animal => animal.CohortId.Value))
        {
            if (animal.Count <= 0)
            {
                continue;
            }

            var cohort = state.GetAnimalCohort(animal.CohortId);
            if (cohort == null)
            {
                continue;
            }

            state.RecordAnimalCohortForSimulation(cohort with
            {
                Count = cohort.Count + animal.Count,
                LastUpdatedTick = tick
            });
            state.RecordEvent(
                WorldEventKind.AnimalCohortGrew,
                cohort.Id,
                $"Animal cohort {cohort.Id} returned {animal.Count} {cohort.AnimalKind} from settlement map: {reason}.");
            returned += animal.Count;
        }

        return returned;
    }
}
