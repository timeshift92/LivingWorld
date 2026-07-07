namespace LivingWorld.Core;

public sealed record WorldStateSnapshot(
    int WorldSeed,
    int CurrentTick,
    IReadOnlyList<WorldSettlement> Settlements,
    IReadOnlyList<WorldCitizen> Citizens,
    IReadOnlyList<WorldArmy> Armies,
    IReadOnlyList<WorldMigrationGroup> MigrationGroups,
    IReadOnlyList<WorldIntelReport> IntelReports,
    IReadOnlyList<KnownSettlementInfo> KnownSettlementInfos,
    IReadOnlyList<RaidOpportunity> RaidOpportunities,
    IReadOnlyList<RaidPawnLink> RaidPawnLinks,
    IReadOnlyList<WorldRaidOutcome> RaidOutcomes,
    IReadOnlyList<SettlementProductionProfile> ProductionProfiles,
    IReadOnlyList<WorldFactionRecord> FactionRecords,
    IReadOnlyList<OwnershipRecord> Ownership,
    IReadOnlyList<ResourceStack> Resources,
    IReadOnlyList<WorldEvent> Events,
    IReadOnlyList<Drifter> Drifters)
{
    public IReadOnlyList<SettlementCapability> SettlementCapabilities { get; init; } =
        Array.Empty<SettlementCapability>();

    public IReadOnlyList<SpecialistPool> SpecialistPools { get; init; } =
        Array.Empty<SpecialistPool>();
}
