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

            // Never mobilize a colonist who cannot fight: pacifists / violence-incapable pawns, and pawns
            // that cannot be drafted at all (children, etc.). They would only get armed and drafted in vain.
            if (pawn.WorkTagIsDisabled(WorkTags.Violent) || pawn.drafter == null)
            {
                return false;
            }

            var shooting = SkillLevel(pawn, SkillDefOf.Shooting);
            var melee = SkillLevel(pawn, SkillDefOf.Melee);
            var threshold = LivingWorldSettings.Instance?.mobilizationSkillThreshold
                            ?? MobilizationTuning.CombatSkillThreshold;
            return LoadoutSelectionService.IsCombatEligible(shooting, melee, threshold);
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

    // Picks the weapon and the armour set this pawn should take from the racks. Returns the real things
    // (weapon may be null; armour is a possibly-empty set of pieces that can be worn together).
    public static (Thing? weapon, List<Apparel> armor) ResolveKit(Pawn pawn, Map map)
    {
        try
        {
            if (pawn == null || map == null)
            {
                return (null, new List<Apparel>());
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
            var assignedArmorDefs = assignments?.AssignedArmorDefs(pawn) ?? new List<string>();

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

            var body = pawn.RaceProps?.body;
            var armor = new List<Apparel>();

            if (assignedArmorDefs.Count > 0)
            {
                // Explicit loadout from the UI: wear exactly the assigned pieces that are on the racks, in the
                // chosen order, skipping any that cannot be worn with those already picked.
                foreach (var def in assignedArmorDefs)
                {
                    var piece = armorThings.FirstOrDefault(apparel =>
                        apparel.def.defName == def && !armor.Contains(apparel));
                    if (piece != null && (body == null
                        || armor.All(worn => ApparelUtility.CanWearTogether(worn.def, piece.def, body))))
                    {
                        armor.Add(piece);
                    }
                }
            }
            else
            {
                // Auto: wear a full set in protection priority order, taking every piece that fits with those
                // already chosen, so a colonist ends up in the most protective armour available.
                var armorPool = armorThings
                    .Select(apparel => new ArmorOption(
                        apparel.def.defName,
                        ProtectionScore(apparel),
                        apparel.def.GetStatValueAbstract(StatDefOf.ArmorRating_Sharp) >= HeavyArmorThreshold))
                    .ToList();
                var ranked = LoadoutSelectionService.RankArmor(shooting, melee, null, armorPool);
                foreach (var option in ranked)
                {
                    var piece = armorThings.FirstOrDefault(apparel =>
                        apparel.def.defName == option.DefName && !armor.Contains(apparel));
                    if (piece != null && (body == null
                        || armor.All(worn => ApparelUtility.CanWearTogether(worn.def, piece.def, body))))
                    {
                        armor.Add(piece);
                    }
                }
            }

            return (weapon, armor);
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Armory ResolveKit failed safely: {ex.Message}");
            return (null, new List<Apparel>());
        }
    }

    // Higher = more protective. Sharp + blunt armour rating, scaled to a stable integer for ranking.
    private static int ProtectionScore(Apparel apparel)
    {
        var sharp = apparel.def.GetStatValueAbstract(StatDefOf.ArmorRating_Sharp);
        var blunt = apparel.def.GetStatValueAbstract(StatDefOf.ArmorRating_Blunt);
        return (int)((sharp + blunt) * 100f);
    }

    // Equips a resolved weapon and dons the resolved armour set (Wear auto-drops the conflicting civvies).
    public static void EquipKit(Pawn pawn, Thing? weapon, List<Apparel>? armor)
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

            if (pawn?.apparel != null && armor != null && armor.Count > 0)
            {
                var map = pawn.Map;
                var body = pawn.RaceProps?.body;

                // Take off the civvies that clash with the incoming armour and stow them on the clothing
                // rack, so vanilla can re-dress the colonist from there on stand-down (instead of leaving
                // the clothes on the floor where they may be hauled off or deteriorate).
                if (map != null && body != null)
                {
                    var racks = map.listerBuildings?.AllBuildingsColonistOfClass<Building_ArmoryRack>()?.ToList()
                                ?? new List<Building_ArmoryRack>();
                    var clashing = pawn.apparel.WornApparel?
                        .Where(worn => worn != null && armor.Any(piece =>
                            piece != null && !ApparelUtility.CanWearTogether(worn.def, piece.def, body)))
                        .ToList() ?? new List<Apparel>();
                    foreach (var civ in clashing)
                    {
                        var cell = FreeRackCell(racks, ArmoryRackKind.Apparel, civ, pawn.Position, map) ?? pawn.Position;
                        pawn.apparel.TryDrop(civ, out _, cell);
                    }
                }

                foreach (var apparel in armor)
                {
                    if (apparel == null)
                    {
                        continue;
                    }

                    if (apparel.Spawned)
                    {
                        apparel.DeSpawn();
                    }

                    pawn.apparel.Wear(apparel, dropReplacedApparel: true);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Armory EquipKit failed safely: {ex.Message}");
        }
    }

    // Puts the weapon and worn combat armour back onto the racks. The gear is dropped directly onto a free
    // rack cell so it is actually stored, not left on the floor for someone to haul later. RimWorld's
    // apparel policy then re-dresses civvies.
    public static void ReturnKit(Pawn pawn, Map map)
    {
        try
        {
            if (pawn == null || map == null)
            {
                return;
            }

            var racks = map.listerBuildings?.AllBuildingsColonistOfClass<Building_ArmoryRack>()?.ToList()
                        ?? new List<Building_ArmoryRack>();

            if (pawn.equipment?.Primary is ThingWithComps primary)
            {
                var cell = FreeRackCell(racks, ArmoryRackKind.Weapon, primary, pawn.Position, map) ?? pawn.Position;
                pawn.equipment.TryDropEquipment(primary, out _, cell);
            }

            if (pawn.apparel != null)
            {
                var combatArmor = pawn.apparel.WornApparel?
                    .Where(apparel => apparel.def.GetStatValueAbstract(StatDefOf.ArmorRating_Sharp) >= HeavyArmorThreshold)
                    .ToList() ?? new List<Apparel>();
                foreach (var apparel in combatArmor)
                {
                    var cell = FreeRackCell(racks, ArmoryRackKind.Armor, apparel, pawn.Position, map) ?? pawn.Position;
                    pawn.apparel.TryDrop(apparel, out _, cell);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Armory ReturnKit failed safely: {ex.Message}");
        }
    }

    // A free cell on the nearest armory rack of the given kind that will accept the item, so returned gear
    // lands in storage instead of on the floor. Null if there is no such rack/cell (caller drops at feet).
    private static IntVec3? FreeRackCell(
        List<Building_ArmoryRack> racks, ArmoryRackKind kind, Thing item, IntVec3 from, Map map)
    {
        try
        {
            var candidates = racks
                .Where(rack => rack != null && rack.Spawned && rack.Kind == kind && rack.Accepts(item))
                .OrderBy(rack => from.DistanceToSquared(rack.Position));

            foreach (var rack in candidates)
            {
                var cells = rack.slotGroup?.CellsList;
                if (cells == null)
                {
                    continue;
                }

                var max = rack.def?.building?.maxItemsInCell ?? 1;
                foreach (var cell in cells)
                {
                    var itemCount = cell.GetThingList(map).Count(thing => thing?.def?.category == ThingCategory.Item);
                    if (itemCount < max)
                    {
                        return cell;
                    }
                }
            }
        }
        catch
        {
            // Fall through: caller drops at the pawn's feet.
        }

        return null;
    }

    private static int SkillLevel(Pawn pawn, SkillDef skill)
    {
        return pawn?.skills?.GetSkill(skill)?.Level ?? 0;
    }
}
