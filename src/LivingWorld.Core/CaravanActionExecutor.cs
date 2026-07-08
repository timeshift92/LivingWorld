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
        var arrivalTick = request.Tick + (Math.Max(1, request.TravelDays) * 60_000);
        var caravan = state.CreateCaravan(
            $"{factionId} caravan",
            source.FactionId,
            source.Id,
            target.Id,
            request.Tick,
            arrivalTick);

        var transfer = state.TransferResource(
            source.Id,
            caravan.Id,
            request.CaravanResourceKey,
            quantity,
            $"world-war caravan loaded from {source.Id} to {target.Id}");
        if (transfer.Status != OwnershipTransferStatus.Success)
        {
            state.DestroyCaravan(caravan.Id, transfer.Reason);
            return false;
        }

        return true;
    }
}
