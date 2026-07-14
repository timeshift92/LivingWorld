using System;
using HarmonyLib;
using LivingWorld.Core;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(Pawn), "Kill")]
[HarmonyAfter("helldan.finitepopulation", "rimworld.torann.rimwar", "com.Matathias.Empire")]
[HarmonyPriority(Priority.Last)]
public static class LivingWorldPawnKillPatch
{
    public static void Postfix(Pawn __instance)
    {
        var component = LivingWorldWorldComponent.Instance;
        if (component == null || __instance == null)
        {
            return;
        }

        try
        {
            LivingWorldCompatibilityVisitorComponent.Instance?.NotifyLost(__instance);
            component.NotifyApproachingGroupCarrierLost(__instance);
            if (LivingWorldAnimalMapPawnTracker.TryMarkDead(__instance, "settlement map animal killed"))
            {
                return;
            }

            // CompLivingWorldIdentity is preferred; raid-linked thingID is the compatibility
            // fallback. External mod pawns without either identity remain owned by their source mod.
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
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Pawn death sync skipped safely for {__instance.ThingID}: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
