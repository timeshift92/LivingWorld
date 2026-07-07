namespace LivingWorld.Core;

public sealed record DemographySimulationRequest(
    int Tick,
    int AgeIntervalDays,
    int NaturalDeathAge,
    int MaxNaturalDeathsPerDay);

public sealed record DemographySimulationResult(
    int CitizensAged,
    int NaturalDeaths);

public static class DemographyService
{
    private const int TicksPerDay = 60_000;

    public static DemographySimulationResult SimulateDay(
        WorldState state,
        DemographySimulationRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.Tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Simulation tick cannot be negative.");
        }

        var ageIntervalDays = Math.Max(1, request.AgeIntervalDays);
        var naturalDeathAge = Math.Max(1, request.NaturalDeathAge);
        var maxNaturalDeaths = Math.Max(0, request.MaxNaturalDeathsPerDay);
        var day = Math.Max(1, request.Tick / TicksPerDay);
        if (day % ageIntervalDays != 0)
        {
            state.AdvanceToTick(request.Tick);
            return new DemographySimulationResult(0, 0);
        }

        state.AdvanceToTick(request.Tick);

        var aged = 0;
        var deaths = 0;
        foreach (var citizen in state.Citizens
            .Where(citizen => citizen.Status == CitizenStatus.Alive)
            .OrderBy(citizen => citizen.Id.Value)
            .ToList())
        {
            var next = citizen with { Age = citizen.Age + 1 };
            aged++;

            if (deaths < maxNaturalDeaths && next.Age >= naturalDeathAge)
            {
                var dead = next with { Status = CitizenStatus.Dead };
                state.ReplaceCitizenForSimulation(dead);
                state.RecordEvent(
                    WorldEventKind.CitizenDied,
                    dead.Id,
                    $"Citizen {dead.Id} died from old age at {dead.Age}.");
                deaths++;
                continue;
            }

            state.ReplaceCitizenForSimulation(next);
            state.RecordEvent(
                WorldEventKind.CitizenAged,
                next.Id,
                $"Citizen {next.Id} aged to {next.Age}.");
        }

        return new DemographySimulationResult(aged, deaths);
    }
}
