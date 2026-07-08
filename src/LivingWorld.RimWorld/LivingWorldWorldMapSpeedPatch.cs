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

        // Only boost while the planet view is actually up (WorldRendered is false on colony maps) and
        // the player has asked for Fast+ time — the multiplier lifts Fast/Superfast, never pause or
        // normal speed.
        if (settings == null
            || !settings.worldMapSpeedTestEnabled
            || __result <= 0f
            || __instance.CurTimeSpeed < TimeSpeed.Fast
            || !WorldRendererUtility.WorldRendered)
        {
            return;
        }

        __result = Mathf.Max(__result, settings.worldMapSpeedMultiplier);
    }
}
