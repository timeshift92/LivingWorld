using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Display-only world-map marker for a mechanoid complex (see <see cref="MechClusterNode"/>). Static —
/// it never moves — and reconciled from the ledger of clusters by the world component. Dormant complexes
/// read dim; an awakened one reads as an active threat.
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

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref nodeId, "lwMechNodeId", 0);
        Scribe_Values.Look(ref awake, "lwMechAwake", false);
    }
}
