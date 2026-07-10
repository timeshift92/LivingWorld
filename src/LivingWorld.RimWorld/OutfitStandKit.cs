using System;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace LivingWorld.RimWorld;

/// <summary>
/// Bridges mobilization to the Odyssey Outfit Stand: each combat colonist has a personal stand holding their
/// kit (apparel + weapon), and mobilization makes them walk to it and swap into (or out of) that kit through
/// the game's own jobs. Living World only decides <em>when</em>. The swap is directional — combat on equip,
/// civvies on return — derived from whether the pawn is already armed, never a blind toggle. Fail-safe: a
/// missing/empty/unassigned stand or a missing job def just skips.
/// </summary>
public static class OutfitStandKit
{
    public static Building_OutfitStand? StandOf(Pawn pawn)
    {
        try
        {
            if (pawn?.Map == null)
            {
                return null;
            }

            foreach (var stand in pawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_OutfitStand>())
            {
                var owners = stand?.GetComp<CompAssignableToPawn>()?.AssignedPawnsForReading;
                if (owners != null && owners.Contains(pawn))
                {
                    return stand;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Outfit-stand lookup failed safely: {ex.Message}");
        }

        return null;
    }

    public static bool HasStand(Pawn pawn) => StandOf(pawn) != null;

    // Carrying a weapon is our proxy for "wearing the combat kit" — the stand swap arms and armors together.
    private static bool InCombatKit(Pawn pawn) => pawn?.equipment?.Primary != null;

    // Send the colonist to their stand to don the stored combat kit. No-op if the kit is not on the stand (no
    // stored weapon) or they are already armed (swapping would strip them into civvies).
    public static void PushEquip(Pawn pawn)
    {
        try
        {
            var stand = StandOf(pawn);
            if (stand == null || stand.HeldWeapon == null || InCombatKit(pawn))
            {
                return;
            }

            PushStandJob(pawn, JobDefOf.UseOutfitStand, stand);
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Outfit-stand equip failed safely: {ex.Message}");
        }
    }

    // Send the colonist back to return the kit and re-dress in civvies. No-op if not currently armed (swapping
    // would arm them). Prefers the Outfit Stands Plus dedicated return job when present, else the vanilla swap.
    public static void PushReturn(Pawn pawn)
    {
        try
        {
            var stand = StandOf(pawn);
            if (stand == null || !InCombatKit(pawn))
            {
                return;
            }

            var returnJob = DefDatabase<JobDef>.GetNamedSilentFail("OutfitStandsPlus_JobReturnToStand")
                            ?? JobDefOf.UseOutfitStand;
            PushStandJob(pawn, returnJob, stand);
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Outfit-stand return failed safely: {ex.Message}");
        }
    }

    private static void PushStandJob(Pawn pawn, JobDef jobDef, Building_OutfitStand stand)
    {
        if (pawn?.jobs == null || jobDef == null)
        {
            return;
        }

        // Already walking to the stand for this — don't re-issue the order every recheck.
        if (pawn.CurJobDef == jobDef)
        {
            return;
        }

        if (!RestUtility.Awake(pawn))
        {
            RestUtility.WakeUp(pawn, startNewJob: false);
        }

        pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(jobDef, stand), JobTag.Misc);
    }
}
