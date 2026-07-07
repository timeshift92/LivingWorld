namespace LivingWorld.Core;

public enum IntelSourceKind
{
    Public,
    Trade,
    Scout,
    Refugee,
    Prisoner,
    DirectVisit,
    Survivor,
    Rumor
}

public enum RaidOpportunityStatus
{
    Active,
    Consumed
}

public sealed record WorldIntelReport(
    EntityId Id,
    IntelSourceKind SourceKind,
    string FactionId,
    int Tick,
    int ValueScore,
    string Summary);

public sealed record RaidOpportunity(
    EntityId Id,
    string FactionId,
    EntityId IntelReportId,
    RaidOpportunityStatus Status,
    int CombatantDemand,
    string Reason)
{
    public RaidOpportunity Consume()
    {
        return this with { Status = RaidOpportunityStatus.Consumed };
    }
}
