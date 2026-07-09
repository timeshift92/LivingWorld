namespace LivingWorld.Core;

public enum DailyActivityDomain
{
    Production,
    Consumption,
    Construction,
    Military,
    Trade,
    Ecology,
    Technology,
    Diplomacy,
    Population,
    Migration,
    Other
}

public sealed record DailyActivitySummaryRequest(int StartTick, int EndTick, int MaxEventsPerDomain);

public sealed record DailyActivitySummaryRow(
    DailyActivityDomain Domain,
    int Count,
    IReadOnlyList<WorldEvent> Events);

public sealed record DailyActivitySummary(IReadOnlyList<DailyActivitySummaryRow> Rows, int TotalEvents)
{
    public int CountFor(DailyActivityDomain domain)
    {
        return Rows.FirstOrDefault(row => row.Domain == domain)?.Count ?? 0;
    }

    public string FormatCompact()
    {
        return string.Join(
            "; ",
            Rows
                .Where(row => row.Count > 0)
                .OrderBy(row => row.Domain)
                .Select(row =>
                {
                    var samples = string.Join(", ", row.Events.Select(worldEvent => worldEvent.Kind.ToString()));
                    return $"{row.Domain}: {row.Count} [{samples}]";
                }));
    }
}

public static class DailyActivitySummaryService
{
    public static DailyActivitySummary Summarize(WorldState state, DailyActivitySummaryRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var start = Math.Min(request.StartTick, request.EndTick);
        var end = Math.Max(request.StartTick, request.EndTick);
        var maxEvents = Math.Max(1, request.MaxEventsPerDomain);
        var events = state.Events
            .Where(worldEvent => worldEvent.Tick >= start && worldEvent.Tick <= end)
            .ToList();

        var rows = events
            .GroupBy(worldEvent => Classify(worldEvent.Kind))
            .Select(group => new DailyActivitySummaryRow(
                group.Key,
                group.Count(),
                group.OrderBy(worldEvent => worldEvent.Id.Value).Take(maxEvents).ToList()))
            .OrderBy(row => row.Domain)
            .ToList();

        return new DailyActivitySummary(rows, events.Count);
    }

    private static DailyActivityDomain Classify(WorldEventKind kind)
    {
        return kind switch
        {
            WorldEventKind.ResourceAdded or WorldEventKind.SettlementProductionUpdated or WorldEventKind.AnimalProductsHarvested
                => DailyActivityDomain.Production,
            WorldEventKind.ResourceConsumed or WorldEventKind.FoodShortage
                => DailyActivityDomain.Consumption,
            WorldEventKind.SettlementProjectStarted or WorldEventKind.SettlementProjectCompleted
                or WorldEventKind.SettlementFacilityBuilt or WorldEventKind.SettlementFacilityDamaged
                or WorldEventKind.SettlementFacilityRepaired or WorldEventKind.SettlementDeveloped
                => DailyActivityDomain.Construction,
            WorldEventKind.RaidLaunched or WorldEventKind.RaidResolved or WorldEventKind.WarbandLaunched
                or WorldEventKind.SettlementCaptured or WorldEventKind.CaravanDestroyed
                or WorldEventKind.ConflictStarted or WorldEventKind.ConflictUpdated
                or WorldEventKind.ConflictClaimRecorded or WorldEventKind.ConflictTruceStarted
                or WorldEventKind.WorldMissionDisrupted
                => DailyActivityDomain.Military,
            WorldEventKind.SettlementTradeRecorded or WorldEventKind.CaravanLaunched or WorldEventKind.CaravanArrived
                => DailyActivityDomain.Trade,
            WorldEventKind.AnimalCohortCreated or WorldEventKind.AnimalCohortGrew
                or WorldEventKind.AnimalCohortDeclined or WorldEventKind.AnimalCohortMigrated
                or WorldEventKind.AnimalHunted or WorldEventKind.AnimalBreedingProjectStarted
                or WorldEventKind.AnimalBreedingProjectCompleted or WorldEventKind.AnimalCohortIncubated
                or WorldEventKind.NamedAnimalRegistered
                => DailyActivityDomain.Ecology,
            WorldEventKind.CropStrainProjectStarted or WorldEventKind.CropStrainProjectCompleted
                or WorldEventKind.TechnologyDiffused
                => DailyActivityDomain.Technology,
            WorldEventKind.IntelReported or WorldEventKind.RaidOpportunityCreated
                or WorldEventKind.RaidOpportunityConsumed or WorldEventKind.RaidIntelFactRecorded
                or WorldEventKind.RaidPreparationCreated or WorldEventKind.RaidPreparationReleased
                or WorldEventKind.RaidPreparationLaunched or WorldEventKind.SettlementIntelUpdated
                or WorldEventKind.DiplomaticMissionSent
                => DailyActivityDomain.Diplomacy,
            WorldEventKind.CitizenCreated or WorldEventKind.CitizenImported or WorldEventKind.CitizenAged
                or WorldEventKind.CitizenBorn or WorldEventKind.CitizenDied or WorldEventKind.RefugeeCreated
                or WorldEventKind.DrifterArrived or WorldEventKind.DrifterAssimilated
                or WorldEventKind.DrifterReservoirReplenished or WorldEventKind.WarRefugeesRecorded
                => DailyActivityDomain.Population,
            WorldEventKind.MigrationStarted or WorldEventKind.MigrationCompleted
                or WorldEventKind.SettlementFounded or WorldEventKind.SettlementDestroyed
                or WorldEventKind.SettlementAbandoned or WorldEventKind.SettlementRelocationStarted
                or WorldEventKind.RuinReclaimed or WorldEventKind.RuinPruned
                => DailyActivityDomain.Migration,
            _ => DailyActivityDomain.Other
        };
    }
}
