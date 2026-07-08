namespace LivingWorld.Core;

public static class SettlementQueryService
{
    public static SettlementPopulation GetPopulation(WorldState state, EntityId settlementId)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        return state.GetSettlementPopulation(settlementId);
    }

    public static SettlementFoodStatus GetFoodStatus(
        WorldState state,
        EntityId settlementId,
        string foodResourceKey,
        int foodPerCitizen)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        ThrowIfNullOrWhiteSpace(foodResourceKey, nameof(foodResourceKey));

        var population = GetPopulation(state, settlementId);
        var food = state.GetOwnedResourceQuantity(settlementId, foodResourceKey);
        var dailyNeed = population.Total * Math.Max(0, foodPerCitizen);
        var foodDays = dailyNeed > 0
            ? food / dailyNeed
            : 0;

        return new SettlementFoodStatus(
            population.Total,
            dailyNeed,
            food,
            foodDays,
            dailyNeed > 0 && food < dailyNeed);
    }

    public static SettlementMigrationStatus GetMigrationStatus(
        WorldState state,
        EntityId settlementId,
        string foodResourceKey,
        int foodPerCitizen)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        ThrowIfNullOrWhiteSpace(foodResourceKey, nameof(foodResourceKey));

        var food = GetFoodStatus(state, settlementId, foodResourceKey, foodPerCitizen);
        var refugees = state.Citizens.Count(citizen =>
            citizen.SettlementId == settlementId
            && citizen.Status == CitizenStatus.Refugee);
        var pressure = 0;
        var reason = MigrationService.ReasonNone;

        if (food.DailyNeed > 0 && food.FoodDays <= 0)
        {
            pressure += 70;
            reason = MigrationService.ReasonStarvation;
        }
        else if (food.IsShortage)
        {
            pressure += 45;
            reason = MigrationService.ReasonStarvation;
        }

        var population = GetPopulation(state, settlementId);
        if (population.Adults <= 1 && population.Total > 0)
        {
            pressure += 15;
        }

        return new SettlementMigrationStatus(
            Math.Min(100, pressure),
            refugees,
            reason,
            pressure >= 50);
    }

    public static SettlementProductionStatus GetProductionStatus(WorldState state, EntityId settlementId)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var profile = state.GetSettlementProductionProfile(settlementId);
        var population = GetPopulation(state, settlementId);
        if (profile == null || population.Adults <= 0)
        {
            return new SettlementProductionStatus(
                0,
                0,
                0,
                0,
                0,
                "UnknownBiome",
                "UnknownHilliness",
                "UnknownTech");
        }

        return new SettlementProductionStatus(
            population.Adults,
            SettlementFacilityService.ApplyProductionModifier(
                state,
                settlementId,
                SettlementFacilityKind.Farm,
                profile.EffectiveDailyFood(population.Adults)),
            SettlementFacilityService.ApplyProductionModifier(
                state,
                settlementId,
                SettlementFacilityKind.Workshop,
                profile.EffectiveDailySteel(population.Adults)),
            SettlementFacilityService.ApplyProductionModifier(
                state,
                settlementId,
                SettlementFacilityKind.Clinic,
                profile.EffectiveDailyMedicine(population.Adults)),
            SettlementFacilityService.ApplyProductionModifier(
                state,
                settlementId,
                SettlementFacilityKind.Workshop,
                profile.EffectiveDailyComponents(population.Adults)),
            profile.Biome,
            profile.Hilliness,
            profile.TechLevel);
    }

    private static void ThrowIfNullOrWhiteSpace(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", parameterName);
        }
    }
}
