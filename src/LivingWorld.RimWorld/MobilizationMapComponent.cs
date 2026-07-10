using System;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Per-map mobilization state for the player colony: whether combat colonists should gear up and fight rather
/// than work in civvies. Mobilized when the player toggles it manually, or automatically while the map is
/// genuinely in danger. Danger is read from RimWorld's own <see cref="DangerWatcher"/> so a single wandering
/// manhunter does not put the whole colony on alert. The recheck is throttled and fail-safe — any error reads
/// as "no threat", so the colony is never locked in alert. Auto-created for every map (a MapComponent with a
/// (Map) constructor is instantiated by RimWorld) and persisted per map.
/// </summary>
public sealed class MobilizationMapComponent : MapComponent
{
    private const int RecheckInterval = 250;

    private bool manualMobilized;
    private bool threatPresent;
    private bool wasMobilized;
    private readonly MobilizationDriver driver = new();

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
        if (tick % RecheckInterval != 0 || map == null)
        {
            return;
        }

        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();

        try
        {
            threatPresent = settings.autoMobilizeOnThreat
                && map.dangerWatcher != null
                && map.dangerWatcher.DangerRating >= StoryDanger.Low;
        }
        catch (Exception ex)
        {
            threatPresent = false;
            Log.Warning($"[LivingWorld] Mobilization threat check failed safely: {ex.Message}");
        }

        var mobilized = IsMobilized;

        // When the alert ends, drop per-alert tracking so the next alert engages/drafts afresh.
        if (!mobilized && wasMobilized)
        {
            driver.ResetTransient();
        }

        wasMobilized = mobilized;
        driver.Drive(map, mobilized);
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
