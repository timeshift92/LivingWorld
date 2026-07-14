using System;
using HarmonyLib;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

public static class LivingWorldPrisonerRuntime
{
    public static bool TryApply(
        Pawn pawn,
        PrisonerLifecycleAction action,
        string actorFactionId,
        string reason)
    {
        var component = LivingWorldWorldComponent.Instance;
        if (component == null
            || pawn == null
            || !LivingWorldPawnIdentityService.TryGetLedgerId(pawn, out var ledgerId))
        {
            return false;
        }

        var state = component.State;
        if (action != PrisonerLifecycleAction.Capture
            && state.GetPrisonerRecord(ledgerId) == null
            && state.GetCitizen(ledgerId)?.Status != CitizenStatus.Prisoner)
        {
            return false;
        }

        try
        {
            var result = PrisonerLifecycleService.Apply(
                state,
                new PrisonerTransitionRequest(
                    ledgerId,
                    pawn.thingIDNumber,
                    action,
                    actorFactionId,
                    reason));
            return result.Status is PrisonerTransitionStatus.Success
                or PrisonerTransitionStatus.AlreadyApplied;
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Prisoner {action} sync skipped safely for {pawn.ThingID}: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    public static Pawn? GetPawn(Pawn_GuestTracker tracker)
    {
        return tracker == null
            ? null
            : Traverse.Create(tracker).Field("pawn").GetValue<Pawn>();
    }

    public static string FactionId(Faction? faction)
    {
        return faction?.def?.defName ?? "Unknown";
    }
}

[HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.Notify_PawnRecruited))]
public static class LivingWorldPawnRecruitedPatch
{
    public static void Postfix(Pawn_GuestTracker __instance)
    {
        var pawn = LivingWorldPrisonerRuntime.GetPawn(__instance);
        if (pawn == null)
        {
            return;
        }

        LivingWorldPrisonerRuntime.TryApply(
            pawn,
            PrisonerLifecycleAction.Recruit,
            LivingWorldPrisonerRuntime.FactionId(pawn.Faction ?? Faction.OfPlayer),
            "pawn recruited from captivity");
    }
}

[HarmonyPatch(typeof(Pawn_GuestTracker), nameof(Pawn_GuestTracker.SetGuestStatus))]
public static class LivingWorldPawnEnslavedPatch
{
    public static void Postfix(Pawn_GuestTracker __instance, GuestStatus guestStatus, Faction newHost)
    {
        if (guestStatus != GuestStatus.Slave)
        {
            return;
        }

        var pawn = LivingWorldPrisonerRuntime.GetPawn(__instance);
        if (pawn == null || !pawn.IsSlave)
        {
            return;
        }

        LivingWorldPrisonerRuntime.TryApply(
            pawn,
            PrisonerLifecycleAction.Enslave,
            LivingWorldPrisonerRuntime.FactionId(pawn.Faction ?? newHost ?? Faction.OfPlayer),
            "pawn enslaved from captivity");
    }
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.Notify_Released))]
public static class LivingWorldPawnReleasedPatch
{
    public static void Postfix(Pawn __instance)
    {
        LivingWorldPrisonerRuntime.TryApply(
            __instance,
            PrisonerLifecycleAction.Release,
            LivingWorldPrisonerRuntime.FactionId(__instance.HostFaction ?? Faction.OfPlayer),
            "pawn released from captivity");
    }
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.Notify_PrisonBreakout))]
public static class LivingWorldPawnPrisonBreakoutPatch
{
    public static void Postfix(Pawn __instance)
    {
        LivingWorldPrisonerRuntime.TryApply(
            __instance,
            PrisonerLifecycleAction.Escape,
            LivingWorldPrisonerRuntime.FactionId(__instance.HostFaction ?? Faction.OfPlayer),
            "pawn escaped during a prison break");
    }
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
[HarmonyAfter("helldan.finitepopulation", "rimworld.torann.rimwar", "com.Matathias.Empire")]
[HarmonyPriority(Priority.Last)]
public static class LivingWorldPrisonerFactionFallbackPatch
{
    public static void Postfix(Pawn __instance, Faction newFaction)
    {
        if (newFaction == null || newFaction != Faction.OfPlayer || __instance.IsPrisoner)
        {
            return;
        }

        LivingWorldPrisonerRuntime.TryApply(
            __instance,
            __instance.IsSlaveOfColony
                ? PrisonerLifecycleAction.Enslave
                : PrisonerLifecycleAction.Recruit,
            LivingWorldPrisonerRuntime.FactionId(newFaction),
            "pawn faction changed after captivity");
    }
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
[HarmonyAfter("helldan.finitepopulation", "rimworld.torann.rimwar", "com.Matathias.Empire")]
[HarmonyPriority(Priority.Last)]
public static class LivingWorldPrisonerDeathPatch
{
    public static void Postfix(Pawn __instance)
    {
        LivingWorldPrisonerRuntime.TryApply(
            __instance,
            PrisonerLifecycleAction.Death,
            LivingWorldPrisonerRuntime.FactionId(__instance.Faction ?? __instance.HostFaction),
            "pawn died after entering captivity");
    }
}
