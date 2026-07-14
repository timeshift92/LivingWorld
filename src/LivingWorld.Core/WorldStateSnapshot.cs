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

    public IReadOnlyList<FactionSettlementIntel> FactionSettlementIntel { get; init; } =
        Array.Empty<FactionSettlementIntel>();

    public IReadOnlyList<WorldCaravan> Caravans { get; init; } =
        Array.Empty<WorldCaravan>();

    public IReadOnlyList<WorldMission> Missions { get; init; } =
        Array.Empty<WorldMission>();

    public IReadOnlyList<WorldRuin> Ruins { get; init; } =
        Array.Empty<WorldRuin>();

    public IReadOnlyList<WorldConflict> Conflicts { get; init; } =
        Array.Empty<WorldConflict>();

    public IReadOnlyList<ConflictClaim> ConflictClaims { get; init; } =
        Array.Empty<ConflictClaim>();

    public IReadOnlyList<RaidIntelFact> RaidIntelFacts { get; init; } =
        Array.Empty<RaidIntelFact>();

    public IReadOnlyList<RaidPreparation> RaidPreparations { get; init; } =
        Array.Empty<RaidPreparation>();

    public IReadOnlyList<MaterializationLease> MaterializationLeases { get; init; } =
        Array.Empty<MaterializationLease>();

    public IReadOnlyList<PrisonerRecord> PrisonerRecords { get; init; } =
        Array.Empty<PrisonerRecord>();

    public IReadOnlyList<SettlementFacility> SettlementFacilities { get; init; } =
        Array.Empty<SettlementFacility>();

    public IReadOnlyList<SettlementProject> SettlementProjects { get; init; } =
        Array.Empty<SettlementProject>();

    public IReadOnlyList<WorldAnimalCohort> AnimalCohorts { get; init; } =
        Array.Empty<WorldAnimalCohort>();

    public IReadOnlyList<AnimalBreedingProject> AnimalBreedingProjects { get; init; } =
        Array.Empty<AnimalBreedingProject>();

    public IReadOnlyList<CropStrain> CropStrains { get; init; } =
        Array.Empty<CropStrain>();

    public IReadOnlyList<CropStrainProject> CropStrainProjects { get; init; } =
        Array.Empty<CropStrainProject>();

    public IReadOnlyList<SettlementTechnology> SettlementTechnologies { get; init; } =
        Array.Empty<SettlementTechnology>();

    public IReadOnlyList<DrifterAssimilationJourney> DrifterAssimilationJourneys { get; init; } =
        Array.Empty<DrifterAssimilationJourney>();

    public WorldEventArchiveCheckpoint EventArchive { get; init; } =
        WorldEventArchiveCheckpoint.Empty;
}
