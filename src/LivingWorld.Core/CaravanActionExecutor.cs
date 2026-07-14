using System;
using System.Linq;

namespace LivingWorld.Core;

internal static class CaravanActionExecutor
{
    public static ActionAttemptResult Execute(WorldState state, FactionActionPlan plan, WorldWarRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CaravanResourceKey) || request.CaravanQuantity <= 0)
        {
            return ActionAttemptResult.Failed(ActionAttemptReason.InvalidRequest, "caravan cargo request is empty");
        }

        if (!WorldTrafficPolicy.CanDispatchCaravan(state, plan.FactionId))
        {
            return ActionAttemptResult.Failed(ActionAttemptReason.TrafficCap, "caravan traffic cap reached");
        }

        var source = WorldWarTargetSelector.FindTradeSource(state, plan.FactionId, request.CaravanResourceKey);
        var target = ResolveTarget(state, plan);
        if (source == null)
        {
            return ActionAttemptResult.Failed(ActionAttemptReason.NoCargo, "no settlement owns the requested caravan cargo");
        }

        if (target == null)
        {
            return ActionAttemptResult.Failed(ActionAttemptReason.NoIntel, "no known non-hostile trade target");
        }

        var quantity = Math.Min(
            request.CaravanQuantity,
            state.GetOwnedResourceQuantity(source.Id, request.CaravanResourceKey));
        if (quantity <= 0)
        {
            // Nothing to haul — do not launch an empty caravan that just churns create/destroy events.
            return ActionAttemptResult.Failed(ActionAttemptReason.NoCargo, "requested caravan cargo is unavailable");
        }

        var crew = TravelCrewService.FindAvailableCrew(state, source.Id);
        if (crew == null)
        {
            return ActionAttemptResult.Failed(ActionAttemptReason.NoCrew, "no available caravan crew");
        }

        var arrivalTick = request.Tick + (Math.Max(1, request.TravelDays) * 60_000);
        var caravan = state.CreateCaravan(
            $"{plan.FactionId} caravan",
            source.FactionId,
            source.Id,
            target.Id,
            request.Tick,
            arrivalTick,
            crew.Id,
            request.CaravanResourceKey,
            request.DevelopmentSilverResourceKey,
            tradeBaseUnitPrice: 2,
            requestedTradeQuantity: quantity);

        if (!TravelCrewService.ReserveCrew(state, source.Id, caravan.Id, crew.Id, "world-war caravan crew"))
        {
            state.MarkCaravanRecalled(caravan.Id, "caravan crew unavailable");
            return ActionAttemptResult.Failed(ActionAttemptReason.NoCrew, "caravan crew reservation failed");
        }

        var transfer = state.TransferResource(
            source.Id,
            caravan.Id,
            request.CaravanResourceKey,
            quantity,
            $"world-war caravan loaded from {source.Id} to {target.Id}");
        if (transfer.Status != OwnershipTransferStatus.Success)
        {
            state.MarkCaravanRecalled(caravan.Id, transfer.Reason);
            return ActionAttemptResult.Failed(ActionAttemptReason.NoCargo, transfer.Reason);
        }

        return ActionAttemptResult.Success("caravan launched with paid trade cargo");
    }

    private static WorldSettlement? ResolveTarget(WorldState state, FactionActionPlan plan)
    {
        if (plan.TargetSettlementId.HasValue)
        {
            var target = state.GetSettlement(plan.TargetSettlementId.Value);
            if (target is { IsActive: true }
                && !string.Equals(target.FactionId, plan.FactionId, StringComparison.Ordinal)
                && !state.IsPlayerFaction(target.FactionId)
                && DiplomacyService.GetStance(state, plan.FactionId, target.FactionId) != RelationStance.Hostile)
            {
                return target;
            }
        }

        return WorldWarTargetSelector.FindTradeTarget(state, plan.FactionId);
    }
}
