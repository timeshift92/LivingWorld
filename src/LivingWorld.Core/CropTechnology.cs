namespace LivingWorld.Core;

public enum CropStrainTrait
{
    Yield,
    Hardiness,
    GrowthSpeed
}

public enum CropStrainProjectStatus
{
    Active,
    Completed
}

public enum CropStrainStartStatus
{
    Success,
    MissingSettlement,
    InsufficientCapability,
    InsufficientResources
}

public enum TechnologyDomain
{
    Agriculture,
    Medicine,
    Industry,
    Military
}

public enum TechnologyTier
{
    Neolithic = 1,
    Medieval = 2,
    Industrial = 3,
    Spacer = 4
}

public sealed record CropStrain(
    EntityId SettlementId,
    string CropKind,
    int YieldPercent,
    int HardinessPercent,
    int GrowthSpeedPercent,
    int LastUpdatedTick);

public sealed record CropStrainProject(
    EntityId Id,
    EntityId SettlementId,
    string CropKind,
    CropStrainTrait Trait,
    CropStrainProjectStatus Status,
    int StartedTick,
    int CompletionTick,
    string FoodResourceKey,
    int FoodCost,
    string MedicineResourceKey,
    int MedicineCost);

public sealed record CropStrainStartRequest(
    int Tick,
    EntityId SettlementId,
    string CropKind,
    CropStrainTrait Trait,
    int DurationTicks,
    string FoodResourceKey,
    int FoodCost,
    string MedicineResourceKey,
    int MedicineCost);

public sealed record CropStrainStartResult(
    CropStrainStartStatus Status,
    CropStrainProject? Project,
    string Reason);

public sealed record CropStrainCompletionResult(int CompletedProjects);

public sealed record CropStrainDriverRequest(
    int Tick,
    string FoodResourceKey,
    string MedicineResourceKey,
    int SelectionDurationTicks = 180_000,
    int FoodCost = 20,
    int MedicineCost = 1,
    int MaxProjectsStartedPerDay = 1);

public sealed record CropStrainDriverResult(
    int ProjectsCompleted,
    int ProjectsStarted);

public sealed record SettlementTechnology(
    EntityId SettlementId,
    TechnologyDomain Domain,
    TechnologyTier Tier,
    int LastUpdatedTick,
    string Source);

public sealed record TechnologyDiffusionRequest(
    int Tick,
    int MaxDiffusionsPerDay = 1);

public sealed record TechnologyDiffusionResult(int Diffusions);

public static class CropStrainService
{
    public static CropStrainStartResult StartSelectionProject(WorldState state, CropStrainStartRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        ThrowIfEmpty(request.CropKind, nameof(request.CropKind));
        ThrowIfEmpty(request.FoodResourceKey, nameof(request.FoodResourceKey));
        ThrowIfEmpty(request.MedicineResourceKey, nameof(request.MedicineResourceKey));

        state.AdvanceToTick(request.Tick);
        var settlement = state.GetSettlement(request.SettlementId);
        if (settlement == null || !settlement.IsActive)
        {
            return new CropStrainStartResult(CropStrainStartStatus.MissingSettlement, null, "Settlement is missing or inactive.");
        }

        var capability = state.GetSettlementCapabilityStatus(settlement.Id);
        if (!capability.CanSupportCropProgram)
        {
            return new CropStrainStartResult(CropStrainStartStatus.InsufficientCapability, null, "Settlement lacks crop capability.");
        }

        if (state.GetOwnedResourceQuantity(settlement.Id, request.FoodResourceKey) < Math.Max(0, request.FoodCost)
            || state.GetOwnedResourceQuantity(settlement.Id, request.MedicineResourceKey) < Math.Max(0, request.MedicineCost))
        {
            return new CropStrainStartResult(CropStrainStartStatus.InsufficientResources, null, "Settlement lacks crop project resources.");
        }

        state.ConsumeResource(settlement.Id, request.FoodResourceKey, Math.Max(0, request.FoodCost), "crop strain project");
        state.ConsumeResource(settlement.Id, request.MedicineResourceKey, Math.Max(0, request.MedicineCost), "crop strain project");
        var project = state.CreateCropStrainProject(
            settlement.Id,
            request.CropKind,
            request.Trait,
            request.Tick,
            request.Tick + Math.Max(1, request.DurationTicks),
            request.FoodResourceKey,
            Math.Max(0, request.FoodCost),
            request.MedicineResourceKey,
            Math.Max(0, request.MedicineCost));

        return new CropStrainStartResult(CropStrainStartStatus.Success, project, "Crop strain project started.");
    }

