using LivingWorld.Core;
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

    public EntityId? SettlementId => settlementIdValue > 0
        ? EntityId.Create(EntityKind.Settlement, settlementIdValue)
        : null;

    public int SourceSettlementWorldObjectId => sourceSettlementWorldObjectId;

    public int SourceTile => sourceTile;

    public string VisitKind => visitKind;

    public int MaterializedVersion => materializedVersion;

    public bool Reconciled => reconciled;

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
        Tile = sourceTile;
    }

    public void MarkMaterialized(int version)
    {
        materializedVersion = version < 0 ? 0 : version;
    }

    public void MarkReconciled()
    {
        reconciled = true;
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
    }
}
