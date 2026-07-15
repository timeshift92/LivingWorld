using System;
using HarmonyLib;
using LivingWorld.Core;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(Pawn), "ExitMap")]
public static class LivingWorldPawnExitMapPatch
{
    public static void Postfix(Pawn __instance)
    {
        LivingWorldPawnExitTracker.TryMarkReturned(__instance, "pawn exited map alive");
    }
}

[HarmonyPatch(typeof(Pawn), "DeSpawn")]
public static class LivingWorldPawnDeSpawnPatch
{
    public static void Postfix(Pawn __instance)
    {
        LivingWorldPawnExitTracker.TryMarkReturned(__instance, "pawn despawned alive");
    }
}

public static class LivingWorldPawnExitTracker
{
    public static void TryMarkReturned(Pawn pawn, string reason)
    {
        try
        {
            TryMarkReturnedCore(pawn, reason);
        }
        catch (Exception ex)
        {
            // Fail-safe: this runs from Pawn.ExitMap/DeSpawn postfixes and routes into WorldState ledger
            // methods that throw on a raid-link/army desync (e.g. a link whose army no longer resolves).
            // Such a throw must never unwind into vanilla despawn/exit — pawn exit sync failed safely.
            Log.Warning($"[LivingWorld] Pawn exit sync failed safely for {pawn?.LabelShortCap}: {ex.Message}");
        }
    }

    private static void TryMarkReturnedCore(Pawn pawn, string reason)
    {
        var component = LivingWorldWorldComponent.Instance;
        if (component == null || pawn == null)
        {
            return;
        }

        if (LivingWorldAnimalMapPawnTracker.TryMarkReturned(pawn, reason))
        {
            return;
        }

        // CompLivingWorldIdentity is preferred; thingIDNumber remains an identity fallback.
        if (!LivingWorldPawnIdentityService.TryGetLedgerId(pawn, out var ledgerId))
        {
            return;
        }

        switch (RaidPawnExitPolicy.Resolve(pawn.Dead, pawn.IsPrisoner, pawn.Downed))
        {
            case RaidPawnExitAction.Capture:
                LivingWorldPawnSyncService.Apply(
                    component.State,
                    new PawnFateSyncRequest(
                        ledgerId,
                        PawnFateKind.Prisoner,
                        "pawn is prisoner"));
                break;
            case RaidPawnExitAction.Return:
                LivingWorldPawnSyncService.Apply(
                    component.State,
                    new PawnFateSyncRequest(
                        ledgerId,
                        PawnFateKind.Returned,
                        reason));
                break;
            case RaidPawnExitAction.Miss:
                LivingWorldPawnSyncService.Apply(
                    component.State,
                    new PawnFateSyncRequest(
                        ledgerId,
                        PawnFateKind.Missing,
                        "downed raider left the map with unknown fate"));
                break;
            case RaidPawnExitAction.Ignore:
                // Dead — handled by the kill patch.
                break;
        }
    }
}
