using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Makes mechanoid ground raids depart from a finite complex inventory. Units, steel and energy are
/// reserved before vanilla generation; an unsuccessful incident restores them, while a successful raid
/// permanently removes the launched force from its source complex.
/// </summary>
[HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "TryExecuteWorker")]
[HarmonyAfter("helldan.finitepopulation", "rimworld.torann.rimwar", "com.Matathias.Empire")]
[HarmonyPriority(Priority.Last)]
public static class LivingWorldMechRaidPatch
{
    public static bool Prefix(IncidentParms parms, ref bool __result)
    {
        if (parms?.faction?.def != FactionDefOf.Mechanoid)
        {
            return true;
        }

        var component = LivingWorldWorldComponent.Instance;
        if (component == null || component.IsRimWarActive)
        {
            return true;
        }

        if (component.TryReserveMechanoidRaid(parms, out _))
        {
            return true;
        }

        __result = false;
        Log.Message("[LivingWorld] Blocked a mechanoid raid because no complex could supply its units, steel and energy.");
        return false;
    }

    public static void Postfix(IncidentParms parms, bool __result)
    {
        if (MechRaidReservationRuntime.TryTake(parms, out var committed))
        {
            LivingWorldWorldComponent.Instance?.CompleteMechanoidRaidReservation(committed, __result);
            return;
        }

        if (parms?.faction?.def != FactionDefOf.Mechanoid)
        {
            return;
        }

        try
        {
            var component = LivingWorldWorldComponent.Instance;
            if (component == null || component.IsRimWarActive)
            {
                return;
            }

            component.NotifyMechanoidRaid(parms);
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Mechanoid raid attribution skipped safely: {ex.Message}");
        }
    }

    public static Exception? Finalizer(IncidentParms parms, Exception? __exception)
    {
        if (__exception != null
            && MechRaidReservationRuntime.TryTake(parms, out var reservation))
        {
            LivingWorldWorldComponent.Instance?.CompleteMechanoidRaidReservation(reservation, launched: false);
        }

        return __exception;
    }
}

[HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "TryResolveRaidFaction")]
[HarmonyAfter("helldan.finitepopulation", "rimworld.torann.rimwar", "com.Matathias.Empire")]
[HarmonyPriority(Priority.Last - 1)]
public static class LivingWorldMechRaidFactionResolutionPatch
{
    public static void Postfix(IncidentParms parms, ref bool __result)
    {
        if (!__result
            || parms?.faction?.def != FactionDefOf.Mechanoid
            || MechRaidReservationRuntime.Has(parms))
        {
            return;
        }

        var component = LivingWorldWorldComponent.Instance;
        if (component == null || component.IsRimWarActive)
        {
            return;
        }

        if (!component.TryReserveMechanoidRaid(parms, out _))
        {
            __result = false;
            Log.Message("[LivingWorld] Blocked a mechanoid raid because no complex could supply its force.");
        }
    }
}
