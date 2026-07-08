namespace LivingWorld.Core;

public enum AnimalCohortType
{
    Wild,
    Domesticated,
}

public sealed record WorldAnimalCohort(
    EntityId Id,
    EntityId OwnerId,
    string AnimalKind,
    AnimalCohortType Type,
    int Count,
    int HealthPercent,
    int FertilityPercent,
    int CarryingCapacity,
    int LastUpdatedTick);
