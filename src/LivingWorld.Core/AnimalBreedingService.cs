namespace LivingWorld.Core;

public static class AnimalBreedingService
{
    public static AnimalBreedingStartResult StartSelectionProject(
        WorldState state,
        AnimalBreedingStartRequest request)
    {
        return StartProject(state, request, AnimalBreedingProjectKind.Selection);
    }

    public static AnimalBreedingStartResult StartIncubationProject(
        WorldState state,
        AnimalBreedingStartRequest request)
    {
        return StartProject(state, request, AnimalBreedingProjectKind.Incubation);
    }

    public static AnimalBreedingCompletionResult CompleteReadyProjects(WorldState state, int tick)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tick), "Animal breeding completion tick cannot be negative.");
        }

        state.AdvanceToTick(tick);
        var completed = 0;
        foreach (var project in state.AnimalBreedingProjects
            .Where(project =>
                project.Status == AnimalBreedingProjectStatus.Active
                && project.CompletionTick <= state.CurrentTick)
            .OrderBy(project => project.CompletionTick)
            .ThenBy(project => project.Id.Value)
            .ToList())
        {
            var settlement = state.GetSettlement(project.SettlementId);
            var source = state.GetAnimalCohort(project.SourceCohortId);
            if (settlement?.IsActive != true
                || source == null
                || source.OwnerId != settlement.Id)
            {
                state.RecordAnimalBreedingProjectForSimulation(project with { Status = AnimalBreedingProjectStatus.Cancelled });
                continue;
            }

            CompleteProject(state, project);
            completed++;
        }

        return new AnimalBreedingCompletionResult(completed);
    }

    private static AnimalBreedingStartResult StartProject(
        WorldState state,
        AnimalBreedingStartRequest request,
        AnimalBreedingProjectKind kind)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.Tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Animal breeding tick cannot be negative.");
        }

        ThrowIfResourceKeyIsEmpty(request.FeedResourceKey, nameof(request.FeedResourceKey));
        ThrowIfResourceKeyIsEmpty(request.MedicineResourceKey, nameof(request.MedicineResourceKey));
        ThrowIfResourceKeyIsEmpty(request.ComponentResourceKey, nameof(request.ComponentResourceKey));

        state.AdvanceToTick(request.Tick);
        var settlement = state.GetSettlement(request.SettlementId);
        if (settlement?.IsActive != true)
        {
            return new AnimalBreedingStartResult(
                AnimalBreedingStartStatus.MissingSettlement,
                null,
                $"Settlement {request.SettlementId} does not exist.");
        }

        var cohort = state.GetAnimalCohort(request.SourceCohortId);
        if (cohort == null)
        {
            return new AnimalBreedingStartResult(
                AnimalBreedingStartStatus.MissingCohort,
                null,
                $"Animal cohort {request.SourceCohortId} does not exist.");
        }

        if (cohort.OwnerId != settlement.Id || state.GetOwner(cohort.Id) != settlement.Id)
        {
            return new AnimalBreedingStartResult(
                AnimalBreedingStartStatus.InvalidTarget,
                null,
                $"Animal cohort {cohort.Id} is not owned by settlement {settlement.Id}.");
        }

        if (cohort.Type != AnimalCohortType.Domesticated || cohort.Count < 2)
        {
            return new AnimalBreedingStartResult(
                AnimalBreedingStartStatus.InvalidTarget,
                null,
                $"Animal cohort {cohort.Id} cannot support breeding.");
        }

        var capability = state.GetSettlementCapabilityStatus(settlement.Id);
        if (!capability.CanSupportAnimalProgram
            || (kind == AnimalBreedingProjectKind.Incubation && !capability.CanRunBasicLab))
        {
            return new AnimalBreedingStartResult(
                AnimalBreedingStartStatus.InsufficientCapability,
                null,
                $"Settlement {settlement.Id} lacks breeding capability.");
        }

        if (state.GetOwnedResourceQuantity(settlement.Id, request.FeedResourceKey) < Math.Max(0, request.FeedCost)
            || state.GetOwnedResourceQuantity(settlement.Id, request.MedicineResourceKey) < Math.Max(0, request.MedicineCost)
            || state.GetOwnedResourceQuantity(settlement.Id, request.ComponentResourceKey) < Math.Max(0, request.ComponentCost))
        {
            return new AnimalBreedingStartResult(
                AnimalBreedingStartStatus.InsufficientResources,
                null,
                $"Settlement {settlement.Id} lacks breeding project resources.");
        }

        ConsumeCost(state, settlement.Id, request);
        var project = state.CreateAnimalBreedingProject(
            settlement.Id,
            cohort.Id,
            kind,
            request.Trait,
            request.Tick,
            request.Tick + Math.Max(1, request.DurationTicks),
            request.FeedResourceKey,
            request.FeedCost,
            request.MedicineResourceKey,
            request.MedicineCost,
            request.ComponentResourceKey,
            request.ComponentCost);

        return new AnimalBreedingStartResult(
            AnimalBreedingStartStatus.Success,
            project,
            "Animal breeding project started.");
    }

    private static void CompleteProject(WorldState state, AnimalBreedingProject project)
    {
        var cohort = state.GetAnimalCohort(project.SourceCohortId);
        if (cohort != null)
        {
            if (project.Kind == AnimalBreedingProjectKind.Selection)
            {
                state.RecordAnimalCohortForSimulation(ApplyTrait(cohort, project.Trait, state.CurrentTick));
            }
            else
            {
                var improved = ApplyTrait(cohort, project.Trait, state.CurrentTick);
                var incubated = state.CreateAnimalCohort(
                    project.SettlementId,
                    improved.AnimalKind,
                    improved.Type,
                    count: 1,
                    improved.HealthPercent,
                    improved.FertilityPercent,
                    improved.CarryingCapacity,
                    state.CurrentTick);
                state.RecordEvent(
                    WorldEventKind.AnimalCohortIncubated,
                    incubated.Id,
                    $"Animal cohort {incubated.Id} incubated from {cohort.Id}.");
            }
        }

        state.RecordAnimalBreedingProjectForSimulation(project with { Status = AnimalBreedingProjectStatus.Completed });
        state.RecordEvent(
            WorldEventKind.AnimalBreedingProjectCompleted,
            project.Id,
            $"Animal breeding project {project.Id} completed.");
    }

    private static WorldAnimalCohort ApplyTrait(
        WorldAnimalCohort cohort,
        AnimalBreedingTrait trait,
        int tick)
    {
        return trait switch
        {
            AnimalBreedingTrait.Health => cohort with
            {
                HealthPercent = Math.Min(100, cohort.HealthPercent + 10),
                LastUpdatedTick = tick
            },
            AnimalBreedingTrait.Fertility => cohort with
            {
                FertilityPercent = Math.Min(100, cohort.FertilityPercent + 10),
                LastUpdatedTick = tick
            },
            AnimalBreedingTrait.Capacity => cohort with
            {
                CarryingCapacity = cohort.CarryingCapacity + 5,
                LastUpdatedTick = tick
            },
            _ => cohort with { LastUpdatedTick = tick },
        };
    }

    private static void ConsumeCost(
        WorldState state,
        EntityId settlementId,
        AnimalBreedingStartRequest request)
    {
        state.ConsumeResource(settlementId, request.FeedResourceKey, Math.Max(0, request.FeedCost), "animal breeding");
        state.ConsumeResource(settlementId, request.MedicineResourceKey, Math.Max(0, request.MedicineCost), "animal breeding");
        state.ConsumeResource(settlementId, request.ComponentResourceKey, Math.Max(0, request.ComponentCost), "animal breeding");
    }

    private static void ThrowIfResourceKeyIsEmpty(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Animal breeding resource key cannot be empty.", parameterName);
        }
    }
}
