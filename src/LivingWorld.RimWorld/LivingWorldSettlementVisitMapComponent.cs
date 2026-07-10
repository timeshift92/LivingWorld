using LivingWorld.Core;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Per-map state for a Living World settlement visit map.
/// Later materialization and reconciliation code should read this component instead of inferring state from a vanilla Settlement.
/// </summary>
public sealed class LivingWorldSettlementVisitMapComponent : MapComponent
{
    private long settlementIdValue;
    private int visitSiteWorldObjectId = -1;
    private int materializedVersion;
    private bool reconciled;

    public LivingWorldSettlementVisitMapComponent(Map map)
        : base(map)
    {
    }

    public EntityId? SettlementId => settlementIdValue > 0
        ? EntityId.Create(EntityKind.Settlement, settlementIdValue)
        : null;

    public int VisitSiteWorldObjectId => visitSiteWorldObjectId;

    public int MaterializedVersion => materializedVersion;

    public bool Reconciled => reconciled;

    public void ConfigureFrom(WorldObject_LivingWorldSettlementVisitSite visitSite)
    {
        var settlementId = visitSite?.SettlementId;
        settlementIdValue = settlementId?.Value ?? 0L;
        visitSiteWorldObjectId = visitSite?.ID ?? -1;
        materializedVersion = visitSite?.MaterializedVersion ?? 0;
        reconciled = visitSite?.Reconciled ?? false;
    }

    public void MarkMaterialized(int version)
    {
        materializedVersion = version < 0 ? 0 : version;
    }

    public void MarkReconciled()
    {
        reconciled = true;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref settlementIdValue, "livingWorld_settlementIdValue", 0L);
        Scribe_Values.Look(ref visitSiteWorldObjectId, "livingWorld_visitSiteWorldObjectId", -1);
        Scribe_Values.Look(ref materializedVersion, "livingWorld_materializedVersion", 0);
        Scribe_Values.Look(ref reconciled, "livingWorld_reconciled", false);
    }

    public static LivingWorldSettlementVisitMapComponent? For(Map map)
    {
        return map?.GetComponent<LivingWorldSettlementVisitMapComponent>();
    }
}
