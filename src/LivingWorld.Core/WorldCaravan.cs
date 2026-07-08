namespace LivingWorld.Core;

public enum CaravanStatus
{
    Traveling,
    Arrived,
    Destroyed,
}

public sealed record WorldCaravan(
    EntityId Id,
    string Name,
    string FactionId,
    EntityId SourceSettlementId,
    EntityId TargetSettlementId,
    int DepartTick,
    int ArrivalTick,
    CaravanStatus Status);
