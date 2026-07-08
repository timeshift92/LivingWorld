using HarmonyLib;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(TickManager), "get_TickRateMultiplier")]
public static class LivingWorldWorldMapSpeedPatch
{
    private static float lastLoggedResult = -1f;

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

        var boosted = Mathf.Max(__result, settings.worldMapSpeedMultiplier);

        // Log only when the effective tick rate changes, so we can confirm the multiplier is really
        // applied (e.g. 10x) without spamming every frame. Debug-gated.
        if (settings.debugLogging && Mathf.Abs(boosted - lastLoggedResult) > 0.01f)
        {
            lastLoggedResult = boosted;
            Log.Message(
                $"[LivingWorld] world-map speed: mult={settings.worldMapSpeedMultiplier}"
                + $" speed={__instance.CurTimeSpeed} tickRate {__result} -> {boosted}");
        }

        __result = boosted;
    }
}
