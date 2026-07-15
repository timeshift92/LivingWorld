using System;
using System.Linq;

namespace LivingWorld.Core;

internal static class ScoutingActionExecutor
{
    private const int ScoutIntelValue = 100;

    public static ActionAttemptResult Execute(WorldState state, FactionActionPlan plan, WorldWarRequest request)
    {
        var source = WorldWarTargetSelector.FindReadySourceSettlement(state, plan.FactionId);
        var target = ResolveTarget(state, plan);
        var endpoint = state.PlayerContactEndpoint;
        var targetsPlayerContact = target == null
            && endpoint?.IsAvailable == true
            && string.Equals(endpoint.FactionId, plan.TargetFactionId, StringComparison.Ordinal)
            && WorldWarTargetSelector.CanScoutPlayerContact(state, plan.FactionId);
        if (source == null || (target == null && !targetsPlayerContact))
        {
            return ActionAttemptResult.Failed(ActionAttemptReason.NoTarget, "no unknown scouting target or player contact endpoint");
        }

        var crew = TravelCrewService.FindAvailableCrew(state, source.Id);
        if (crew == null)
        {
            return ActionAttemptResult.Failed(ActionAttemptReason.NoCrew, "no available scout crew");
        }

        if (!WorldTrafficPolicy.CanDispatchMission(state, WorldMissionKind.Scout, plan.FactionId))
        {
            return ActionAttemptResult.Failed(ActionAttemptReason.TrafficCap, "scout mission traffic cap reached");
        }

        var arrivalTick = request.Tick + (Math.Max(1, request.TravelDays) * 60_000);
        var mission = targetsPlayerContact
            ? state.DispatchPlayerContactMission(
                WorldMissionKind.Scout,
                plan.FactionId,
                source.Id,
                request.Tick,
                arrivalTick,
                ScoutIntelValue,
                crew.Id)
            : state.DispatchMission(
                WorldMissionKind.Scout,
                plan.FactionId,
                source.Id,
                target!.Id,
                request.Tick,
                arrivalTick,
                amount: ScoutIntelValue,
                crewCitizenId: crew.Id);
        if (TravelCrewService.ReserveCrew(state, source.Id, mission.Id, crew.Id, "scouting mission launched"))
        {
            return ActionAttemptResult.Success("scouting party launched");
        }

        state.RemoveMissionForLedger(mission.Id);
        return ActionAttemptResult.Failed(ActionAttemptReason.NoCrew, "scout crew reservation failed");
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
