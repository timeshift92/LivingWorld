namespace LivingWorld.Core;

public sealed record AnimalEcologyDriverRequest(
    int Tick,
    string FeedResourceKey,
    int FeedPerDomesticatedAnimal);

public sealed record AnimalEcologyDriverResult(
    int CohortsSeeded,
    int Births,
    int Deaths);

public static class AnimalEcologyDriver
{
    public static AnimalEcologyDriverResult SimulateDay(
        WorldState state,
        AnimalEcologyDriverRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.Tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Animal ecology driver tick cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(request.FeedResourceKey))
        {
            throw new ArgumentException("Animal feed resource key cannot be empty.", nameof(request));
        }

        var seeded = 0;
        foreach (var profile in state.ProductionProfiles.OrderBy(profile => profile.SettlementId.Value))
        {
            seeded += SeedWildSettlementCohorts(state, profile.SettlementId, request.Tick);
        }

        var ecology = AnimalEcologyService.SimulateDay(
            state,
            new AnimalEcologyRequest(
                request.Tick,
                request.FeedResourceKey,
                Math.Max(0, request.FeedPerDomesticatedAnimal)));

        return new AnimalEcologyDriverResult(seeded, ecology.Births, ecology.Deaths);
    }

    public static int SeedSettlementCohorts(WorldState state, EntityId settlementId, int tick)
    {
        return SeedWildSettlementCohorts(state, settlementId, tick);
    }

    public static int SeedWildSettlementCohorts(WorldState state, EntityId settlementId, int tick)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tick), "Animal seeding tick cannot be negative.");
        }

        var settlement = state.GetSettlement(settlementId);
        var profile = state.GetSettlementProductionProfile(settlementId);
        if (settlement == null || !settlement.IsActive || profile == null)
        {
            return 0;
        }

        var seeded = 0;
        var wildKind = SelectWildKind(profile);
        if (!HasCohort(state, settlementId, wildKind, AnimalCohortType.Wild))
        {
            var carryingCapacity = WildCarryingCapacity(profile);
            if (carryingCapacity > 0)
            {
                state.CreateAnimalCohort(
                    settlementId,
                    wildKind,
                    AnimalCohortType.Wild,
                    Math.Max(1, carryingCapacity / 2),
                    HealthFor(profile),
                    FertilityFor(profile),
                    carryingCapacity,
                    tick);
                seeded++;
            }
        }

        return seeded;
    }

    private static bool HasCohort(
        WorldState state,
        EntityId settlementId,
        string animalKind,
        AnimalCohortType type)
    {
        return state.GetAnimalCohorts(settlementId).Any(cohort =>
            cohort.Type == type
            && string.Equals(cohort.AnimalKind, animalKind, StringComparison.Ordinal));
    }

    private static string SelectWildKind(SettlementProductionProfile profile)
    {
        if (Contains(profile.Biome, "Desert") || Contains(profile.Biome, "Arid"))
        {
            return "Ibex";
        }

        if (Contains(profile.Biome, "Tundra")
            || Contains(profile.Biome, "Boreal")
            || profile.AverageTemperature <= 0)
        {
            return "Caribou";
        }

        return "Deer";
    }

    private static int WildCarryingCapacity(SettlementProductionProfile profile)
    {
        var capacity = 4;
        capacity += Math.Max(0, profile.GrowingDays) / 10;
        capacity += Math.Max(0, profile.Rainfall) / 250;

        if (profile.AverageTemperature < -20 || profile.AverageTemperature > 45)
        {
            capacity -= 3;
        }
        else if (profile.AverageTemperature >= 0 && profile.AverageTemperature <= 30)
        {
            capacity += 2;
        }

        if (Contains(profile.Hilliness, "Hill") || Contains(profile.Hilliness, "Mountain"))
        {
            capacity += 2;
        }

        return Clamp(capacity, 0, 40);
    }

    private static int HealthFor(SettlementProductionProfile profile)
    {
        var value = 70;
        if (profile.FoodPerAdult > 0)
        {
            value += 10;
        }

        if (profile.AverageTemperature < -25 || profile.AverageTemperature > 45)
        {
            value -= 15;
        }

        return Clamp(value, 30, 95);
    }

    private static int FertilityFor(SettlementProductionProfile profile)
    {
        var value = 55 + (profile.GrowingDays / 2) + (profile.Rainfall / 100);
        if (Contains(profile.Biome, "Desert") || Contains(profile.Biome, "Tundra"))
        {
            value -= 10;
        }

        return Clamp(value, 20, 90);
    }

    private static bool Contains(string value, string token)
    {
        return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int Clamp(int value, int min, int max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }
}
