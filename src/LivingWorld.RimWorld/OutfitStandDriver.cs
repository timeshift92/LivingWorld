using System;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace LivingWorld.RimWorld;

/// <summary>
/// Bridges mobilization to the Odyssey Outfit Stand: each combat colonist has a personal stand holding their
/// kit (apparel + weapon), and mobilization makes them walk to it and swap into (or out of) that kit through
/// the game's own jobs. Living World only decides <em>when</em>; the stand does the storing, equipping and
/// repairing. Everything is fail-safe — a missing/empty stand, no assigned stand, or a missing job def just
/// skips that colonist.
/// </summary>
public static class OutfitStandDriver
{
    // The colonist's assigned outfit stand, if they own one.
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

    public static bool HasStand(Pawn pawn)
    {
        return StandOf(pawn) != null;
    }

    // A colonist is "in their combat kit" once they carry a weapon. Using the stand swaps between the worn
    // set and the stored set, so we use this to only ever swap in the correct direction — combat on mobilize,
    // civvies on stand-down — instead of blindly toggling (which put the wrong outfit on).
    private static bool InCombatKit(Pawn pawn)
    {
        return pawn?.equipment?.Primary != null;
    }

    // Send the colonist to their stand to put on the stored combat kit. Only acts when the combat kit is on
    // the stand (a weapon is stored there) and the colonist is not already armed — otherwise the swap would
    // strip them into civvies. Wakes a sleeping colonist so the reaction is immediate.
    public static void EquipFromStand(Pawn pawn)
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

    // On stand-down, send the colonist back to their stand to return the kit and re-dress in civvies. Only
    // acts when they are actually wearing the combat kit (armed); otherwise the swap would arm them. Uses the
    // Outfit Stands Plus dedicated return job when present, else the vanilla use-stand swap.
    public static void ReturnToStand(Pawn pawn)
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
