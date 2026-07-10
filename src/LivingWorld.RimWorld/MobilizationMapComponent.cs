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

    // Drives each combat colonist's Odyssey Outfit Stand: on mobilize, send them to their stand to equip
    // their kit; on stand-down, send them back to return it. We act once per colonist per alert (tracked in
    // mobilizedByUs), so we never re-trigger the swap every recheck. Fail-safe; only runs with Odyssey active.
    private void PushMobilizationJobs()
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        if (!settings.armoryMobilizationEnabled || !ModsConfig.OdysseyActive)
        {
            return;
        }

        try
        {
            var mobilized = IsMobilized;
            var colonists = map?.mapPawns?.FreeColonistsSpawned;
            if (colonists == null)
            {
                return;
            }

            mobilizedByUs.RemoveAll(pawn => pawn == null);

            // Diagnostic: one line when the alert flips, so behaviour is visible in the log while tuning.
            if (settings.debugLogging && mobilized != loggedMobilized)
            {
                loggedMobilized = mobilized;
                var eligible = colonists.Count(LoadoutAdapter.IsMobilizationCandidate);
                var withStand = colonists.Count(pawn => pawn != null && OutfitStandDriver.HasStand(pawn));
                if (mobilized)
                {
                    var reason = ManualMobilized ? "manual" : "threat";
                    Log.Message($"[LivingWorld] Mobilization ON ({reason}): {eligible} eligible, "
                                + $"{withStand} with an outfit stand.");
                }
                else
                {
                    Log.Message($"[LivingWorld] Mobilization OFF: standing down "
                                + $"{mobilizedByUs.Count} colonist(s) we mobilized.");
                }
            }

            foreach (var pawn in colonists)
            {
                if (pawn == null || pawn.Drafted || pawn.Downed || pawn.InMentalState)
                {
                    continue;
                }

                if (mobilized)
                {
                    // Send eligible colonists with a stand to equip their kit — once each per alert.
                    if (mobilizedByUs.Contains(pawn)
                        || !LoadoutAdapter.IsMobilizationCandidate(pawn)
                        || !OutfitStandDriver.HasStand(pawn))
                    {
                        continue;
                    }

                    mobilizedByUs.Add(pawn);
                    OutfitStandDriver.EquipFromStand(pawn);
                }
                else
                {
                    // Stand down only colonists we mobilized: send them back to their stand.
                    if (!mobilizedByUs.Contains(pawn))
                    {
                        continue;
                    }

                    mobilizedByUs.Remove(pawn);
                    OutfitStandDriver.ReturnToStand(pawn);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Mobilization job push failed safely: {ex.Message}");
        }
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
