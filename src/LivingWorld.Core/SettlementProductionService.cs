namespace LivingWorld.Core;

public sealed record SettlementProductionEnvironment(
    string Biome,
    string Hilliness,
    string TechLevel,
    int GrowingDays,
    int Rainfall,
    int AverageTemperature);

public enum ProductionArchetype
{
    Balanced,
    Farmer,
    Miner,
    Medical,
    Warrior,
}

public sealed record SettlementProductionProfile(
    EntityId SettlementId,
    string Biome,
    string Hilliness,
    string TechLevel,
    int GrowingDays,
    int Rainfall,
    int AverageTemperature,
    int FoodPerAdult,
    int SteelPerAdult,
    int MedicinePerAdult,
    int ComponentPerAdult)
{
    public ProductionArchetype Archetype { get; init; } = ProductionArchetype.Balanced;

    public int LaborEfficiencyPercent { get; init; } = 100;

    public int EconomyScalePercent { get; init; } = 100;

    public int ComplexityPenaltyPercent { get; init; } = 100;

    public static SettlementProductionProfile FromEnvironment(
        EntityId settlementId,
        SettlementProductionEnvironment environment)
    {
        if (environment == null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        var food = 0;
        if (environment.GrowingDays >= 50)
        {
            food += 2;
        }
        else if (environment.GrowingDays >= 30)
        {
            food += 1;
        }

        if (environment.Rainfall >= 600)
        {
            food += 1;
        }

        if (environment.AverageTemperature >= 10 && environment.AverageTemperature <= 30)
        {
            food += 1;
        }

        food -= HillinessFoodPenalty(environment.Hilliness);
        food = Math.Max(0, food);

        var steel = HillinessSteelScore(environment.Hilliness);
        if (TechScore(environment.TechLevel) >= 3 && steel > 0)
        {
            steel += 1;
        }

        var medicine = food >= 3 && TechScore(environment.TechLevel) >= 3 ? 1 : 0;
        var components = TechScore(environment.TechLevel) >= 3 && steel >= 2 ? 1 : 0;
        if (TechScore(environment.TechLevel) >= 4 && components > 0)
        {
            components += 1;
        }

        return new SettlementProductionProfile(
            settlementId,
            Clean(environment.Biome, "UnknownBiome"),
            Clean(environment.Hilliness, "UnknownHilliness"),
            Clean(environment.TechLevel, "UnknownTech"),
            Math.Max(0, environment.GrowingDays),
            Math.Max(0, environment.Rainfall),
            environment.AverageTemperature,
            food,
            steel,
            medicine,
            components);
    }

    public int TotalDailyOutputPerAdult =>
        FoodPerAdult + SteelPerAdult + MedicinePerAdult + ComponentPerAdult;

    public int EffectiveDailyFood(int adults) =>
        EffectiveOutput(adults, FoodPerAdult, ArchetypeFoodPercent(Archetype));

    public int EffectiveDailySteel(int adults) =>
        EffectiveOutput(adults, SteelPerAdult, ArchetypeSteelPercent(Archetype));

    public int EffectiveDailyMedicine(int adults) =>
        EffectiveOutput(adults, MedicinePerAdult, ArchetypeMedicinePercent(Archetype));

    public int EffectiveDailyComponents(int adults) =>
        EffectiveOutput(adults, ComponentPerAdult, ArchetypeComponentPercent(Archetype));

    private int EffectiveOutput(int adults, int perAdult, int archetypePercent)
    {
        if (adults <= 0 || perAdult <= 0)
        {
            return 0;
        }

        var value = (long)adults
            * perAdult
            * Math.Max(0, archetypePercent)
            * Math.Max(0, LaborEfficiencyPercent)
            * Math.Max(0, EconomyScalePercent)
            * Math.Max(0, ComplexityPenaltyPercent);
        return (int)(value / 100_000_000L);
    }

    private static int ArchetypeFoodPercent(ProductionArchetype archetype) =>
        archetype switch
        {
            ProductionArchetype.Farmer => 150,
            ProductionArchetype.Miner => 75,
            ProductionArchetype.Medical => 90,
            ProductionArchetype.Warrior => 80,
            _ => 100,
        };

    private static int ArchetypeSteelPercent(ProductionArchetype archetype) =>
        archetype switch
        {
            ProductionArchetype.Miner => 150,
            ProductionArchetype.Warrior => 120,
            ProductionArchetype.Farmer => 80,
            _ => 100,
        };

    private static int ArchetypeMedicinePercent(ProductionArchetype archetype) =>
        archetype switch
        {
            ProductionArchetype.Medical => 160,
            ProductionArchetype.Miner => 80,
            ProductionArchetype.Warrior => 80,
            _ => 100,
        };

    private static int ArchetypeComponentPercent(ProductionArchetype archetype) =>
        archetype switch
        {
            ProductionArchetype.Miner => 120,
            ProductionArchetype.Medical => 110,
            ProductionArchetype.Warrior => 90,
            _ => 100,
        };

    private static int HillinessFoodPenalty(string hilliness)
    {
        var value = Clean(hilliness, string.Empty);
        if (ContainsIgnoreCase(value, "Impassable"))
        {
            return 2;
        }

        return ContainsIgnoreCase(value, "Mountain") ? 1 : 0;
    }

    private static int HillinessSteelScore(string hilliness)
    {
        var value = Clean(hilliness, string.Empty);
        if (ContainsIgnoreCase(value, "Impassable")
            || ContainsIgnoreCase(value, "Mountain"))
        {
            return 3;
        }

        if (ContainsIgnoreCase(value, "Large"))
        {
            return 2;
        }

        return ContainsIgnoreCase(value, "Small") ? 1 : 0;
    }

    private static int TechScore(string techLevel)
    {
        var value = Clean(techLevel, string.Empty);
        if (ContainsIgnoreCase(value, "Ultra")
            || ContainsIgnoreCase(value, "Archotech"))
        {
            return 5;
        }

        if (ContainsIgnoreCase(value, "Spacer"))
        {
            return 4;
        }

        if (ContainsIgnoreCase(value, "Industrial"))
        {
            return 3;
        }

        if (ContainsIgnoreCase(value, "Medieval"))
        {
            return 2;
        }

        return ContainsIgnoreCase(value, "Neolithic") ? 1 : 0;
    }

    private static bool ContainsIgnoreCase(string value, string token)
    {
        return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string Clean(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}

public sealed record SettlementProductionRequest(
    int Tick,
    string FoodResourceKey,
    string SteelResourceKey,
    string MedicineResourceKey,
    string ComponentResourceKey);

public sealed record SettlementProductionResult(
    int FoodProduced,
    int SteelProduced,
    int MedicineProduced,
    int ComponentsProduced);

public sealed record SettlementProductionStatus(
    int AdultWorkers,
    int FoodPerDay,
    int SteelPerDay,
    int MedicinePerDay,
    int ComponentsPerDay,
    string Biome,
    string Hilliness,
    string TechLevel);

public static class SettlementProductionService
{
    public static SettlementProductionResult SimulateDay(
        WorldState state,
        SettlementProductionRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        ThrowIfResourceKeyIsEmpty(request.FoodResourceKey, nameof(request.FoodResourceKey));
        ThrowIfResourceKeyIsEmpty(request.SteelResourceKey, nameof(request.SteelResourceKey));
        ThrowIfResourceKeyIsEmpty(request.MedicineResourceKey, nameof(request.MedicineResourceKey));
        ThrowIfResourceKeyIsEmpty(request.ComponentResourceKey, nameof(request.ComponentResourceKey));

        if (request.Tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Production tick cannot be negative.");
        }

        state.AdvanceToTick(request.Tick);

        var food = 0;
        var steel = 0;
        var medicine = 0;
        var components = 0;

        foreach (var profile in state.ProductionProfiles.OrderBy(profile => profile.SettlementId.Value))
        {
            var population = state.GetSettlementPopulation(profile.SettlementId);
            var adults = population.Adults;
            if (adults <= 0)
            {
                continue;
            }

            var producedFood = profile.EffectiveDailyFood(adults);
            var producedSteel = profile.EffectiveDailySteel(adults);
            var producedMedicine = profile.EffectiveDailyMedicine(adults);
            var producedComponents = profile.EffectiveDailyComponents(adults);

            producedFood = SettlementFacilityService.ApplyProductionModifier(
                state,
                profile.SettlementId,
                SettlementFacilityKind.Farm,
                producedFood);
            producedSteel = SettlementFacilityService.ApplyProductionModifier(
                state,
                profile.SettlementId,
                SettlementFacilityKind.Workshop,
                producedSteel);
            producedMedicine = SettlementFacilityService.ApplyProductionModifier(
                state,
                profile.SettlementId,
                SettlementFacilityKind.Clinic,
                producedMedicine);
            producedComponents = SettlementFacilityService.ApplyProductionModifier(
                state,
                profile.SettlementId,
                SettlementFacilityKind.Workshop,
                producedComponents);

            AddProducedResource(state, profile.SettlementId, request.FoodResourceKey, producedFood);
            AddProducedResource(state, profile.SettlementId, request.SteelResourceKey, producedSteel);
            AddProducedResource(state, profile.SettlementId, request.MedicineResourceKey, producedMedicine);
            AddProducedResource(state, profile.SettlementId, request.ComponentResourceKey, producedComponents);

            if (producedFood + producedSteel + producedMedicine + producedComponents > 0)
            {
                state.RecordEvent(
                    WorldEventKind.SettlementProductionUpdated,
                    profile.SettlementId,
                    $"Settlement {profile.SettlementId} produced food {producedFood}, steel {producedSteel}, medicine {producedMedicine}, components {producedComponents}.");
            }

            food += producedFood;
            steel += producedSteel;
            medicine += producedMedicine;
            components += producedComponents;
        }

        return new SettlementProductionResult(food, steel, medicine, components);
    }

    private static void AddProducedResource(
        WorldState state,
        EntityId ownerId,
        string resourceKey,
        int quantity)
    {
        if (quantity > 0)
        {
            state.AddResource(ownerId, resourceKey, quantity);
        }
    }

    private static void ThrowIfResourceKeyIsEmpty(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Production resource key cannot be empty.", parameterName);
        }
    }
}
