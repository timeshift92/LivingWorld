using System;
using System.Linq;

namespace LivingWorld.Core;

internal static class ScoutingActionExecutor
{
    private const int ScoutIntelValue = 100;

    public static bool Execute(WorldState state, FactionActionPlan plan, WorldWarRequest request)
    {
        var source = WorldWarTargetSelector.FindReadySourceSettlement(state, plan.FactionId);
        var target = ResolveTarget(state, plan);
        if (source == null || target == null)
        {
            return false;
        }

        var crew = TravelCrewService.FindAvailableCrew(state, source.Id);
        if (crew == null)
        {
            return false;
        }

        if (!WorldTrafficPolicy.CanDispatchMission(state, WorldMissionKind.Scout, plan.FactionId))
        {
            return false;
        }

        var arrivalTick = request.Tick + (Math.Max(1, request.TravelDays) * 60_000);
        var mission = state.DispatchMission(
            WorldMissionKind.Scout,
            plan.FactionId,
            source.Id,
            target.Id,
            request.Tick,
            arrivalTick,
            amount: ScoutIntelValue,
            crewCitizenId: crew.Id);
        if (TravelCrewService.ReserveCrew(state, source.Id, mission.Id, crew.Id, "scouting mission launched"))
        {
            return true;
        }

        state.RemoveMissionForLedger(mission.Id);
        return false;
    }

    private static WorldSettlement? ResolveTarget(WorldState state, FactionActionPlan plan)
    {
        if (plan.TargetSettlementId.HasValue)
        {
            var target = state.GetSettlement(plan.TargetSettlementId.Value);
            if (target is { IsActive: true }
                && !string.Equals(target.FactionId, plan.FactionId, StringComparison.Ordinal)
                && !state.IsPlayerFaction(target.FactionId)
                && DiplomacyService.GetStance(state, plan.FactionId, target.FactionId) != RelationStance.Ally
                && !state.HasFactionSettlementIntel(plan.FactionId, target.Id))
            {
                return target;
            }
        }

        return WorldWarTargetSelector.FindScoutingTarget(state, plan.FactionId);
    }
}
