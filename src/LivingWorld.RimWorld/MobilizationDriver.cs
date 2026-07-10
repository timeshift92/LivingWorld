using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace LivingWorld.RimWorld;

/// <summary>
/// Executes the mobilization phase machine against the live colony. Each recheck it snapshots the colonists,
/// builds a <see cref="PawnMobState"/> for each, asks <see cref="MobilizationPlan.NextAction"/> for the one
/// action to take, and performs exactly that action. One action per pawn per tick keeps everything idempotent:
/// the driver stores no step counters, only two transient sets tracking who it engaged / drafted so it can
/// release them on stand-down and not re-issue every recheck. Every executed transition is logged when
/// diagnostics are on, so live behavior is debugged from facts. Fail-safe throughout.
/// </summary>
public sealed class MobilizationDriver
{
    private readonly HashSet<Pawn> engagedByUs = new();
    private readonly HashSet<Pawn> draftedByUs = new();

    public void Drive(Map map, bool mobilized)
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        if (!settings.armoryMobilizationEnabled || !ModsConfig.OdysseyActive)
        {
            return;
        }

        try
        {
            var colonists = map?.mapPawns?.FreeColonistsSpawned;
            if (colonists == null)
            {
                return;
            }

            engagedByUs.RemoveWhere(p => p == null || !p.Spawned);
            draftedByUs.RemoveWhere(p => p == null || !p.Spawned);

            // Snapshot: equipping/dropping gear can mutate the live colonist list mid-loop.
            foreach (var pawn in colonists.ToList())
            {
                if (pawn == null)
                {
                    continue;
                }

                var state = Snapshot(pawn);
                var action = MobilizationPlan.NextAction(mobilized, state);
                Execute(pawn, action, settings.mobilizationDiagnostics);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Mobilization drive failed safely: {ex.Message}");
        }
    }

    // Diagnostics: the phase the machine would pick for this pawn right now, and our per-alert tracking.
    public MobPhase PeekPhase(Pawn pawn, bool mobilized) => MobilizationPlan.NextAction(mobilized, Snapshot(pawn));

    public bool IsEngagedByUs(Pawn pawn) => engagedByUs.Contains(pawn);

    public bool IsDraftedByUs(Pawn pawn) => draftedByUs.Contains(pawn);

    private PawnMobState Snapshot(Pawn pawn)
    {
        return new PawnMobState
        {
            IsCandidate = MobilizationCandidates.IsCandidate(pawn),
            IsBusyUrgent = MobilizationCandidates.IsBusyUrgent(pawn),
            Asleep = !RestUtility.Awake(pawn),
            PolicyIsCombat = MobilizationPolicyService.IsCombatPolicy(pawn),
            PolicyIsCivilian = MobilizationPolicyService.IsCivilianPolicy(pawn),
            InCombatKit = MobilizationCandidates.IsArmed(pawn),
            HasStand = OutfitStandKit.HasStand(pawn),
            KitAvailable = OutfitStandKit.StandHasWeapon(pawn),
            Drafted = pawn.Drafted,
            DraftedByUs = draftedByUs.Contains(pawn),
            HasLwDuty = engagedByUs.Contains(pawn),
            CaiAvailable = CaiBridge.Available,
        };
    }

    private void Execute(Pawn pawn, MobPhase action, bool diagnostics)
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
                if (CaiBridge.TryEngage(pawn))
                {
                    engagedByUs.Add(pawn);
                }
                else if (pawn.drafter != null)
                {
                    // CAI is present globally but could not take this specific pawn — fall back to drafting so
                    // it still fights instead of looping on Engage. Track in both sets: engagedByUs stops the
                    // re-Engage loop (HasLwDuty), draftedByUs lets stand-down undraft it.
                    pawn.drafter.Drafted = true;
                    draftedByUs.Add(pawn);
                    engagedByUs.Add(pawn);
                }
                break;

            case MobPhase.Draft:
                if (pawn.drafter != null)
                {
                    pawn.drafter.Drafted = true;
                    draftedByUs.Add(pawn);
                }
                break;

            case MobPhase.ClearCombat:
                engagedByUs.Remove(pawn);
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
            Log.Message($"[LivingWorld] Mobilization: {pawn.LabelShort} -> {action}");
        }
    }
}
