namespace LivingWorld.Core;

public enum NamedAnimalRole
{
    Breeding,
    Pack,
    War,
    Pet
}

public enum NamedAnimalStatus
{
    Active,
    Dead,
    Missing,
    Transferred
}

public enum NamedAnimalRegistrationStatus
{
    Success,
    InvalidRequest,
    UnknownCohort
}

public sealed record NamedAnimal(
    EntityId Id,
    EntityId CohortId,
    string Name,
    NamedAnimalRole Role,
    NamedAnimalStatus Status,
    string Note,
    int CreatedTick,
    int LastUpdatedTick);

public sealed record NamedAnimalRegistrationRequest(
    EntityId CohortId,
    string Name,
    NamedAnimalRole Role,
    string Note,
    int Tick);

public sealed record NamedAnimalRegistrationResult(
    NamedAnimalRegistrationStatus Status,
    NamedAnimal? Animal,
    string Reason);

public static class NamedAnimalRegistryService
{
    public static NamedAnimalRegistrationResult Register(WorldState state, NamedAnimalRegistrationRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.CohortId.Value <= 0 || string.IsNullOrWhiteSpace(request.Name))
        {
            return new NamedAnimalRegistrationResult(
                NamedAnimalRegistrationStatus.InvalidRequest,
                null,
                "Named animal registrations require an existing cohort and a non-empty name.");
        }

        if (state.GetAnimalCohort(request.CohortId) == null)
        {
            return new NamedAnimalRegistrationResult(
                NamedAnimalRegistrationStatus.UnknownCohort,
                null,
                $"Animal cohort {request.CohortId} does not exist.");
        }

        var animal = state.CreateNamedAnimal(
            request.CohortId,
            request.Name.Trim(),
            request.Role,
            request.Note.Trim(),
            request.Tick);

        return new NamedAnimalRegistrationResult(
            NamedAnimalRegistrationStatus.Success,
            animal,
            $"Named animal {animal.Name} registered.");
    }
}
