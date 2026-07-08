namespace LivingWorld.Core;

public sealed record AnimalBreedingDriverRequest(
    int Tick,
    string FeedResourceKey,
    string MedicineResourceKey,
    string ComponentResourceKey,
    int SelectionDurationTicks = 60_000,
    int IncubationDurationTicks = 180_000,
    int SelectionFeedCost = 12,
    int SelectionMedicineCost = 1,
    int SelectionComponentCost = 1,
    int IncubationFeedCost = 20,
    int IncubationMedicineCost = 2,
    int IncubationComponentCost = 2,
    int MaxProjectsStartedPerDay = 1);

public sealed record AnimalBreedingDriverResult(
    int ProjectsCompleted,
    int ProjectsStarted);

public static class AnimalBreedingDriver
{
    public static AnimalBreedingDriverResult SimulateDay(WorldState state, AnimalBreedingDriverRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.Tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Animal breeding driver tick cannot be negative.");
        }

        ThrowIfEmpty(request.FeedResourceKey, nameof(request.FeedResourceKey));
        ThrowIfEmpty(request.MedicineResourceKey, nameof(request.MedicineResourceKey));
        ThrowIfEmpty(request.ComponentResourceKey, nameof(request.ComponentResourceKey));

        var completed = AnimalBreedingService.CompleteReadyProjects(state, request.Tick).CompletedProjects;
        if (completed > 0 || request.MaxProjectsStartedPerDay <= 0)
        {
            return new AnimalBreedingDriverResult(completed, 0);
        }

        var started = 0;
        foreach (var settlement in state.Settlements
            .Where(settlement => settlement.IsActive)
            .OrderBy(settlement => settlement.Id.Value))
        {
            if (started >= request.MaxProjectsStartedPerDay)
            {
                break;
            }

            if (state.AnimalBreedingProjects.Any(project =>
                project.SettlementId == settlement.Id
                && project.Status == AnimalBreedingProjectStatus.Active))
            {
                continue;
            }

            var status = state.GetSettlementCapabilityStatus(settlement.Id);
            if (!status.CanSupportAnimalProgram)
            {
                continue;
            }

            var cohort = state.GetAnimalCohorts(settlement.Id)
                .Where(cohort => cohort.Type == AnimalCohortType.Domesticated && cohort.Count >= 2)
                .OrderBy(cohort => Math.Min(cohort.HealthPercent, cohort.FertilityPercent))
                .ThenBy(cohort => cohort.CarryingCapacity - cohort.Count)
                .ThenBy(cohort => cohort.Id.Value)
                .FirstOrDefault();
            if (cohort == null)
            {
                continue;
            }

            var result = TryStartProject(state, request, status, cohort);
            if (result.Status == AnimalBreedingStartStatus.Success)
            {
                started++;
            }
        }

        return new AnimalBreedingDriverResult(completed, started);
    }

    private static AnimalBreedingStartResult TryStartProject(
        WorldState state,
        AnimalBreedingDriverRequest request,
        SettlementCapabilityStatus status,
        WorldAnimalCohort cohort)
    {
        var trait = ChooseTrait(cohort);
        if (status.CanRunBasicLab
            && cohort.HealthPercent >= 80
            && cohort.FertilityPercent >= 80
            && cohort.Count < Math.Max(2, status.AnimalCapacity / 2))
        {
            return AnimalBreedingService.StartIncubationProject(
                state,
                new AnimalBreedingStartRequest(
                    request.Tick,
                    status.SettlementId,
                    cohort.Id,
                    trait,
                    request.IncubationDurationTicks,
                    request.FeedResourceKey,
                    request.IncubationFeedCost,
                    request.MedicineResourceKey,
                    request.IncubationMedicineCost,
                    request.ComponentResourceKey,
                    request.IncubationComponentCost));
        }

        if (cohort.HealthPercent >= 90
            && cohort.FertilityPercent >= 90
            && cohort.CarryingCapacity >= Math.Max(status.AnimalCapacity, cohort.Count + 5))
        {
            return new AnimalBreedingStartResult(
                AnimalBreedingStartStatus.InvalidTarget,
                null,
                "Animal cohort is already strong enough.");
        }

        return AnimalBreedingService.StartSelectionProject(
            state,
            new AnimalBreedingStartRequest(
                request.Tick,
                status.SettlementId,
                cohort.Id,
                trait,
                request.SelectionDurationTicks,
                request.FeedResourceKey,
                request.SelectionFeedCost,
                request.MedicineResourceKey,
                request.SelectionMedicineCost,
                request.ComponentResourceKey,
                request.SelectionComponentCost));
    }

    private static AnimalBreedingTrait ChooseTrait(WorldAnimalCohort cohort)
    {
        if (cohort.HealthPercent <= cohort.FertilityPercent && cohort.HealthPercent < 90)
        {
            return AnimalBreedingTrait.Health;
        }

        if (cohort.FertilityPercent < 90)
        {
            return AnimalBreedingTrait.Fertility;
        }

        return AnimalBreedingTrait.Capacity;
    }

    private static void ThrowIfEmpty(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Animal breeding driver resource key cannot be empty.", parameterName);
        }
    }
}
