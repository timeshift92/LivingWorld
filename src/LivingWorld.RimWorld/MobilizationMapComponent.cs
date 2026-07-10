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

    // Colonists this system auto-drafted for the current raid. Transient (not persisted): used only to draft
    // each combat pawn once per raid, so the player can still undraft someone mid-fight without us re-drafting
    // them. Cleared when the threat ends. We never auto-undraft — that stays the player's call.
    private readonly List<Pawn> draftedByUs = new();

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
        AutoDraftOnThreat();
    }

    // When a real enemy is on the map, wake and draft the colony's combat-capable, armed colonists so they do
    // not sleep or work through a raid — the player then positions them. Only on an actual threat (not the
    // manual toggle), gated by a setting, and each pawn is drafted only once per raid so a manual undraft
    // sticks. We never auto-undraft. Fail-safe.
    private void AutoDraftOnThreat()
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        if (!settings.armoryMobilizationEnabled || !settings.autoDraftOnThreat)
        {
            return;
        }

        try
        {
            draftedByUs.RemoveAll(pawn => pawn == null);

            if (!threatPresent)
            {
                // Raid over — reset so the next raid drafts afresh; leave everyone's drafted state alone.
                draftedByUs.Clear();
                return;
            }

            var colonists = map?.mapPawns?.FreeColonistsSpawned;
            if (colonists == null)
            {
                return;
            }

            foreach (var pawn in colonists)
            {
                if (pawn == null || pawn.Downed || pawn.InMentalState || pawn.drafter == null)
                {
                    continue;
                }

                // Only combat-capable, already-armed colonists; unarmed ones arm up from the racks first and
                // get drafted on a later tick once they are carrying a weapon.
                if (!LoadoutAdapter.IsMobilizationCandidate(pawn) || !LoadoutAdapter.IsArmed(pawn))
                {
                    continue;
                }

                if (pawn.Drafted || draftedByUs.Contains(pawn))
                {
                    continue;
                }

                // Don't yank a colonist off an urgent life-or-base-saving task (firefighting, tending a
                // patient, rescuing the downed). They get drafted on a later tick once that job is done.
                var job = pawn.CurJobDef;
                if (job == JobDefOf.BeatFire || job == JobDefOf.TendPatient || job == JobDefOf.Rescue)
                {
                    continue;
                }

                if (!RestUtility.Awake(pawn))
                {
                    RestUtility.WakeUp(pawn, startNewJob: false);
                }

                pawn.drafter.Drafted = true;
                draftedByUs.Add(pawn);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Auto-draft on threat skipped safely: {ex.Message}");
        }
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
            var sources = ArmorySources.All(map);
            if (sources.Count == 0)
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
            var weaponAvailable = ArmorySources.Items(map, ArmoryRackKind.Weapon)
                .Any(thing => thing?.def != null && thing.def.IsWeapon);

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

                    var rack = ArmorySources.Nearest(map, pawn.Position, ArmoryRackKind.Weapon) ?? sources[0].building;
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
                    // Move colonists we mobilized to the civilian policy (also covers pawns who never found a
                    // weapon): the policy forbids armour, so vanilla strips the combat armour to the racks and
                    // re-dresses them in civvies instead of putting the armour straight back on.
                    MobilizationOutfitService.ToCivilian(pawn);

                    // Stand down only colonists this system armed; leave the player's own armed pawns alone.
                    if (!LoadoutAdapter.IsArmed(pawn)
                        || !mobilizedByUs.Contains(pawn)
                        || pawn.CurJobDef == LivingWorldArmoryJobDefOf.LivingWorld_ReturnKit)
                    {
                        continue;
                    }

                    // Return to a weapon source so the kit lands next to its storage, not across the base.
                    var rack = ArmorySources.Nearest(map, pawn.Position, ArmoryRackKind.Weapon) ?? sources[0].building;
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
    private static void PushArmoryJob(Pawn pawn, JobDef jobDef, Building_Storage rack)
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
