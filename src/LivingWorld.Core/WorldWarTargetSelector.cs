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

    public static bool CanScoutPlayerContact(WorldState state, string factionId)
    {
        var endpoint = state.PlayerContactEndpoint;
        if (endpoint?.IsAvailable != true
            || string.Equals(endpoint.FactionId, factionId, StringComparison.Ordinal)
            || DiplomacyService.GetStance(state, factionId, endpoint.FactionId) == RelationStance.Ally)
        {
            return false;
        }

        return !FactionKnowledgeService.GetActiveRaidIntelFacts(state, factionId).Any(fact =>
            fact.TargetKind == RaidIntelTargetKind.PlayerColony
            && string.Equals(fact.TargetKey, endpoint.StableKey, StringComparison.Ordinal));
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
        var candidates = state.Settlements
            .Where(settlement => settlement.IsActive)
            .Where(settlement => !string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal))
            .Where(settlement => !state.IsFactionIrreconcilable(settlement.FactionId))
            .Where(settlement => !state.IsFactionIrreconcilable(factionId))
            .Where(settlement => state.HasFactionSettlementIntel(factionId, settlement.Id))
            .Select(settlement => new
            {
                settlement.FactionId,
                Pressure = WorldTargetPressureService.GetTargetPressure(state, settlement.Id, plannedTargets),
                Score = WorldTargetPressureService.StableTargetScore("diplomacy-faction", factionId, settlement.Id),
                TieBreak = settlement.Id.Value
            })
            .ToList();

        var endpoint = state.PlayerContactEndpoint;
        if (endpoint?.IsAvailable == true
            && !string.Equals(endpoint.FactionId, factionId, StringComparison.Ordinal)
            && !state.IsFactionIrreconcilable(endpoint.FactionId)
            && !state.IsFactionIrreconcilable(factionId))
        {
            candidates.Add(new
            {
                endpoint.FactionId,
                Pressure = state.Missions.Count(mission =>
                    mission.Status == WorldMissionStatus.Traveling
                    && mission.TargetsPlayerContact),
                Score = WorldTargetPressureService.StableTextScore("diplomacy-contact", factionId, endpoint.StableKey),
                TieBreak = long.MaxValue
            });
        }

        return candidates
            .OrderBy(candidate => candidate.Pressure)
            .ThenBy(candidate => candidate.Score)
            .ThenBy(candidate => candidate.TieBreak)
            .Select(candidate => candidate.FactionId)
            .Distinct(StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public static WorldSettlement? FindDiplomacyTargetSettlement(
        WorldState state,
        string factionId,
        string targetFactionId,
        IReadOnlyCollection<EntityId>? plannedTargets = null)
    {
        if (state.PlayerContactEndpoint is { IsAvailable: true } endpoint
            && string.Equals(endpoint.FactionId, targetFactionId, StringComparison.Ordinal))
        {
            return null;
        }

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
