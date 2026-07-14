using HarmonyLib;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(Pawn_GuestTracker), "CapturedBy")]
public static class LivingWorldPawnCapturePatch
{
    public static void Postfix(Pawn_GuestTracker __instance, Faction by)
    {
        var pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
        if (pawn == null || !pawn.IsPrisoner)
        {
            return;
        }

        LivingWorldPrisonerRuntime.TryApply(
            pawn,
            PrisonerLifecycleAction.Capture,
            by?.def?.defName ?? pawn.HostFaction?.def?.defName ?? "Unknown",
            "pawn captured");
    }
}
