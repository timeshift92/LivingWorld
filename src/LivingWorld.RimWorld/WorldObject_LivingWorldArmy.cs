using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// A display-only world-map marker for a Living World faction mission in transit — a warband,
/// caravan, or (later) scout/settler/diplomat. The simulation lives in the ledger (Core has no tile
/// geometry); this object never pathfinds or drives anything. It interpolates along the sphere from
/// the origin settlement tile to the target tile as the ledger's travel clock advances, tinted by
/// faction colour, and shows a per-kind icon. The world component reconciles these markers from the
/// ledger each simulated day, so they are safe to recreate at any time.
/// </summary>
public sealed class WorldObject_LivingWorldArmy : WorldObject
{
    private const float MarkerDrawSize = 0.46f;

    private string markerKey = string.Empty;
    private string textureName = "World/LivingWorld_Warband";
    private string kindNoun = string.Empty;
    private int originTile = -1;
    private int targetTile = -1;
    private int departTick;
    private int arrivalTick;
    private string factionLabel = string.Empty;
    private string targetLabel = string.Empty;
    private int combatants;
    private int strength;
    private string resourceSummary = string.Empty;
    private string reason = string.Empty;

    private Material? cachedMaterial;

    public string MarkerKey => markerKey;

    internal int OriginTile => originTile;

    internal int TargetTile => targetTile;

    internal int Combatants => combatants;

    internal int Strength => strength;

    internal string KindNoun => kindNoun;

    public void Configure(
        string markerKey,
        string textureName,
        string kindNoun,
        int originTile,
        int targetTile,
        int departTick,
        int arrivalTick,
        string factionLabel,
        string targetLabel,
        int combatants,
        int strength,
        string resourceSummary,
        string reason)
    {
        this.markerKey = markerKey ?? string.Empty;
        this.textureName = string.IsNullOrEmpty(textureName) ? "World/LivingWorld_Warband" : textureName;
        this.kindNoun = kindNoun ?? string.Empty;
        this.originTile = originTile;
        this.targetTile = targetTile;
        this.departTick = departTick;
        this.arrivalTick = arrivalTick;
        this.factionLabel = factionLabel ?? string.Empty;
        this.targetLabel = targetLabel ?? string.Empty;
        this.combatants = combatants;
        this.strength = strength;
        this.resourceSummary = resourceSummary ?? string.Empty;
        this.reason = reason ?? string.Empty;
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
                    texPath: textureName,
                    shader: ShaderDatabase.WorldOverlayTransparentLit,
                    color: color,
                    renderQueue: WorldMaterials.DynamicObjectRenderQueue);
                ConfigureIconTexture(cachedMaterial);
            }

            return cachedMaterial;
        }
    }

    public override string Label => string.IsNullOrEmpty(factionLabel) ? base.Label : factionLabel;

    public override void Draw()
    {
        var material = Material;
        if (Tile.LayerDef.isSpace || !material)
        {
            return;
        }

        var averageTileSize = Tile.Layer.AverageTileSize;
        var altitudeOffset = Rand.RangeSeeded(0f, 0.01f, ID) + def.drawAltitudeOffset;
        WorldRendererUtility.DrawQuadTangentialToPlanet(
            DrawPos,
            MarkerDrawSize * averageTileSize,
            DrawAltitude + altitudeOffset,
            material);
    }

    public override string GetInspectString()
    {
        return DetailsText;
    }

    public string DetailsText
    {
        get
        {
            var remainingTicks = arrivalTick - (Find.TickManager?.TicksGame ?? arrivalTick);
            var days = Mathf.Max(0, Mathf.RoundToInt(remainingTicks / 60000f));
            var builder = new StringBuilder();
            builder.Append("LW_MissionMarkerInspect".Translate(
                factionLabel.Named("faction"),
                kindNoun.Named("kind"),
                targetLabel.Named("target"),
                days.Named("days")));

            if (combatants > 0 || strength > 0)
            {
                builder.AppendLine();
                builder.Append("LW_MissionMarkerStrengthLine".Translate(
                    combatants.Named("combatants"),
                    strength.Named("strength")));
            }

            if (!string.IsNullOrWhiteSpace(resourceSummary))
            {
                builder.AppendLine();
                builder.Append("LW_MissionMarkerResourceLine".Translate(resourceSummary.Named("resources")));
            }

            if (!string.IsNullOrWhiteSpace(reason))
            {
                builder.AppendLine();
                builder.Append("LW_MissionMarkerReasonLine".Translate(reason.Named("reason")));
            }

            return builder.ToString();
        }
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (var gizmo in base.GetGizmos())
        {
            yield return gizmo;
        }

        yield return new Command_Action
        {
            defaultLabel = "LW_MissionMarkerDetails".Translate(),
            defaultDesc = DetailsText,
            icon = TexButton.Info,
            action = () => Find.WindowStack?.Add(new Dialog_MessageBox(DetailsText)),
        };
    }

    private static void ConfigureIconTexture(Material material)
    {
        if (material?.mainTexture is Texture2D texture)
        {
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 2;
            texture.mipMapBias = -0.15f;
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref markerKey, "lwMarkerKey", string.Empty);
        Scribe_Values.Look(ref textureName, "lwTextureName", "World/LivingWorld_Warband");
        Scribe_Values.Look(ref kindNoun, "lwKindNoun", string.Empty);
        Scribe_Values.Look(ref originTile, "lwOriginTile", -1);
        Scribe_Values.Look(ref targetTile, "lwTargetTile", -1);
        Scribe_Values.Look(ref departTick, "lwDepartTick", 0);
        Scribe_Values.Look(ref arrivalTick, "lwArrivalTick", 0);
        Scribe_Values.Look(ref factionLabel, "lwFactionLabel", string.Empty);
        Scribe_Values.Look(ref targetLabel, "lwTargetLabel", string.Empty);
        Scribe_Values.Look(ref combatants, "lwCombatants", 0);
        Scribe_Values.Look(ref strength, "lwStrength", 0);
        Scribe_Values.Look(ref resourceSummary, "lwResourceSummary", string.Empty);
        Scribe_Values.Look(ref reason, "lwReason", string.Empty);
    }
}
