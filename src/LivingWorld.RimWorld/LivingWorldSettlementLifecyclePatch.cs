using System;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Immediate triggers that keep the ledger in sync when other mods add/remove/capture real
/// settlement world objects at runtime. Each postfix is thin, guarded, and fail-open — a daily
/// reconcile pass is the safety net. Player-ownership is decided inside the coordinator, not here,
/// because a capture *to* the player must still be handled (abandon), unlike adds/removes.
/// </summary>
internal static class SettlementLifecycleHooks
{
    internal static void Dispatch(WorldObject? worldObject, Action<SettlementSyncCoordinator, Settlement> action)
    {
        try
        {
            if (worldObject is not Settlement settlement)
            {
                return;
            }

            var component = LivingWorldWorldComponent.Instance;
            if (component?.SettlementSync == null)
            {
                return;
            }

            action(component.SettlementSync, settlement);
        }
        catch (Exception ex)
        {
            if ((LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging)
            {
                Log.Warning($"[LivingWorld] Settlement lifecycle hook failed safely: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}

[HarmonyPatch(typeof(WorldObjectsHolder), nameof(WorldObjectsHolder.Add))]
public static class LivingWorldWorldObjectAddPatch
{
    public static void Postfix(WorldObject o)
    {
        SettlementLifecycleHooks.Dispatch(o, (coordinator, settlement) => coordinator.OnSettlementAdded(settlement));
    }
}

[HarmonyPatch(typeof(WorldObjectsHolder), nameof(WorldObjectsHolder.Remove))]
public static class LivingWorldWorldObjectRemovePatch
{
    public static void Postfix(WorldObject o)
    {
        SettlementLifecycleHooks.Dispatch(o, (coordinator, settlement) => coordinator.OnSettlementRemoved(settlement));
    }
}

[HarmonyPatch(typeof(WorldObject), nameof(WorldObject.SetFaction))]
public static class LivingWorldWorldObjectSetFactionPatch
{
    public static void Postfix(WorldObject __instance)
    {
        SettlementLifecycleHooks.Dispatch(__instance, (coordinator, settlement) => coordinator.OnSettlementFactionChanged(settlement));
    }
}
