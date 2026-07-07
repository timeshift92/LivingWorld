namespace LivingWorld.Core;

public sealed record WorldArmy(
    EntityId Id,
    string Name,
    string FactionId,
    EntityId SourceSettlementId);
