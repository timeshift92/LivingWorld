namespace LivingWorld.Core;

public enum ActionAttemptReason
{
    Success,
    InvalidRequest,
    NoTarget,
    NoIntel,
    NoCrew,
    TrafficCap,
    NoCargo,
    InsufficientPopulation,
    InsufficientSupplies,
    Cooldown,
    Truce,
    AlreadyInFlight,
    ExecutionFailed,
}

public sealed record ActionAttemptResult(
    bool Succeeded,
    ActionAttemptReason Reason,
    string Detail)
{
    public static ActionAttemptResult Success(string detail = "launched") =>
        new(true, ActionAttemptReason.Success, detail);

    public static ActionAttemptResult Failed(ActionAttemptReason reason, string detail) =>
        new(false, reason, detail);
}

public sealed record WorldActionAttempt(
    int Tick,
    string FactionId,
    WarAction Action,
    ActionAttemptReason Reason,
    string Detail,
    EntityId? TargetSettlementId);

public sealed record ActionAttemptCounter(
    WarAction Action,
    ActionAttemptReason Reason,
    long Count);
