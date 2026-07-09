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

        if (LivingWorldAnimalMapPawnTracker.TryMarkDead(__instance, "settlement map animal killed"))
        {
            return;
        }

        // CompLivingWorldIdentity is preferred; thingIDNumber remains an identity fallback.
        if (!LivingWorldPawnIdentityService.TryGetLedgerId(__instance, out var ledgerId))
        {
            return;
        }

        LivingWorldPawnSyncService.Apply(
            component.State,
            new PawnFateSyncRequest(
                ledgerId,
                PawnFateKind.Dead,
                "pawn killed on map"));
    }
}
