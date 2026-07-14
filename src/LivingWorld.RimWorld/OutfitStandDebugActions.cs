using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;
using Verse.AI;

namespace LivingWorld.RimWorld;

/// <summary>
/// Developer-mode helpers for the outfit-stand mobilization flow, reachable through the in-game dev "Debug
/// actions" menu. "Spawn combat test gear" drops a pile of weapons, a full armour set and civilian clothing at
/// the map centre so the player can quickly stock outfit stands. "Dump mobilization phases" prints, for every
/// combat colonist, the exact state the phase machine sees — for debugging live behaviour without guessing.
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

    [DebugAction("LivingWorld", "Dump mobilization phases", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void DumpMobilizationPhases()
    {
        var map = Find.CurrentMap;
        var component = MobilizationMapComponent.For(map);
        if (map == null || component == null)
        {
            return;
        }

        Log.Message($"[LivingWorld] Mobilization dump — mobilized={component.IsMobilized} "
                    + $"(manual={component.ManualMobilized}, threat={component.ThreatPresent}, tier={component.CurrentTier}), "
                    + $"CAI={CaiBridge.Available}");

        foreach (var pawn in map.mapPawns.FreeColonistsSpawned.ToList())
        {
            if (pawn == null)
            {
                continue;
            }

            Log.Message($"[LivingWorld]   {pawn.LabelShort}: candidate={MobilizationCandidates.IsCandidate(pawn)}, "
                        + $"busyUrgent={MobilizationCandidates.IsBusyUrgent(pawn)}, "
                        + $"asleep={!RestUtility.Awake(pawn)}, armed={MobilizationCandidates.IsArmed(pawn)}, "
                        + $"stand={OutfitStandKit.HasStand(pawn)}, drafted={pawn.Drafted}, "
                        + $"combatPolicy={MobilizationPolicyService.IsCombatPolicy(pawn)}, "
                        + $"civilianPolicy={MobilizationPolicyService.IsCivilianPolicy(pawn)}, "
                        + component.DiagnosePawn(pawn));
        }
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
