using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Arms player pawns for expeditions before RimWorld converts them into a world caravan. This keeps the
/// armory system useful outside base raids without rewriting the vanilla caravan formation flow.
/// </summary>
[HarmonyPatch(typeof(CaravanExitMapUtility), "ExitMapAndCreateCaravan")]
public static class LivingWorldCaravanArmoryPatch
{
    public static void Prefix(IEnumerable<Pawn> pawns)
    {
        CaravanArmoryService.ArmDepartingPawns(pawns);
    }
}

public static class CaravanArmoryService
{
    public static int ArmDepartingPawns(IEnumerable<Pawn>? pawns)
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        if (!settings.armoryMobilizationEnabled || pawns == null)
        {
            return 0;
        }

        var armed = 0;
        try
        {
            foreach (var pawn in pawns.Where(LoadoutAdapter.IsMobilizationCandidate))
            {
                if (pawn.Map == null || LoadoutAdapter.IsArmed(pawn))
                {
                    continue;
                }

                var (weapon, armor) = LoadoutAdapter.ResolveKit(pawn, pawn.Map);
                if (weapon == null && armor == null)
                {
                    continue;
                }

                LoadoutAdapter.EquipKit(pawn, weapon, armor);
                armed++;
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Caravan armory preparation skipped safely: {ex.Message}");
        }

        return armed;
    }
}
