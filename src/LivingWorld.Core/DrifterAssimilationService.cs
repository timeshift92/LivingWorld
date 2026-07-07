namespace LivingWorld.Core;

public sealed record DrifterAssimilationRequest(
    int Tick,
    int MaxAssimilationsPerStep);

public sealed record DrifterAssimilationResult(
    int Assimilated,
    int RemainingDrifters);

/// <summary>
/// Settles unaffiliated drifters into existing settlements as citizens, at a metered rate.
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

        var max = Math.Max(0, request.MaxAssimilationsPerStep);
        if (max == 0 || state.Settlements.Count == 0)
        {
            return new DrifterAssimilationResult(0, state.Drifters.Count);
        }

        var candidates = state.Drifters
            .OrderBy(drifter => drifter.ArrivalTick)
            .ThenBy(drifter => drifter.Id.Value)
            .Take(max)
            .ToList();

        var assimilated = 0;
        foreach (var drifter in candidates)
        {
            var settlement = state.Settlements
                .OrderBy(candidate => state.GetSettlementPopulation(candidate.Id).Total)
                .ThenBy(candidate => candidate.Id.Value)
                .First();
            state.AssimilateDrifter(drifter.Id, settlement.Id);
            assimilated++;
        }

        return new DrifterAssimilationResult(assimilated, state.Drifters.Count);
    }
}
