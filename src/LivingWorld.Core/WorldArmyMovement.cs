namespace LivingWorld.Core;

/// <summary>
/// Lifecycle of an army travelling across the world map toward a target settlement.
/// </summary>
public enum ArmyMovementStatus
{
    Traveling,
    Arrived,
    Recalled,
    Disbanded,
}

/// <summary>
/// A world-war army in transit: which army, where it is headed, when it left and when it is
/// due, and its current lifecycle state. Movement is an abstract travel time in the ledger
/// (Core has no RimWorld tile geometry); the RimWorld layer maps arrival onto a real raid.
///
/// Only armies the world-war planner dispatches get a movement — armies reserved for a vanilla
/// player-facing raid are never moved here, so the two paths never fight over the same army.
/// </summary>
public sealed record WorldArmyMovement(
    EntityId ArmyId,
    EntityId TargetSettlementId,
    int DepartTick,
    int ArrivalTick,
    ArmyMovementStatus Status)
{
    public int StatusTick { get; init; } = DepartTick;

    public string ExpectedTargetFactionId { get; init; } = string.Empty;

    public bool RequiresHostileRelation { get; init; }
}
