namespace LivingWorld.Core;

public sealed record AnimalProductionRequest(
    int Tick,
    string FoodResourceKey,
    int RanchOutputPerHealthyAnimal,
    int WildHarvestDivisor,
    int MaxWildAnimalsHarvestedPerCohort);

public sealed record AnimalProductionResult(
    int RanchFoodProduced,
    int HuntingFoodProduced,
    int WildAnimalsHarvested);

public static class AnimalProductionService
{
    public static AnimalProductionResult SimulateDay(WorldState state, AnimalProductionRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.Tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Animal production tick cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(request.FoodResourceKey))
        {
            throw new ArgumentException("Animal production food resource key cannot be empty.", nameof(request));
        }

        state.AdvanceToTick(request.Tick);
        var ranchFood = 0;
        var huntingFood = 0;
        var hunted = 0;

        foreach (var cohort in state.AnimalCohorts
            .Where(cohort => cohort.Count > 0
                && cohort.OwnerId.Kind == EntityKind.Settlement
                && state.GetSettlement(cohort.OwnerId)?.IsActive == true)
            .OrderBy(cohort => cohort.OwnerId.Kind)
            .ThenBy(cohort => cohort.OwnerId.Value)
            .ThenBy(cohort => cohort.AnimalKind, StringComparer.Ordinal)
            .ThenBy(cohort => cohort.Id.Value)
            .ToList())
        {
            if (cohort.Type == AnimalCohortType.Domesticated)
            {
                var produced = CalculateRanchOutput(cohort, request.RanchOutputPerHealthyAnimal);
                if (produced <= 0)
                {
                    continue;
                }

                state.AddResource(cohort.OwnerId, request.FoodResourceKey, produced);
                state.RecordEvent(
                    WorldEventKind.AnimalProductsHarvested,
                    cohort.Id,
                    $"Animal cohort {cohort.Id} produced {produced} {request.FoodResourceKey}.");
                ranchFood += produced;
                continue;
            }

            var harvested = CalculateWildHarvest(cohort, request.WildHarvestDivisor, request.MaxWildAnimalsHarvestedPerCohort);
            if (harvested <= 0)
            {
                continue;
            }

            var food = harvested * 2;
            state.RecordAnimalCohortForSimulation(cohort with
            {
                Count = cohort.Count - harvested,
                LastUpdatedTick = request.Tick
            });
            state.AddResource(cohort.OwnerId, request.FoodResourceKey, food);
            state.RecordEvent(
                WorldEventKind.AnimalHunted,
                cohort.Id,
                $"Animal cohort {cohort.Id} hunted: {harvested} {cohort.AnimalKind} became {food} {request.FoodResourceKey}.");
            hunted += harvested;
            huntingFood += food;
        }

        return new AnimalProductionResult(ranchFood, huntingFood, hunted);
    }

    private static int CalculateRanchOutput(WorldAnimalCohort cohort, int outputPerHealthyAnimal)
    {
        if (outputPerHealthyAnimal <= 0 || cohort.Count < 4 || cohort.HealthPercent < 40)
        {
            return 0;
        }

        return Math.Max(1, cohort.Count * Math.Max(0, cohort.HealthPercent) * outputPerHealthyAnimal / 400);
    }

    private static int CalculateWildHarvest(
        WorldAnimalCohort cohort,
        int harvestDivisor,
        int maxHarvested)
    {
        if (harvestDivisor <= 0 || maxHarvested <= 0 || cohort.HealthPercent < 35)
        {
            return 0;
        }

        var preserve = Math.Max(1, cohort.CarryingCapacity / harvestDivisor);
        var available = cohort.Count - preserve;
        if (available <= 0)
        {
            return 0;
        }

        return Math.Min(maxHarvested, available);
    }
}
