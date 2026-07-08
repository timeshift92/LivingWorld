namespace LivingWorld.Core;

/// <summary>
/// A non-combat, non-cargo world-war mission that travels to a target settlement and applies its
/// effect on arrival (a scout hands over intel; a diplomat improves relations). Warbands and caravans
/// have their own travelling entities (they carry real citizens/goods); these missions carry nothing,
/// so they are conservation-trivial. Movement is abstract travel time in the ledger — the RimWorld
/// layer visualizes it on the globe with a per-kind icon.
/// </summary>
public enum WorldMissionKind
{
    Scout,
    Diplomat,
}

public enum WorldMissionStatus
{
    Traveling,
    Arrived,
    Failed,
}

public sealed record WorldMission(
    EntityId Id,
    WorldMissionKind Kind,
    string FactionId,
    EntityId OriginSettlementId,
    EntityId TargetSettlementId,
    int DepartTick,
    int ArrivalTick,
    WorldMissionStatus Status)
{
    /// <summary>Second party for relationship missions (diplomacy); empty otherwise.</summary>
    public string TargetFactionId { get; init; } = string.Empty;

    /// <summary>Effect magnitude applied on arrival (diplomacy goodwill delta, scout intel value).</summary>
    public int Amount { get; init; }
}
