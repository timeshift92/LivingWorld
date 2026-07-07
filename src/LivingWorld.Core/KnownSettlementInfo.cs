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

public sealed record KnownSettlementInfo(
    EntityId SettlementId,
    IntelSourceKind SourceKind,
    int Tick,
    KnowledgeConfidence Confidence,
    SettlementPopulationBand PopulationBand,
    SettlementFoodKnowledge Food,
    SettlementMigrationKnowledge Migration,
    bool ExactValuesVisible,
    string Summary);
