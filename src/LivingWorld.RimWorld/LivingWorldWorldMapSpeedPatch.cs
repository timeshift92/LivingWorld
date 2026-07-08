using HarmonyLib;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(TickManager), "get_TickRateMultiplier")]
public static class LivingWorldWorldMapSpeedPatch
{
    private static bool loggedGateOnce;

    public static void Postfix(TickManager __instance, ref float __result)
    {
        var settings = LivingWorldSettings.Instance;
        var worldRendered = WorldRendererUtility.WorldRendered;

        // One-shot diagnostic: the first time the planet is on screen, record every gate input so a
        // "the speed-up isn't working" report can be diagnosed straight from Player.log. Fires once
        // per session, so it never spams.
        if (!loggedGateOnce && worldRendered)
        {
            loggedGateOnce = true;
            Log.Message(
                $"[LivingWorld] WorldMapSpeed gate: enabled={settings?.worldMapSpeedTestEnabled} "
                + $"multiplier={settings?.worldMapSpeedMultiplier} speed={__instance.CurTimeSpeed} "
                + $"rendered={worldRendered} selected={WorldRendererUtility.WorldSelected} "
                + $"background={WorldRendererUtility.WorldBackgroundNow} base={__result}");
        }

        // Only boost while the planet view is actually up (WorldRendered is false on colony maps) and
        // the player has asked for Fast+ time. WorldSelected was too strict — it also required an
        // active world selection, so the override never fired during ordinary world-map viewing.
        if (settings == null
            || !settings.worldMapSpeedTestEnabled
            || __result <= 0f
            || __instance.CurTimeSpeed < TimeSpeed.Fast
            || !worldRendered)
        {
            return;
        }

        __result = Mathf.Max(__result, settings.worldMapSpeedMultiplier);
    }
}
