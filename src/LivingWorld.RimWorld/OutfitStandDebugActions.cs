using System.Collections.Generic;
using LudeonTK;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Developer-mode helper for testing the outfit-stand mobilization flow. Only reachable through the in-game
/// dev "Debug actions" menu. Spawns a ready-made pile of combat gear (weapons, a full armour set, and summer
/// + winter civilian clothing) on the ground so the player can quickly stock their colonists' outfit stands
/// without hand-gathering it.
/// </summary>
public static class OutfitStandDebugActions
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

    private static readonly string[] Civilian =
    {
        "Apparel_BasicShirt", "Apparel_CollarShirt", "Apparel_Pants", "Apparel_CowboyHat",
        "Apparel_Duster", "Apparel_Parka", "Apparel_Tuque",
    };

    [DebugAction("LivingWorld", "Spawn combat test gear", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnCombatTestGear()
    {
        var map = Find.CurrentMap;
        if (map == null)
        {
            return;
        }

        var cell = map.Center;
        Spawn(map, cell, Weapons);
        Spawn(map, cell, Armor);
        Spawn(map, cell, Civilian);

        Messages.Message("[LivingWorld] Spawned combat test gear at the map centre — haul it onto your outfit stands.",
            MessageTypeDefOf.TaskCompletion, historical: false);
    }

    private static void Spawn(Map map, IntVec3 cell, IReadOnlyList<string> defNames)
    {
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