    public static CropStrainCompletionResult CompleteReadyProjects(WorldState state, int tick)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        state.AdvanceToTick(tick);
        var completed = 0;
        foreach (var project in state.CropStrainProjects
            .Where(project => project.Status == CropStrainProjectStatus.Active && project.CompletionTick <= state.CurrentTick)
            .OrderBy(project => project.CompletionTick)
            .ThenBy(project => project.Id.Value)
            .ToList())
        {
            CompleteProject(state, project);
            completed++;
        }

        return new CropStrainCompletionResult(completed);
    }

    private static void CompleteProject(WorldState state, CropStrainProject project)
    {
        var current = state.GetCropStrain(project.SettlementId, project.CropKind)
            ?? new CropStrain(project.SettlementId, project.CropKind, 100, 100, 100, state.CurrentTick);
        var improved = project.Trait switch
        {
            CropStrainTrait.Yield => current with { YieldPercent = Math.Min(160, current.YieldPercent + 10), LastUpdatedTick = state.CurrentTick },
            CropStrainTrait.Hardiness => current with { HardinessPercent = Math.Min(160, current.HardinessPercent + 10), LastUpdatedTick = state.CurrentTick },
            CropStrainTrait.GrowthSpeed => current with { GrowthSpeedPercent = Math.Min(160, current.GrowthSpeedPercent + 10), LastUpdatedTick = state.CurrentTick },
            _ => current with { LastUpdatedTick = state.CurrentTick }
        };
        state.RecordCropStrain(improved);
        ApplyProductionBonus(state, improved.SettlementId);
        state.RecordCropStrainProjectForSimulation(project with { Status = CropStrainProjectStatus.Completed });
        state.RecordEvent(WorldEventKind.CropStrainProjectCompleted, project.Id, $"Crop strain project {project.Id} completed.");
    }

    private static void ApplyProductionBonus(WorldState state, EntityId settlementId)
    {
        var bestYield = state.GetCropStrains(settlementId)
            .Select(strain => strain.YieldPercent)
            .DefaultIfEmpty(100)
            .Max();
        var profile = state.GetSettlementProductionProfile(settlementId);
        if (profile != null)
        {
            state.RecordSettlementProductionProfile(profile with
            {
                EconomyScalePercent = Math.Max(profile.EconomyScalePercent, bestYield)
            });
        }
    }

    private static void ThrowIfEmpty(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Crop strain value cannot be empty.", parameterName);
        }
    }
}

public static class TechnologyDiffusionService
{
    public static TechnologyDiffusionResult SimulateDay(WorldState state, TechnologyDiffusionRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.Tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Technology diffusion tick cannot be negative.");
        }

        state.AdvanceToTick(request.Tick);
        if (request.MaxDiffusionsPerDay <= 0)
        {
            return new TechnologyDiffusionResult(0);
        }

        var diffusions = 0;
        foreach (var target in state.Settlements
            .Where(settlement => settlement.IsActive)
            .OrderBy(settlement => settlement.Id.Value))
        {
            if (diffusions >= request.MaxDiffusionsPerDay)
            {
                break;
            }

            var source = FindSourceForTarget(state, target);
            if (source == null)
            {
                continue;
            }

            var currentTier = state.GetSettlementTechnology(target.Id, source.Domain)?.Tier ?? TechnologyTier.Neolithic;
            var nextTier = (TechnologyTier)Math.Min((int)source.Tier, (int)currentTier + 1);
            if (nextTier <= currentTier)
            {
                continue;
            }

            state.RecordSettlementTechnology(new SettlementTechnology(
                target.Id,
                source.Domain,
                nextTier,
                request.Tick,
                $"diffused:{source.SourceSettlement.Slug}"));
            state.RecordEvent(
                WorldEventKind.TechnologyDiffused,
                target.Id,
                $"Technology {source.Domain} diffused from {source.SourceSettlement.Id} to {target.Id}.");
            diffusions++;
        }

