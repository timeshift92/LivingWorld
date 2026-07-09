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
    private bool loggedMobilized;

    // Colonists this system actually armed, so on stand-down we disarm only them — never a hunter or a
    // pawn the player armed on purpose. Kept by reference, persisted with the map.
    private List<Pawn> mobilizedByUs = new();

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

            // Nothing to arm with means every fetch would end unarmed and re-fire next tick — don't thrash.
            var weaponAvailable = racks.Any(rack =>
                rack != null && rack.Spawned && rack.Kind == ArmoryRackKind.Weapon
                && rack.StoredItems.Any(thing => thing?.def != null && thing.def.IsWeapon));

            mobilizedByUs.RemoveAll(pawn => pawn == null);

            // Diagnostic: one line when the alert flips, so behaviour is visible in the log while tuning.
            if (settings.debugLogging && mobilized != loggedMobilized)
            {
                loggedMobilized = mobilized;
                var eligible = colonists.Count(LoadoutAdapter.IsMobilizationCandidate);
                var armed = colonists.Count(pawn => pawn != null && LoadoutAdapter.IsArmed(pawn));
                if (mobilized)
                {
                    var reason = ManualMobilized ? "manual" : "threat";
                    Log.Message($"[LivingWorld] Mobilization ON ({reason}): {eligible} eligible, {armed} already armed, "
                                + $"weapons on racks: {(weaponAvailable ? "yes" : "no")}.");
                }
                else
                {
                    Log.Message($"[LivingWorld] Mobilization OFF: standing down {mobilizedByUs.Count} armed by us "
                                + $"({armed} armed in total).");
                }
            }

            foreach (var pawn in colonists)
            {
                if (pawn == null || pawn.Drafted || pawn.Downed || pawn.InMentalState)
                {
                    continue;
                }

                // Once a pawn is no longer armed its kit is back — forget it, so we never disarm a colonist
                // we did not arm (a hunter, or someone the player armed on purpose).
                if (!LoadoutAdapter.IsArmed(pawn))
                {
                    mobilizedByUs.Remove(pawn);
                }

                if (mobilized)
                {
                    if (!LoadoutAdapter.IsMobilizationCandidate(pawn)
                        || LoadoutAdapter.IsArmed(pawn)
                        || !weaponAvailable
                        || pawn.CurJobDef == LivingWorldArmoryJobDefOf.LivingWorld_FetchKit)
                    {
                        continue;
                    }

                    var rack = NearestRack(pawn, racks, ArmoryRackKind.Weapon) ?? racks[0];
                    if (rack != null)
                    {
                        if (!mobilizedByUs.Contains(pawn))
                        {
                            mobilizedByUs.Add(pawn);
                        }

                        // Move to the combat apparel policy so vanilla keeps the armour on during the alert.
                        MobilizationOutfitService.ToCombat(pawn);
                        PushArmoryJob(pawn, LivingWorldArmoryJobDefOf.LivingWorld_FetchKit, rack);
                    }
                }
                else
                {
                    // Restore the pre-mobilization apparel policy (also covers pawns who never found a
                    // weapon); once the combat armour is off, vanilla re-dresses them in civvies.
                    MobilizationOutfitService.Restore(pawn);

                    // Stand down only colonists this system armed; leave the player's own armed pawns alone.
                    if (!LoadoutAdapter.IsArmed(pawn)
                        || !mobilizedByUs.Contains(pawn)
                        || pawn.CurJobDef == LivingWorldArmoryJobDefOf.LivingWorld_ReturnKit)
                    {
                        continue;
                    }

                    // Return to the weapon rack so the kit lands next to its storage, not across the base.
                    var rack = NearestRack(pawn, racks, ArmoryRackKind.Weapon) ?? racks[0];
                    if (rack != null)
                    {
                        PushArmoryJob(pawn, LivingWorldArmoryJobDefOf.LivingWorld_ReturnKit, rack);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Mobilization job push failed safely: {ex.Message}");
        }
    }

    // Push a forced armory job. Mobilization is meant to be reacted to at once, so a sleeping colonist is
    // woken first (the forced order alone would interrupt sleep, but we make it explicit and certain).
    private static void PushArmoryJob(Pawn pawn, JobDef jobDef, Building_ArmoryRack rack)
    {
        if (pawn?.jobs == null)
        {
            return;
        }

        if (!RestUtility.Awake(pawn))
        {
            RestUtility.WakeUp(pawn, startNewJob: false);
        }

        pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(jobDef, rack), JobTag.Misc);
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
        Scribe_Collections.Look(ref mobilizedByUs, "livingWorld_mobilizedByUs", LookMode.Reference);
        mobilizedByUs ??= new List<Pawn>();
    }

    public static MobilizationMapComponent? For(Map map)
    {
        return map?.GetComponent<MobilizationMapComponent>();
    }
}
