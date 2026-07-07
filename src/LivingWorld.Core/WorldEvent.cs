namespace LivingWorld.Core;

public enum WorldEventKind
{
    SettlementCreated,
    CitizenCreated,
    CitizenImported,
    ArmyCreated,
    OwnershipAssigned,
    OwnershipTransferred,
    ResourceAdded,
    ResourceConsumed,
    FoodShortage,
    CitizenBorn,
    RaidLaunched,
    IntelReported,
    RaidOpportunityCreated,
    RaidOpportunityConsumed,
    SettlementIntelUpdated,
    RaidPawnBound,
    CitizenDied,
    RefugeeCreated,
    MigrationCompleted,
    RaidPawnReturned,
    RaidPawnCaptured,
    RaidPawnMissing,
    RaidResolved,
    DrifterArrived
}

public sealed record WorldEvent(
    EntityId Id,
    WorldEventKind Kind,
    int Tick,
    EntityId? SubjectId,
    string Summary);
