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

    public void Drive(Map map, bool mobilized, ThreatTier tier, in MusterContext muster)
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        if (!settings.armoryMobilizationEnabled || !ModsConfig.OdysseyActive)
        {
            return;
        }

        try
        {
            var colonists = map.mapPawns?.FreeColonistsSpawned;
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
                Execute(pawn, action, tier, muster, settings.mobilizationDiagnostics);
            }

            // Player combat creatures (Odyssey ghouls): drafted + CAI-driven alongside the fighters. No outfit
            // stand or apparel policy applies to them — they just fight when mobilized and stand down after.
            foreach (var creature in map.mapPawns?.SpawnedPawnsInFaction(Faction.OfPlayer)?.ToList() ?? new List<Pawn>())
            {
                // Loop 1 already owns colonists (draft/engage/release via Execute). SpawnedPawnsInFaction is a
                // superset that includes them, so falling into the release branch below would ReleaseCreature
                // (undraft + CAI-disengage) a fighter colonist that loop 1 just drafted this same tick — the
                // whole mobilization would thrash. Only non-colonist combat creatures (ghouls) belong here.
                if (creature == null || creature.IsColonist)
                {
                    continue;
                }

                if (!MobilizationCandidates.IsCombatCreature(creature))
                {
                    if (engagedByUs.ContainsKey(creature) || draftedByUs.Contains(creature))
                    {
                        ReleaseCreature(creature);
                    }

                    continue;
                }

                DriveCreature(creature, mobilized, tier, muster, settings.mobilizationDiagnostics);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Mobilization drive failed safely: {ex.Message}");
        }
    }

    // Pass-1 for the muster gate: how many reachable fighters (colonists + combat creatures) exist, and how many
    // have reached the anchor. Unreachable fighters are excluded from BOTH counts — they can never gather, so
    // counting them would keep the gate below 100% forever. Cheap (distance + reachability), fail-safe.
    public (int total, int atAnchor) MusterProgress(Map map, IntVec3 anchor, float holdRadius)
    {
        var total = 0;
        var at = 0;
        try
        {
            if (map?.mapPawns == null || !anchor.IsValid)
            {
                return (0, 0);
            }

            foreach (var p in map.mapPawns.FreeColonistsSpawned?.ToList() ?? new List<Pawn>())
            {
                // Exclude busy-urgent fighters (firefight/tend/rescue) from the denominator — they will never
                // march to the line, so counting them would hold the gate below 100% until the timeout fires.
                if (p == null || !MobilizationCandidates.IsCandidate(p)
                    || MobilizationCandidates.IsBusyUrgent(p) || !CanReach(p, anchor))
                {
                    continue;
                }

                total++;
                if (AtAnchor(p, anchor, holdRadius))
                {
                    at++;
                }
            }

            foreach (var c in map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer)?.ToList() ?? new List<Pawn>())
            {
                if (c == null || !MobilizationCandidates.IsCombatCreature(c) || !CanReach(c, anchor))
                {
                    continue;
                }

                total++;
                if (AtAnchor(c, anchor, holdRadius))
                {
                    at++;
                }
            }
        }
        catch
        {
            // fall through with whatever we counted
        }

        return (total, at);
    }

    private void DriveCreature(Pawn creature, bool mobilized, ThreatTier tier, in MusterContext muster, bool diagnostics)
    {
        if (!mobilized)
        {
            if (engagedByUs.ContainsKey(creature) || draftedByUs.Contains(creature))
            {
                ReleaseCreature(creature);
                if (diagnostics)
                {
                    Log.Message($"[LivingWorld] Mobilization: {creature.LabelShort} (creature) -> Release");
                }
            }

            return;
        }

        EngageOrMuster(creature, tier, muster, diagnostics, " (creature)");
    }

    // The fight step for a ready fighter/creature: instead of always free-engaging where it stands, march to the
    // muster anchor and hold there (vanilla drafted Wait_Combat — fires from the cell, melee does not chase, and
    // crucially CAI's aggro-contagion cannot fray a line we never handed to CAI), until the squad is released.
    // Drafting is done up front (needed for both hold and engage); a held pawn carries NO CAI duty.
    private void EngageOrMuster(Pawn pawn, ThreatTier tier, in MusterContext muster, bool diagnostics, string tag)
    {
        if (pawn.drafter != null)
        {
            pawn.drafter.Drafted = true;
            draftedByUs.Add(pawn);
        }

        var state = new MusterState
        {
            IsFighter = true,
            Mobilized = true,
            IsBusyUrgent = false,
            WantsMuster = muster.WantsMuster,
            AtAnchor = AtAnchor(pawn, muster.Anchor, muster.HoldRadius),
            AnchorReachable = CanReach(pawn, muster.Anchor),
            Released = muster.Released,
        };

        string action;
        switch (MusterPlan.NextAction(state))
        {
            case MusterPhase.March:
                if (engagedByUs.Remove(pawn))
                {
                    CaiBridge.Disengage(pawn);
                }

                PushGoto(pawn, muster.Anchor);
                action = "March";
                break;

            case MusterPhase.Hold:
                // Vanilla think-tree drops the drafted pawn into Wait_Combat on its own — hold, no CAI duty.
                if (engagedByUs.Remove(pawn))
                {
                    CaiBridge.Disengage(pawn);
                }

                action = "Hold";
                break;

            default: // Release — free engage
                if (MobilizationCandidates.IsCombatCreature(pawn))
                {
                    // Ghouls are melee-only. CAI's ranged tactical layer (cover/duck/cast position
                    // search) cannot position them, so handing a ghoul to CAI makes it retreat. Never
                    // CAI-drive a combat creature: charge the nearest reachable hostile in melee.
                    // Re-issued every drive (PushMeleeCharge keeps a still-valid attack) so the ghoul
                    // re-targets when its victim dies.
                    PushMeleeCharge(pawn);
                    engagedByUs[pawn] = tier;
                    action = "Charge";
                    break;
                }

                if (engagedByUs.TryGetValue(pawn, out var engagedTier) && engagedTier == tier)
                {
                    return; // already engaged at this tier — steady, no re-issue
                }

                CaiBridge.TryEngage(pawn, tier, muster.Anchor);
                engagedByUs[pawn] = tier;
                action = "Engage";
                break;
        }

        if (diagnostics)
        {
            Log.Message($"[LivingWorld] Muster: {pawn.LabelShort}{tag} -> {action} "
                        + $"(tier {tier}, anchor={muster.Anchor}, atAnchor={state.AtAnchor}, released={muster.Released})");
        }
    }

    // Idempotent drafted move to the anchor. Skip re-issuing when the pawn is already heading there — restarting
    // the Goto after arrival would cancel the Wait_Combat auto-attack the hold relies on.
    private static void PushGoto(Pawn pawn, IntVec3 anchor)
    {
        if (pawn?.jobs == null || !anchor.IsValid)
        {
            return;
        }

        var cur = pawn.CurJob;
        if (cur != null && cur.def == JobDefOf.Goto && cur.targetA.Cell == anchor)
        {
            return;
        }

        var job = JobMaker.MakeJob(JobDefOf.Goto, anchor);
        pawn.jobs.TryTakeOrderedJob(job, JobTag.DraftedOrder, requestQueueing: false);
    }

    // Melee charge for a combat creature (ghoul): order it to attack the nearest reachable hostile.
    // Idempotent — a still-valid melee attack is left running so re-drives don't restart the swing;
    // when the victim dies or none is reachable the ghoul falls back to its drafted stance until the
    // next drive re-targets. Never routes through CAI, whose ranged position search flees a melee pawn.
    private static void PushMeleeCharge(Pawn pawn)
    {
        if (pawn?.jobs == null || pawn.Map == null)
        {
            return;
        }

        var cur = pawn.CurJob;
        if (cur != null && cur.def == JobDefOf.AttackMelee
            && cur.targetA.Thing is Pawn ongoing && !ongoing.Dead && !ongoing.Downed && ongoing.HostileTo(pawn))
        {
            return;
        }

        var target = FindNearestHostile(pawn);
        if (target == null)
        {
            return;
        }

        var job = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
        job.playerForced = true;
        pawn.jobs.TryTakeOrderedJob(job, JobTag.DraftedOrder, requestQueueing: false);
    }

    private static Pawn? FindNearestHostile(Pawn pawn)
    {
        var map = pawn.Map;
        if (map?.mapPawns == null)
        {
            return null;
        }

        Pawn? best = null;
        var bestDist = float.MaxValue;
        foreach (var other in map.mapPawns.AllPawnsSpawned.ToList())
        {
            if (other == null || other == pawn || other.Dead || other.Downed || !other.HostileTo(pawn))
            {
                continue;
            }

            if (!pawn.CanReach(other, PathEndMode.Touch, Danger.Deadly))
            {
                continue;
            }

            var dist = (other.Position - pawn.Position).LengthHorizontalSquared;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = other;
            }
        }

        return best;
    }

    private static bool AtAnchor(Pawn pawn, IntVec3 anchor, float holdRadius)
        => pawn != null && anchor.IsValid && (pawn.Position - anchor).LengthHorizontal <= holdRadius;

    private static bool CanReach(Pawn pawn, IntVec3 anchor)
    {
        try
        {
            return pawn?.Map != null && anchor.IsValid
                   && pawn.Map.reachability.CanReach(pawn.Position, anchor, PathEndMode.Touch, TraverseParms.For(pawn));
        }
        catch
        {
            return false;
        }
    }

    private void ReleaseCreature(Pawn creature)
    {
        if (engagedByUs.Remove(creature))
        {
            CaiBridge.Disengage(creature);
        }

        if (draftedByUs.Remove(creature) && creature.drafter != null && creature.Drafted)
        {
            creature.drafter.Drafted = false;
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

    private void Execute(Pawn pawn, MobPhase action, ThreatTier tier, in MusterContext muster, bool diagnostics)
    {
        switch (action)
        {
            case MobPhase.None:
            case MobPhase.SteadyCombat:
            case MobPhase.SteadyCivilian:
                return;

            case MobPhase.Wake:
                RestUtility.WakeUp(pawn, startNewJob: false);
                // Keep them up. A bare WakeUp lets an off-shift/exhausted pawn crawl straight back into bed
                // before the next 250-tick recheck, so the machine just re-Wakes forever and the pawn never
                // equips or fights (observed live: "Тиберий -> Wake" every recheck). Drafting holds them awake
                // and in place; equipping still works (it is issued as an ordered job), and stand-down undrafts.
                if (pawn.drafter != null)
                {
                    pawn.drafter.Drafted = true;
                    draftedByUs.Add(pawn);
                }
                break;

            case MobPhase.SetCombatPolicy:
                MobilizationPolicyService.ApplyCombat(pawn);
                break;

            case MobPhase.Equip:
                OutfitStandKit.PushEquip(pawn);
                break;

            case MobPhase.Engage:
                // A ready fighter musters and holds the line (vanilla) until the squad is released, then free-
                // engages via CAI. EngageOrMuster drafts first (aiAutoControl needs Drafted; a drafted pawn also
                // avoids the "ThinkNode_Duty with no duty" spam and is cleanly pulled out of any ritual/lord),
                // sets engagedByUs on release so the re-Engage loop stops, and re-issues on a tier change.
                EngageOrMuster(pawn, tier, muster, diagnostics, string.Empty);
                return;

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
