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
    private bool announcedThreat;
    private ThreatTier currentTier;
    private int belowCount;
    private ThreatSignals lastSignals;
    private List<int> savedDraftedIds = new();
    private readonly MobilizationDriver driver = new();
    private readonly ShelterDriver shelterDriver = new();
    private Dictionary<int, int> savedShelterAreas = new();

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
            // Proportionate response (now that the Fighters roster exists): any real threat — including a lone
            // nuisance animal like a revenge-seeking elephant — mobilizes the FIGHTERS to deal with it, while
            // non-combatants only take shelter at Raid or worse (ShelterDriver gates on tier >= Raid). So a
            // manhunter animal gets a fighter response without sending the whole colony to cover.
            threatPresent = currentTier != ThreatTier.None;
        }
        catch (Exception ex)
        {
            currentTier = ThreatTier.None;
            threatPresent = false;
            Log.Warning($"[LivingWorld] Mobilization threat check failed safely: {ex.Message}");
        }

        AnnounceThreatEdge(settings);

        driver.Drive(map, IsMobilized, currentTier);
        shelterDriver.Drive(map, currentTier);
    }

    // Fire a one-shot alert on the rising/falling edge of an AUTOMATIC threat so the player notices the colony
    // arming itself (a manual toggle stays silent — they initiated it). Rising edge = a letter (draws the eye,
    // can pause); falling edge = a lightweight message. Edge-tracked via announcedThreat, which is persisted so
    // a mid-raid reload does not re-announce.
    private void AnnounceThreatEdge(LivingWorldSettings settings)
    {
        if (!settings.armoryMobilizationEnabled || !settings.autoMobilizeOnThreat)
        {
            announcedThreat = threatPresent;
            return;
        }

        try
        {
            if (threatPresent && !announcedThreat)
            {
                Find.LetterStack?.ReceiveLetter(
                    "LW_MobilizedLetterLabel".Translate(),
                    "LW_MobilizedLetterText".Translate(currentTier.ToStringSafe(), lastSignals.HostileCount),
                    LetterDefOf.ThreatSmall);
            }
            else if (!threatPresent && announcedThreat)
            {
                Messages.Message("LW_StoodDownMessage".Translate(), MessageTypeDefOf.NeutralEvent, false);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Mobilization alert failed safely: {ex.Message}");
        }

        announcedThreat = threatPresent;
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

        var dangerBody = settings.mobilizationDangerousAnimalBodySize;
        return new ThreatSignals
        {
            AnyHostile = true,
            OnlyAnimals = hostiles.All(p => p.RaceProps?.Animal == true),
            AnyDangerousAnimal = hostiles.Any(p => p.RaceProps?.Animal == true
                                                   && (p.RaceProps?.baseBodySize ?? 0f) >= dangerBody),
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
               + $"fighter={!MobilizationCandidates.IsNonCombatant(pawn)}, "
               + $"engagedByUs={driver.IsEngagedByUs(pawn)}, draftedByUs={driver.IsDraftedByUs(pawn)}, "
               + $"aiAutoControl={CaiBridge.IsAutoControlled(pawn)}, "
               + $"inShelter={ShelterAreaService.IsInShelter(pawn)}, shelteredByUs={shelterDriver.WeChangedArea(pawn)}, "
               + $"prevArea={shelterDriver.PrevAreaId(pawn)}";
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref manualMobilized, "livingWorld_manualMobilized", false);
        Scribe_Values.Look(ref announcedThreat, "livingWorld_announcedThreat", false);

        if (Scribe.mode == LoadSaveMode.Saving)
        {
            savedDraftedIds = driver.ExportDraftedIds();
        }

        Scribe_Collections.Look(ref savedDraftedIds, "livingWorld_draftedByUs", LookMode.Value);
        savedDraftedIds ??= new List<int>();

        if (Scribe.mode == LoadSaveMode.Saving)
        {
            savedShelterAreas = shelterDriver.ExportPrevAreas();
        }

        Scribe_Collections.Look(ref savedShelterAreas, "livingWorld_shelterPrevAreas", LookMode.Value, LookMode.Value);
        savedShelterAreas ??= new Dictionary<int, int>();
    }

    public override void FinalizeInit()
    {
        base.FinalizeInit();
        driver.ImportDraftedIds(savedDraftedIds, map);
        shelterDriver.ImportPrevAreas(savedShelterAreas);
    }

    public static MobilizationMapComponent? For(Map map)
    {
        return map?.GetComponent<MobilizationMapComponent>();
    }
}
