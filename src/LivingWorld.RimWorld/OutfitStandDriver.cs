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

    private static bool HasKit(Building_OutfitStand stand)
    {
        return (stand.HeldItems != null && stand.HeldItems.Count > 0) || stand.HeldWeapon != null;
    }

    // Send the colonist to their stand to put on the stored combat kit. No-op if the stand is empty (kit
    // already worn) or they have no stand. Wakes a sleeping colonist so the reaction is immediate.
    public static void EquipFromStand(Pawn pawn)
    {
        try
        {
            var stand = StandOf(pawn);
            if (stand == null || !HasKit(stand))
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

    // On stand-down, send the colonist back to their stand to return the kit and re-dress. Uses the Outfit
    // Stands Plus dedicated return job when that mod is present, otherwise the vanilla use-stand swap.
    public static void ReturnToStand(Pawn pawn)
    {
        try
        {
            var stand = StandOf(pawn);
            if (stand == null)
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

        if (!RestUtility.Awake(pawn))
        {
            RestUtility.WakeUp(pawn, startNewJob: false);
        }

        pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(jobDef, stand), JobTag.Misc);
    }
}
