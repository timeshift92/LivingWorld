namespace LivingWorld.Core;

public enum WorldFactionStatus
{
    Active,
    Collapsed
}

public sealed record WorldFactionRecord(
    string FactionId,
    WorldFactionStatus Status,
    int Tick,
    string Reason);

public sealed record FactionLifecycleRequest(
    int Tick,
    IReadOnlyCollection<string>? EligibleFactionIds = null,
    string CollapseReason = FactionLifecycleService.ReasonPopulationCollapse);

public sealed record FactionLifecycleResult(int CollapsedFactions);

public static class FactionLifecycleService
{
    public const string ReasonPopulationCollapse = "population collapse";

    public static FactionLifecycleResult SimulateCollapses(
        WorldState state,
        FactionLifecycleRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.Tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Simulation tick cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(request.CollapseReason))
        {
            throw new ArgumentException("Collapse reason cannot be empty.", nameof(request));
        }

        state.AdvanceToTick(request.Tick);

        var collapsed = 0;
        foreach (var factionId in state.Settlements
            .Select(settlement => settlement.FactionId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(faction => faction, StringComparer.Ordinal))
        {
            if (request.EligibleFactionIds != null
                && !request.EligibleFactionIds.Contains(factionId))
            {
                continue;
            }

            if (state.IsFactionCollapsed(factionId))
            {
                continue;
            }

            if (CountLivingFactionCitizens(state, factionId) > 0)
            {
                continue;
            }

            state.MarkFactionCollapsedForLifecycle(
                factionId,
                request.Tick,
                request.CollapseReason);
            collapsed++;
        }

        return new FactionLifecycleResult(collapsed);
    }

    private static int CountLivingFactionCitizens(WorldState state, string factionId)
    {
        var count = 0;
        foreach (var citizen in state.Citizens)
        {
            if (citizen.Status == CitizenStatus.Dead || citizen.Status == CitizenStatus.Missing)
            {
                continue;
            }

            var settlement = state.GetSettlement(citizen.SettlementId);
            if (settlement != null
                && string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }
}
