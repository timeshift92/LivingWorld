using System.Linq;
using LivingWorld.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

public sealed class CaravanArrivalAction_LivingWorldSettlementVisitSite : CaravanArrivalAction
{
    private int sourceSettlementWorldObjectId = -1;
    private int sourceTileValue = -1;
    private long ledgerSettlementIdValue;
    private string sourceLabel = string.Empty;

    public CaravanArrivalAction_LivingWorldSettlementVisitSite()
    {
    }

    public CaravanArrivalAction_LivingWorldSettlementVisitSite(Settlement sourceSettlement)
    {
        if (sourceSettlement == null)
        {
            return;
        }

        sourceSettlementWorldObjectId = sourceSettlement.ID;
        sourceTileValue = sourceSettlement.Tile;
        sourceLabel = sourceSettlement.Label ?? string.Empty;
        if (LivingWorldSettlementDirectVisitPatch.TryResolveLedgerSettlement(sourceSettlement, out var settlementId))
        {
            ledgerSettlementIdValue = settlementId.Value;
        }
    }

    public override string Label => string.IsNullOrWhiteSpace(sourceLabel)
        ? "LW_SettlementVisitSiteFloatMenu".Translate("Living World settlement")
        : "LW_SettlementVisitSiteFloatMenu".Translate(sourceLabel);

    public override string ReportString => Label;

    public override FloatMenuAcceptanceReport StillValid(Caravan caravan, PlanetTile destinationTile)
    {
        var baseReport = base.StillValid(caravan, destinationTile);
        if (!baseReport)
        {
            return baseReport;
        }

        return TryResolveLiveSource(out var sourceSettlement, out _)
            && sourceSettlement.Tile == destinationTile;
    }

    public override void Arrived(Caravan caravan)
    {
        if (!TryResolveLiveSource(out var sourceSettlement, out var settlementId))
        {
            Messages.Message("MessageCaravanArrivalActionNoLongerValid".Translate(Label), MessageTypeDefOf.RejectInput, false);
            return;
        }

        if (!LivingWorldSettlementVisitSiteService.TryCreateOrReuse(
                sourceSettlement,
                settlementId,
                "visit",
                out var visitSite,
                out var failureReason)
            || visitSite == null)
        {
            Messages.Message("LW_SettlementVisitSiteUnavailable".Translate(failureReason), MessageTypeDefOf.RejectInput, false);
            return;
        }

        LivingWorldSettlementVisitMapEntryService.OpenOrEnter(visitSite, caravan);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref sourceSettlementWorldObjectId, "sourceSettlementWorldObjectId", -1);
        Scribe_Values.Look(ref sourceTileValue, "sourceTile", -1);
        Scribe_Values.Look(ref ledgerSettlementIdValue, "ledgerSettlementId", 0L);
        Scribe_Values.Look(ref sourceLabel, "sourceLabel", string.Empty);
    }

    private bool TryResolveLiveSource(out Settlement sourceSettlement, out EntityId settlementId)
    {
        sourceSettlement = null!;
        settlementId = default;
        var worldObjects = Find.WorldObjects?.AllWorldObjects;
        if (worldObjects == null)
        {
            return false;
        }

        sourceSettlement = worldObjects
            .OfType<Settlement>()
            .FirstOrDefault(candidate =>
                candidate != null
                && !candidate.Destroyed
                && candidate.Spawned
                && (candidate.ID == sourceSettlementWorldObjectId
                    || (candidate.Tile == (PlanetTile)sourceTileValue
                        && string.Equals(candidate.Label, sourceLabel, System.StringComparison.Ordinal))))!;
        if (sourceSettlement == null)
        {
            return false;
        }

        if (!LivingWorldSettlementDirectVisitPatch.TryResolveLedgerSettlement(sourceSettlement, out var resolved))
        {
            return false;
        }

        if (ledgerSettlementIdValue > 0 && resolved.Value != ledgerSettlementIdValue)
        {
            return false;
        }

        settlementId = resolved;
        return true;
    }
}
