using HarmonyLib;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(TickManager), "get_TickRateMultiplier")]
public static class LivingWorldWorldMapSpeedPatch
{
    // Ceiling on the boosted tick rate. Vanilla tops out at 15 (Ultrafast); multiplying can push far
    // past that, so cap it to keep the world map responsive instead of running unbounded.
    private const float MaxBoostedTickRate = 60f;

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

        // A real multiplier (as the "{N}x" label promises): scale whatever fast speed the player chose,
        // not Max(base, N) — which did nothing at Superfast/Ultrafast, where vanilla already exceeds N.
        var boosted = Mathf.Min(__result * settings.worldMapSpeedMultiplier, MaxBoostedTickRate);

        // Log only when the effective tick rate changes, so we can confirm the multiplier is really
        // applied without spamming every frame. Debug-gated.
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
