using UnityEngine;
using Verse;
using System;
using System.Runtime.CompilerServices;
using RimWorld;

namespace LivingWorld.RimWorld;

/// <summary>
/// A dormant mechanoid complex on the world map — the believable *source* of mechanoid ground raids,
/// so mechs no longer appear for no reason. A complex accumulates "awakening pressure" from the player
/// colony's wealth and its proximity, and once roused it becomes the attributed origin of mech raids
/// (with a telegraph letter). Persisted; all fields are plain values.
/// </summary>
public sealed class MechClusterNode : IExposable
{
    public int Id;
    public int Tile = -1;
    public bool Awake;
    public int AwakenTick;
    public float Pressure;
    public bool Resolved;
    public int ResolvedTick;
    public int AvailableUnits = 24;
    public int Steel = 1_200;
    public int Energy = 1_000;
    public int RaidsLaunched;

    public void ExposeData()
    {
        Scribe_Values.Look(ref Id, "id", 0);
        Scribe_Values.Look(ref Tile, "tile", -1);
        Scribe_Values.Look(ref Awake, "awake", false);
        Scribe_Values.Look(ref AwakenTick, "awakenTick", 0);
        Scribe_Values.Look(ref Pressure, "pressure", 0f);
        Scribe_Values.Look(ref Resolved, "resolved", false);
        Scribe_Values.Look(ref ResolvedTick, "resolvedTick", 0);
        Scribe_Values.Look(ref AvailableUnits, "availableUnits", 24);
        Scribe_Values.Look(ref Steel, "steel", 1_200);
        Scribe_Values.Look(ref Energy, "energy", 1_000);
        Scribe_Values.Look(ref RaidsLaunched, "raidsLaunched", 0);
    }
}

public sealed record MechRaidReservation(int ClusterId, int Units, int Steel, int Energy);

internal static class MechRaidReservationRuntime
{
    private static readonly ConditionalWeakTable<IncidentParms, MechRaidReservation> Reservations = new();

    public static bool TryAdd(IncidentParms parms, MechRaidReservation reservation)
    {
        if (parms == null || reservation == null || Reservations.TryGetValue(parms, out _))
        {
            return false;
        }

        Reservations.Add(parms, reservation);
        return true;
    }

    public static bool Has(IncidentParms parms)
    {
        return parms != null && Reservations.TryGetValue(parms, out _);
    }

    public static bool TryTake(IncidentParms parms, out MechRaidReservation reservation)
    {
        reservation = null!;
        if (parms == null || !Reservations.TryGetValue(parms, out var found))
        {
            return false;
        }

        Reservations.Remove(parms);
        reservation = found;
        return true;
    }
}

/// <summary>
/// Pure tuning + math for mechanoid-cluster awakening. A complex wakes when accumulated pressure crosses
/// a threshold; daily pressure is the product of a wealth factor (richer colony draws more attention) and
/// a proximity factor (nearer complexes stir sooner) — the "wealth + proximity" combo cause. Side-effect
/// free so the pace can be reasoned about and tuned in isolation.
/// </summary>
public static class MechClusterRuntime
{
    public const int MaxClusters = 2;
    public const int MinClusterDistanceFromPlayer = 6;
    public const int MaxClusterDistanceFromPlayer = 30;

    public const float AwakenThreshold = 40f;
    public const float WealthPerPressurePoint = 50000f;
    public const float MaxWealthFactor = 3f;
    public const float ProximityRangeTiles = 40f;
    public const float MinProximityFactor = 0.2f;

    public static float WealthFactor(float colonyWealth)
    {
        return Mathf.Clamp(colonyWealth / WealthPerPressurePoint, 0f, MaxWealthFactor);
    }

    public static float ProximityFactor(float distanceTiles)
    {
        var near = Mathf.Clamp01((ProximityRangeTiles - Mathf.Max(0f, distanceTiles)) / ProximityRangeTiles);
        return MinProximityFactor + (near * (1f - MinProximityFactor));
    }

    public static float DailyPressure(float colonyWealth, float distanceTiles)
    {
        return WealthFactor(colonyWealth) * ProximityFactor(distanceTiles);
    }

    public static bool ShouldAwaken(float accumulatedPressure)
    {
        return accumulatedPressure >= AwakenThreshold;
    }
}
