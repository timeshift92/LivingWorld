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
    public string? PlayerFactionId { get; init; }

    public int DrifterArrivalReservoir { get; init; }

    public IReadOnlyList<SettlementCapability> SettlementCapabilities { get; init; } =
        Array.Empty<SettlementCapability>();

    public IReadOnlyList<SpecialistPool> SpecialistPools { get; init; } =
        Array.Empty<SpecialistPool>();

    public IReadOnlyList<SettlementWealthSnapshot> SettlementWealth { get; init; } =
        Array.Empty<SettlementWealthSnapshot>();

    public IReadOnlyList<FactionWealthSnapshot> FactionWealth { get; init; } =
        Array.Empty<FactionWealthSnapshot>();

    public IReadOnlyList<WorldCaravan> Caravans { get; init; } =
        Array.Empty<WorldCaravan>();

    public IReadOnlyList<WorldMission> Missions { get; init; } =
        Array.Empty<WorldMission>();
}
