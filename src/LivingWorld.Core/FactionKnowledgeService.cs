namespace LivingWorld.Core;

public static class FactionKnowledgeService
{
    public static IReadOnlyList<RaidIntelFact> GetActiveRaidIntelFacts(
        WorldState state,
        string factionId)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(factionId))
        {
            return Array.Empty<RaidIntelFact>();
        }

        return state.RaidIntelFacts
            .Where(fact =>
                string.Equals(fact.FactionId, factionId, StringComparison.Ordinal)
                && !fact.IsExpired(state.CurrentTick))
            .OrderByDescending(fact => fact.CombatantDemand)
            .ThenByDescending(fact => fact.Confidence)
            .ThenBy(fact => fact.Id.Value)
            .ToList();
    }
}
