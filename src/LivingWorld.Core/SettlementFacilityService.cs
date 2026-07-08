namespace LivingWorld.Core;

public enum SettlementFacilityKind
{
    Farm,
    Workshop,
    Clinic,
    PowerPlant,
    Storage,
}

public enum SettlementProjectKind
{
    BuildFacility,
    RepairFacility,
}

public enum SettlementProjectStatus
{
    Active,
    Completed,
    Cancelled,
}

public enum SettlementProjectStartStatus
{
    Success,
    MissingSettlement,
    MissingFacility,
    InvalidTarget,
    InsufficientResources,
}

public sealed record SettlementFacility(
    EntityId Id,
    EntityId SettlementId,
    SettlementFacilityKind Kind,
    int Level,
    int ConditionPercent,
    int BuiltTick);

public sealed record SettlementProject(
    EntityId Id,
    EntityId SettlementId,
    SettlementProjectKind Kind,
    SettlementProjectStatus Status,
    SettlementFacilityKind FacilityKind,
    EntityId? TargetFacilityId,
    int FacilityLevel,
    int StartedTick,
    int CompletionTick,
    int SteelCost,
    int ComponentCost);

public sealed record SettlementProjectStartResult(
    SettlementProjectStartStatus Status,
    SettlementProject? Project,
    string Reason);

public sealed record SettlementProjectCompletionResult(int CompletedProjects);

public static class SettlementFacilityService
{
    public static SettlementFacility DamageFacility(
        WorldState state,
        EntityId facilityId,
        int damagePercent,
        string reason)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Damage reason cannot be empty.", nameof(reason));
        }

        var facility = state.GetSettlementFacility(facilityId)
            ?? throw new InvalidOperationException($"Facility {facilityId} does not exist.");
        var damaged = state.RecordSettlementFacility(facility with
        {
            ConditionPercent = Math.Max(0, facility.ConditionPercent - Math.Max(0, damagePercent))
        });
        state.RecordEvent(
            WorldEventKind.SettlementFacilityDamaged,
            damaged.SettlementId,
            $"Settlement {damaged.SettlementId} facility {damaged.Id} damaged: {reason}.");

        return damaged;
    }

    public static int ApplyProductionModifier(
        WorldState state,
        EntityId settlementId,
        SettlementFacilityKind facilityKind,
        int baseOutput)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (baseOutput <= 0)
        {
            return 0;
        }

        var bonusPercent = state.GetSettlementFacilities(settlementId)
            .Where(facility => facility.Kind == facilityKind)
            .Sum(EffectiveBonusPercent);
        return (int)((long)baseOutput * (100 + bonusPercent) / 100);
    }

    private static int EffectiveBonusPercent(SettlementFacility facility)
    {
        if (facility == null)
        {
            return 0;
        }

        var level = Math.Max(0, facility.Level);
        var condition = Math.Max(0, Math.Min(100, facility.ConditionPercent));
        if (level == 0 || condition == 0)
        {
            return 0;
        }

        return level * 20 * condition / 100;
    }
}

public static class SettlementProjectService
{
    private const string SteelResourceKey = "Steel";
    private const string ComponentResourceKey = "ComponentIndustrial";

    public static SettlementProjectStartResult StartBuildFacility(
        WorldState state,
        EntityId settlementId,
        SettlementFacilityKind facilityKind,
        int level,
        int startTick,
        int durationTicks,
        int steelCost,
        int componentCost)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (state.GetSettlement(settlementId) == null)
        {
            return new SettlementProjectStartResult(
                SettlementProjectStartStatus.MissingSettlement,
                null,
                $"Settlement {settlementId} does not exist.");
        }

        var cost = TryConsumeProjectCost(state, settlementId, steelCost, componentCost);
        if (cost != null)
        {
            return cost;
        }

        var project = state.CreateSettlementProject(
            settlementId,
            SettlementProjectKind.BuildFacility,
            facilityKind,
            targetFacilityId: null,
            facilityLevel: Math.Max(1, level),
            startedTick: Math.Max(0, startTick),
            completionTick: Math.Max(0, startTick) + Math.Max(1, durationTicks),
            steelCost: Math.Max(0, steelCost),
            componentCost: Math.Max(0, componentCost));

