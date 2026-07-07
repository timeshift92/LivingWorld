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
        var component = LivingWorldWorldComponent.Instance;
        if (component == null || pawn == null)
        {
            return;
        }

        switch (RaidPawnExitPolicy.Resolve(pawn.Dead, pawn.IsPrisoner, pawn.Downed))
        {
            case RaidPawnExitAction.Capture:
                RaidPawnBindingService.MarkPawnPrisoner(
                    component.State,
                    pawn.thingIDNumber,
                    "pawn is prisoner");
                break;
            case RaidPawnExitAction.Return:
                RaidPawnBindingService.MarkPawnReturned(
                    component.State,
                    pawn.thingIDNumber,
                    reason);
                break;
            case RaidPawnExitAction.Ignore:
                // Dead (handled by the kill patch) or downed and not yet resolved.
                break;
        }
    }
}
