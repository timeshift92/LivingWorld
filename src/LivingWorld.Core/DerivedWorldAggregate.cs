namespace LivingWorld.Core;

public sealed record SettlementDerivedAggregate(
    EntityId SettlementId,
    string FactionId,
    SettlementPopulation Population,
    SettlementPower Power);

public sealed record FactionDerivedAggregate(
    string FactionId,
    SettlementPopulation Population,
    SettlementPower Power);
