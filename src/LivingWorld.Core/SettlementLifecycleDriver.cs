namespace LivingWorld.Core;

public sealed record SettlementLifecycleDriverRequest(
    int Tick,
    string FoodResourceKey,
    int FoodPerCitizen,
    int RelocationTravelTicks = 60_000,
    int RuinRetentionDays = 30)
{
    public bool ResolveFactionCollapses { get; init; }

    public string CollapseReason { get; init; } = FactionLifecycleService.ReasonPopulationCollapse;
}

public sealed record SettlementLifecycleDriverResult(
    int RelocationsStarted,
    int RuinsPruned)
{
    public int CollapsedFactions { get; init; }

    public int DestroyedCollapsedSettlements { get; init; }
}

public static class SettlementLifecycleDriver
{
    public static SettlementLifecycleDriverResult SimulateDay(
        WorldState state,
        SettlementLifecycleDriverRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.Tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Lifecycle tick cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(request.FoodResourceKey))
        {
            throw new ArgumentException("Food resource key cannot be empty.", nameof(request));
        }

        state.AdvanceToTick(request.Tick);

        var collapsedFactions = 0;
        var destroyedCollapsedSettlements = 0;
        if (request.ResolveFactionCollapses)
        {
            var collapse = FactionLifecycleService.SimulateCollapses(
                state,
                new FactionLifecycleRequest(request.Tick, CollapseReason: request.CollapseReason));
            collapsedFactions = collapse.CollapsedFactions;
            destroyedCollapsedSettlements = DestroyCollapsedFactionSettlements(state, request);
        }

        var prune = SettlementLifecycleService.PruneInactiveRuins(
            state,
            request.Tick,
            request.RuinRetentionDays);
        var relocations = StartStarvationRelocations(state, request);

        return new SettlementLifecycleDriverResult(relocations, prune.Pruned)
        {
            CollapsedFactions = collapsedFactions,
            DestroyedCollapsedSettlements = destroyedCollapsedSettlements,
        };
    }

    private static int DestroyCollapsedFactionSettlements(
        WorldState state,
        SettlementLifecycleDriverRequest request)
    {
        var destroyed = 0;
        foreach (var settlement in state.Settlements
            .Where(settlement =>
                settlement.IsActive
                && !state.IsPlayerFaction(settlement.FactionId)
                && state.IsFactionCollapsed(settlement.FactionId))
            .OrderBy(settlement => settlement.Id.Value)
            .ToList())
        {
            SettlementLifecycleService.DestroySettlement(
                state,
                settlement.Id,
                request.Tick,
                request.CollapseReason);
            destroyed++;
        }

        return destroyed;
    }

    private static int StartStarvationRelocations(
        WorldState state,
        SettlementLifecycleDriverRequest request)
    {
        var started = 0;
        foreach (var source in state.Settlements
            .Where(settlement =>
                settlement.IsActive
                && !state.IsPlayerFaction(settlement.FactionId))
            .OrderBy(settlement => settlement.Id.Value)
            .ToList())
        {
            if (state.GetSettlementPopulation(source.Id).Total <= 0)
            {
                continue;
            }

            var status = state.GetSettlementFoodStatus(
                source.Id,
                request.FoodResourceKey,
                request.FoodPerCitizen);
            if (!status.IsShortage)
            {
                continue;
            }

            if (HasActiveRelocationFrom(state, source.Id))
            {
                continue;
            }

            var target = FindStableTarget(state, source, request);
            if (target == null)
            {
                continue;
            }

            SettlementLifecycleService.StartRelocation(
                state,
                source.Id,
                target.Id,
                request.Tick,
                request.Tick + Math.Max(1, request.RelocationTravelTicks),
                MigrationService.ReasonStarvation);
            started++;
        }

        return started;
    }

    private static bool HasActiveRelocationFrom(WorldState state, EntityId sourceSettlementId)
    {
        return state.MigrationGroups.Any(group =>
            group.SourceSettlementId == sourceSettlementId
            && group.Status == MigrationGroupStatus.Traveling);
    }

    private static WorldSettlement? FindStableTarget(
        WorldState state,
        WorldSettlement source,
        SettlementLifecycleDriverRequest request)
    {
        return state.Settlements
            .Where(settlement =>
                settlement.Id != source.Id
                && settlement.IsActive
                && string.Equals(settlement.FactionId, source.FactionId, StringComparison.Ordinal)
                && !state.GetSettlementFoodStatus(
                    settlement.Id,
                    request.FoodResourceKey,
                    request.FoodPerCitizen).IsShortage)
            .OrderByDescending(settlement => state.GetSettlementFoodStatus(
                settlement.Id,
                request.FoodResourceKey,
                request.FoodPerCitizen).FoodDays)
            .ThenBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
    }
}
