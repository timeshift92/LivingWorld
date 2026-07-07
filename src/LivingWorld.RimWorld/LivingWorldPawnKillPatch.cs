using HarmonyLib;
using LivingWorld.Core;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(Pawn), "Kill")]
public static class LivingWorldPawnKillPatch
{
    public static void Postfix(Pawn __instance)
    {
        var component = LivingWorldWorldComponent.Instance;
        if (component == null || __instance == null)
        {
            return;
        }

        RaidPawnBindingService.MarkPawnDead(
            component.State,
            __instance.thingIDNumber,
            "pawn killed on map");
    }
}
