using HarmonyLib;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(Pawn_GuestTracker), "CapturedBy")]
public static class LivingWorldPawnCapturePatch
{
    public static void Postfix(Pawn_GuestTracker __instance)
    {
        var pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
        if (pawn == null || !pawn.IsPrisoner)
        {
            return;
        }

        var component = LivingWorldWorldComponent.Instance;
        if (component == null)
        {
            return;
        }

        RaidPawnBindingService.MarkPawnPrisoner(
            component.State,
            pawn.thingIDNumber,
            "pawn captured by player");
    }
}
