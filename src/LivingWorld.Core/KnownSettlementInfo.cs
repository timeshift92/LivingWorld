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
    string Summary);

public readonly record struct SettlementKnowledgeFreshness(
    int AgeTicks,
    int AgeDays,
    bool IsStale);
