using HarmonyLib;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(TickManager), "get_TickRateMultiplier")]
public static class LivingWorldWorldMapSpeedPatch
{
    public static void Postfix(TickManager __instance, ref float __result)
    {
        var settings = LivingWorldSettings.Instance;
        if (settings == null ||
            !settings.worldMapSpeedTestEnabled ||
            __result <= 0f ||
            __instance.CurTimeSpeed < TimeSpeed.Fast ||
            !WorldRendererUtility.WorldRendered ||
            !WorldRendererUtility.WorldSelected)
        {
            return;
        }

        __result = Mathf.Max(__result, settings.worldMapSpeedMultiplier);
    }
}
