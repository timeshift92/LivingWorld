namespace LivingWorld.Core;

internal static class FactionReturnService
{
    public static string? ResolveFactionId(WorldState state, EntityId ownerId)
    {
        return ownerId.Kind switch
        {
            EntityKind.Settlement => state.GetSettlement(ownerId)?.FactionId,
            EntityKind.Army => state.GetArmy(ownerId)?.FactionId,
            EntityKind.Caravan => state.GetCaravan(ownerId)?.FactionId,
            EntityKind.Mission => state.GetMission(ownerId)?.FactionId,
            EntityKind.MigrationGroup => state.GetMigrationGroup(ownerId)?.FactionId,
            _ => null
        };
    }

    public static WorldSettlement? Resolve(
        WorldState state,
        string factionId,
        EntityId preferredSettlementId)
    {
        var preferred = state.GetSettlement(preferredSettlementId);
        if (preferred?.IsActive == true
            && string.Equals(preferred.FactionId, factionId, StringComparison.Ordinal))
        {
            return preferred;
        }

        return state.Settlements
            .Where(settlement => settlement.IsActive)
            .Where(settlement => string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal))
            .OrderBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
    }
}
