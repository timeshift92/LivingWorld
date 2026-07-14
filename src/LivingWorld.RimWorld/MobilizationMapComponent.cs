using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
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
    private ThreatTier currentTier;
    private int belowCount;
    private List<int> savedDraftedIds = new();
    private readonly MobilizationDriver driver = new();

    public MobilizationMapComponent(Map map)
        : base(map)
    {
    }

    public bool ManualMobilized => manualMobilized;

    public bool ThreatPresent => threatPresent;

    public bool IsMobilized => manualMobilized || threatPresent;

    public ThreatTier CurrentTier => currentTier;

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
            var rawTier = settings.autoMobilizeOnThreat ? ThreatClassifier.Classify(ComputeSignals(map, settings)) : ThreatTier.None;
            (currentTier, belowCount) = ThreatDebounce.Step(currentTier, rawTier, belowCount, settings.mobilizationDeescalateRechecks);
            threatPresent = currentTier != ThreatTier.None;
        }
        catch (Exception ex)
        {
            currentTier = ThreatTier.None;
            threatPresent = false;
            Log.Warning($"[LivingWorld] Mobilization threat check failed safely: {ex.Message}");
        }

        driver.Drive(map, IsMobilized, currentTier);
    }

    private ThreatSignals ComputeSignals(Map liveMap, LivingWorldSettings settings)
    {
        var player = Faction.OfPlayer;
        var pawns = liveMap.mapPawns?.AllPawnsSpawned;
        if (player == null || pawns == null)
        {
            return default;
        }

        var hostiles = pawns.Where(p => p != null && !p.Downed && !p.IsPrisoner && p.HostileTo(player)).ToList();
        if (hostiles.Count == 0)
        {
            return default;
        }

        var center = liveMap.Center;
        var atBaseRadius = settings.mobilizationAtBaseRadius;

        return new ThreatSignals
        {
            AnyHostile = true,
            OnlyAnimals = hostiles.All(p => p.RaceProps?.Animal == true),
            AnyMechanoid = hostiles.Any(p => p.RaceProps?.IsMechanoid == true),
            AnyEntity = hostiles.Any(p => p.IsEntity || p.IsMutant),
            AnyInsect = hostiles.Any(p => p.RaceProps?.Insect == true),
            AnySapper = false,
            EnemyAtBase = hostiles.Any(p => (p.Position - center).LengthHorizontal <= atBaseRadius),
            HostileCount = hostiles.Count,
            BigRaid = hostiles.Count >= settings.mobilizationBigRaidThreshold,
        };
    }

    // Dev diagnostics: the driver's derived phase + transient tracking for one pawn (see OutfitStandDebugActions).
    public string DiagnosePawn(Pawn pawn)
    {
        return $"phase={driver.PeekPhase(pawn, IsMobilized, currentTier)}, tier={currentTier}, "
               + $"engagedByUs={driver.IsEngagedByUs(pawn)}, draftedByUs={driver.IsDraftedByUs(pawn)}";
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref manualMobilized, "livingWorld_manualMobilized", false);

        if (Scribe.mode == LoadSaveMode.Saving)
        {
            savedDraftedIds = driver.ExportDraftedIds();
        }

        Scribe_Collections.Look(ref savedDraftedIds, "livingWorld_draftedByUs", LookMode.Value);
        savedDraftedIds ??= new List<int>();
    }

    public override void FinalizeInit()
    {
        base.FinalizeInit();
        driver.ImportDraftedIds(savedDraftedIds, map);
    }

    public static MobilizationMapComponent? For(Map map)
    {
        return map?.GetComponent<MobilizationMapComponent>();
    }
}
