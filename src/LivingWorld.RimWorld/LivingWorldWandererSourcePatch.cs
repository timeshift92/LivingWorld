using System;
using HarmonyLib;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Sources the vanilla "a wanderer joins" incident against the tracked outside-world population instead of
/// conjuring a colonist from nowhere. A wandering joiner is drawn from the drifter reservoir: the incident
/// is vetoed when that reservoir is empty (a depleted world has no one left to send), and each successful
/// join draws one person from it. Fail-open: any error leaves the vanilla incident untouched, and the whole
/// behaviour is gated by the drifter-flow setting.
/// </summary>
[HarmonyPatch(typeof(IncidentWorker_WandererJoin), "CanFireNowSub")]
public static class LivingWorldWandererGatePatch
{
    public static void Postfix(ref bool __result)
    {
        if (!__result)
        {
            return;
        }

        try
        {
            var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
            if (!settings.drifterFlowEnabled)
            {
                return;
            }

            var component = LivingWorldWorldComponent.Instance;
            if (component != null && component.IsBootstrapped && component.State.DrifterArrivalReservoir < 1)
            {
                // No one left in the outside world to wander in.
                __result = false;
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Wanderer-source gate skipped safely: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(IncidentWorker_WandererJoin), "TryExecuteWorker")]
public static class LivingWorldWandererConsumePatch
{
    public static void Postfix(bool __result)
    {
        if (!__result)
        {
            return;
        }

        try
        {
            var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
            if (!settings.drifterFlowEnabled)
            {
                return;
            }

            var component = LivingWorldWorldComponent.Instance;
            if (component != null && component.IsBootstrapped)
            {
                // The joiner came from the outside-world pool — deplete it by one.
                DrifterArrivalService.TakeForArrival(component.State, 1);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Wanderer-source consume skipped safely: {ex.Message}");
        }
    }
}
