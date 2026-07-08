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
        var transfer = state.TransferResource(
            source.Id,
            target.Id,
            request.CaravanResourceKey,
            quantity,
            $"world-war caravan from {source.Id} to {target.Id}");
        if (transfer.Status != OwnershipTransferStatus.Success)
        {
            return false;
        }

        state.RecordEvent(
            WorldEventKind.SettlementTradeRecorded,
            source.Id,
            $"Caravan from {source.Id} delivered {quantity} {request.CaravanResourceKey} to {target.Id}.");
        return true;
    }
}
