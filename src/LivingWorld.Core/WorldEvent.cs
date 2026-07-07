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
    CitizenAged,
    CitizenBorn,
    RaidLaunched,
    IntelReported,
    RaidOpportunityCreated,
    RaidOpportunityConsumed,
    SettlementIntelUpdated,
    RaidPawnBound,
    CitizenDied,
    RefugeeCreated,
    MigrationStarted,
    MigrationCompleted,
    RaidPawnReturned,
    RaidPawnCaptured,
    RaidPawnMissing,
    RaidResolved,
    DrifterArrived,
    DrifterAssimilated,
    SettlementFounded,
    FactionCollapsed,
    SettlementProductionUpdated,
    SettlementTradeRecorded
}

public sealed record WorldEvent(
    EntityId Id,
    WorldEventKind Kind,
    int Tick,
    EntityId? SubjectId,
    string Summary);
