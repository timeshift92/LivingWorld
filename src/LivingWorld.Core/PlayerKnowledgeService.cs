namespace LivingWorld.Core;

public static class PlayerKnowledgeService
{
    private const int TicksPerDay = 60_000;
    public const int ExactIntelStaleAfterTicks = 1_800_000;

    public static KnownSettlementInfo RecordPublicSettlementInfo(
        WorldState state,
        EntityId settlementId,
        string summary)
    {
        return RecordSettlementInfo(
            state,
            settlementId,
            IntelSourceKind.Public,
            KnowledgeConfidence.Medium,
            exactValuesVisible: false,
            summary);
    }

    public static KnownSettlementInfo RecordTraderSettlementInfo(
        WorldState state,
        EntityId settlementId,
        string summary)
    {
        return RecordSettlementInfo(
            state,
            settlementId,
            IntelSourceKind.Trade,
            KnowledgeConfidence.High,
            exactValuesVisible: false,
            summary);
    }

    public static KnownSettlementInfo RecordScoutSettlementInfo(
        WorldState state,
        EntityId settlementId,
        string summary)
    {
        return RecordSettlementInfo(
            state,
            settlementId,
            IntelSourceKind.Scout,
            KnowledgeConfidence.High,
            exactValuesVisible: false,
            summary);
    }

    public static KnownSettlementInfo RecordDirectVisitSettlementInfo(
        WorldState state,
        EntityId settlementId,
        string summary)
    {
        return RecordSettlementInfo(
            state,
            settlementId,
            IntelSourceKind.DirectVisit,
            KnowledgeConfidence.Confirmed,
            exactValuesVisible: true,
            summary);
    }

    public static SettlementKnowledgeFreshness GetFreshness(
        KnownSettlementInfo info,
        int currentTick,
        int staleAfterTicks)
    {
        var ageTicks = Math.Max(0, currentTick - info.Tick);
        return new SettlementKnowledgeFreshness(
            ageTicks,
            ageTicks / TicksPerDay,
            staleAfterTicks > 0 && ageTicks > staleAfterTicks);
    }

    public static bool HasFreshExactSnapshot(KnownSettlementInfo? info, int currentTick)
    {
        return info?.ExactValuesVisible == true
            && info.ExactSnapshot != null
            && !GetFreshness(info, currentTick, ExactIntelStaleAfterTicks).IsStale;
    }

    private static KnownSettlementInfo RecordSettlementInfo(
        WorldState state,
        EntityId settlementId,
        IntelSourceKind sourceKind,
        KnowledgeConfidence confidence,
        bool exactValuesVisible,
        string summary)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Knowledge summary cannot be empty.", nameof(summary));
        }

        var settlement = state.GetSettlement(settlementId)
            ?? throw new InvalidOperationException($"Settlement {settlementId} does not exist.");
        var population = state.GetSettlementPopulation(settlement.Id);
        var food = state.GetSettlementFoodStatus(settlement.Id, "PackagedSurvivalMeal", 1);
        var migration = state.GetSettlementMigrationStatus(settlement.Id, "PackagedSurvivalMeal", 1);
        var production = state.GetSettlementProductionStatus(settlement.Id);

        var info = new KnownSettlementInfo(
            settlement.Id,
            sourceKind,
            state.CurrentTick,
            confidence,
            ToPopulationBand(population.Total),
            food.IsShortage ? SettlementFoodKnowledge.Shortage : SettlementFoodKnowledge.Stable,
            ToMigrationKnowledge(migration),
            ToProductionKnowledge(production),
            exactValuesVisible,
            summary)
        {
            ExactSnapshot = exactValuesVisible
                ? CreateExactSnapshot(state, settlement.Id, population, food, production)
                : null
        };

        state.RecordKnownSettlementInfo(info);
        return state.GetKnownSettlementInfo(settlement.Id) ?? info;
    }

    public static SettlementPopulationBand ToPopulationBand(int population)
    {
        if (population <= 0)
        {
            return SettlementPopulationBand.Unknown;
        }

        if (population < 8)
        {
            return SettlementPopulationBand.Tiny;
        }

        if (population < 20)
        {
            return SettlementPopulationBand.Small;
        }

        if (population < 60)
        {
            return SettlementPopulationBand.Medium;
        }

        return SettlementPopulationBand.Large;
    }

    private static SettlementKnowledgeSnapshot CreateExactSnapshot(
        WorldState state,
        EntityId settlementId,
        SettlementPopulation population,
        SettlementFoodStatus food,
        SettlementProductionStatus production)
    {
        var summary = WorldActivitySummaryService.Summarize(
            state,
            new WorldActivitySummaryRequest(state.CurrentTick, TicksPerDay, settlementId));
        var wealth = state.GetSettlementWealth(settlementId)?.TotalWealth
            ?? state.ResourcesForOwner(settlementId).Sum(resource =>
                resource.Quantity * SettlementWealthService.DefaultPriceBook.PriceOf(resource.ResourceKey));

        return new SettlementKnowledgeSnapshot(
            population.Total,
            population.Children,
            population.Adults,
            population.Elderly,
            food.Food,
            food.DailyNeed,
            food.FoodDays,
            production.AdultWorkers,
            production.FoodPerDay,
            production.SteelPerDay,
            production.MedicinePerDay,
            production.ComponentsPerDay,
            production.Biome,
            production.Hilliness,
            production.TechLevel,
            wealth,
            SettlementDevelopmentService.GetTier(state, settlementId),
            state.GetSettlementFacilities(settlementId).Count,
            state.AnimalCohorts
                .Where(cohort => cohort.OwnerId == settlementId)
                .Sum(cohort => cohort.Count),
            state.SettlementProjects.Count(project =>
                project.SettlementId == settlementId && project.Status == SettlementProjectStatus.Active),
            summary.PopulationDelta,
            summary.MigrationEvents);
    }

    private static SettlementMigrationKnowledge ToMigrationKnowledge(SettlementMigrationStatus migration)
    {
        if (migration.Refugees > 0)
        {
            return SettlementMigrationKnowledge.Refugees;
        }

        if (migration.Pressure >= 50)
        {
            return SettlementMigrationKnowledge.Pressure;
        }

        return SettlementMigrationKnowledge.Stable;
    }

    private static SettlementProductionKnowledge ToProductionKnowledge(SettlementProductionStatus production)
    {
        if (production.AdultWorkers <= 0)
        {
            return SettlementProductionKnowledge.Unknown;
        }

        var output = production.FoodPerDay
            + production.SteelPerDay
            + production.MedicinePerDay
            + production.ComponentsPerDay;
        if (output <= 0)
        {
            return SettlementProductionKnowledge.Poor;
        }

        if (output < 8)
        {
            return SettlementProductionKnowledge.Adequate;
        }

        return SettlementProductionKnowledge.Strong;
    }
}
