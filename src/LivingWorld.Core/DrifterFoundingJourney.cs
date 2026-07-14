namespace LivingWorld.Core;

public enum DrifterFoundingJourneyStatus
{
    Traveling,
    Arrived,
    Cancelled
}

public enum DrifterFoundingMemberFate
{
    Pending,
    Returned,
    Missing,
    Captured,
    Settled,
}

public sealed record DrifterFoundingMemberOutcome(
    EntityId DrifterId,
    DrifterFoundingMemberFate Fate,
    EntityId? CitizenId,
    EntityId? SettlementId);

/// <summary>
/// A persisted group of unaffiliated drifters carrying real sponsor supplies to a reserved world
/// destination. No settlement exists until this journey physically arrives.
/// </summary>
public sealed record DrifterFoundingJourney(
    EntityId Id,
    EntityId LeaderDrifterId,
    IReadOnlyList<EntityId> FounderDrifterIds,
    EntityId SponsorSettlementId,
    string FactionId,
    string PlannedSlug,
    string PlannedName,
    string PhysicalStableKey,
    int CreatedTick,
    int ArrivalTick,
    DrifterFoundingJourneyStatus Status,
    bool IsRaiderBand,
    int FoodQuantity,
    int SteelQuantity,
    int ComponentQuantity)
{
    public IReadOnlyList<DrifterFoundingMemberOutcome> MemberOutcomes { get; init; } =
        Array.Empty<DrifterFoundingMemberOutcome>();
}
