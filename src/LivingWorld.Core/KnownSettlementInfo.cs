namespace LivingWorld.Core;

public enum KnowledgeConfidence
{
    Low,
    Medium,
    High,
    Confirmed
}

public enum SettlementPopulationBand
{
    Unknown,
    Tiny,
    Small,
    Medium,
    Large
}

public enum SettlementFoodKnowledge
{
    Unknown,
    Stable,
    Shortage
}

public enum SettlementMigrationKnowledge
{
    Unknown,
    Stable,
    Pressure,
    Refugees
}

public enum SettlementProductionKnowledge
{
    Unknown,
    Poor,
    Adequate,
    Strong
}

public sealed record KnownSettlementInfo(
    EntityId SettlementId,
    IntelSourceKind SourceKind,
    int Tick,
    KnowledgeConfidence Confidence,
    SettlementPopulationBand PopulationBand,
    SettlementFoodKnowledge Food,
    SettlementMigrationKnowledge Migration,
    SettlementProductionKnowledge Production,
    bool ExactValuesVisible,
    string Summary)
{
    /// <summary>
    /// Immutable values observed when this report was created. A direct visit may reveal this
    /// snapshot while it is fresh; it must never be replaced with live ledger reads by a UI.
    /// Older saves can legitimately have no snapshot even when ExactValuesVisible was persisted.
    /// </summary>
    public SettlementKnowledgeSnapshot? ExactSnapshot { get; init; }
}

public sealed record SettlementKnowledgeSnapshot(
    int Population,
    int Children,
    int Adults,
    int Elderly,
    int FoodStock,
    int DailyFoodNeed,
    int FoodDays,
    int AdultWorkers,
    int FoodPerDay,
    int SteelPerDay,
    int MedicinePerDay,
    int ComponentsPerDay,
    string Biome,
    string Hilliness,
    string TechLevel,
    int Wealth,
    SettlementTier Tier,
    int FacilityCount,
    int AnimalCount,
    int ActiveProjectCount,
    int RecentPopulationDelta,
    int RecentMigrationEvents);

public readonly record struct SettlementKnowledgeFreshness(
    int AgeTicks,
    int AgeDays,
    bool IsStale);
