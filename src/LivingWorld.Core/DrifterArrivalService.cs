namespace LivingWorld.Core;

public sealed record DrifterArrivalRequest(
    int Tick,
    int TargetWorldPopulation,
    int HardCeiling,
    int MaxArrivalsPerStep);

public sealed record DrifterArrivalResult(int Arrived, int PoolSize);

/// <summary>
/// The metered external reservoir: a homeostatic trickle of unaffiliated newcomers
/// (drifters) that keeps the world population near a target without ever exceeding
/// a hard ceiling or the finite outside-world reserve.
///
/// Homeostatic: arrivals only fill the deficit toward the target, so a healthy world gets
/// none and a depleted one is topped up — but never more than <c>MaxArrivalsPerStep</c> at a
/// time (metered "1–2, rarely more"), never past <c>HardCeiling</c>, and never beyond
/// <see cref="WorldState.DrifterArrivalReservoir"/>.
/// </summary>
public static class DrifterArrivalService
{
    public static DrifterArrivalResult SimulateArrivals(WorldState state, DrifterArrivalRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        state.AdvanceToTick(request.Tick);

        var maxPerStep = Math.Max(0, request.MaxArrivalsPerStep);
        var ceiling = Math.Max(0, request.HardCeiling);
        var effectiveTarget = Math.Min(Math.Max(0, request.TargetWorldPopulation), ceiling);

        var currentPopulation = WorldPopulation(state);
        var deficit = effectiveTarget - currentPopulation;
        var requestedArrivals = Math.Max(0, Math.Min(maxPerStep, deficit));
        var arrivals = state.ConsumeDrifterArrivalReservoir(requestedArrivals);

        for (var i = 0; i < arrivals; i++)
        {
            var sequence = state.Drifters.Count + 1;
            state.CreateDrifter(
                $"Drifter {sequence}",
                DeterministicAge(state.WorldSeed, request.Tick, sequence),
                DeterministicSex(request.Tick, sequence),
                DeterministicAptitude(state.WorldSeed, request.Tick, sequence, salt: 7),
                DeterministicAptitude(state.WorldSeed, request.Tick, sequence, salt: 13));
        }

        return new DrifterArrivalResult(arrivals, state.Drifters.Count);
    }

    private static int WorldPopulation(WorldState state)
    {
        return state.Citizens.Count(citizen => citizen.Status == CitizenStatus.Alive)
            + state.Drifters.Count;
    }

    private static int DeterministicAge(int worldSeed, int tick, int sequence)
    {
        // Adults seeking a new life: deterministic 18..59, no RNG (keeps the sim reproducible).
        var span = (int)((((long)worldSeed + tick + (sequence * 31)) % 42 + 42) % 42);
        return 18 + span;
    }

    private static Sex DeterministicSex(int tick, int sequence)
    {
        return ((tick / 60_000) + sequence) % 2 == 0 ? Sex.Female : Sex.Male;
    }

    private static int DeterministicAptitude(int worldSeed, int tick, int sequence, int salt)
    {
        // Deterministic 0..100 leadership proxy; most arrivals are ordinary, a few capable.
        return (int)((((long)worldSeed + tick + (sequence * salt)) % 101 + 101) % 101);
    }
}
