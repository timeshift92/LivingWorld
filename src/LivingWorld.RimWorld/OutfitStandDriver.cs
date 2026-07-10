using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace LivingWorld.RimWorld;

/// <summary>
/// Bridges mobilization to the colony's gear. Armour and civilian clothing live on each colonist's Odyssey
/// Outfit Stand (a swap between the worn set and the stored set); the weapon is auto-picked from anywhere in
/// the colony by skill. Living World only decides <em>when</em>. Everything is fail-safe — a missing stand,
/// no weapon, or a missing job def just skips that colonist.
/// </summary>
public static class OutfitStandDriver
{
    // Combat armour on the stand is detected by armour rating, so it works whatever the pieces are.
    private const float CombatArmorRating = 0.15f;

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

    private static bool IsArmed(Pawn pawn)
    {
        return pawn?.equipment?.Primary != null;
    }

    // The stand currently holds the combat armour set (so the colonist is in civvies and should equip on
    // mobilize). When false the stand holds the civvies (the colonist is in combat armour, ready to stand down).
    private static bool StandHoldsCombatArmor(Building_OutfitStand stand)
    {
        var items = stand?.HeldItems;
        return items != null && items.Any(thing =>
            thing?.def != null && thing.def.IsApparel
            && thing.def.GetStatValueAbstract(StatDefOf.ArmorRating_Sharp) >= CombatArmorRating);
    }

    // Send the colonist to their stand to swap into their combat armour. Only acts when the combat armour is
    // on the stand; otherwise the swap would strip them back into civvies. Wakes a sleeping colonist.
    public static void EquipFromStand(Pawn pawn)
    {
        try
        {
            var stand = StandOf(pawn);
            if (stand == null || !StandHoldsCombatArmor(stand))
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

    // On stand-down, swap the colonist back into civvies. Only acts when the stand holds the civvies (the
    // colonist is wearing the combat armour); otherwise the swap would armour them up again.
    public static void ReturnToStand(Pawn pawn)
    {
        try
        {
            var stand = StandOf(pawn);
            if (stand == null || StandHoldsCombatArmor(stand))
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

    // True while there is a weapon this colonist could go pick up and they are not already armed.
    public static bool HasWeaponToPick(Pawn pawn)
    {
        return !IsArmed(pawn) && ColonyWeapons(pawn).Any();
    }

    // Send the colonist to equip the best available colony weapon for their skills (ranged unless melee wins
    // by the configured advantage; best quality of the chosen kind). No-op if already armed.
    public static void EquipBestWeapon(Pawn pawn, int meleeAdvantage)
    {
        try
        {
            if (pawn?.Map == null || IsArmed(pawn))
            {
                return;
            }

            var candidates = ColonyWeapons(pawn).ToList();
            if (candidates.Count == 0)
            {
                return;
            }

            var pool = candidates
                .Select(thing => new WeaponOption(thing.def.defName, thing.def.IsRangedWeapon, (int)thing.MarketValue))
                .ToList();
            var shooting = SkillLevel(pawn, SkillDefOf.Shooting);
            var melee = SkillLevel(pawn, SkillDefOf.Melee);
            var chosen = LoadoutSelectionService.SelectWeapon(shooting, melee, meleeAdvantage, pool);
            if (chosen == null)
            {
                return;
            }

            var weapon = candidates
                .Where(thing => thing.def.defName == chosen.DefName)
                .OrderByDescending(thing => thing.MarketValue)
                .FirstOrDefault();
            if (weapon == null)
            {
                return;
            }

            PushEquipJob(pawn, weapon);
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Outfit-stand weapon pick failed safely: {ex.Message}");
        }
    }

    // On stand-down, drop the auto-picked weapon so it is hauled back to storage.
    public static void DropWeapon(Pawn pawn)
    {
        try
        {
            if (pawn?.equipment?.Primary is ThingWithComps primary)
            {
                pawn.equipment.TryDropEquipment(primary, out _, pawn.Position);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Outfit-stand weapon drop failed safely: {ex.Message}");
        }
    }

    private static IEnumerable<Thing> ColonyWeapons(Pawn pawn)
    {
        var map = pawn?.Map;
        if (map == null)
        {
            return Enumerable.Empty<Thing>();
        }

        return map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon)
            .Where(thing => thing != null && thing.Spawned && thing.def.IsWeapon && !thing.IsForbidden(pawn));
    }

    private static int SkillLevel(Pawn pawn, SkillDef skill)
    {
        return pawn?.skills?.GetSkill(skill)?.Level ?? 0;
    }

    private static void PushStandJob(Pawn pawn, JobDef jobDef, Building_OutfitStand stand)
    {
        PushJob(pawn, jobDef, stand);
    }

    private static void PushEquipJob(Pawn pawn, Thing weapon)
    {
        PushJob(pawn, JobDefOf.Equip, weapon);
    }

    private static void PushJob(Pawn pawn, JobDef jobDef, Thing target)
    {
        if (pawn?.jobs == null || jobDef == null || target == null)
        {
            return;
        }

        // Already doing this exact job — don't re-issue the order every recheck.
        if (pawn.CurJobDef == jobDef)
        {
            return;
        }

        if (!RestUtility.Awake(pawn))
        {
            RestUtility.WakeUp(pawn, startNewJob: false);
        }

        pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(jobDef, target), JobTag.Misc);
    }
}
