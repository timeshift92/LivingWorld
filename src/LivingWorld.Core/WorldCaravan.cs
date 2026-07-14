namespace LivingWorld.Core;

public enum CaravanStatus
{
    Traveling,
    Arrived,
    Destroyed,
    Recalled,
}

public sealed record WorldCaravan(
    EntityId Id,
    string Name,
    string FactionId,
    EntityId SourceSettlementId,
    EntityId TargetSettlementId,
    int DepartTick,
    int ArrivalTick,
    CaravanStatus Status,
    EntityId? CrewCitizenId = null)
{
    public WorldTransitPhase Phase { get; init; } = WorldTransitPhase.Outbound;

    public int StatusTick { get; init; } = DepartTick;

    public int ReturnArrivalTick { get; init; } = ArrivalTick;

    public bool CompleteAsRecalled { get; init; }

    public string TradeResourceKey { get; init; } = string.Empty;

    public string SilverResourceKey { get; init; } = "Silver";

    public int TradeBaseUnitPrice { get; init; } = 1;

    public int RequestedTradeQuantity { get; init; }

    public bool TradeExecuted { get; init; }
}
