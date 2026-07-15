namespace LivingWorld.Core;

/// <summary>
/// A non-combat, non-cargo world-war mission that travels to a target settlement or player contact
/// endpoint and applies its effect only after arrival. The mission reserves
/// a real citizen crew member while travelling so visible world-map markers are backed by ledger
/// population instead of virtual traffic.
/// </summary>
public enum WorldMissionKind
{
    Scout,
    Diplomat,
}

public enum WorldMissionStatus
{
    Traveling,
    Arrived,
    Failed,
}

public sealed record WorldMission(
    EntityId Id,
    WorldMissionKind Kind,
    string FactionId,
    EntityId OriginSettlementId,
    EntityId? TargetSettlementId,
    int DepartTick,
    int ArrivalTick,
    WorldMissionStatus Status,
    EntityId? CrewCitizenId = null)
{
    /// <summary>Second party for relationship missions (diplomacy); empty otherwise.</summary>
    public string TargetFactionId { get; init; } = string.Empty;

    /// <summary>Effect magnitude applied on arrival (diplomacy goodwill delta, scout intel value).</summary>
    public int Amount { get; init; }

    public WorldTransitPhase Phase { get; init; } = WorldTransitPhase.Outbound;

    public int StatusTick { get; init; } = DepartTick;

    public int ReturnArrivalTick { get; init; } = ArrivalTick;

    public bool CompleteAsFailure { get; init; }

    public bool EffectApplied { get; init; }

    /// <summary>
    /// The scout physically observed the target, but the report is not faction knowledge until
    /// the crew returns. Keeping the observation on the mission makes the result save-stable.
    /// </summary>
    public bool ReportCollected { get; init; }

    public RaidIntelValueBand ReportedValueBand { get; init; } = RaidIntelValueBand.Low;

    public int ReportedCombatantDemand { get; init; }

    public string ReportedTargetKey { get; init; } = string.Empty;

    public string TargetContactKey { get; init; } = string.Empty;

    public bool TargetsPlayerContact =>
        !TargetSettlementId.HasValue
        && !string.IsNullOrWhiteSpace(TargetContactKey);
}
