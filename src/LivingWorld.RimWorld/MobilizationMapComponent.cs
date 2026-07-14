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

    // Muster state: where the fighters hold (cached, recomputed only on a tier transition so it does not jitter
    // between rechecks), the tier it was computed for, and the one-way per-alert "release the line" latch.
    private IntVec3 cachedAnchor = IntVec3.Invalid;
    private IntVec3 hostileCentroid = IntVec3.Invalid;
    private ThreatTier anchorTier = ThreatTier.None;
    private bool musterReleased;
    private int musterHoldRechecks;

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

        hostileCentroid = IntVec3.Invalid;

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

        var muster = BuildMusterContext(settings);
        driver.Drive(map, IsMobilized, currentTier, muster);
        shelterDriver.Drive(map, currentTier);
    }

    // Resolve the muster anchor (cached, recomputed only when the tier changes) and advance the one-way release
    // latch: hold the line until enough fighters have gathered (MusterGate) or the line is breached, with a
    // timeout safety valve so one unreachable straggler cannot freeze the squad forever. The latch is per-ALERT:
    // it resets whenever no automatic threat is present, so it does not leak across raids when the player leaves
    // the manual "Mobilize colony" toggle on (which keeps IsMobilized true between raids). Fully fail-safe.
    private MusterContext BuildMusterContext(LivingWorldSettings settings)
    {
        var wantsMuster = settings.musterEnabled
                          && (currentTier == ThreatTier.Raid || currentTier == ThreatTier.Serious);

        try
        {
            if (!threatPresent)
            {
                musterReleased = false;
                musterHoldRechecks = 0;
                anchorTier = ThreatTier.None;
            }
            else
            {
                if (wantsMuster && (currentTier != anchorTier || !cachedAnchor.IsValid))
                {
                    cachedAnchor = MusterAnchorService.Resolve(map, hostileCentroid, lastSignals.EnemyAtBase, out _);
                    anchorTier = currentTier;
                }

                if (wantsMuster && !musterReleased)
                {
                    // Breach = the enemy is at/among the base (proximity) or actively sapping the wall — holding a
                    // forward line is moot, engage now. NOTE: enemy TYPE (mech/insect/entity) does NOT force a
                    // breach — those raids hold a line too and release via proximity when they arrive.
                    var breached = lastSignals.EnemyAtBase || lastSignals.AnySapper;

                    var (total, atAnchor) = driver.MusterProgress(map, cachedAnchor, settings.musterHoldRadius);
                    musterHoldRechecks++;

                    var progress = new MusterSignals
                    {
                        FightersTotal = total,
                        FightersAtAnchor = atAnchor,
                        LineBreached = breached,
                    };

                    if (MusterGate.WantsRelease(progress, settings.musterReadyFraction)
                        || musterHoldRechecks >= settings.musterReleaseTimeoutRechecks)
                    {
                        musterReleased = true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Fail open: no held line this recheck, everyone free-engages — never lock the colony.
            Log.Warning($"[LivingWorld] Muster context failed safely: {ex.Message}");
            return new MusterContext
            {
                Anchor = map.Center,
                HoldRadius = settings.musterHoldRadius,
                WantsMuster = false,
                Released = true,
            };
        }

        return new MusterContext
        {
            Anchor = cachedAnchor.IsValid ? cachedAnchor : map.Center,
            HoldRadius = settings.musterHoldRadius,
            WantsMuster = wantsMuster,
            Released = musterReleased,
        };
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

        // Centroid of the live hostiles — the muster anchor faces this direction (perimeter-facing hold).
        long hx = 0, hz = 0;
        foreach (var h in hostiles)
        {
            hx += h.Position.x;
            hz += h.Position.z;
        }

        hostileCentroid = new IntVec3((int)(hx / hostiles.Count), 0, (int)(hz / hostiles.Count));

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
               + $"prevArea={shelterDriver.PrevAreaId(pawn)}, "
               + $"musterAnchor={cachedAnchor}, musterReleased={musterReleased}";
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref manualMobilized, "livingWorld_manualMobilized", false);
        Scribe_Values.Look(ref announcedThreat, "livingWorld_announcedThreat", false);
        // Persist the muster line + release latch so a mid-raid reload keeps holding (or keeps free-engaging)
        // instead of yanking already-released fighters back to the line.
        Scribe_Values.Look(ref cachedAnchor, "livingWorld_musterAnchor", IntVec3.Invalid);
        Scribe_Values.Look(ref anchorTier, "livingWorld_musterAnchorTier", ThreatTier.None);
        Scribe_Values.Look(ref musterReleased, "livingWorld_musterReleased", false);

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
