namespace LivingWorld.Core;

internal static class SettlementExpansionExecutor
{
    private const int MinSettlersRemaining = 8;

    public static int CompleteArrived(WorldState state, int tick)
    {
        var founded = 0;
        foreach (var group in state.MigrationGroups
            .Where(group =>
                group.Status == MigrationGroupStatus.Traveling
                && string.Equals(group.Reason, MigrationService.ReasonSettlementFounding, StringComparison.Ordinal)
                && group.ArrivalTick <= tick)
            .OrderBy(group => group.ArrivalTick)
            .ThenBy(group => group.Id.Value)
            .ToList())
        {
            state.CompleteSettlementExpedition(group.Id);
            founded++;
        }

        return founded;
    }

    public static bool Execute(WorldState state, string factionId, WorldWarRequest request)
    {
        var settlers = Math.Max(1, request.SettlerCount);
        var source = WorldWarTargetSelector.FindExpansionSource(state, factionId);
        if (source == null || state.GetSettlementPopulation(source.Id).Adults < settlers + MinSettlersRemaining)
        {
            return false;
        }

        if (state.MigrationGroups.Any(group =>
            group.Status == MigrationGroupStatus.Traveling
            && string.Equals(group.FactionId, factionId, StringComparison.Ordinal)
            && string.Equals(group.Reason, MigrationService.ReasonSettlementFounding, StringComparison.Ordinal)))
        {
            return false;
        }

        var ordinal = state.Settlements.Count + 1;
        var slug = $"{factionId}-colony-{ordinal}";
        while (state.Settlements.Any(settlement => string.Equals(settlement.Slug, slug, StringComparison.Ordinal))
            || state.MigrationGroups.Any(group =>
                group.Status == MigrationGroupStatus.Traveling
                && string.Equals(group.PlannedSettlementSlug, slug, StringComparison.Ordinal)))
        {
            ordinal++;
            slug = $"{factionId}-colony-{ordinal}";
        }

        state.StartSettlementExpedition(
            source.Id,
            slug,
            $"{factionId} colony {ordinal}",
            settlers,
            request.Tick,
            request.Tick + Math.Max(1, request.TravelDays) * 60_000);
        return true;
    }
}
