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
            if (state.TryCompleteSettlementExpedition(group.Id, out _, out _))
            {
                founded++;
            }
        }

        return founded;
    }

    public static ActionAttemptResult Execute(WorldState state, string factionId, WorldWarRequest request)
    {
        var settlers = Math.Max(1, request.SettlerCount);
        var source = WorldWarTargetSelector.FindExpansionSource(state, factionId);
        if (source == null || state.GetSettlementPopulation(source.Id).Adults < settlers + MinSettlersRemaining)
        {
            return ActionAttemptResult.Failed(ActionAttemptReason.InsufficientPopulation, "not enough adults remain to found a colony");
        }

        if (state.MigrationGroups.Any(group =>
            group.Status == MigrationGroupStatus.Traveling
            && string.Equals(group.FactionId, factionId, StringComparison.Ordinal)
            && string.Equals(group.Reason, MigrationService.ReasonSettlementFounding, StringComparison.Ordinal)))
        {
            return ActionAttemptResult.Failed(ActionAttemptReason.AlreadyInFlight, "a settlement expedition is already in flight");
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

        var expedition = state.StartSettlementExpedition(
            source.Id,
            slug,
            $"{factionId} colony {ordinal}",
            settlers,
            request.Tick,
            request.Tick + Math.Max(1, request.TravelDays) * 60_000,
            request.RequirePhysicalSettlementDestinations);

        // Force the concrete location identity to be resolved while the departure is still being
        // committed. The value is derived from persisted fields and remains stable after save/load.
        return !string.IsNullOrWhiteSpace(expedition.PlannedLocationToken)
            ? ActionAttemptResult.Success("settlement expedition launched")
            : ActionAttemptResult.Failed(ActionAttemptReason.ExecutionFailed, "expedition has no physical destination token");
    }
}
