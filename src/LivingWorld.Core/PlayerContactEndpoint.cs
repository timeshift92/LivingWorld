namespace LivingWorld.Core;

/// <summary>
/// A runtime-owned contact destination for the player. It is deliberately not a settlement and
/// owns no population or resources; the RimWorld bridge may resolve StableKey to the current
/// colony/world tile without importing that colony into the NPC ledger.
/// </summary>
public sealed record PlayerContactEndpoint(
    string FactionId,
    string StableKey,
    bool IsAvailable,
    int UpdatedTick)
{
    /// <summary>Coarse player-colony value signal supplied by the runtime bridge.</summary>
    public RaidIntelValueBand ValueBand { get; init; } = RaidIntelValueBand.Moderate;

    /// <summary>Bounded force estimate; never an exact player pawn or wealth count.</summary>
    public int CombatantDemand { get; init; } = 3;
}
