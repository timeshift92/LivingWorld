namespace LivingWorld.Core;

public enum DrifterFoundingJourneyStatus
{
    Traveling,
    Arrived,
    Cancelled
}

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
    int ComponentQuantity);
