using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Arms player pawns for expeditions before RimWorld converts them into a world caravan. This keeps the
/// armory system useful outside base raids without rewriting the vanilla caravan formation flow.
///
/// <para><see cref="CaravanExitMapUtility.ExitMapAndCreateCaravan"/> has two overloads (a Direction8Way
/// convenience and the all-PlanetTile core), so a bare <c>[HarmonyPatch(type, name)]</c> is an ambiguous
/// match and throws at mod load. We resolve both explicit signatures via <c>TargetMethods</c>. Arming is
/// idempotent — already-armed pawns are skipped — so covering both overloads never double-equips.</para>
/// </summary>
[HarmonyPatch]
public static class LivingWorldCaravanArmoryPatch
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        var pawns = typeof(IEnumerable<Pawn>);

        var core = AccessTools.Method(
            typeof(CaravanExitMapUtility), nameof(CaravanExitMapUtility.ExitMapAndCreateCaravan),
            new[] { pawns, typeof(Faction), typeof(PlanetTile), typeof(PlanetTile), typeof(PlanetTile), typeof(bool) });
        if (core != null)
        {
            yield return core;
        }

        var directional = AccessTools.Method(
            typeof(CaravanExitMapUtility), nameof(CaravanExitMapUtility.ExitMapAndCreateCaravan),
            new[] { pawns, typeof(Faction), typeof(PlanetTile), typeof(Direction8Way), typeof(PlanetTile), typeof(bool) });
        if (directional != null)
        {
            yield return directional;
        }
    }

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
                if (weapon == null && armor.Count == 0)
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
