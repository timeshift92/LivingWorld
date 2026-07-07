namespace LivingWorld.Core;

public enum MigrationGroupStatus
{
    Traveling,
    Arrived,
    Lost
}

public sealed record WorldMigrationGroup(
    EntityId Id,
    EntityId SourceSettlementId,
    EntityId? TargetSettlementId,
    string FactionId,
    int CreatedTick,
    int ArrivalTick,
    MigrationGroupStatus Status,
    string Reason);
