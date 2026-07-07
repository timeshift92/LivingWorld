namespace LivingWorld.Core;

public static class PlayerKnowledgeService
{
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

        var info = new KnownSettlementInfo(
            settlement.Id,
            sourceKind,
            state.CurrentTick,
            confidence,
            ToPopulationBand(population.Total),
            food.IsShortage ? SettlementFoodKnowledge.Shortage : SettlementFoodKnowledge.Stable,
            ToMigrationKnowledge(migration),
            exactValuesVisible,
            summary);

        state.RecordKnownSettlementInfo(info);
        return info;
    }

    private static SettlementPopulationBand ToPopulationBand(int population)
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
}
