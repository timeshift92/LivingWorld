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
    /// RimWorld runtime expeditions may require a concrete reachable world tile before Core is
    /// allowed to turn migrants into a settlement. Standalone Core callers keep the legacy false
    /// default and can provide their own materialization layer.
    /// </summary>
    public bool PhysicalDestinationRequired { get; init; }

    /// <summary>
    /// Stable world-object key selected by the runtime bridge. An empty value means no concrete
    /// destination has been committed yet.
    /// </summary>
    public string PhysicalStableKey { get; init; } = string.Empty;

    public bool PhysicalDestinationReady =>
        !PhysicalDestinationRequired || !string.IsNullOrWhiteSpace(PhysicalStableKey);

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
