namespace LivingWorld.Core;

public enum AnimalBreedingProjectKind
{
    Selection,
    Incubation,
}

public enum AnimalBreedingTrait
{
    Health,
    Fertility,
    Capacity,
}

public enum AnimalBreedingProjectStatus
{
    Active,
    Completed,
    Cancelled,
}

public enum AnimalBreedingStartStatus
{
    Success,
    MissingSettlement,
    MissingCohort,
    InvalidTarget,
    InsufficientCapability,
    InsufficientResources,
}

public sealed record AnimalBreedingProject(
    EntityId Id,
    EntityId SettlementId,
    EntityId SourceCohortId,
    AnimalBreedingProjectKind Kind,
    AnimalBreedingTrait Trait,
    AnimalBreedingProjectStatus Status,
    int StartedTick,
    int CompletionTick,
    string FeedResourceKey,
    int FeedCost,
    string MedicineResourceKey,
    int MedicineCost,
    string ComponentResourceKey,
    int ComponentCost);

public sealed record AnimalBreedingStartRequest(
    int Tick,
    EntityId SettlementId,
    EntityId SourceCohortId,
    AnimalBreedingTrait Trait,
    int DurationTicks,
    string FeedResourceKey,
    int FeedCost,
    string MedicineResourceKey,
    int MedicineCost,
    string ComponentResourceKey,
    int ComponentCost);

public sealed record AnimalBreedingStartResult(
    AnimalBreedingStartStatus Status,
    AnimalBreedingProject? Project,
    string Reason);

public sealed record AnimalBreedingCompletionResult(int CompletedProjects);
