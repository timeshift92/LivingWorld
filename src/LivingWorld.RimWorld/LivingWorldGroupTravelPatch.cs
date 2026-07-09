using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Gives neutral faction arrivals (visitors, traders, travellers) a believable source: instead of
/// appearing at the map edge, the group sets out from one of its faction's settlements and crosses the
/// world map to the colony, materializing on arrival. A Prefix on each arrival incident defers it into a
/// travelling group; on arrival the world component re-fires the same incident with FiringArrival set,
/// which lets the vanilla worker run and spawn the group. Fail-open: if travel can't be set up (disabled,
/// no map, no source settlement) the incident fires immediately as before, so an arrival is never lost.
///
/// Each of visitor/trader/traveller overrides its own TryExecuteWorker, so each needs its own patch; all
/// route through the same shared deferral.
/// </summary>
internal static class LivingWorldGroupTravel
{
    // Returns true if the incident was deferred into a travelling group (caller returns false to skip the
    // vanilla worker); false to let the vanilla incident fire now.
    public static bool TryDefer(IncidentWorker instance, IncidentParms parms, string kindKey)
    {
        if (ApproachingGroupRuntime.FiringArrival)
        {
            return false;
        }

        try
        {
            var component = LivingWorldWorldComponent.Instance;
            return component != null && component.TryLaunchApproachingGroup(instance?.def, parms, kindKey);
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Group-arrival travel deferral skipped safely: {ex.Message}");
            return false;
        }
    }
}

[HarmonyPatch(typeof(IncidentWorker_VisitorGroup), "TryExecuteWorker")]
public static class LivingWorldVisitorGroupTravelPatch
{
    public static bool Prefix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
    {
        if (LivingWorldGroupTravel.TryDefer(__instance, parms, "LW_ArrivalKind_Visitors"))
        {
            __result = true;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(IncidentWorker_TraderCaravanArrival), "TryExecuteWorker")]
public static class LivingWorldTraderCaravanTravelPatch
{
    public static bool Prefix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
    {
        if (LivingWorldGroupTravel.TryDefer(__instance, parms, "LW_ArrivalKind_Traders"))
        {
            __result = true;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(IncidentWorker_TravelerGroup), "TryExecuteWorker")]
public static class LivingWorldTravelerGroupTravelPatch
{
    public static bool Prefix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
    {
        if (LivingWorldGroupTravel.TryDefer(__instance, parms, "LW_ArrivalKind_Travelers"))
        {
            __result = true;
            return false;
        }

        return true;
    }
}
