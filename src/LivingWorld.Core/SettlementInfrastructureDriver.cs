namespace LivingWorld.Core;

public sealed record SettlementInfrastructureDriverRequest(
    int Tick,
    int ProjectDurationTicks = 60_000,
    int RepairConditionThreshold = 70,
    int BuildSteelCost = 80,
    int BuildComponentCost = 4,
    int RepairSteelCost = 40,
    int RepairComponentCost = 1);

public sealed record SettlementInfrastructureDriverResult(
    int ProjectsStarted,
    int ProjectsCompleted);

public static class SettlementInfrastructureDriver
{
    public static SettlementInfrastructureDriverResult SimulateDay(
        WorldState state,
        SettlementInfrastructureDriverRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.Tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Infrastructure tick cannot be negative.");
        }

        state.AdvanceToTick(request.Tick);
        var completed = SettlementProjectService.CompleteReadyProjects(state, request.Tick);
        var started = 0;

        foreach (var settlement in state.Settlements
            .Where(settlement => settlement.IsActive)
            .OrderBy(settlement => settlement.Id.Value)
            .ToList())
        {
            if (HasActiveProject(state, settlement.Id))
            {
                continue;
            }

            var repair = TryStartRepair(state, settlement.Id, request);
            if (repair.Status == SettlementProjectStartStatus.Success)
            {
                started++;
                continue;
            }

            var buildKind = ChooseFacilityToBuild(state, settlement.Id);
            if (!buildKind.HasValue)
            {
                continue;
            }

            var build = SettlementProjectService.StartBuildFacility(
                state,
                settlement.Id,
                buildKind.Value,
                level: GetFacilityLevel(state, settlement.Id),
                startTick: request.Tick,
                durationTicks: Math.Max(1, request.ProjectDurationTicks),
                steelCost: Math.Max(0, request.BuildSteelCost),
                componentCost: Math.Max(0, request.BuildComponentCost));
            if (build.Status == SettlementProjectStartStatus.Success)
            {
                started++;
            }
        }

        return new SettlementInfrastructureDriverResult(started, completed.CompletedProjects);
    }

    private static bool HasActiveProject(WorldState state, EntityId settlementId)
    {
        return state.SettlementProjects.Any(project =>
            project.SettlementId == settlementId
            && project.Status == SettlementProjectStatus.Active);
    }

    private static SettlementProjectStartResult TryStartRepair(
        WorldState state,
        EntityId settlementId,
        SettlementInfrastructureDriverRequest request)
    {
        var threshold = Math.Max(1, Math.Min(100, request.RepairConditionThreshold));
        var damaged = state.GetSettlementFacilities(settlementId)
            .Where(facility => facility.ConditionPercent < threshold)
            .OrderBy(facility => facility.ConditionPercent)
            .ThenBy(facility => facility.Id.Value)
            .FirstOrDefault();
        if (damaged == null)
        {
            return new SettlementProjectStartResult(
                SettlementProjectStartStatus.InvalidTarget,
                null,
                "No damaged facility needs repair.");
        }

        return SettlementProjectService.StartRepairFacility(
            state,
            damaged.Id,
            startTick: request.Tick,
            durationTicks: Math.Max(1, request.ProjectDurationTicks),
            steelCost: Math.Max(0, request.RepairSteelCost),
            componentCost: Math.Max(0, request.RepairComponentCost));
    }

    private static SettlementFacilityKind? ChooseFacilityToBuild(WorldState state, EntityId settlementId)
    {
        var profile = state.GetSettlementProductionProfile(settlementId);
        if (profile == null)
        {
            return null;
        }

        var existingKinds = new HashSet<SettlementFacilityKind>(
            state.GetSettlementFacilities(settlementId).Select(facility => facility.Kind));
        var candidates = new List<(SettlementFacilityKind Kind, int Score, int Order)>
        {
            (SettlementFacilityKind.Farm, profile.FoodPerAdult, 0),
            (SettlementFacilityKind.Workshop, profile.SteelPerAdult + profile.ComponentPerAdult, 1),
            (SettlementFacilityKind.Clinic, profile.MedicinePerAdult, 2),
        };

        return candidates
            .Where(candidate => candidate.Score > 0 && !existingKinds.Contains(candidate.Kind))
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Order)
            .Select(candidate => (SettlementFacilityKind?)candidate.Kind)
            .FirstOrDefault();
    }

    private static int GetFacilityLevel(WorldState state, EntityId settlementId)
    {
        return SettlementDevelopmentService.GetTier(state, settlementId) switch
        {
            SettlementTier.City => 3,
            SettlementTier.Town => 2,
            _ => 1,
        };
    }
}
