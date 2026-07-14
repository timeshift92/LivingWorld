using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// A faction raid that has departed its home settlement and is marching across the world map toward
/// the player's colony. It is registered when the storyteller fires a Living World faction raid, shown
/// as a travelling warband marker, and materialized into a real raid when it reaches the colony tile.
/// Persisted so an in-flight raid survives save/load; all fields are plain values (no live object
/// references) so reloading never dangles.
/// </summary>
public sealed class PendingApproachingRaid : IExposable
{
    public long PreparationId;
    public long ArmyId;
    public long SourceSettlementId;
    public string FactionDefName = string.Empty;
    public float Points;
    public int TargetTile = -1;
    public int OriginTile = -1;
    public int DepartTick;
    public int ArrivalTick;
    public string MarkerKey = string.Empty;
    public string TargetLabel = string.Empty;
    public int Attempts;
    public int NextAttemptTick;

    public void ExposeData()
    {
        Scribe_Values.Look(ref PreparationId, "preparationId", 0L);
        Scribe_Values.Look(ref ArmyId, "armyId", 0L);
        Scribe_Values.Look(ref SourceSettlementId, "sourceSettlementId", 0L);
        Scribe_Values.Look(ref FactionDefName, "factionDefName", string.Empty);
        Scribe_Values.Look(ref Points, "points", 0f);
        Scribe_Values.Look(ref TargetTile, "targetTile", -1);
        Scribe_Values.Look(ref OriginTile, "originTile", -1);
        Scribe_Values.Look(ref DepartTick, "departTick", 0);
        Scribe_Values.Look(ref ArrivalTick, "arrivalTick", 0);
        Scribe_Values.Look(ref MarkerKey, "markerKey", string.Empty);
        Scribe_Values.Look(ref TargetLabel, "targetLabel", string.Empty);
        Scribe_Values.Look(ref Attempts, "attempts", 0);
        Scribe_Values.Look(ref NextAttemptTick, "nextAttemptTick", 0);
    }
}

/// <summary>
/// Shared runtime state for the travelling-raid path. The re-entrancy flag lets the world component
/// re-fire the same faction-raid incident at arrival time without the incident worker deferring it a
/// second time (RimWorld fires incidents on the main thread only, so a plain static is safe).
/// </summary>
public static class ApproachingRaidRuntime
{
    public const string MarkerKeyPrefix = "approachraid:";

    // Travel-time envelope: never so short it fails to telegraph, never so long it stalls the raid.
    private const int MinTravelTicks = 18000; // ~0.3 day
    private const int MaxTravelTicks = 78000; // ~1.3 days
    private const float TicksPerTile = 3500f;

    /// <summary>
    /// True while a deferred raid is being materialized at its destination. The incident worker checks
    /// this to skip the "turn into a travelling warband" branch and run the normal spawn path.
    /// </summary>
    public static bool FiringArrival;

    public static PendingApproachingRaid? ArrivingRaid;

    public static int TravelTicksFor(float distanceTiles)
    {
        var raw = (int)(Mathf.Max(0f, distanceTiles) * TicksPerTile);
        return Mathf.Clamp(raw, MinTravelTicks, MaxTravelTicks);
    }
}
