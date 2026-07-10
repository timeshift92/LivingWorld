using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Fallback world-map marker for a mechanoid complex (see <see cref="MechClusterNode"/>). The normal
/// path materializes complexes as vanilla sites; this marker remains only when site creation is not
/// available yet, so the player still gets inspect/gizmo context instead of a silent dot.
/// </summary>
public sealed class WorldObject_MechCluster : WorldObject
{
    private int nodeId;
    private bool awake;

    private Material? cachedMaterial;

    public int NodeId => nodeId;

    public void Configure(int nodeId, bool awake)
    {
        this.nodeId = nodeId;
        this.awake = awake;
        cachedMaterial = null;
    }

    public override Material Material
    {
        get
        {
            if (cachedMaterial == null)
            {
                // Awake complexes glow an angry red; dormant ones a muted grey.
                var color = awake ? new Color(0.85f, 0.2f, 0.15f) : new Color(0.45f, 0.45f, 0.5f);
                cachedMaterial = MaterialPool.MatFrom(
                    texPath: "World/LivingWorld_Warband",
                    shader: ShaderDatabase.WorldOverlayTransparentLit,
                    color: color,
                    renderQueue: WorldMaterials.DynamicObjectRenderQueue);
            }

            return cachedMaterial;
        }
    }

    public override string Label => "LW_MechClusterLabel".Translate();

    public override string GetInspectString()
    {
        return awake
            ? "LW_MechClusterActive".Translate()
            : "LW_MechClusterDormant".Translate();
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (var gizmo in base.GetGizmos())
        {
            yield return gizmo;
        }

        var details = "LW_MechClusterFallbackDetails".Translate(GetInspectString()).ToString();
        yield return new Command_Action
        {
            defaultLabel = "LW_MechClusterDetails".Translate(),
            defaultDesc = details,
            icon = TexButton.Info,
            action = () => Find.WindowStack?.Add(new Dialog_MessageBox(details)),
        };
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref nodeId, "lwMechNodeId", 0);
        Scribe_Values.Look(ref awake, "lwMechAwake", false);
    }
}
