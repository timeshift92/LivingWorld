using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace LivingWorld.RimWorld;

/// <summary>
/// Per-map mobilization state for the player colony's armory system: whether colonists should be armed and
/// armoured rather than working in civvies. Mobilized when the player manually toggles it, or automatically
/// while a live hostile is present on the map. The threat check is throttled (not every tick) and fail-safe
/// — any error reads as "no threat", so the colony is never locked in alert. Persisted per map. Auto-created
/// by RimWorld for every map (any MapComponent with a (Map) constructor is instantiated).
///
/// Part of the self-contained armory / mobilization module (own files, own namespace helpers) so it does not
/// collide with the world-simulation code.
/// </summary>
public sealed class MobilizationMapComponent : MapComponent
{
    private const int ThreatRecheckInterval = 250;

    private bool manualMobilized;
    private bool threatPresent;

    public MobilizationMapComponent(Map map)
        : base(map)
    {
    }

    public bool ManualMobilized => manualMobilized;

    public bool ThreatPresent => threatPresent;

    public bool IsMobilized => manualMobilized || threatPresent;

    public void ToggleManual()
    {
        manualMobilized = !manualMobilized;
    }

    public override void MapComponentTick()
    {
        base.MapComponentTick();

        var tick = Find.TickManager?.TicksGame ?? 0;
        if (tick % ThreatRecheckInterval != 0)
        {
            return;
        }

        try
        {
            var player = Faction.OfPlayer;
            var pawns = map?.mapPawns?.AllPawnsSpawned;
            threatPresent = player != null
                && pawns != null
                && pawns.Any(pawn =>
                    pawn != null
                    && !pawn.Downed
                    && !pawn.IsPrisoner
                    && pawn.HostileTo(player));
        }
        catch (Exception ex)
        {
            threatPresent = false;
            Log.Warning($"[LivingWorld] Mobilization threat check failed safely: {ex.Message}");
        }

        PushMobilizationJobs();
    }

    // Autonomous armory behaviour without touching the vanilla think tree (which would risk breaking all
    // colonist AI): on each throttled tick we push a fetch-kit job to eligible undrafted colonists who are
    // not yet armed while mobilized, and a return-kit job to armed ones once stood down. Pushed as ordered
    // jobs so they stick; fail-safe (any error just skips this tick, and a job that can't run ends and the
    // pawn resumes normal behaviour). Only acts when armory racks exist on the map.
    private void PushMobilizationJobs()
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        if (!settings.armoryMobilizationEnabled)
        {
            return;
        }

        try
        {
            var racks = map?.listerBuildings?.AllBuildingsColonistOfClass<Building_ArmoryRack>()?.ToList();
            if (racks == null || racks.Count == 0)
            {
                return;
            }

            var mobilized = IsMobilized;
            var colonists = map?.mapPawns?.FreeColonistsSpawned;
            if (colonists == null)
            {
                return;
            }

            foreach (var pawn in colonists)
            {
                if (pawn == null || pawn.Drafted || pawn.Downed || pawn.InMentalState)
                {
                    continue;
                }

                if (mobilized)
                {
                    if (!LoadoutAdapter.IsMobilizationCandidate(pawn)
                        || LoadoutAdapter.IsArmed(pawn)
                        || pawn.CurJobDef == LivingWorldArmoryJobDefOf.LivingWorld_FetchKit)
                    {
                        continue;
                    }

                    var rack = NearestRack(pawn, racks, ArmoryRackKind.Weapon) ?? racks[0];
                    if (rack != null)
                    {
                        pawn.jobs?.TryTakeOrderedJob(
                            JobMaker.MakeJob(LivingWorldArmoryJobDefOf.LivingWorld_FetchKit, rack),
                            JobTag.Misc);
                    }
                }
                else
                {
                    if (!LoadoutAdapter.IsArmed(pawn)
                        || pawn.CurJobDef == LivingWorldArmoryJobDefOf.LivingWorld_ReturnKit)
                    {
                        continue;
                    }

                    var rack = NearestRack(pawn, racks, ArmoryRackKind.Apparel) ?? racks[0];
                    if (rack != null)
                    {
                        pawn.jobs?.TryTakeOrderedJob(
                            JobMaker.MakeJob(LivingWorldArmoryJobDefOf.LivingWorld_ReturnKit, rack),
                            JobTag.Misc);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Mobilization job push failed safely: {ex.Message}");
        }
    }

    private static Building_ArmoryRack? NearestRack(Pawn pawn, List<Building_ArmoryRack> racks, ArmoryRackKind kind)
    {
        return racks
            .Where(rack => rack != null && rack.Spawned && rack.Kind == kind)
            .OrderBy(rack => pawn.Position.DistanceToSquared(rack.Position))
            .FirstOrDefault();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref manualMobilized, "livingWorld_manualMobilized", false);
    }

    public static MobilizationMapComponent? For(Map map)
    {
        return map?.GetComponent<MobilizationMapComponent>();
    }
}
