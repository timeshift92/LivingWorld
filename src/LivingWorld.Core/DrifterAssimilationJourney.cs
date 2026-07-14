namespace LivingWorld.Core;

public enum DrifterAssimilationJourneyStatus
{
    Traveling,
    Arrived,
    Cancelled
}

/// <summary>
/// A concrete drifter moving toward a concrete settlement. The drifter stays in the world pool
/// while traveling, but is reserved by this journey so no other assimilation or founding flow can
/// consume it. Runtime worlds may require a physical origin tile before arrival is allowed.
/// </summary>
public sealed record DrifterAssimilationJourney(
    EntityId Id,
    EntityId DrifterId,
    EntityId TargetSettlementId,
    string ExpectedTargetFactionId,
    int CreatedTick,
    int ArrivalTick,
    DrifterAssimilationJourneyStatus Status,
    string Reason)
{
    public bool PhysicalOriginRequired { get; init; }

    public string PhysicalOriginStableKey { get; init; } = string.Empty;

    public bool PhysicalOriginReady =>
        !PhysicalOriginRequired || !string.IsNullOrWhiteSpace(PhysicalOriginStableKey);
}
