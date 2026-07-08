namespace LivingWorld.Core;

public enum RaidIntelTargetKind
{
    PlayerColony,
    Settlement,
    Caravan
}

public enum RaidIntelValueBand
{
    Low,
    Moderate,
    High,
    Extreme
}

public sealed record RaidIntelFact(
    EntityId Id,
    IntelSourceKind SourceKind,
    string FactionId,
    RaidIntelTargetKind TargetKind,
    string TargetKey,
    RaidIntelValueBand ValueBand,
    int Confidence,
    int CreatedTick,
    int ExpiresTick,
    int CombatantDemand,
    string Summary)
{
    public bool IsExpired(int tick)
    {
        return tick > ExpiresTick;
    }
}
