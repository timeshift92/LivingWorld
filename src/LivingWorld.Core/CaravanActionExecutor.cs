using System;
using System.Linq;

namespace LivingWorld.Core;

internal static class CaravanActionExecutor
{
    public static bool Execute(WorldState state, FactionActionPlan plan, WorldWarRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CaravanResourceKey) || request.CaravanQuantity <= 0)
        {
            return false;
        }

        // One travelling caravan per faction at a time keeps the world-map readable and prevents
        // a merchant faction from flooding the same trade route every simulated day.
        if (state.Caravans.Any(caravan =>
            caravan.Status == CaravanStatus.Traveling
            && string.Equals(caravan.FactionId, plan.FactionId, StringComparison.Ordinal)))
        {
            return false;
        }

        var source = WorldWarTargetSelector.FindTradeSource(state, plan.FactionId, request.CaravanResourceKey);
        var target = ResolveTarget(state, plan);
        if (source == null || target == null)
        {
            return false;
        }

        var quantity = Math.Min(
            request.CaravanQuantity,
            state.GetOwnedResourceQuantity(source.Id, request.CaravanResourceKey));
        if (quantity <= 0)
        {
            // Nothing to haul — do not launch an empty caravan that just churns create/destroy events.
            return false;
        }

        var crew = TravelCrewService.FindAvailableCrew(state, source.Id);
        if (crew == null)
        {
            return false;
        }

        var arrivalTick = request.Tick + (Math.Max(1, request.TravelDays) * 60_000);
        var caravan = state.CreateCaravan(
            $"{plan.FactionId} caravan",
            source.FactionId,
            source.Id,
            target.Id,
            request.Tick,
            arrivalTick,
            crew.Id);

        if (!TravelCrewService.ReserveCrew(state, source.Id, caravan.Id, crew.Id, "world-war caravan crew"))
        {
            state.MarkCaravanRecalled(caravan.Id, "caravan crew unavailable");
            return false;
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
            return false;
        }

        return true;
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
