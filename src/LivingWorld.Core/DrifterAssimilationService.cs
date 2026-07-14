namespace LivingWorld.Core;

public sealed record DrifterAssimilationRequest(
    int Tick,
    int MaxAssimilationsPerStep)
{
    public int TravelDurationTicks { get; init; } = 60_000;

    public bool RequirePhysicalOrigin { get; init; }
}

public sealed record DrifterAssimilationResult(
    int Assimilated,
    int RemainingDrifters)
{
    public int JourneysStarted { get; init; }
}

/// <summary>
/// Moves unaffiliated drifters into existing settlements through explicit, persisted journeys.
///
/// Drifters are taken oldest-first (longest waiting) and each joins the currently least
/// populated settlement, so newcomers spread out and the smallest communities — who need
/// people most — pull first. A faction-weighted variant (peaceful settlements vs pirates
/// competing for drifters) is a later refinement tied to the settlement-typing design.
/// </summary>
public static class DrifterAssimilationService
{
    public static DrifterAssimilationResult SimulateAssimilation(
        WorldState state,
        DrifterAssimilationRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        state.AdvanceToTick(request.Tick);

        var assimilated = 0;
        foreach (var journey in state.DrifterAssimilationJourneys
            .Where(candidate => candidate.Status == DrifterAssimilationJourneyStatus.Traveling)
            .OrderBy(candidate => candidate.ArrivalTick)
            .ThenBy(candidate => candidate.Id.Value)
            .ToList())
        {
            if (journey.ArrivalTick > request.Tick)
            {
                continue;
            }

            var drifter = state.GetDrifter(journey.DrifterId);
            if (drifter == null)
            {
                state.CancelDrifterAssimilationJourney(journey.Id, "drifter no longer exists");
                continue;
            }

            var target = state.GetSettlement(journey.TargetSettlementId);
            if (target?.IsActive != true)
            {
                state.CancelDrifterAssimilationJourney(journey.Id, "target settlement is no longer active");
                continue;
            }

            if (!string.Equals(target.FactionId, journey.ExpectedTargetFactionId, StringComparison.Ordinal))
            {
                state.CancelDrifterAssimilationJourney(journey.Id, "target settlement changed ownership");
                continue;
            }

            if (!journey.PhysicalOriginReady)
            {
                continue;
            }

            state.CompleteDrifterAssimilationJourney(journey.Id);
            assimilated++;
        }

        var max = Math.Max(0, request.MaxAssimilationsPerStep);
        var eligibleSettlements = state.Settlements
            .Where(settlement => settlement.IsActive)
            .Where(settlement => string.IsNullOrWhiteSpace(state.PlayerFactionId)
                || !string.Equals(settlement.FactionId, state.PlayerFactionId, StringComparison.Ordinal))
            .ToList();
        if (max == 0 || eligibleSettlements.Count == 0)
        {
            return new DrifterAssimilationResult(assimilated, state.Drifters.Count);
        }

        var candidates = state.Drifters
            .Where(drifter => !state.IsDrifterReserved(drifter.Id))
            .OrderBy(drifter => drifter.ArrivalTick)
            .ThenBy(drifter => drifter.Id.Value)
            .Take(max)
            .ToList();

        var started = 0;
        var projectedPopulation = eligibleSettlements.ToDictionary(
            settlement => settlement.Id,
            settlement => state.GetSettlementPopulation(settlement.Id).Total);
        foreach (var drifter in candidates)
        {
            var settlement = eligibleSettlements
                .OrderBy(candidate => projectedPopulation[candidate.Id])
                .ThenBy(candidate => candidate.Id.Value)
                .First();
            state.CreateDrifterAssimilationJourney(
                drifter.Id,
                settlement.Id,
                request.Tick,
                checked(request.Tick + Math.Max(1, request.TravelDurationTicks)),
                "seeking a permanent home",
                request.RequirePhysicalOrigin);
            projectedPopulation[settlement.Id]++;
            started++;
        }

        return new DrifterAssimilationResult(assimilated, state.Drifters.Count)
        {
            JourneysStarted = started
        };
    }
}
