using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// A display-only world-map marker for a Living World army in transit. The simulation lives in the
/// ledger (Core has no tile geometry); this object never pathfinds or drives anything — it only
/// visualizes an existing <c>WorldArmyMovement</c> by interpolating between the origin and target
/// settlement tiles as the ledger's travel clock advances. The world component reconciles these
/// markers from the ledger each simulated day, so they are safe to recreate at any time.
/// </summary>
public sealed class WorldObject_LivingWorldArmy : WorldObject
{
    private long armyId = -1L;
    private int originTile = -1;
    private int targetTile = -1;
    private int departTick;
    private int arrivalTick;
    private string factionLabel = string.Empty;
    private string targetLabel = string.Empty;

    private Material? cachedMaterial;

    public long ArmyId => armyId;

    public void Configure(
        long armyId,
        int originTile,
        int targetTile,
        int departTick,
        int arrivalTick,
        string factionLabel,
        string targetLabel)
    {
        this.armyId = armyId;
        this.originTile = originTile;
        this.targetTile = targetTile;
        this.departTick = departTick;
        this.arrivalTick = arrivalTick;
        this.factionLabel = factionLabel ?? string.Empty;
        this.targetLabel = targetLabel ?? string.Empty;
        cachedMaterial = null;
    }

    private float ProgressPct
    {
        get
        {
            var span = arrivalTick - departTick;
            if (span <= 0)
            {
                return 1f;
            }

            var elapsed = (Find.TickManager?.TicksGame ?? arrivalTick) - departTick;
            return Mathf.Clamp01((float)elapsed / span);
        }
    }

    // Position is derived from the ledger clock, not a path: slerp along the sphere from the origin
    // settlement tile to the target settlement tile as travel progresses. useDynamicDrawer redraws
    // this every frame, so the marker glides smoothly even though the ledger only ticks daily.
    public override Vector3 DrawPos
    {
        get
        {
            var grid = Find.WorldGrid;
            if (grid == null || originTile < 0 || targetTile < 0)
            {
                return base.DrawPos;
            }

            var from = grid.GetTileCenter(originTile);
            var to = grid.GetTileCenter(targetTile);
            return Vector3.Slerp(from, to, ProgressPct);
        }
    }

    public override Material Material
    {
        get
        {
            if (cachedMaterial == null)
            {
                var color = Faction != null ? Faction.Color : Color.white;
                cachedMaterial = MaterialPool.MatFrom(
                    texPath: def.texture,
                    shader: ShaderDatabase.WorldOverlayTransparentLit,
                    color: color,
                    renderQueue: WorldMaterials.DynamicObjectRenderQueue);
            }

            return cachedMaterial;
        }
    }

    public override string Label => string.IsNullOrEmpty(factionLabel) ? base.Label : factionLabel;

    public override string GetInspectString()
    {
        var remainingTicks = arrivalTick - (Find.TickManager?.TicksGame ?? arrivalTick);
        var days = Mathf.Max(0, Mathf.RoundToInt(remainingTicks / 60000f));
        return "LW_ArmyMarkerInspect".Translate(
            factionLabel.Named("faction"),
            targetLabel.Named("target"),
            days.Named("days"));
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref armyId, "lwArmyId", -1L);
        Scribe_Values.Look(ref originTile, "lwOriginTile", -1);
        Scribe_Values.Look(ref targetTile, "lwTargetTile", -1);
        Scribe_Values.Look(ref departTick, "lwDepartTick", 0);
        Scribe_Values.Look(ref arrivalTick, "lwArrivalTick", 0);
        Scribe_Values.Look(ref factionLabel, "lwFactionLabel", string.Empty);
        Scribe_Values.Look(ref targetLabel, "lwTargetLabel", string.Empty);
    }
}
