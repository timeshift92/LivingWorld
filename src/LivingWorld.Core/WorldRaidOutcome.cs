namespace LivingWorld.Core;

public sealed record WorldRaidOutcome(
    EntityId ArmyId,
    EntityId SourceSettlementId,
    string FactionId,
    int Tick,
    int Sent,
    int Active,
    int Dead,
    int Returned,
    int Prisoner)
{
    public bool IsResolved => Sent > 0 && Active == 0;
}
