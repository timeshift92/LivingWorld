using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Gives neutral faction arrivals (visitors, traders, travellers) a believable source: instead of
/// appearing at the map edge, the group sets out from one of its faction's settlements and crosses the
/// world map to the colony, materializing on arrival. A Prefix on each arrival incident defers it into a
/// travelling group; on arrival the world component re-fires the same incident with FiringArrival set,
/// which lets the vanilla worker run and spawn the group. Explicitly disabling travel keeps vanilla
/// behavior for compatibility; once a faction is ledger-backed, reservation failures block the incident.
///
/// Each of visitor/trader/traveller overrides its own TryExecuteWorker, so each needs its own patch; all
/// route through the same shared deferral. A ledger-backed faction is never allowed to fall through to
/// an immediate vanilla spawn when its reservation fails: that incident is blocked and may be retried by
/// the storyteller later.
/// </summary>
internal static class LivingWorldGroupTravel
{
    public static ApproachingGroupLaunchResult TryDefer(IncidentWorker instance, IncidentParms parms, string kindKey)
    {
        if (ApproachingGroupRuntime.FiringArrival)
        {
            return ApproachingGroupLaunchResult.NotHandled;
        }

        var component = LivingWorldWorldComponent.Instance;
        try
        {
            return component?.TryLaunchApproachingGroup(instance?.def, parms, kindKey)
                ?? ApproachingGroupLaunchResult.NotHandled;
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Group-arrival travel deferral failed safely: {ex.Message}");
            var factionId = parms?.faction?.def?.defName;
            var tracked = component != null
                && !string.IsNullOrWhiteSpace(factionId)
                && component.State.Settlements.Any(settlement =>
                    settlement.IsActive
                    && string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal));
            return tracked ? ApproachingGroupLaunchResult.Blocked : ApproachingGroupLaunchResult.NotHandled;
        }
    }

    public static bool ApplyDecision(ApproachingGroupLaunchResult decision, ref bool result)
    {
        if (decision == ApproachingGroupLaunchResult.NotHandled)
        {
            return true;
        }

        result = decision == ApproachingGroupLaunchResult.Deferred;
        return false;
    }
}

[HarmonyPatch(typeof(IncidentWorker_VisitorGroup), "TryExecuteWorker")]
public static class LivingWorldVisitorGroupTravelPatch
{
    public static bool Prefix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
    {
        return LivingWorldGroupTravel.ApplyDecision(
            LivingWorldGroupTravel.TryDefer(__instance, parms, "LW_ArrivalKind_Visitors"),
            ref __result);
    }
}

[HarmonyPatch(typeof(IncidentWorker_TraderCaravanArrival), "TryExecuteWorker")]
public static class LivingWorldTraderCaravanTravelPatch
{
    public static bool Prefix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
    {
        return LivingWorldGroupTravel.ApplyDecision(
            LivingWorldGroupTravel.TryDefer(__instance, parms, "LW_ArrivalKind_Traders"),
            ref __result);
    }
}

[HarmonyPatch(typeof(IncidentWorker_TravelerGroup), "TryExecuteWorker")]
public static class LivingWorldTravelerGroupTravelPatch
{
    public static bool Prefix(IncidentWorker __instance, IncidentParms parms, ref bool __result)
    {
        return LivingWorldGroupTravel.ApplyDecision(
            LivingWorldGroupTravel.TryDefer(__instance, parms, "LW_ArrivalKind_Travelers"),
            ref __result);
    }
}
