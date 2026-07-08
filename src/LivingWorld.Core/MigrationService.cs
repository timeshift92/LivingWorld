namespace LivingWorld.Core;

public sealed record MigrationSimulationRequest(
    int Tick,
    string FoodResourceKey,
    int FoodPerCitizen,
    int RefugeePressureThreshold,
    int MaxRefugeesPerSettlement,
    int TravelDurationTicks = 60_000);

public sealed record MigrationSimulationResult(
    int RefugeesCreated,
    int MigrationGroupsCreated,
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

        var completed = CompleteArrivedMigrationGroups(state, request);
        var (created, groupsCreated) = CreateRefugees(state, request);

        return new MigrationSimulationResult(created, groupsCreated, completed);
    }

    private static (int RefugeesCreated, int MigrationGroupsCreated) CreateRefugees(WorldState state, MigrationSimulationRequest request)
    {
        var created = 0;
        var groupsCreated = 0;
        var threshold = Math.Max(1, request.RefugeePressureThreshold);
        var maxPerSettlement = Math.Max(0, request.MaxRefugeesPerSettlement);
        if (maxPerSettlement == 0)
        {
            return (0, 0);
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
                var refugee = state.MarkCitizenRefugee(citizen.Id, status.PrimaryReason);
                created++;
                var target = FindStableTarget(state, settlement, request);
                if (target == null)
                {
                    continue;
                }

                var travelTicks = Math.Max(1, request.TravelDurationTicks);
                var group = state.CreateMigrationGroup(
                    settlement.Id,
                    target.Id,
                    settlement.FactionId,
                    request.Tick,
                    request.Tick + travelTicks,
                    status.PrimaryReason);
                var migrating = refugee with { Status = CitizenStatus.Migrating };
                state.ReplaceCitizenForSimulation(migrating);
                state.SetOwnerForLedger(migrating.Id, group.Id);
                groupsCreated++;
            }
        }

        return (created, groupsCreated);
    }

    private static int CompleteArrivedMigrationGroups(WorldState state, MigrationSimulationRequest request)
    {
        var completed = 0;
        var groups = state.MigrationGroups
            .Where(group =>
                group.Status == MigrationGroupStatus.Traveling
                && group.TargetSettlementId.HasValue
                && group.ArrivalTick <= request.Tick)
            .OrderBy(group => group.ArrivalTick)
            .ThenBy(group => group.Id.Value)
            .ToList();

        foreach (var group in groups)
        {
            var target = state.GetSettlement(group.TargetSettlementId!.Value);
            if (target == null)
            {
                continue;
            }

            var migrants = state.Citizens
                .Where(citizen =>
                    citizen.Status == CitizenStatus.Migrating
                    && state.GetOwner(citizen.Id) == group.Id)
                .OrderBy(citizen => citizen.Id.Value)
                .ToList();

            foreach (var migrant in migrants)
            {
                state.CompleteCitizenMigration(migrant.Id, target.Id, "migration group arrived");
                completed++;
            }

            foreach (var resource in state.ResourcesForOwner(group.Id).ToList())
            {
                var transfer = state.TransferResource(
                    group.Id,
                    target.Id,
                    resource.ResourceKey,
                    resource.Quantity,
                    "migration group arrived");
                if (transfer.Status != OwnershipTransferStatus.Success)
                {
                    throw new InvalidOperationException(transfer.Reason);
                }
            }

            state.MarkMigrationGroupArrived(group.Id);
        }

        return completed;
    }

    private static WorldSettlement? FindStableTarget(
        WorldState state,
        WorldSettlement source,
        MigrationSimulationRequest request)
    {
        return state.Settlements
            .Where(settlement =>
                settlement.Id != source.Id
                && string.Equals(settlement.FactionId, source.FactionId, StringComparison.Ordinal)
                && !state.GetSettlementFoodStatus(settlement.Id, request.FoodResourceKey, request.FoodPerCitizen).IsShortage)
            .OrderByDescending(settlement => state.GetSettlementFoodStatus(settlement.Id, request.FoodResourceKey, request.FoodPerCitizen).FoodDays)
            .ThenBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
    }
}
