namespace LivingWorld.Core;

public sealed record MigrationSimulationRequest(
    int Tick,
    string FoodResourceKey,
    int FoodPerCitizen,
    int RefugeePressureThreshold,
    int MaxRefugeesPerSettlement);

public sealed record MigrationSimulationResult(
    int RefugeesCreated,
    int MigrationsCompleted);

public static class MigrationService
{
    public const string ReasonNone = "none";
    public const string ReasonStarvation = "starvation";

    public static MigrationSimulationResult SimulateDay(
        WorldState state,
        MigrationSimulationRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(request.FoodResourceKey))
        {
            throw new ArgumentException("Food resource key cannot be empty.", nameof(request));
        }

        state.AdvanceToTick(request.Tick);

        var completed = CompleteRefugeeMigrations(state, request);
        var created = CreateRefugees(state, request);

        return new MigrationSimulationResult(created, completed);
    }

    private static int CreateRefugees(WorldState state, MigrationSimulationRequest request)
    {
        var created = 0;
        var threshold = Math.Max(1, request.RefugeePressureThreshold);
        var maxPerSettlement = Math.Max(0, request.MaxRefugeesPerSettlement);
        if (maxPerSettlement == 0)
        {
            return 0;
        }

        foreach (var settlement in state.Settlements.OrderBy(settlement => settlement.Id.Value).ToList())
        {
            var status = state.GetSettlementMigrationStatus(settlement.Id, request.FoodResourceKey, request.FoodPerCitizen);
            if (status.Pressure < threshold)
            {
                continue;
            }

            var candidates = state.Citizens
                .Where(citizen =>
                    citizen.SettlementId == settlement.Id
                    && citizen.Status == CitizenStatus.Alive
                    && state.GetOwner(citizen.Id) == settlement.Id)
                .OrderBy(citizen => citizen.IsChild ? 0 : 1)
                .ThenBy(citizen => citizen.Id.Value)
                .Take(maxPerSettlement)
                .ToList();

            foreach (var citizen in candidates)
            {
                state.MarkCitizenRefugee(citizen.Id, status.PrimaryReason);
                created++;
            }
        }

        return created;
    }

    private static int CompleteRefugeeMigrations(WorldState state, MigrationSimulationRequest request)
    {
        var completed = 0;
        var refugees = state.Citizens
            .Where(citizen => citizen.Status == CitizenStatus.Refugee)
            .OrderBy(citizen => citizen.Id.Value)
            .ToList();

        foreach (var refugee in refugees)
        {
            var source = state.GetSettlement(refugee.SettlementId);
            if (source == null)
            {
                continue;
            }

            var target = state.Settlements
                .Where(settlement =>
                    settlement.Id != source.Id
                    && string.Equals(settlement.FactionId, source.FactionId, StringComparison.Ordinal)
                    && !state.GetSettlementFoodStatus(settlement.Id, request.FoodResourceKey, request.FoodPerCitizen).IsShortage)
                .OrderByDescending(settlement => state.GetSettlementFoodStatus(settlement.Id, request.FoodResourceKey, request.FoodPerCitizen).FoodDays)
                .ThenBy(settlement => settlement.Id.Value)
                .FirstOrDefault();

            if (target == null)
            {
                continue;
            }

            state.CompleteCitizenMigration(refugee.Id, target.Id, "stable same-faction settlement");
            completed++;
        }

        return completed;
    }
}
