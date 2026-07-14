using System.Collections.Generic;
using LivingWorld.Core;
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
public enum PendingApproachingGroupStatus
{
    Traveling,
    Materializing,
    Materialized
}

public enum ApproachingGroupLaunchResult
{
    NotHandled,
    Deferred,
    Blocked
}

public sealed class PendingApproachingGroupCargo : IExposable
{
    public string ResourceKey = string.Empty;
    public int ReservedQuantity;
    public int MaterializedQuantity;

    public void ExposeData()
    {
        Scribe_Values.Look(ref ResourceKey, "resourceKey", string.Empty);
        Scribe_Values.Look(ref ReservedQuantity, "reservedQuantity", 0);
        Scribe_Values.Look(ref MaterializedQuantity, "materializedQuantity", 0);
    }
}

public sealed class PendingApproachingGroupAnimal : IExposable
{
    public long CohortIdValue;
    public string AnimalKind = string.Empty;
    public AnimalCohortType Type;
    public int PawnThingId;
    public bool Resolved;

    public void ExposeData()
    {
        Scribe_Values.Look(ref CohortIdValue, "cohortIdValue", 0L);
        Scribe_Values.Look(ref AnimalKind, "animalKind", string.Empty);
        Scribe_Values.Look(ref Type, "type", AnimalCohortType.Domesticated);
        Scribe_Values.Look(ref PawnThingId, "pawnThingId", 0);
        Scribe_Values.Look(ref Resolved, "resolved", false);
    }
}

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
    public long SourceSettlementIdValue;
    public string PurposeKey = string.Empty;
    public List<long> LeaseIdValues = new();
    public long ResourceOwnerLeaseIdValue;
    public List<PendingApproachingGroupCargo> Cargo = new();
    public List<PendingApproachingGroupAnimal> Animals = new();
    public List<int> BoundPawnThingIds = new();
    public List<int> ResolvedCarrierThingIds = new();
    public PendingApproachingGroupStatus Status;
    public int MaterializedTick;
    public int ArrivalAttempts;
    public bool CargoCommitted;
    public string TraderKindDefName = string.Empty;
    public string PawnGroupKindDefName = string.Empty;
    public int PawnCount;
    public float PointMultiplier = 1f;
    public bool Forced;
    public bool HasPawnGroupMakerSeed;
    public int PawnGroupMakerSeed;

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
        Scribe_Values.Look(ref SourceSettlementIdValue, "sourceSettlementIdValue", 0L);
        Scribe_Values.Look(ref PurposeKey, "purposeKey", string.Empty);
        Scribe_Collections.Look(ref LeaseIdValues, "leaseIdValues", LookMode.Value);
        LeaseIdValues ??= new List<long>();
        Scribe_Values.Look(ref ResourceOwnerLeaseIdValue, "resourceOwnerLeaseIdValue", 0L);
        Scribe_Collections.Look(ref Cargo, "cargo", LookMode.Deep);
        Cargo ??= new List<PendingApproachingGroupCargo>();
        Scribe_Collections.Look(ref Animals, "animals", LookMode.Deep);
        Animals ??= new List<PendingApproachingGroupAnimal>();
        Scribe_Collections.Look(ref BoundPawnThingIds, "boundPawnThingIds", LookMode.Value);
        BoundPawnThingIds ??= new List<int>();
        Scribe_Collections.Look(ref ResolvedCarrierThingIds, "resolvedCarrierThingIds", LookMode.Value);
        ResolvedCarrierThingIds ??= new List<int>();
        Scribe_Values.Look(ref Status, "status", PendingApproachingGroupStatus.Traveling);
        Scribe_Values.Look(ref MaterializedTick, "materializedTick", 0);
        Scribe_Values.Look(ref ArrivalAttempts, "arrivalAttempts", 0);
        Scribe_Values.Look(ref CargoCommitted, "cargoCommitted", false);
        Scribe_Values.Look(ref TraderKindDefName, "traderKindDefName", string.Empty);
        Scribe_Values.Look(ref PawnGroupKindDefName, "pawnGroupKindDefName", string.Empty);
        Scribe_Values.Look(ref PawnCount, "pawnCount", 0);
        Scribe_Values.Look(ref PointMultiplier, "pointMultiplier", 1f);
        Scribe_Values.Look(ref Forced, "forced", false);
        Scribe_Values.Look(ref HasPawnGroupMakerSeed, "hasPawnGroupMakerSeed", false);
        Scribe_Values.Look(ref PawnGroupMakerSeed, "pawnGroupMakerSeed", 0);
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

    public static PendingApproachingGroup? CurrentGroup;

    public static IReadOnlyList<Pawn> GeneratedPawns => generatedPawns;

    private static readonly List<Pawn> generatedPawns = new();

    public static void BeginArrival(PendingApproachingGroup group)
    {
        FiringArrival = true;
        CurrentGroup = group;
        generatedPawns.Clear();
    }

    public static void RecordGeneratedPawns(IEnumerable<Pawn> pawns)
    {
        generatedPawns.Clear();
        if (pawns != null)
        {
            generatedPawns.AddRange(pawns);
        }
    }

    public static void EndArrival()
    {
        FiringArrival = false;
        CurrentGroup = null;
        generatedPawns.Clear();
    }

    public static int TravelTicksFor(float distanceTiles)
    {
        var raw = (int)(Mathf.Max(0f, distanceTiles) * TicksPerTile);
        return Mathf.Clamp(raw, MinTravelTicks, MaxTravelTicks);
    }
}
