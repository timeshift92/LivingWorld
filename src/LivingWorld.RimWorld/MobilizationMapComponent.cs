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
/// genuinely in danger. Threat is classified per-recheck by <see cref="ThreatClassifier"/> from the live
/// hostiles (see ComputeSignals), debounced by <see cref="ThreatDebounce"/>; dormant threats are ignored.
/// Mobilizes on a manual toggle or a Raid-or-worse tier. The recheck is throttled and fail-safe — any error
/// reads as "no threat", so the colony is never locked in alert. Auto-created for every map (a MapComponent
/// with a (Map) constructor is instantiated by RimWorld) and persisted per map.
/// </summary>
public sealed class MobilizationMapComponent : MapComponent
{
    private const int RecheckInterval = 250;

    private bool manualMobilized;
    private bool threatPresent;
    private ThreatTier currentTier;
    private int belowCount;
    private ThreatSignals lastSignals;
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
            lastSignals = settings.autoMobilizeOnThreat ? ComputeSignals(map, settings) : default;
            var rawTier = ThreatClassifier.Classify(lastSignals);
            (currentTier, belowCount) = ThreatDebounce.Step(currentTier, rawTier, belowCount, settings.mobilizationDeescalateRechecks);
            // Phase A has no fighter/non-combatant split yet, so mobilizing means EVERY combat-capable
            // colonist gears up — too heavy for a lone nuisance animal. Only Raid+ mobilizes; proportionate
            // Nuisance handling lands with the roster in Phase B. (A big animal pack classifies as Serious.)
            threatPresent = currentTier >= ThreatTier.Raid;
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

        var hostiles = pawns.Where(p => p != null && !p.Downed && !p.IsPrisoner && p.HostileTo(player) && IsAwakeThreat(p)).ToList();
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

    // A dormant threat (an un-woken mech cluster / Anomaly entity / hive) is hostile-by-faction but not an
    // active danger — vanilla's DangerWatcher ignored it, so we must too, or the colony stays mobilized
    // forever staring at a sleeping cluster.
    private static bool IsAwakeThreat(Pawn p)
    {
        try
        {
            var dormant = p.GetComp<CompCanBeDormant>();
            return dormant == null || dormant.Awake;
        }
        catch
        {
            return true;
        }
    }

    public string DescribeThreat()
    {
        return $"tier={currentTier}, belowCount={belowCount}, "
               + $"signals=[hostiles={lastSignals.HostileCount}, onlyAnimals={lastSignals.OnlyAnimals}, "
               + $"mech={lastSignals.AnyMechanoid}, entity={lastSignals.AnyEntity}, insect={lastSignals.AnyInsect}, "
               + $"atBase={lastSignals.EnemyAtBase}, big={lastSignals.BigRaid}]";
    }

    // Dev diagnostics: the driver's derived phase + transient tracking for one pawn (see OutfitStandDebugActions).
    public string DiagnosePawn(Pawn pawn)
    {
        return $"phase={driver.PeekPhase(pawn, IsMobilized, currentTier)}, tier={currentTier}, "
               + $"engagedByUs={driver.IsEngagedByUs(pawn)}, draftedByUs={driver.IsDraftedByUs(pawn)}, "
               + $"aiAutoControl={CaiBridge.IsAutoControlled(pawn)}";
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
