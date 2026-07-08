namespace LivingWorld.Core;

public enum RuinStatus
{
    Active,
    Reclaimed,
}

public enum RuinSalvageBand
{
    None,
    Low,
    Medium,
    High,
}

public enum RuinDangerBand
{
    Low,
    Medium,
    High,
}

public sealed record WorldRuin(
    EntityId Id,
    EntityId OriginalSettlementId,
    string Slug,
    string Name,
    string FormerFactionId,
    string ClaimFactionId,
    RuinSalvageBand SalvageBand,
    RuinDangerBand DangerBand,
    int CreatedTick,
    RuinStatus Status,
    EntityId? ReclaimedSettlementId,
    int StatusTick);

public sealed record SettlementDestructionResult(WorldRuin Ruin, int RefugeesCreated);

public sealed record SettlementRelocationResult(WorldRuin Ruin, WorldMigrationGroup MigrationGroup, int CitizensMoved, int ResourceStacksMoved);

public sealed record RuinReclaimResult(WorldRuin Ruin, WorldSettlement Settlement);

public sealed record RuinPruneResult(int Pruned);
