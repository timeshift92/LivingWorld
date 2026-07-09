using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Bridges the pure <see cref="LoadoutSelectionService"/> to real RimWorld things: it reads a colonist's
/// combat skills, builds weapon/armour pools from the armory racks, asks Core what to take, and actually
/// equips/returns the gear. Every operation is fail-safe — a bad state just skips, never throws into the
/// job or the tick.
/// </summary>
public static class LoadoutAdapter
{
    private const float HeavyArmorThreshold = 0.4f;

    public static bool IsMobilizationCandidate(Pawn pawn)
    {
        try
        {
            if (pawn == null || !pawn.IsColonist || pawn.Dead || pawn.Downed || pawn.InMentalState)
            {
                return false;
            }

            var shooting = SkillLevel(pawn, SkillDefOf.Shooting);
            var melee = SkillLevel(pawn, SkillDefOf.Melee);
            return LoadoutSelectionService.IsCombatEligible(shooting, melee);
        }
        catch
        {
            return false;
        }
    }

    // True once the pawn is carrying a weapon (its combat kit is on). Used to decide fetch vs return.
    public static bool IsArmed(Pawn pawn)
    {
        return pawn?.equipment?.Primary != null;
    }

    // Picks the weapon+armour this pawn should take from the racks. Returns the real things (may be null).
    public static (Thing? weapon, Thing? armor) ResolveKit(Pawn pawn, Map map)
    {
        try
        {
            if (pawn == null || map == null)
            {
                return (null, null);
            }

            var racks = map.listerBuildings?.AllBuildingsColonistOfClass<Building_ArmoryRack>()?.ToList()
                        ?? new List<Building_ArmoryRack>();

            var weaponThings = racks
                .Where(rack => rack.Kind == ArmoryRackKind.Weapon)
                .SelectMany(rack => rack.StoredItems)
                .Where(thing => thing?.def != null && thing.def.IsWeapon)
                .ToList();
            var armorThings = racks
                .Where(rack => rack.Kind == ArmoryRackKind.Armor)
                .SelectMany(rack => rack.StoredItems)
                .OfType<Apparel>()
                .ToList();

            var shooting = SkillLevel(pawn, SkillDefOf.Shooting);
            var melee = SkillLevel(pawn, SkillDefOf.Melee);

            // Hybrid: a colonist's assigned kit wins if that gear is on the racks; otherwise the skill pick.
            var assignments = ArmoryAssignmentComponent.Instance;
            var assignedWeaponDef = assignments?.AssignedWeaponDef(pawn);
            var assignedArmorDef = assignments?.AssignedArmorDef(pawn);

            var weaponPool = weaponThings
                .Select(thing => new WeaponOption(thing.def.defName, thing.def.IsRangedWeapon, (int)thing.MarketValue))
                .ToList();
            var assignedWeapon = string.IsNullOrEmpty(assignedWeaponDef)
                ? null
                : weaponPool.FirstOrDefault(option => option.DefName == assignedWeaponDef);
            var chosenWeapon = LoadoutSelectionService.SelectWeapon(shooting, melee, assignedWeapon, weaponPool);
            var weapon = chosenWeapon == null
                ? null
                : weaponThings.FirstOrDefault(thing => thing.def.defName == chosenWeapon.DefName);

            var armorPool = armorThings
                .Select(apparel => new ArmorOption(
                    apparel.def.defName,
                    (int)apparel.MarketValue,
                    apparel.def.GetStatValueAbstract(StatDefOf.ArmorRating_Sharp) >= HeavyArmorThreshold))
                .ToList();
            var assignedArmor = string.IsNullOrEmpty(assignedArmorDef)
                ? null
                : armorPool.FirstOrDefault(option => option.DefName == assignedArmorDef);
            var chosenArmor = LoadoutSelectionService.SelectArmor(shooting, melee, assignedArmor, armorPool);
            var armor = chosenArmor == null
                ? null
                : armorThings.FirstOrDefault(apparel => apparel.def.defName == chosenArmor.DefName);

            return (weapon, armor);
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Armory ResolveKit failed safely: {ex.Message}");
            return (null, null);
        }
    }

    // Equips a resolved weapon and dons resolved armour (Wear auto-drops the conflicting civvies).
    public static void EquipKit(Pawn pawn, Thing? weapon, Thing? armor)
    {
        try
        {
            if (pawn?.equipment != null && weapon is ThingWithComps weaponWithComps)
            {
                if (weapon.Spawned)
                {
                    weapon.DeSpawn();
                }

                pawn.equipment.MakeRoomFor(weaponWithComps);
                pawn.equipment.AddEquipment(weaponWithComps);
            }

            if (pawn?.apparel != null && armor is Apparel apparel)
            {
                if (apparel.Spawned)
                {
                    apparel.DeSpawn();
                }

                pawn.apparel.Wear(apparel, dropReplacedApparel: true);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Armory EquipKit failed safely: {ex.Message}");
        }
    }

    // Puts the weapon and worn combat armour back. RimWorld's apparel policy then re-dresses civvies.
    public static void ReturnKit(Pawn pawn, Map map)
    {
        try
        {
            if (pawn?.equipment?.Primary is ThingWithComps primary)
            {
                pawn.equipment.TryDropEquipment(primary, out _, pawn.Position);
            }

            if (pawn?.apparel != null && map != null)
            {
                var combatArmor = pawn.apparel.WornApparel?
                    .Where(apparel => apparel.def.GetStatValueAbstract(StatDefOf.ArmorRating_Sharp) >= HeavyArmorThreshold)
                    .ToList() ?? new List<Apparel>();
                foreach (var apparel in combatArmor)
                {
                    pawn.apparel.TryDrop(apparel, out _, pawn.Position);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Armory ReturnKit failed safely: {ex.Message}");
        }
    }

    private static int SkillLevel(Pawn pawn, SkillDef skill)
    {
        return pawn?.skills?.GetSkill(skill)?.Level ?? 0;
    }
}
