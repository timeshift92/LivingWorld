using System;
using System.Linq;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Per-map mobilization state for the player colony's armory system: whether colonists should be armed and
/// armoured rather than working in civvies. Mobilized when the player manually toggles it, or automatically
/// while a live hostile is present on the map. The threat check is throttled (not every tick) and fail-safe
/// — any error reads as "no threat", so the colony is never locked in alert. Persisted per map. Auto-created
/// by RimWorld for every map (any MapComponent with a (Map) constructor is instantiated).
///
/// Part of the self-contained armory / mobilization module (own files, own namespace helpers) so it does not
/// collide with the world-simulation code.
/// </summary>
public sealed class MobilizationMapComponent : MapComponent
{
    private const int ThreatRecheckInterval = 250;

    private bool manualMobilized;
    private bool threatPresent;

    public MobilizationMapComponent(Map map)
        : base(map)
    {
    }

    public bool ManualMobilized => manualMobilized;

    public bool ThreatPresent => threatPresent;

    public bool IsMobilized => manualMobilized || threatPresent;

    public void ToggleManual()
    {
        manualMobilized = !manualMobilized;
    }

    public override void MapComponentTick()
    {
        base.MapComponentTick();

        var tick = Find.TickManager?.TicksGame ?? 0;
        if (tick % ThreatRecheckInterval != 0)
        {
            return;
        }

        try
        {
            var player = Faction.OfPlayer;
            var pawns = map?.mapPawns?.AllPawnsSpawned;
            threatPresent = player != null
                && pawns != null
                && pawns.Any(pawn =>
                    pawn != null
                    && !pawn.Downed
                    && !pawn.IsPrisoner
                    && pawn.HostileTo(player));
        }
        catch (Exception ex)
        {
            threatPresent = false;
            Log.Warning($"[LivingWorld] Mobilization threat check failed safely: {ex.Message}");
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref manualMobilized, "livingWorld_manualMobilized", false);
    }

    public static MobilizationMapComponent? For(Map map)
    {
        return map?.GetComponent<MobilizationMapComponent>();
    }
}
