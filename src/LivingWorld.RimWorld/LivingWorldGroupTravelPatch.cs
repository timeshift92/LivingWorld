using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Gives neutral faction visitor groups a believable source: instead of appearing at the map edge, the
/// group sets out from one of its faction's settlements and marches across the world map to the colony,
/// materializing on arrival. A Prefix on the visitor incident defers it into a travelling group; on
/// arrival the world component re-fires the same incident with FiringArrival set, which lets the vanilla
/// worker run and spawn the group. Fail-open: if travel can't be set up (disabled, no map, no source
/// settlement) the incident fires immediately as before, so a visit is never lost.
///
/// Traders and travellers use the same machinery and are wired in follow-up increments.
/// </summary>
[HarmonyPatch(typeof(IncidentWorker_VisitorGroup), "TryExecuteWorker")]
public static class LivingWorldVisitorGroupTravelPatch
{
    public static bool Prefix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
    {
        if (ApproachingGroupRuntime.FiringArrival)
        {
            // Arrival re-fire: let the vanilla worker run and spawn the group.
            return true;
        }

        try
        {
            var component = LivingWorldWorldComponent.Instance;
            if (component != null && component.TryLaunchApproachingGroup(__instance?.def, parms, "LW_ArrivalKind_Visitors"))
            {
                __result = true;
                return false;
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Visitor-group travel deferral skipped safely: {ex.Message}");
        }

        return true;
    }
}
