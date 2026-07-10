using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

public sealed class CaravanArrivalAction_LivingWorldSettlementVisitSite : CaravanArrivalAction
{
    private Settlement? sourceSettlement;

    public CaravanArrivalAction_LivingWorldSettlementVisitSite()
    {
    }

    public CaravanArrivalAction_LivingWorldSettlementVisitSite(Settlement sourceSettlement)
    {
        this.sourceSettlement = sourceSettlement;
    }

    public override string Label => sourceSettlement == null
        ? "LW_SettlementVisitSiteFloatMenu".Translate("Living World settlement")
        : "LW_SettlementVisitSiteFloatMenu".Translate(sourceSettlement.Label);

    public override string ReportString => Label;

    public override FloatMenuAcceptanceReport StillValid(Caravan caravan, PlanetTile destinationTile)
    {
        var baseReport = base.StillValid(caravan, destinationTile);
        if (!baseReport)
        {
            return baseReport;
        }

        return sourceSettlement != null
            && sourceSettlement.Tile == destinationTile
            && sourceSettlement.Spawned
            && LivingWorldSettlementDirectVisitPatch.TryResolveLedgerSettlement(sourceSettlement, out _);
    }

    public override void Arrived(Caravan caravan)
    {
        if (sourceSettlement == null
            || !LivingWorldSettlementDirectVisitPatch.TryResolveLedgerSettlement(sourceSettlement, out var settlementId))
        {
            Messages.Message("MessageCaravanArrivalActionNoLongerValid".Translate(Label), caravan, MessageTypeDefOf.RejectInput, false);
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
            Messages.Message("LW_SettlementVisitSiteUnavailable".Translate(failureReason), caravan, MessageTypeDefOf.RejectInput, false);
            return;
        }

        LivingWorldSettlementVisitMapEntryService.OpenOrEnter(visitSite, caravan);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref sourceSettlement, "sourceSettlement");
    }
}
