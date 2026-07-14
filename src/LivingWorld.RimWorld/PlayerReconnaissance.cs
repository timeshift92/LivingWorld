using LivingWorld.Core;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>A concrete faction citizen travelling to observe the player's colony.</summary>
public sealed class PendingPlayerReconnaissance : IExposable
{
    public string FactionDefName = string.Empty;
    public long SourceSettlementId;
    public long LeaseId;
    public int OriginTile = -1;
    public int TargetTile = -1;
    public int DepartTick;
    public int ArrivalTick;
    public string MarkerKey = string.Empty;
    public string TargetLabel = string.Empty;
    public bool Returning;
    public bool HasReport;
    public RaidIntelValueBand ReportedValueBand;
    public int ReportedCombatantDemand;

    public void ExposeData()
    {
        Scribe_Values.Look(ref FactionDefName, "factionDefName", string.Empty);
        Scribe_Values.Look(ref SourceSettlementId, "sourceSettlementId", 0L);
        Scribe_Values.Look(ref LeaseId, "leaseId", 0L);
        Scribe_Values.Look(ref OriginTile, "originTile", -1);
        Scribe_Values.Look(ref TargetTile, "targetTile", -1);
        Scribe_Values.Look(ref DepartTick, "departTick", 0);
        Scribe_Values.Look(ref ArrivalTick, "arrivalTick", 0);
        Scribe_Values.Look(ref MarkerKey, "markerKey", string.Empty);
        Scribe_Values.Look(ref TargetLabel, "targetLabel", string.Empty);
        Scribe_Values.Look(ref Returning, "returning", false);
        Scribe_Values.Look(ref HasReport, "hasReport", false);
        Scribe_Values.Look(ref ReportedValueBand, "reportedValueBand", RaidIntelValueBand.Low);
        Scribe_Values.Look(ref ReportedCombatantDemand, "reportedCombatantDemand", 0);
    }
}
