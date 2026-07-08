namespace LivingWorld.Core;

public enum WorldConflictStatus
{
    Active,
    Truce,
    Resolved,
}

public sealed record WorldConflict(
    EntityId Id,
    string FactionA,
    string FactionB,
    WorldConflictStatus Status,
    int StartedTick,
    int StatusTick,
    int WarExhaustionA,
    int WarExhaustionB,
    int TruceExpiresTick,
    int RefugeesCreated)
{
    public bool Involves(string factionId) =>
        string.Equals(FactionA, factionId, StringComparison.Ordinal)
        || string.Equals(FactionB, factionId, StringComparison.Ordinal);

    public bool IsPair(string factionA, string factionB) =>
        (string.Equals(FactionA, factionA, StringComparison.Ordinal)
            && string.Equals(FactionB, factionB, StringComparison.Ordinal))
        || (string.Equals(FactionA, factionB, StringComparison.Ordinal)
            && string.Equals(FactionB, factionA, StringComparison.Ordinal));

    public int GetWarExhaustion(string factionId)
    {
        if (string.Equals(FactionA, factionId, StringComparison.Ordinal))
        {
            return WarExhaustionA;
        }

        return string.Equals(FactionB, factionId, StringComparison.Ordinal)
            ? WarExhaustionB
            : 0;
    }
}

public sealed record ConflictClaim(
    EntityId ConflictId,
    EntityId SettlementId,
    string ClaimantFactionId,
    int Tick);
