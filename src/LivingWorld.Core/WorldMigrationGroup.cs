namespace LivingWorld.Core;

public enum MigrationGroupStatus
{
    Traveling,
    Arrived,
    Lost
}

public sealed record WorldMigrationGroup(
    EntityId Id,
    EntityId SourceSettlementId,
    EntityId? TargetSettlementId,
    string FactionId,
    int CreatedTick,
    int ArrivalTick,
    MigrationGroupStatus Status,
    string Reason)
{
    public string PlannedSettlementSlug { get; init; } = string.Empty;

    public string PlannedSettlementName { get; init; } = string.Empty;

    /// <summary>
    /// Stable Core-side identity of the intended founding site. It is deliberately derived from
    /// fields already serialized by older versions, preserving save compatibility while giving
    /// the RimWorld bridge a concrete token to bind to a world tile before departure.
    /// </summary>
    public string PlannedLocationToken => SettlementExpansionSiteSelector.CreateLocationToken(
        SourceSettlementId,
        FactionId,
        PlannedSettlementSlug);
}
