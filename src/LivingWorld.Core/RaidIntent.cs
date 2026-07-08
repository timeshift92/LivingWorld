namespace LivingWorld.Core;

public enum FactionHostility
{
    Neutral,
    Hostile,
    Irreconcilable
}

public enum RaidIntentReason
{
    GenericHostility,
    PlunderOpportunity
}

public sealed record RaidIntentRequest(
    string FactionId,
    FactionHostility Hostility);

public sealed record RaidIntent(
    string FactionId,
    RaidIntentReason Reason,
    EntityId? IntelFactId,
    int DesiredCombatants,
    int DesiredSupplies,
    int CreatedTick,
    int LatestLaunchTick,
    string Summary);

public static class RaidIntentService
{
    public static bool TryCreateBestIntent(
        WorldState state,
        RaidIntentRequest request,
        out RaidIntent? intent)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(request.FactionId))
        {
            intent = null;
            return false;
        }

        var fact = FactionKnowledgeService.GetActiveRaidIntelFacts(state, request.FactionId)
            .FirstOrDefault();

        if (fact != null)
        {
            intent = new RaidIntent(
                request.FactionId,
                RaidIntentReason.PlunderOpportunity,
                fact.Id,
                Math.Max(1, fact.CombatantDemand),
                Math.Max(1, fact.CombatantDemand),
                state.CurrentTick,
                Math.Max(state.CurrentTick, fact.ExpiresTick),
                fact.Summary);
            return true;
        }

        if (request.Hostility is FactionHostility.Hostile or FactionHostility.Irreconcilable)
        {
            intent = new RaidIntent(
                request.FactionId,
                RaidIntentReason.GenericHostility,
                null,
                1,
                1,
                state.CurrentTick,
                state.CurrentTick + RaidIntelService.DefaultTradeIntelLifetimeTicks,
                "Generic hostility raid intent.");
            return true;
        }

        intent = null;
        return false;
    }
}
