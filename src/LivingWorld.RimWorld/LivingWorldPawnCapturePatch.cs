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

        // CompLivingWorldIdentity is preferred; thingIDNumber remains an identity fallback.
        if (!LivingWorldPawnIdentityService.TryGetLedgerId(pawn, out var ledgerId))
        {
            return;
        }

        LivingWorldPawnSyncService.Apply(
            component.State,
            new PawnFateSyncRequest(
                ledgerId,
                PawnFateKind.Prisoner,
                "pawn captured by player"));
    }
}
