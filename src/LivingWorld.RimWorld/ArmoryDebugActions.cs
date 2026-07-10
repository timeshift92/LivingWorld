using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Developer-mode helpers for testing the armory / mobilization system. Only reachable through the in-game
/// dev "Debug actions" menu (dev mode on), never in normal play. Spawns a ready-made test kit onto the
/// colony's armory racks so mobilization can be exercised without hand-gathering gear.
/// </summary>
public static class ArmoryDebugActions
{
    private const int CopiesPerItem = 6;

    private static readonly string[] Weapons =
    {
        "Gun_AssaultRifle", "Gun_BoltActionRifle", "Gun_Autopistol", "Gun_PumpShotgun", "MeleeWeapon_LongSword",
    };

    private static readonly string[] Armor =
    {
        "Apparel_FlakVest", "Apparel_FlakPants", "Apparel_FlakJacket", "Apparel_ArmorHelmet", "Apparel_SimpleHelmet",
    };

    // Summer (light) and winter (warm) civilian clothing to test the peacetime redressing.
    private static readonly string[] Civilian =
    {
        "Apparel_BasicShirt", "Apparel_CollarShirt", "Apparel_Pants", "Apparel_CowboyHat",
        "Apparel_Duster", "Apparel_Parka", "Apparel_Tuque",
    };

    [DebugAction("LivingWorld", "Stock armory test kit", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void StockArmoryTestKit()
    {
        var map = Find.CurrentMap;
        if (map == null)
        {
            return;
        }

        StockRack(map, ArmoryRackKind.Weapon, Weapons);
        StockRack(map, ArmoryRackKind.Armor, Armor);
        StockRack(map, ArmoryRackKind.Apparel, Civilian);

        Messages.Message("[LivingWorld] Stocked the armory racks with a test kit.",
            MessageTypeDefOf.TaskCompletion, historical: false);
    }

    private static void StockRack(Map map, ArmoryRackKind kind, IReadOnlyList<string> defNames)
    {
        var rack = map.listerBuildings?.AllBuildingsColonistOfClass<Building_ArmoryRack>()
            ?.FirstOrDefault(candidate => candidate.Kind == kind);
        var cell = rack?.Position ?? map.Center;

        foreach (var name in defNames)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
            if (def == null)
            {
                continue;
            }

            for (var i = 0; i < CopiesPerItem; i++)
            {
                var stuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
                var thing = ThingMaker.MakeThing(def, stuff);
                GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near);
            }
        }
    }
}
