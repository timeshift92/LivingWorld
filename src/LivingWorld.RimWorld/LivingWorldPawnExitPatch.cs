using System;
using System.Collections.Generic;
using HarmonyLib;
using LivingWorld.Core;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(Pawn), "ExitMap")]
[HarmonyAfter("helldan.finitepopulation", "rimworld.torann.rimwar", "com.Matathias.Empire")]
[HarmonyPriority(Priority.Last)]
public static class LivingWorldPawnExitMapPatch
{
    public static void Prefix(Pawn __instance)
    {
        LivingWorldPawnExitTracker.BeginExit(__instance);
    }

    public static void Postfix(Pawn __instance)
    {
        try
        {
            LivingWorldPawnExitTracker.TryMarkReturnedIfExiting(__instance, "pawn exited map alive");
        }
        finally
        {
            LivingWorldPawnExitTracker.EndExit(__instance);
        }
    }

    public static Exception? Finalizer(Pawn __instance, Exception? __exception)
    {
        LivingWorldPawnExitTracker.EndExit(__instance);
        return __exception;
    }
}

[HarmonyPatch(typeof(Pawn), "DeSpawn")]
[HarmonyAfter("helldan.finitepopulation", "rimworld.torann.rimwar", "com.Matathias.Empire")]
[HarmonyPriority(Priority.Last)]
public static class LivingWorldPawnDeSpawnPatch
{
    public static void Prefix(Pawn __instance)
    {
        // DeSpawn is used by many mods for map transfers and temporary holders. Only treat it as
        // a departure while the owning Pawn.ExitMap call is in progress. This must be a prefix so
        // tracked gear returns to the ledger before RimWorld detaches the pawn and its holders.
        LivingWorldPawnExitTracker.TryMarkReturnedIfExiting(__instance, "pawn despawned while exiting map");
    }
}

public static class LivingWorldPawnExitTracker
{
    [ThreadStatic]
    private static HashSet<Pawn>? exitingPawns;

    [ThreadStatic]
    private static HashSet<Pawn>? synchronizedPawns;

    public static void BeginExit(Pawn pawn)
    {
        if (pawn == null)
        {
            return;
        }

        (exitingPawns ??= new HashSet<Pawn>()).Add(pawn);
        synchronizedPawns?.Remove(pawn);
    }

    public static void EndExit(Pawn pawn)
    {
        if (pawn == null)
        {
            return;
        }

        exitingPawns?.Remove(pawn);
        synchronizedPawns?.Remove(pawn);
    }

    public static void TryMarkReturnedIfExiting(Pawn pawn, string reason)
    {
        if (pawn == null
            || exitingPawns?.Contains(pawn) != true
            || !(synchronizedPawns ??= new HashSet<Pawn>()).Add(pawn))
        {
            return;
        }

        TryMarkReturned(pawn, reason);
    }

    public static void TryMarkReturned(Pawn pawn, string reason)
    {
        var component = LivingWorldWorldComponent.Instance;
        if (component == null || pawn == null)
        {
            return;
        }

        try
        {
            var exitAction = RaidPawnExitPolicy.Resolve(pawn.Dead, pawn.IsPrisoner, pawn.Downed);
            if (exitAction == RaidPawnExitAction.Return)
            {
                try
                {
                    LivingWorldSettlementMapResourceTracker.ReconcilePawnGear(
                        component.State,
                        pawn,
                        reason);
                }
                catch (Exception gearError)
                {
                    // The persisted resource state remains retryable by map reconciliation. Pawn fate
                    // must still resolve so a failed gear destroy cannot strand the citizen forever.
                    Log.Warning(
                        $"[LivingWorld] Tracked pawn gear return remains pending for {pawn.ThingID}: "
                        + $"{gearError.GetType().Name}: {gearError.Message}");
                }

                LivingWorldCompatibilityVisitorComponent.Instance?.NotifyReturned(
                    component.State,
                    pawn,
                    reason);
                component.NotifyApproachingGroupCarrierReturned(pawn, reason);
            }

            if (LivingWorldAnimalMapPawnTracker.TryMarkReturned(pawn, reason))
            {
                return;
            }

            // CompLivingWorldIdentity is preferred; raid-linked thingID is the compatibility
            // fallback. External mod pawns without either identity remain owned by their source mod.
            if (!LivingWorldPawnIdentityService.TryGetLedgerId(pawn, out var ledgerId))
            {
                return;
            }

            switch (exitAction)
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
        catch (Exception ex)
        {
            // Fail-safe: TryMarkReturned runs from Pawn.ExitMap/DeSpawn postfixes and routes into WorldState
            // ledger methods that throw on a raid-link/army desync. That throw must never unwind into vanilla
            // despawn/exit — pawn exit sync failed safely.
            Log.Warning($"[LivingWorld] Pawn exit sync failed safely for {pawn.ThingID}: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
