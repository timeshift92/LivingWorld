namespace LivingWorld.Core;

internal static class CaravanActionExecutor
{
    public static bool Execute(WorldState state, string factionId, WorldWarRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CaravanResourceKey) || request.CaravanQuantity <= 0)
        {
            return false;
        }

        var source = WorldWarTargetSelector.FindTradeSource(state, factionId, request.CaravanResourceKey);
        var target = WorldWarTargetSelector.FindTradeTarget(state, factionId);
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
            $"{factionId} caravan",
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
}