        return new SettlementProjectStartResult(
            SettlementProjectStartStatus.Success,
            project,
            "Project started.");
    }

    public static SettlementProjectStartResult StartRepairFacility(
        WorldState state,
        EntityId facilityId,
        int startTick,
        int durationTicks,
        int steelCost,
        int componentCost)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var facility = state.GetSettlementFacility(facilityId);
        if (facility == null)
        {
            return new SettlementProjectStartResult(
                SettlementProjectStartStatus.MissingFacility,
                null,
                $"Facility {facilityId} does not exist.");
        }

        if (facility.ConditionPercent >= 100)
        {
            return new SettlementProjectStartResult(
                SettlementProjectStartStatus.InvalidTarget,
                null,
                $"Facility {facilityId} is already fully repaired.");
        }

        var cost = TryConsumeProjectCost(state, facility.SettlementId, steelCost, componentCost);
        if (cost != null)
        {
            return cost;
        }

        var project = state.CreateSettlementProject(
            facility.SettlementId,
            SettlementProjectKind.RepairFacility,
            facility.Kind,
            facility.Id,
            facility.Level,
            startedTick: Math.Max(0, startTick),
            completionTick: Math.Max(0, startTick) + Math.Max(1, durationTicks),
            steelCost: Math.Max(0, steelCost),
            componentCost: Math.Max(0, componentCost));

        return new SettlementProjectStartResult(
            SettlementProjectStartStatus.Success,
            project,
            "Repair project started.");
    }

    public static SettlementProjectCompletionResult CompleteReadyProjects(WorldState state, int tick)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        state.AdvanceToTick(Math.Max(0, tick));

        var completed = 0;
        foreach (var project in state.SettlementProjects
            .Where(project => project.Status == SettlementProjectStatus.Active
                && project.CompletionTick <= state.CurrentTick)
            .OrderBy(project => project.CompletionTick)
            .ThenBy(project => project.Id.Value)
            .ToList())
        {
            CompleteProject(state, project);
            completed++;
        }

        return new SettlementProjectCompletionResult(completed);
    }

    private static void CompleteProject(WorldState state, SettlementProject project)
    {
        if (project.Kind == SettlementProjectKind.BuildFacility)
        {
            var facility = new SettlementFacility(
                state.NextFacilityIdForLedger(),
                project.SettlementId,
                project.FacilityKind,
                project.FacilityLevel,
                ConditionPercent: 100,
                BuiltTick: state.CurrentTick);
            state.RecordSettlementFacility(facility);
            state.RecordEvent(
                WorldEventKind.SettlementFacilityBuilt,
                project.SettlementId,
                $"Settlement {project.SettlementId} completed {project.FacilityKind} facility {facility.Id}.");
        }
        else if (project.Kind == SettlementProjectKind.RepairFacility && project.TargetFacilityId.HasValue)
        {
            var facility = state.GetSettlementFacility(project.TargetFacilityId.Value);
            if (facility != null)
            {
                state.RecordSettlementFacility(facility with { ConditionPercent = 100 });
                state.RecordEvent(
                    WorldEventKind.SettlementFacilityRepaired,
                    project.SettlementId,
                    $"Settlement {project.SettlementId} repaired facility {facility.Id}.");
            }
        }

        state.RecordSettlementProject(project with { Status = SettlementProjectStatus.Completed });
        state.RecordEvent(
            WorldEventKind.SettlementProjectCompleted,
            project.SettlementId,
            $"Settlement {project.SettlementId} completed project {project.Id}.");
    }

    private static SettlementProjectStartResult? TryConsumeProjectCost(
        WorldState state,
        EntityId settlementId,
        int steelCost,
        int componentCost)
    {
        var steel = Math.Max(0, steelCost);
        var components = Math.Max(0, componentCost);
        if (state.GetOwnedResourceQuantity(settlementId, SteelResourceKey) < steel
            || state.GetOwnedResourceQuantity(settlementId, ComponentResourceKey) < components)
        {
            return new SettlementProjectStartResult(
                SettlementProjectStartStatus.InsufficientResources,
                null,
                "Settlement lacks project materials.");
        }

        state.ConsumeResource(settlementId, SteelResourceKey, steel, "settlement project");
        state.ConsumeResource(settlementId, ComponentResourceKey, components, "settlement project");
        return null;
    }
}
