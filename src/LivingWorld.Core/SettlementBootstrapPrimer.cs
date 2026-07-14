namespace LivingWorld.Core;

public sealed record SettlementBootstrapPrimerRequest(
    int Tick,
    EntityId SettlementId,
    string FoodResourceKey,
    string SteelResourceKey,
    string ComponentResourceKey);

public sealed record SettlementBootstrapPrimerResult(
    int FacilitiesSeeded,
    int AnimalCohortsSeeded,
    SettlementWealthSnapshot WealthSnapshot);

public static class SettlementBootstrapPrimer
{
    public static SettlementBootstrapPrimerResult PrimeSettlement(
        WorldState state,
        SettlementBootstrapPrimerRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var settlement = state.GetSettlement(request.SettlementId)
            ?? throw new InvalidOperationException($"Settlement {request.SettlementId} does not exist.");

        state.AdvanceToTick(Math.Max(0, request.Tick));

        // Runtime-founded settlements receive only records that are backed by transferred assets.
        // Facilities are built later through SettlementProjectService (which consumes materials),
        // and domestic animals may only be transferred, traded or born. Environment seeding is
        // limited to wildlife.
        var facilitiesSeeded = 0;
        var animalCohortsSeeded = AnimalEcologyDriver.SeedWildSettlementCohorts(
            state,
            settlement.Id,
            state.CurrentTick);
        var wealth = SettlementWealthService.RefreshSettlement(
            state,
            settlement.Id,
            SettlementWealthService.DefaultPriceBook);

        return new SettlementBootstrapPrimerResult(facilitiesSeeded, animalCohortsSeeded, wealth);
    }

}
