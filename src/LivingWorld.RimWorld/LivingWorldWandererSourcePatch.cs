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
/// join draws one person from it. Reservation failures close the incident without spawning a free pawn;
/// the whole behaviour remains gated by the drifter-flow setting.
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
    public static bool Prefix(ref bool __result, out WandererSourceReservation? __state)
    {
        __state = null;
        try
        {
            var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
            var component = LivingWorldWorldComponent.Instance;
            if (!settings.drifterFlowEnabled || component == null || !component.IsBootstrapped)
            {
                return true;
            }

            var reservoirBefore = component.State.DrifterArrivalReservoir;
            if (DrifterArrivalService.TakeForArrival(component.State, 1) != 1)
            {
                __result = false;
                return false;
            }

            __state = new WandererSourceReservation(component.State, reservoirBefore);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Wanderer-source reservation failed closed: {ex.Message}");
            __result = false;
            return false;
        }
    }

    public static void Postfix(bool __result, WandererSourceReservation? __state)
    {
        if (__state == null)
        {
            return;
        }

        if (__result)
        {
            __state.Commit();
        }
        else
        {
            __state.Rollback("vanilla wanderer incident did not execute");
        }
    }

    public static Exception? Finalizer(Exception? __exception, WandererSourceReservation? __state)
    {
        if (__exception != null)
        {
            __state?.Rollback("vanilla wanderer incident threw");
        }

        return __exception;
    }

    public sealed class WandererSourceReservation
    {
        private readonly WorldState state;
        private readonly int reservoirBefore;
        private bool resolved;

        public WandererSourceReservation(WorldState state, int reservoirBefore)
        {
            this.state = state;
            this.reservoirBefore = Math.Max(0, reservoirBefore);
        }

        public void Commit()
        {
            resolved = true;
        }

        public void Rollback(string reason)
        {
            if (resolved)
            {
                return;
            }

            try
            {
                var current = state.DrifterArrivalReservoir;
                if (current < reservoirBefore)
                {
                    state.AddDrifterArrivalReservoir(reservoirBefore - current, reason);
                }
                else if (current > reservoirBefore)
                {
                    DrifterArrivalService.TakeForArrival(state, current - reservoirBefore);
                }

                resolved = state.DrifterArrivalReservoir == reservoirBefore;
                if (!resolved)
                {
                    Log.Error(
                        $"[LivingWorld] Wanderer reservoir rollback mismatch: "
                        + $"expected {reservoirBefore}, found {state.DrifterArrivalReservoir}.");
                }
            }
            catch (Exception rollbackError)
            {
                resolved = state.DrifterArrivalReservoir == reservoirBefore;
                Log.Error(
                    $"[LivingWorld] Wanderer reservoir rollback failed: "
                    + $"{rollbackError.GetType().Name}: {rollbackError.Message}");
            }
        }
    }
}