        return new TechnologyDiffusionResult(diffusions);
    }

    private static TechnologySource? FindSourceForTarget(WorldState state, WorldSettlement target)
    {
        return state.SettlementTechnologies
            .Select(technology => new
            {
                Technology = technology,
                SourceSettlement = state.GetSettlement(technology.SettlementId)
            })
            .Where(candidate =>
                candidate.SourceSettlement != null
                && candidate.SourceSettlement.Id != target.Id
                && candidate.SourceSettlement.IsActive
                && string.Equals(candidate.SourceSettlement.FactionId, target.FactionId, StringComparison.Ordinal)
                && (int)candidate.Technology.Tier > (int)(state.GetSettlementTechnology(target.Id, candidate.Technology.Domain)?.Tier ?? TechnologyTier.Neolithic))
            .OrderBy(candidate => candidate.Technology.Domain)
            .ThenByDescending(candidate => candidate.Technology.Tier)
            .ThenBy(candidate => candidate.SourceSettlement!.Id.Value)
            .Select(candidate => new TechnologySource(candidate.Technology.Domain, candidate.Technology.Tier, candidate.SourceSettlement!))
            .FirstOrDefault();
    }

    private sealed record TechnologySource(
        TechnologyDomain Domain,
        TechnologyTier Tier,
        WorldSettlement SourceSettlement);
}

public static class CropStrainDriver
{
    public static CropStrainDriverResult SimulateDay(WorldState state, CropStrainDriverRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.Tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Crop strain driver tick cannot be negative.");
        }

        var completed = CropStrainService.CompleteReadyProjects(state, request.Tick).CompletedProjects;
        if (completed > 0 || request.MaxProjectsStartedPerDay <= 0)
        {
            return new CropStrainDriverResult(completed, 0);
        }

        var started = 0;
        foreach (var settlement in state.Settlements
            .Where(settlement => settlement.IsActive)
            .OrderBy(settlement => settlement.Id.Value))
        {
            if (started >= request.MaxProjectsStartedPerDay)
            {
                break;
            }

            if (state.CropStrainProjects.Any(project =>
                project.SettlementId == settlement.Id
                && project.Status == CropStrainProjectStatus.Active))
            {
                continue;
            }

            var status = state.GetSettlementCapabilityStatus(settlement.Id);
            var specialists = state.GetSpecialistPool(settlement.Id);
            if (!status.CanSupportCropProgram
                || specialists == null
                || specialists.Farmers <= 0
                || specialists.Researchers <= 0)
            {
                continue;
            }

            var result = CropStrainService.StartSelectionProject(
                state,
                new CropStrainStartRequest(
                    request.Tick,
                    settlement.Id,
                    ChooseCropKind(state, settlement.Id),
                    ChooseTrait(state, settlement.Id),
                    request.SelectionDurationTicks,
                    request.FoodResourceKey,
                    request.FoodCost,
                    request.MedicineResourceKey,
                    request.MedicineCost));
            if (result.Status == CropStrainStartStatus.Success)
            {
                started++;
            }
        }

        return new CropStrainDriverResult(completed, started);
    }

    private static string ChooseCropKind(WorldState state, EntityId settlementId)
    {
        var profile = state.GetSettlementProductionProfile(settlementId);
        if (profile == null)
        {
            return "Rice";
        }

        if (profile.GrowingDays < 20 || profile.Rainfall < 250)
        {
            return "Agave";
        }

        return profile.GrowingDays >= 45 ? "Rice" : "Corn";
    }

    private static CropStrainTrait ChooseTrait(WorldState state, EntityId settlementId)
    {
        var weakest = state.GetCropStrains(settlementId)
            .OrderBy(strain => Math.Min(strain.YieldPercent, Math.Min(strain.HardinessPercent, strain.GrowthSpeedPercent)))
            .FirstOrDefault();
        if (weakest == null)
        {
            return CropStrainTrait.Yield;
        }

        if (weakest.YieldPercent <= weakest.HardinessPercent && weakest.YieldPercent <= weakest.GrowthSpeedPercent)
        {
            return CropStrainTrait.Yield;
        }

        return weakest.HardinessPercent <= weakest.GrowthSpeedPercent
            ? CropStrainTrait.Hardiness
            : CropStrainTrait.GrowthSpeed;
    }
}
