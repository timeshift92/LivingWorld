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
    public const string ReasonSettlementFounding = "settlement-founding";

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

        var plans = new List<(WorldSettlement Settlement, SettlementMigrationStatus Status, IReadOnlyList<WorldCitizen> Candidates, WorldSettlement? Target)>();
        foreach (var settlement in state.Settlements.OrderBy(settlement => settlement.Id.Value))
        {
            var status = state.GetSettlementMigrationStatus(settlement.Id, request.FoodResourceKey, request.FoodPerCitizen);
            if (status.Pressure < threshold)
            {
                continue;
            }

            var candidates = state.GetCitizensBySettlement(settlement.Id)
                .Where(citizen =>
                    citizen.Status == CitizenStatus.Alive
                    && state.GetOwner(citizen.Id) == settlement.Id)
                .OrderBy(citizen => citizen.IsChild ? 0 : 1)
                .ThenBy(citizen => citizen.Id.Value)
                .Take(maxPerSettlement)
                .ToList();

            plans.Add((
                settlement,
                status,
                candidates,
                FindStableTarget(state, settlement, request)));
        }

        foreach (var plan in plans)
        {
            foreach (var citizen in plan.Candidates)
            {
                var refugee = state.MarkCitizenRefugee(citizen.Id, plan.Status.PrimaryReason);
                created++;
                if (plan.Target == null)
                {
                    continue;
                }

                var travelTicks = Math.Max(1, request.TravelDurationTicks);
                var group = state.CreateMigrationGroup(
                    plan.Settlement.Id,
                    plan.Target.Id,
                    plan.Settlement.FactionId,
                    request.Tick,
                    request.Tick + travelTicks,
                    plan.Status.PrimaryReason);
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

        var arrivals = groups
            .Select(group => new
            {
                Group = group,
                Target = state.GetSettlement(group.TargetSettlementId!.Value),
                Migrants = state.GetCitizensOwnedBy(group.Id)
                    .Where(citizen => citizen.Status == CitizenStatus.Migrating)
                    .OrderBy(citizen => citizen.Id.Value)
                    .ToList(),
                Resources = state.ResourcesForOwner(group.Id)
            })
            .ToList();

        foreach (var arrival in arrivals)
        {
            var group = arrival.Group;
            var target = arrival.Target;
            if (target?.IsActive != true
                || !string.Equals(target.FactionId, group.FactionId, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var migrant in arrival.Migrants)
            {
                state.CompleteCitizenMigration(migrant.Id, target.Id, "migration group arrived");
                completed++;
            }

            foreach (var resource in arrival.Resources)
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
                && settlement.IsActive
                && string.Equals(settlement.FactionId, source.FactionId, StringComparison.Ordinal)
                && !state.GetSettlementFoodStatus(settlement.Id, request.FoodResourceKey, request.FoodPerCitizen).IsShortage)
            .OrderByDescending(settlement => state.GetSettlementFoodStatus(settlement.Id, request.FoodResourceKey, request.FoodPerCitizen).FoodDays)
            .ThenBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
    }
}
