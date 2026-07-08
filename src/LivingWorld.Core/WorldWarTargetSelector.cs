namespace LivingWorld.Core;

internal static class WorldWarTargetSelector
{
    public static WorldSettlement? FindReadySourceSettlement(WorldState state, string factionId)
    {
        return state.Settlements
            .Where(settlement => string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal))
            .Where(settlement => state.GetSettlementPopulation(settlement.Id).Adults > 0)
            .OrderBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
    }

    public static WorldSettlement? FindTradeSource(WorldState state, string factionId, string resourceKey)
    {
        return state.Settlements
            .Where(settlement => string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal))
            .Where(settlement => state.GetSettlementPopulation(settlement.Id).Adults > 0)
            .Where(settlement => state.GetOwnedResourceQuantity(settlement.Id, resourceKey) > 0)
            .OrderByDescending(settlement => state.GetOwnedResourceQuantity(settlement.Id, resourceKey))
            .ThenBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
    }

    public static WorldSettlement? FindTradeTarget(WorldState state, string factionId)
    {
        return state.Settlements
            .Where(settlement => !string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal))
            .Where(settlement => !state.IsPlayerFaction(settlement.FactionId))
            .Where(settlement => DiplomacyService.GetStance(state, factionId, settlement.FactionId) != RelationStance.Hostile)
            .OrderBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
    }

    public static WorldSettlement? FindScoutingTarget(WorldState state, string factionId)
    {
        return state.Settlements
            .Where(settlement => !string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal))
            .Where(settlement => !state.IsPlayerFaction(settlement.FactionId))
            .OrderBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
    }

    public static WorldSettlement? FindExpansionSource(WorldState state, string factionId)
    {
        return state.Settlements
            .Where(settlement => string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal))
            .OrderByDescending(settlement => state.GetSettlementPopulation(settlement.Id).Adults)
            .ThenBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
    }

    public static string? FindDiplomacyTargetFaction(WorldState state, string factionId)
    {
        return state.Settlements
            .OrderBy(settlement => settlement.Id.Value)
            .Select(settlement => settlement.FactionId)
            .Where(targetFaction => !string.Equals(targetFaction, factionId, StringComparison.Ordinal))
            .Where(targetFaction => !state.IsPlayerFaction(targetFaction))
            .Where(targetFaction => !state.IsFactionIrreconcilable(targetFaction))
            .Where(targetFaction => !state.IsFactionIrreconcilable(factionId))
            .Distinct(StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
