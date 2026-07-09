namespace LivingWorld.Core;

public sealed record PopulationFlowTargetRequest(int HardCeiling);

public sealed record PopulationFlowTarget(
    int CurrentPopulation,
    int ExternalReserve,
    int TargetPopulation,
    int HardCeiling);

public static class PopulationFlowTargetService
{
    public static PopulationFlowTarget Calculate(WorldState state, PopulationFlowTargetRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var currentPopulation = state.Citizens.Count(citizen => citizen.Status == CitizenStatus.Alive)
            + state.Drifters.Count;
        var externalReserve = Math.Max(0, state.DrifterArrivalReservoir);
        var requestedTarget = currentPopulation + externalReserve;
        var ceiling = request.HardCeiling > 0
            ? Math.Max(currentPopulation, request.HardCeiling)
            : requestedTarget;
        var target = Math.Min(requestedTarget, ceiling);

        return new PopulationFlowTarget(
            currentPopulation,
            externalReserve,
            target,
            ceiling);
    }
}
