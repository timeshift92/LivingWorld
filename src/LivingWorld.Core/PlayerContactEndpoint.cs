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
    int UpdatedTick);
