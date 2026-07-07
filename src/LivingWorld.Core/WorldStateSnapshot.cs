namespace LivingWorld.Core;

public sealed record WorldStateSnapshot(
    int WorldSeed,
    int CurrentTick,
    IReadOnlyList<WorldSettlement> Settlements,
    IReadOnlyList<WorldCitizen> Citizens,
    IReadOnlyList<WorldArmy> Armies,
    IReadOnlyList<WorldIntelReport> IntelReports,
    IReadOnlyList<KnownSettlementInfo> KnownSettlementInfos,
    IReadOnlyList<RaidOpportunity> RaidOpportunities,
    IReadOnlyList<RaidPawnLink> RaidPawnLinks,
    IReadOnlyList<WorldRaidOutcome> RaidOutcomes,
    IReadOnlyList<OwnershipRecord> Ownership,
    IReadOnlyList<ResourceStack> Resources,
    IReadOnlyList<WorldEvent> Events);
