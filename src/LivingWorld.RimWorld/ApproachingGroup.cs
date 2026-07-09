using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// A neutral faction group (visitors, and later traders/travellers) that has set out from one of its
/// settlements and is crossing the world map toward the player's colony, instead of appearing at the map
/// edge from nowhere. Registered when the storyteller fires the group's arrival incident, shown as a
/// travelling marker, and materialized into the real group when it reaches the colony. Persisted; all
/// fields are plain values so an in-flight group survives save/load.
/// </summary>
public sealed class PendingApproachingGroup : IExposable
{
    public string IncidentDefName = string.Empty;
    public string FactionDefName = string.Empty;
    public float Points;
    public int TargetTile = -1;
    public int OriginTile = -1;
    public int DepartTick;
    public int ArrivalTick;
    public string MarkerKey = string.Empty;
    public string KindKey = string.Empty;
    public string TargetLabel = string.Empty;

    public void ExposeData()
    {
        Scribe_Values.Look(ref IncidentDefName, "incidentDefName", string.Empty);
        Scribe_Values.Look(ref FactionDefName, "factionDefName", string.Empty);
        Scribe_Values.Look(ref Points, "points", 0f);
        Scribe_Values.Look(ref TargetTile, "targetTile", -1);
        Scribe_Values.Look(ref OriginTile, "originTile", -1);
        Scribe_Values.Look(ref DepartTick, "departTick", 0);
        Scribe_Values.Look(ref ArrivalTick, "arrivalTick", 0);
        Scribe_Values.Look(ref MarkerKey, "markerKey", string.Empty);
        Scribe_Values.Look(ref KindKey, "kindKey", string.Empty);
        Scribe_Values.Look(ref TargetLabel, "targetLabel", string.Empty);
    }
}

/// <summary>
/// Shared runtime for the travelling-group path. The re-entrancy flag lets the world component re-fire the
/// same neutral-group incident at arrival without the travel patch deferring it a second time (RimWorld
/// fires incidents on the main thread only, so a plain static is safe).
/// </summary>
public static class ApproachingGroupRuntime
{
    public const string MarkerKeyPrefix = "approachgroup:";

    // Groups travel a little quicker than a war party: they are not sneaking up, just arriving.
    private const int MinTravelTicks = 15000; // ~0.25 day
    private const int MaxTravelTicks = 60000; // ~1 day
    private const float TicksPerTile = 3000f;

    public static bool FiringArrival;

    public static int TravelTicksFor(float distanceTiles)
    {
        var raw = (int)(Mathf.Max(0f, distanceTiles) * TicksPerTile);
        return Mathf.Clamp(raw, MinTravelTicks, MaxTravelTicks);
    }
}
