using System.Linq;
using LivingWorld.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Stable proxy MapParent for a future Living World NPC settlement visit.
/// The vanilla settlement remains the world-map identity; this object owns the temporary map lifecycle.
/// </summary>
public sealed class WorldObject_LivingWorldSettlementVisitSite : MapParent
{
    private long settlementIdValue;
    private int sourceSettlementWorldObjectId = -1;
    private int sourceTile = -1;
    private string visitKind = "observe";
    private string settlementLabel = string.Empty;
    private int materializedVersion;
    private bool reconciled;
    private bool activeMapSession;
    private bool playerCaravanEntered;
    private bool sourceRemoved;

    public EntityId? SettlementId => settlementIdValue > 0
        ? EntityId.Create(EntityKind.Settlement, settlementIdValue)
        : null;

    public int SourceSettlementWorldObjectId => sourceSettlementWorldObjectId;

    public int SourceTile => sourceTile;

    public string VisitKind => visitKind;

    public int MaterializedVersion => materializedVersion;

    public bool Reconciled => reconciled;

    public bool ActiveMapSession => activeMapSession;

    public bool SourceRemoved => sourceRemoved;

    protected override bool UseGenericEnterMapFloatMenuOption => false;

    public override string Label => string.IsNullOrWhiteSpace(settlementLabel)
        ? "LW_SettlementVisitSiteLabel".Translate().ToString()
        : settlementLabel;

    public void Configure(
        EntityId settlementId,
        int sourceSettlementWorldObjectId,
        int sourceTile,
        string visitKind,
        string settlementLabel,
        int materializedVersion = 0,
        bool reconciled = false)
    {
        settlementIdValue = settlementId.Kind == EntityKind.Settlement ? settlementId.Value : 0;
        this.sourceSettlementWorldObjectId = sourceSettlementWorldObjectId;
        this.sourceTile = sourceTile;
        this.visitKind = string.IsNullOrWhiteSpace(visitKind) ? "observe" : visitKind;
        this.settlementLabel = settlementLabel ?? string.Empty;
        this.materializedVersion = materializedVersion;
        this.reconciled = reconciled;
        activeMapSession = false;
        playerCaravanEntered = false;
        sourceRemoved = false;
        Tile = sourceTile;
    }

    public void BeginMapSession()
    {
        activeMapSession = true;
        playerCaravanEntered = false;
        reconciled = false;
    }

    public void RefreshSource(
        EntityId settlementId,
        int sourceSettlementWorldObjectId,
        int sourceTile,
        string visitKind,
        string settlementLabel)
    {
        settlementIdValue = settlementId.Kind == EntityKind.Settlement ? settlementId.Value : 0;
        this.sourceSettlementWorldObjectId = sourceSettlementWorldObjectId;
        this.sourceTile = sourceTile;
        this.visitKind = string.IsNullOrWhiteSpace(visitKind) ? "observe" : visitKind;
        this.settlementLabel = settlementLabel ?? string.Empty;
        sourceRemoved = false;
        if (!HasMap)
        {
            Tile = sourceTile;
        }
    }

    public void MarkPlayerCaravanEntered()
    {
        activeMapSession = true;
        playerCaravanEntered = true;
    }

    public void AbortMapSession()
    {
        activeMapSession = false;
        playerCaravanEntered = false;
    }

    public void MarkMaterialized(int version)
    {
        materializedVersion = version < 0 ? 0 : version;
    }

    public void MarkReconciled()
    {
        reconciled = true;
    }

    public void MarkSourceRemoved()
    {
        sourceRemoved = true;
    }

    public override void Notify_CaravanFormed(Caravan caravan)
    {
        base.Notify_CaravanFormed(caravan);
        // Keep the session eligible for removal. Resetting this flag here prevented
        // ShouldRemoveMapNow from ever reconciling and removing a completed visit map.
        playerCaravanEntered = true;
    }

    public override void Notify_MyMapRemoved(Map map)
    {
        base.Notify_MyMapRemoved(map);
        activeMapSession = false;
        playerCaravanEntered = false;
        reconciled = true;
    }

    protected override void Tick()
    {
        base.Tick();
        var tick = Find.TickManager?.TicksGame ?? 0;
        if ((tick + ID) % 2_500 == 0
            && !LivingWorldSettlementVisitSiteService.TryResolveSource(this, out _))
        {
            LivingWorldSettlementVisitSiteService.CloseOrphanedSite(this);
        }
    }

    public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
    {
        alsoRemoveWorldObject = false;
        if (!activeMapSession || !playerCaravanEntered || Map == null)
        {
            return false;
        }

        if (Map.mapPawns.PawnsInFaction(Faction.OfPlayer)
            .Any(pawn => pawn != null && pawn.Spawned && !pawn.Dead))
        {
            return false;
        }

        var canRemove = !TransporterUtility.IncomingTransporterPreventingMapRemoval(Map);
        alsoRemoveWorldObject = canRemove;
        return canRemove;
    }

    public override string GetInspectString()
    {
        return "LW_SettlementVisitSiteInspect".Translate(
            visitKind.Named("visitKind"),
            materializedVersion.Named("version"),
            reconciled.Named("reconciled"));
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref settlementIdValue, "livingWorld_settlementIdValue", 0L);
        Scribe_Values.Look(ref sourceSettlementWorldObjectId, "livingWorld_sourceSettlementWorldObjectId", -1);
        Scribe_Values.Look(ref sourceTile, "livingWorld_sourceTile", -1);
        Scribe_Values.Look(ref visitKind, "livingWorld_visitKind", "observe");
        Scribe_Values.Look(ref settlementLabel, "livingWorld_settlementLabel", string.Empty);
        Scribe_Values.Look(ref materializedVersion, "livingWorld_materializedVersion", 0);
        Scribe_Values.Look(ref reconciled, "livingWorld_reconciled", false);
        Scribe_Values.Look(ref activeMapSession, "livingWorld_activeMapSession", false);
        Scribe_Values.Look(ref playerCaravanEntered, "livingWorld_playerCaravanEntered", false);
        Scribe_Values.Look(ref sourceRemoved, "livingWorld_sourceRemoved", false);
    }
}
