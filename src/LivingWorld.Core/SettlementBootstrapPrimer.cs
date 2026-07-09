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

        var facilitiesSeeded = EnsureStarterFacility(state, settlement.Id, state.CurrentTick);
        var animalCohortsSeeded = AnimalEcologyDriver.SeedSettlementCohorts(state, settlement.Id, state.CurrentTick);
        var wealth = SettlementWealthService.RefreshSettlement(
            state,
            settlement.Id,
            SettlementWealthService.DefaultPriceBook);

        return new SettlementBootstrapPrimerResult(facilitiesSeeded, animalCohortsSeeded, wealth);
    }

    private static int EnsureStarterFacility(WorldState state, EntityId settlementId, int tick)
    {
        if (state.GetSettlementFacilities(settlementId).Count > 0)
        {
            return 0;
        }

        var kind = ChooseStarterFacility(state, settlementId);
        if (!kind.HasValue)
        {
            return 0;
        }

        state.RecordSettlementFacility(new SettlementFacility(
            state.NextFacilityIdForLedger(),
            settlementId,
            kind.Value,
            Level: 1,
            ConditionPercent: 85,
            BuiltTick: Math.Max(0, tick)));
        state.RecordEvent(
            WorldEventKind.SettlementFacilityBuilt,
            settlementId,
            $"Settlement {settlementId} starts with a {kind.Value} facility.");
        return 1;
    }

    private static SettlementFacilityKind? ChooseStarterFacility(WorldState state, EntityId settlementId)
    {
        var profile = state.GetSettlementProductionProfile(settlementId);
        if (profile == null)
        {
            return null;
        }

        var candidates = new List<(SettlementFacilityKind Kind, int Score, int Order)>
        {
            (SettlementFacilityKind.Farm, profile.FoodPerAdult, 0),
            (SettlementFacilityKind.Workshop, profile.SteelPerAdult + profile.ComponentPerAdult, 1),
            (SettlementFacilityKind.Clinic, profile.MedicinePerAdult, 2),
        };

        return candidates
            .Where(candidate => candidate.Score > 0)
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Order)
            .Select(candidate => (SettlementFacilityKind?)candidate.Kind)
            .FirstOrDefault();
    }
}
