namespace LivingWorld.Core;

public sealed record SettlementDailySimulationRequest(
    int Tick,
    string FoodResourceKey,
    int FoodPerCitizen,
    int BirthIntervalDays);

public sealed record SettlementDailySimulationResult(
    int FoodConsumed,
    int Births,
    int FoodShortages);

public static class SettlementDailySimulationService
{
    private const int TicksPerDay = 60_000;

    public static SettlementDailySimulationResult SimulateDay(
        WorldState state,
        SettlementDailySimulationRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(request.FoodResourceKey))
        {
            throw new ArgumentException("Food resource key cannot be empty.", nameof(request));
        }

        if (request.Tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Simulation tick cannot be negative.");
        }

        var foodPerCitizen = Math.Max(0, request.FoodPerCitizen);
        var birthIntervalDays = Math.Max(1, request.BirthIntervalDays);
        var day = Math.Max(1, request.Tick / TicksPerDay);
        var consumed = 0;
        var births = 0;
        var shortages = 0;

        state.AdvanceToTick(request.Tick);

        foreach (var settlement in state.Settlements.OrderBy(settlement => settlement.Id.Value).ToList())
        {
            var population = state.GetSettlementPopulation(settlement.Id);
            if (population.Total == 0)
            {
                continue;
            }

            if (foodPerCitizen > 0)
            {
                var requestedFood = population.Total * foodPerCitizen;
                var beforeFood = state.GetOwnedResourceQuantity(settlement.Id, request.FoodResourceKey);
                consumed += state.ConsumeResource(
                    settlement.Id,
                    request.FoodResourceKey,
                    requestedFood,
                    "daily settlement consumption");
                if (beforeFood < requestedFood)
                {
                    state.RecordEvent(
                        WorldEventKind.FoodShortage,
                        settlement.Id,
                        $"Settlement {settlement.Id} has food shortage: needed {requestedFood}, available {beforeFood}.");
                    shortages++;
                    continue;
                }
            }

            if (day % birthIntervalDays == 0
                && population.Adults >= 2
                && state.GetOwnedResourceQuantity(settlement.Id, request.FoodResourceKey) >= population.Total)
            {
                var childIndex = state.Citizens.Count(citizen => citizen.SettlementId == settlement.Id) + 1;
                var sex = DeterministicSex(state.WorldSeed, settlement.Id.Value, day, childIndex);
                var child = state.CreateCitizen(
                    $"{settlement.Name} child {childIndex}",
                    0,
                    sex,
                    "child",
                    settlement.Id);
                state.RecordEvent(WorldEventKind.CitizenBorn, child.Id, $"Citizen {child.Id} born in {settlement.Id}.");
                births++;
            }
        }

        return new SettlementDailySimulationResult(consumed, births, shortages);
    }

    private static Sex DeterministicSex(int worldSeed, long settlementId, int day, int childIndex)
    {
        var value = unchecked(worldSeed + ((int)settlementId * 397) + (day * 17) + childIndex);
        return value % 2 == 0 ? Sex.Female : Sex.Male;
    }
}
