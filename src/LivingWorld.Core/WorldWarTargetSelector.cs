namespace LivingWorld.Core;

public static class WorldWarTargetSelector
{
    public static WorldSettlement? FindReadySourceSettlement(WorldState state, string factionId)
    {
        return state.Settlements
            .Where(settlement => settlement.IsActive)
            .Where(settlement => string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal))
            .Where(settlement => state.GetSettlementPopulation(settlement.Id).Adults > 0)
            .OrderBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
    }

    public static WorldSettlement? FindTradeSource(WorldState state, string factionId, string resourceKey)
    {
        return state.Settlements
            .Where(settlement => settlement.IsActive)
            .Where(settlement => string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal))
            .Where(settlement => state.GetSettlementPopulation(settlement.Id).Adults > 0)
            .Where(settlement => state.GetOwnedResourceQuantity(settlement.Id, resourceKey) > 0)
            .OrderByDescending(settlement => state.GetOwnedResourceQuantity(settlement.Id, resourceKey))
            .ThenBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
    }

    public static WorldSettlement? FindTradeTarget(
        WorldState state,
        string factionId,
        IReadOnlyCollection<EntityId>? plannedTargets = null)
    {
        return state.Settlements
            .Where(settlement => settlement.IsActive)
            .Where(settlement => !string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal))
            .Where(settlement => !state.IsPlayerFaction(settlement.FactionId))
            .Where(settlement => DiplomacyService.GetStance(state, factionId, settlement.FactionId) != RelationStance.Hostile)
            .Where(settlement => state.HasFactionSettlementIntel(factionId, settlement.Id))
            .OrderBy(settlement => WorldTargetPressureService.GetTargetPressure(state, settlement.Id, plannedTargets))
            .ThenBy(settlement => WorldTargetPressureService.StableTargetScore("trade", factionId, settlement.Id))
            .ThenBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
    }

    public static WorldSettlement? FindScoutingTarget(
        WorldState state,
        string factionId,
        IReadOnlyCollection<EntityId>? plannedTargets = null)
    {
        return state.Settlements
            .Where(settlement => settlement.IsActive)
            .Where(settlement => !string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal))
            .Where(settlement => !state.IsPlayerFaction(settlement.FactionId))
            .Where(settlement => DiplomacyService.GetStance(state, factionId, settlement.FactionId) != RelationStance.Ally)
            .Where(settlement => !state.HasFactionSettlementIntel(factionId, settlement.Id))
            .OrderBy(settlement => WorldTargetPressureService.GetTargetPressure(state, settlement.Id, plannedTargets))
            .ThenBy(settlement => WorldTargetPressureService.StableTargetScore("scout", factionId, settlement.Id))
            .ThenBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
    }

    public static WorldSettlement? FindExpansionSource(WorldState state, string factionId)
    {
        return state.Settlements
            .Where(settlement => settlement.IsActive)
            .Where(settlement => string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal))
            .OrderByDescending(settlement => state.GetSettlementPopulation(settlement.Id).Adults)
            .ThenBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
    }

    public static string? FindDiplomacyTargetFaction(
        WorldState state,
        string factionId,
        IReadOnlyCollection<EntityId>? plannedTargets = null)
    {
        return state.Settlements
            .Where(settlement => settlement.IsActive)
            .Where(settlement => !string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal))
            .Where(settlement => !state.IsPlayerFaction(settlement.FactionId))
            .Where(settlement => !state.IsFactionIrreconcilable(settlement.FactionId))
            .Where(settlement => !state.IsFactionIrreconcilable(factionId))
            .Where(settlement => state.HasFactionSettlementIntel(factionId, settlement.Id))
            .OrderBy(settlement => WorldTargetPressureService.GetTargetPressure(state, settlement.Id, plannedTargets))
            .ThenBy(settlement => WorldTargetPressureService.StableTargetScore("diplomacy-faction", factionId, settlement.Id))
            .ThenBy(settlement => settlement.Id.Value)
            .Select(settlement => settlement.FactionId)
            .Distinct(StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public static WorldSettlement? FindDiplomacyTargetSettlement(
        WorldState state,
        string factionId,
        string targetFactionId,
        IReadOnlyCollection<EntityId>? plannedTargets = null)
    {
        return state.Settlements
            .Where(settlement => settlement.IsActive)
            .Where(settlement => string.Equals(settlement.FactionId, targetFactionId, StringComparison.Ordinal))
            .Where(settlement => state.HasFactionSettlementIntel(factionId, settlement.Id))
            .OrderBy(settlement => WorldTargetPressureService.GetTargetPressure(state, settlement.Id, plannedTargets))
            .ThenBy(settlement => WorldTargetPressureService.StableTargetScore("diplomacy-settlement", factionId, settlement.Id))
            .ThenBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
    }
}
