using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// A display-only world-map marker for a settlement destroyed in the Living World (a <c>WorldRuin</c>
/// in the ledger). Static — unlike the army marker it never moves or interpolates; it just sits on
/// the former settlement's tile and reports who used to live there plus coarse salvage/danger bands.
/// The world component reconciles these markers from the ledger, so they are safe to recreate at any
/// time and are dropped once the ruin is reclaimed or pruned.
/// </summary>
public sealed class WorldObject_LivingWorldRuin : WorldObject
{
    private string markerKey = string.Empty;
    private string ruinName = string.Empty;
    private string formerFactionLabel = string.Empty;
    private string salvageBand = string.Empty;
    private string dangerBand = string.Empty;

    public string MarkerKey => markerKey;

    public void Configure(
        string markerKey,
        string ruinName,
        string formerFactionLabel,
        string salvageBand,
        string dangerBand)
    {
        this.markerKey = markerKey ?? string.Empty;
        this.ruinName = ruinName ?? string.Empty;
        this.formerFactionLabel = formerFactionLabel ?? string.Empty;
        this.salvageBand = salvageBand ?? string.Empty;
        this.dangerBand = dangerBand ?? string.Empty;
    }

    public override string Label => string.IsNullOrEmpty(ruinName) ? base.Label : ruinName;

    public override string GetInspectString()
    {
        return "LW_RuinMarkerInspect".Translate(
            formerFactionLabel.Named("faction"),
            salvageBand.Named("salvage"),
            dangerBand.Named("danger"));
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref markerKey, "lwRuinMarkerKey", string.Empty);
        Scribe_Values.Look(ref ruinName, "lwRuinName", string.Empty);
        Scribe_Values.Look(ref formerFactionLabel, "lwRuinFormerFaction", string.Empty);
        Scribe_Values.Look(ref salvageBand, "lwRuinSalvage", string.Empty);
        Scribe_Values.Look(ref dangerBand, "lwRuinDanger", string.Empty);
    }
}
