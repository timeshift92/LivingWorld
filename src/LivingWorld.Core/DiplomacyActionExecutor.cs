using System;
using System.Linq;

namespace LivingWorld.Core;

internal static class DiplomacyActionExecutor
{
    public static ActionAttemptResult Execute(WorldState state, FactionActionPlan plan, WorldWarRequest request)
    {
        var source = WorldWarTargetSelector.FindReadySourceSettlement(state, plan.FactionId);
        var targetFaction = ResolveTargetFaction(state, plan);
        var delta = Math.Abs(request.DiplomatGoodwill);
        if (source == null || targetFaction == null || delta <= 0)
        {
            return ActionAttemptResult.Failed(
                targetFaction == null ? ActionAttemptReason.NoIntel : ActionAttemptReason.InvalidRequest,
                targetFaction == null ? "no known diplomatic target faction" : "diplomatic request has no source or effect");
        }

        if (state.GetSpecialistPool(source.Id) is not { Diplomats: > 0 })
        {
            return ActionAttemptResult.Failed(ActionAttemptReason.NoCrew, "source settlement has no diplomat specialist");
        }

        var crew = TravelCrewService.FindAvailableCrew(state, source.Id);
        if (crew == null)
        {
            return ActionAttemptResult.Failed(ActionAttemptReason.NoCrew, "no available diplomat crew");
        }

        // The mission travels to a settlement of the target faction (for the world-map marker + tile).
        var targetSettlement = ResolveTargetSettlement(state, plan, targetFaction);
        var targetsPlayerContact = state.PlayerContactEndpoint is { IsAvailable: true } endpoint
            && string.Equals(endpoint.FactionId, targetFaction, StringComparison.Ordinal);
        if (targetSettlement == null && !targetsPlayerContact)
        {
            return ActionAttemptResult.Failed(ActionAttemptReason.NoIntel, "no known settlement for the diplomatic target");
        }

        if (!WorldTrafficPolicy.CanDispatchMission(state, WorldMissionKind.Diplomat, plan.FactionId))
        {
            return ActionAttemptResult.Failed(ActionAttemptReason.TrafficCap, "diplomatic mission traffic cap reached");
        }

        var arrivalTick = request.Tick + (Math.Max(1, request.TravelDays) * 60_000);
        var mission = targetsPlayerContact
            ? state.DispatchPlayerContactMission(
                plan.FactionId,
                source.Id,
                request.Tick,
                arrivalTick,
                delta,
                crew.Id)
            : state.DispatchMission(
                WorldMissionKind.Diplomat,
                plan.FactionId,
                source.Id,
                targetSettlement!.Id,
                request.Tick,
                arrivalTick,
                targetFactionId: targetFaction,
                amount: delta,
                crewCitizenId: crew.Id);
        if (TravelCrewService.ReserveCrew(state, source.Id, mission.Id, crew.Id, "diplomatic mission launched"))
        {
            return ActionAttemptResult.Success("physical diplomatic mission launched");
        }

        state.RemoveMissionForLedger(mission.Id);
        return ActionAttemptResult.Failed(ActionAttemptReason.NoCrew, "diplomat crew reservation failed");
    }

    private static string? ResolveTargetFaction(WorldState state, FactionActionPlan plan)
    {
        if (!string.IsNullOrWhiteSpace(plan.TargetFactionId)
            && !state.IsFactionIrreconcilable(plan.FactionId)
            && !state.IsFactionIrreconcilable(plan.TargetFactionId!))
        {
            return plan.TargetFactionId;
        }

        return WorldWarTargetSelector.FindDiplomacyTargetFaction(state, plan.FactionId);
    }

    private static WorldSettlement? ResolveTargetSettlement(
        WorldState state,
        FactionActionPlan plan,
        string targetFaction)
    {
        if (plan.TargetSettlementId.HasValue)
        {
            var target = state.GetSettlement(plan.TargetSettlementId.Value);
            if (target is { IsActive: true }
                && string.Equals(target.FactionId, targetFaction, StringComparison.Ordinal)
                && !state.IsFactionIrreconcilable(target.FactionId))
            {
                return target;
            }
        }

        return WorldWarTargetSelector.FindDiplomacyTargetSettlement(state, plan.FactionId, targetFaction);
    }
}
