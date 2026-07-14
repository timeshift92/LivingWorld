using System;
using System.Linq;

namespace LivingWorld.Core;

internal static class DiplomacyActionExecutor
{
    public static bool Execute(WorldState state, FactionActionPlan plan, WorldWarRequest request)
    {
        var source = WorldWarTargetSelector.FindReadySourceSettlement(state, plan.FactionId);
        var targetFaction = ResolveTargetFaction(state, plan);
        var delta = Math.Abs(request.DiplomatGoodwill);
        if (source == null || targetFaction == null || delta <= 0)
        {
            return false;
        }

        var crew = TravelCrewService.FindAvailableCrew(state, source.Id);
        if (crew == null)
        {
            return false;
        }

        // The mission travels to a settlement of the target faction (for the world-map marker + tile).
        var targetSettlement = ResolveTargetSettlement(state, plan, targetFaction);
        if (targetSettlement == null)
        {
            return false;
        }

        if (!WorldTrafficPolicy.CanDispatchMission(state, WorldMissionKind.Diplomat, plan.FactionId))
        {
            return false;
        }

        var arrivalTick = request.Tick + (Math.Max(1, request.TravelDays) * 60_000);
        var mission = state.DispatchMission(
            WorldMissionKind.Diplomat,
            plan.FactionId,
            source.Id,
            targetSettlement.Id,
            request.Tick,
            arrivalTick,
            targetFactionId: targetFaction,
            amount: delta,
            crewCitizenId: crew.Id);
        if (TravelCrewService.ReserveCrew(state, source.Id, mission.Id, crew.Id, "diplomatic mission launched"))
        {
            return true;
        }

        state.RemoveMissionForLedger(mission.Id);
        return false;
    }

    private static string? ResolveTargetFaction(WorldState state, FactionActionPlan plan)
    {
        if (!string.IsNullOrWhiteSpace(plan.TargetFactionId)
            && !state.IsFactionIrreconcilable(plan.FactionId)
            && !state.IsFactionIrreconcilable(plan.TargetFactionId!)
            && !state.IsPlayerFaction(plan.TargetFactionId!))
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
                && !state.IsPlayerFaction(target.FactionId)
                && !state.IsFactionIrreconcilable(target.FactionId))
            {
                return target;
            }
        }

        return WorldWarTargetSelector.FindDiplomacyTargetSettlement(state, plan.FactionId, targetFaction);
    }
}
