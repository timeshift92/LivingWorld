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
        var strandedGroups = CreateStrandedRefugeeMigrations(state, request);
        var (created, groupsCreated) = CreateRefugees(state, request);

        return new MigrationSimulationResult(created, groupsCreated + strandedGroups, completed);
    }

    private static int CreateStrandedRefugeeMigrations(WorldState state, MigrationSimulationRequest request)
    {
        var created = 0;
        var maxPerSource = Math.Max(0, request.MaxRefugeesPerSettlement);
        if (maxPerSource == 0)
        {
            return 0;
        }

        foreach (var source in state.Settlements
            .Where(settlement => !settlement.IsActive)
            .OrderBy(settlement => settlement.Id.Value))
        {
            var target = state.Settlements
                .Where(settlement => settlement.IsActive
                    && string.Equals(settlement.FactionId, source.FactionId, StringComparison.Ordinal)
                    && !state.GetSettlementFoodStatus(settlement.Id, request.FoodResourceKey, request.FoodPerCitizen).IsShortage)
                .OrderByDescending(settlement => state.GetSettlementFoodStatus(
                    settlement.Id,
                    request.FoodResourceKey,
                    request.FoodPerCitizen).FoodDays)
                .ThenBy(settlement => settlement.Id.Value)
                .FirstOrDefault();
            if (target == null)
            {
                continue;
            }

            foreach (var refugee in state.GetCitizensBySettlement(source.Id)
                .Where(citizen => citizen.Status == CitizenStatus.Refugee
                    && state.GetOwner(citizen.Id) == citizen.Id
                    && !state.HasActiveMaterializationLease(citizen.Id))
                .OrderBy(citizen => citizen.Id.Value)
                .Take(maxPerSource)
                .ToList())
            {
                var group = state.CreateMigrationGroup(
                    source.Id,
                    target.Id,
                    source.FactionId,
                    request.Tick,
                    request.Tick + Math.Max(1, request.TravelDurationTicks),
                    "destroyed-settlement-refugees");
                state.ReplaceCitizenForSimulation(refugee with { Status = CitizenStatus.Migrating });
                state.SetOwnerForLedger(refugee.Id, group.Id);
                created++;
            }
        }

        return created;
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

        var plans = new List<(WorldSettlement Settlement, SettlementMigrationStatus Status, IReadOnlyList<WorldCitizen> Candidates, WorldSettlement Target)>();
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
                    && state.GetOwner(citizen.Id) == settlement.Id
                    && !state.HasActiveMaterializationLease(citizen.Id))
                .OrderBy(citizen => citizen.IsChild ? 0 : 1)
                .ThenBy(citizen => citizen.Id.Value)
                .Take(maxPerSettlement)
                .ToList();

            var target = FindStableTarget(state, settlement, request);
            if (target != null && candidates.Count > 0)
            {
                plans.Add((settlement, status, candidates, target));
            }
        }

        foreach (var plan in plans)
        {
            foreach (var citizen in plan.Candidates)
            {
                var travelTicks = Math.Max(1, request.TravelDurationTicks);
                var group = state.CreateMigrationGroup(
                    plan.Settlement.Id,
                    plan.Target.Id,
                    plan.Settlement.FactionId,
                    request.Tick,
                    request.Tick + travelTicks,
                    plan.Status.PrimaryReason);
                var refugee = state.MarkCitizenRefugee(citizen.Id, plan.Status.PrimaryReason);
                var migrating = refugee with { Status = CitizenStatus.Migrating };
                state.ReplaceCitizenForSimulation(migrating);
                state.SetOwnerForLedger(migrating.Id, group.Id);
                created++;
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
                && !string.Equals(group.Reason, ReasonSettlementFounding, StringComparison.Ordinal)
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
                || !string.Equals(target.FactionId, group.FactionId, StringComparison.Ordinal)
                || state.GetSettlementFoodStatus(target.Id, request.FoodResourceKey, request.FoodPerCitizen).IsShortage)
            {
                var reroute = FindStableTargetForGroup(state, group, request);
                if (reroute != null)
                {
                    state.RerouteMigrationGroup(
                        group.Id,
                        reroute.Id,
                        request.Tick + Math.Max(1, request.TravelDurationTicks),
                        "migration target unavailable");
                    continue;
                }

                var fallback = FindReturnSettlement(state, group, request);
                if (fallback != null)
                {
                    state.ReturnMigrationGroup(group.Id, fallback.Id, "no viable migration destination");
                }
                else
                {
                    state.LoseMigrationGroup(group.Id, "no viable migration destination or return settlement");
                }

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

    private static WorldSettlement? FindStableTargetForGroup(
        WorldState state,
        WorldMigrationGroup group,
        MigrationSimulationRequest request)
    {
        return state.Settlements
            .Where(settlement => settlement.IsActive)
            .Where(settlement => settlement.Id != group.SourceSettlementId)
            .Where(settlement => settlement.Id != group.TargetSettlementId)
            .Where(settlement => string.Equals(settlement.FactionId, group.FactionId, StringComparison.Ordinal))
            .Where(settlement => !state.GetSettlementFoodStatus(
                settlement.Id,
                request.FoodResourceKey,
                request.FoodPerCitizen).IsShortage)
            .OrderByDescending(settlement => state.GetSettlementFoodStatus(
                settlement.Id,
                request.FoodResourceKey,
                request.FoodPerCitizen).FoodDays)
            .ThenBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
    }

    private static WorldSettlement? FindReturnSettlement(
        WorldState state,
        WorldMigrationGroup group,
        MigrationSimulationRequest request)
    {
        var source = state.GetSettlement(group.SourceSettlementId);
        if (source?.IsActive == true
            && string.Equals(source.FactionId, group.FactionId, StringComparison.Ordinal))
        {
            return source;
        }

        return state.Settlements
            .Where(settlement => settlement.IsActive)
            .Where(settlement => string.Equals(settlement.FactionId, group.FactionId, StringComparison.Ordinal))
            .OrderByDescending(settlement => state.GetSettlementFoodStatus(
                settlement.Id,
                request.FoodResourceKey,
                request.FoodPerCitizen).FoodDays)
            .ThenBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
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
