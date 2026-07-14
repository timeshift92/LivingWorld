using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace LivingWorld.RimWorld;

/// <summary>
/// Executes the mobilization phase machine against the live colony each recheck: snapshot the colonists, build
/// a <see cref="PawnMobState"/> for each (tier-aware), ask <see cref="MobilizationPlan.NextAction"/> for the
/// one action, and perform exactly that. One action per pawn per tick keeps everything idempotent. Two
/// transient maps track who we engaged (with the tier we engaged them at, so a tier change re-issues the right
/// CAI duty) and who we drafted (persisted across save/load so stand-down still releases them). Fail-safe.
/// </summary>
public sealed class MobilizationDriver
{
    private readonly Dictionary<Pawn, ThreatTier> engagedByUs = new();
    private readonly HashSet<Pawn> draftedByUs = new();

    public void Drive(Map map, bool mobilized, ThreatTier tier)
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        if (!settings.armoryMobilizationEnabled || !ModsConfig.OdysseyActive)
        {
            return;
        }

        try
        {
            // Read anchor before the defensive null-conditional chain below — Roslyn's nullable flow analysis
            // otherwise treats `map` as maybe-null afterward even though the parameter itself is non-nullable.
            var anchor = map.Center;
            var colonists = map?.mapPawns?.FreeColonistsSpawned;
            if (colonists == null)
            {
                return;
            }

            PrunePawns();

            // Snapshot: equipping/dropping gear can mutate the live colonist list mid-loop.
            foreach (var pawn in colonists.ToList())
            {
                if (pawn == null)
                {
                    continue;
                }

                var state = Snapshot(pawn, tier);
                var action = MobilizationPlan.NextAction(mobilized, state);
                Execute(pawn, action, tier, anchor, settings.mobilizationDiagnostics);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Mobilization drive failed safely: {ex.Message}");
        }
    }

    // Diagnostics: the phase the machine would pick for this pawn right now at the given tier.
    public MobPhase PeekPhase(Pawn pawn, bool mobilized, ThreatTier tier)
        => MobilizationPlan.NextAction(mobilized, Snapshot(pawn, tier));

    public bool IsEngagedByUs(Pawn pawn) => engagedByUs.ContainsKey(pawn);

    public bool IsDraftedByUs(Pawn pawn) => draftedByUs.Contains(pawn);

    // Persist only the drafted set (thingIDNumbers) — CAI duties expire on their own, but a drafted pawn stays
    // drafted forever across a reload unless we remember we drafted it.
    public List<int> ExportDraftedIds()
        => draftedByUs.Where(p => p != null).Select(p => p.thingIDNumber).ToList();

    public void ImportDraftedIds(IEnumerable<int> ids, Map map)
    {
        draftedByUs.Clear();
        if (ids == null || map?.mapPawns == null)
        {
            return;
        }

        var wanted = new HashSet<int>(ids);
        foreach (var pawn in map.mapPawns.AllPawns)
        {
            if (pawn != null && wanted.Contains(pawn.thingIDNumber))
            {
                draftedByUs.Add(pawn);
            }
        }
    }

    private void PrunePawns()
    {
        foreach (var dead in engagedByUs.Keys.Where(p => p == null || !p.Spawned).ToList())
        {
            engagedByUs.Remove(dead);
        }

        draftedByUs.RemoveWhere(p => p == null || !p.Spawned);
    }

    private PawnMobState Snapshot(Pawn pawn, ThreatTier tier)
    {
        // A pawn keeps its "engaged" status only while the tier it was engaged at still matches — a tier change
        // makes HasLwDuty read false so the machine re-issues the duty that fits the new tier.
        var hasDuty = engagedByUs.TryGetValue(pawn, out var engagedTier) && engagedTier == tier;

        return new PawnMobState
        {
            IsCandidate = MobilizationCandidates.IsCandidate(pawn),
            IsBusyUrgent = MobilizationCandidates.IsBusyUrgent(pawn),
            Asleep = !RestUtility.Awake(pawn),
            PolicyIsCombat = MobilizationPolicyService.IsCombatPolicy(pawn),
            PolicyIsCivilian = MobilizationPolicyService.IsCivilianPolicy(pawn),
            InCombatKit = MobilizationCandidates.IsInCombatKit(pawn),
            HasStand = OutfitStandKit.HasStand(pawn),
            KitAvailable = OutfitStandKit.StandHasWeapon(pawn),
            Drafted = pawn.Drafted,
            DraftedByUs = draftedByUs.Contains(pawn),
            HasLwDuty = hasDuty,
            WasEngagedByUs = engagedByUs.ContainsKey(pawn),
            CaiAvailable = CaiBridge.Available,
        };
    }

    private void Execute(Pawn pawn, MobPhase action, ThreatTier tier, IntVec3 anchor, bool diagnostics)
    {
        switch (action)
        {
            case MobPhase.None:
            case MobPhase.SteadyCombat:
            case MobPhase.SteadyCivilian:
                return;

            case MobPhase.Wake:
                RestUtility.WakeUp(pawn, startNewJob: false);
                break;

            case MobPhase.SetCombatPolicy:
                MobilizationPolicyService.ApplyCombat(pawn);
                break;

            case MobPhase.Equip:
                OutfitStandKit.PushEquip(pawn);
                break;

            case MobPhase.Engage:
                // CAI's autonomous control (aiAutoControl) only takes effect on a DRAFTED pawn, and giving an
                // UNDRAFTED colonist a CAI duty makes the free-colonist think tree spam "ThinkNode_Duty with no
                // duty" (and fights a ritual/lord for control). So draft first — this makes aiAutoControl
                // effective, uses the drafted think tree (no duty error), and cleanly pulls the pawn out of any
                // ritual — then hand CAI the objective + reactive control on top (best-effort).
                if (pawn.drafter != null)
                {
                    pawn.drafter.Drafted = true;
                    draftedByUs.Add(pawn);
                }

                CaiBridge.TryEngage(pawn, tier, anchor);

                // Mark handled at this tier either way: stops the re-Engage loop, and a tier change re-issues.
                // If CAI could not take the pawn it is still drafted (fallback); ClearCombat disengages + undrafts.
                engagedByUs[pawn] = tier;
                break;

            case MobPhase.Draft:
                if (pawn.drafter != null)
                {
                    pawn.drafter.Drafted = true;
                    draftedByUs.Add(pawn);
                }
                break;

            case MobPhase.ClearCombat:
                if (engagedByUs.Remove(pawn))
                {
                    CaiBridge.Disengage(pawn);
                }

                if (draftedByUs.Remove(pawn) && pawn.drafter != null && pawn.Drafted)
                {
                    pawn.drafter.Drafted = false;
                }
                break;

            case MobPhase.SetCivilianPolicy:
                MobilizationPolicyService.ApplyCivilian(pawn);
                break;

            case MobPhase.ReturnKit:
                OutfitStandKit.PushReturn(pawn);
                break;
        }

        if (diagnostics)
        {
            Log.Message($"[LivingWorld] Mobilization: {pawn.LabelShort} -> {action} (tier {tier})");
        }
    }
}
