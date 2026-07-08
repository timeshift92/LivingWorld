namespace LivingWorld.Core;

internal static class SettlementExpansionExecutor
{
    private const int MinSettlersRemaining = 8;

    public static bool Execute(WorldState state, string factionId, int settlerCount)
    {
        var settlers = Math.Max(1, settlerCount);
        var source = WorldWarTargetSelector.FindExpansionSource(state, factionId);
        if (source == null || state.GetSettlementPopulation(source.Id).Adults < settlers + MinSettlersRemaining)
        {
            return false;
        }

        var ordinal = state.Settlements.Count + 1;
        var slug = $"{factionId}-colony-{ordinal}";
        while (state.Settlements.Any(settlement => string.Equals(settlement.Slug, slug, StringComparison.Ordinal)))
        {
            ordinal++;
            slug = $"{factionId}-colony-{ordinal}";
        }

        state.ExpandSettlement(source.Id, slug, $"{factionId} colony {ordinal}", settlers);
        return true;
    }
}
