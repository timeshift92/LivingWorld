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
    // The ready-fraction must hold for TWO consecutive rechecks before releasing, so the recheck where the last
    // fighter arrives shows a real Hold phase before the line breaks (otherwise arrival and release land on the
    // same recheck and Hold is skipped entirely). A breach still releases immediately.
    private bool musterGateArmed;

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

        // Non-combatants shelter for a Serious threat or a non-animal Raid. A lone dangerous animal grades Raid
        // (so fighters respond) but must NOT drive the whole colony to cover — that is a fighter's job.
        var shelterWorthy = currentTier == ThreatTier.Serious
                            || (currentTier == ThreatTier.Raid && !lastSignals.OnlyAnimals);
        shelterDriver.Drive(map, currentTier, shelterWorthy);
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
                musterGateArmed = false;
                anchorTier = ThreatTier.None;
            }
            else
            {
                // Re-resolve the anchor whenever the tier changes OR the threat's centre of mass has swung to a
                // different facing (a second wave from another side) — not only on a tier transition, which left
                // the line pointing the wrong way for the rest of a same-tier raid. Cheap (a bounded walk).
                if (wantsMuster && (currentTier != anchorTier || !cachedAnchor.IsValid || AnchorStale()))
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

                    // A breach or the timeout releases immediately. The ready-fraction, however, must hold for two
                    // consecutive rechecks (arm, then fire) — so the recheck the last fighter arrives shows Hold
                    // before the line breaks, instead of arrival and release colliding on the same recheck.
                    if (breached || musterHoldRechecks >= settings.musterReleaseTimeoutRechecks)
                    {
                        musterReleased = true;
                    }
                    else
                    {
                        var fractionMet = MusterGate.WantsRelease(progress, settings.musterReadyFraction);
                        if (fractionMet && musterGateArmed)
                        {
                            musterReleased = true;
                        }

                        musterGateArmed = fractionMet;
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

    // The cached anchor faces where the threat WAS. If the hostiles' centre of mass has swung to a materially
    // different bearing from the colony (a second wave from another side), the held line now faces the wrong way
    // and should be re-resolved. Compares the two bearings by their cosine (~50° divergence trips it).
    private bool AnchorStale()
    {
        try
        {
            if (!cachedAnchor.IsValid || !hostileCentroid.IsValid)
            {
                return false;
            }

            var center = MusterAnchorService.ColonyCenter(map);
            double ax = cachedAnchor.x - center.x, az = cachedAnchor.z - center.z;
            double hx = hostileCentroid.x - center.x, hz = hostileCentroid.z - center.z;
            var aLen = Math.Sqrt(ax * ax + az * az);
            var hLen = Math.Sqrt(hx * hx + hz * hz);
            if (aLen < 0.5 || hLen < 0.5)
            {
                return false;
            }

            var cos = (ax * hx + az * hz) / (aLen * hLen);
            return cos < 0.64;
        }
        catch
        {
            return false;
        }
    }

    // Fire a one-shot alert on the rising/falling edge of an AUTOMATIC threat so the player notices the colony
    // arming itself (a manual toggle stays silent — they initiated it). Rising edge = a letter (draws the eye,
    // can pause); falling edge = a lightweight message. Edge-tracked via announcedThreat, which is persisted so
    // a mid-raid reload does not re-announce.
    private void AnnounceThreatEdge(LivingWorldSettings settings)
    {
        // Gate the alert on Odyssey too: without it the whole mobilization/shelter machinery is a no-op (the
        // drivers early-return on !OdysseyActive), so a "colony mobilized" letter with nobody actually arming
        // would be a lie.
        if (!settings.armoryMobilizationEnabled || !settings.autoMobilizeOnThreat || !ModsConfig.OdysseyActive)
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

        var hostiles = pawns.Where(p => p != null && !p.Downed && !p.IsPrisoner && p.HostileTo(player)
                                        && IsAwakeThreat(p) && !IsHuntedByColony(p, liveMap)).ToList();
        if (hostiles.Count == 0)
        {
            return default;
        }

        // Measure "at the base" from where the colony actually IS (Home-area centroid), not the geometric middle
        // of the map — bases are almost never map-centred, so map.Center made EnemyAtBase fire for a hostile near
        // the map middle and NEVER fire for a raider on the real doorstep. This drives Serious grading, full
        // shelter, and the muster breach-release, so the wrong reference point broke all three.
        var center = MusterAnchorService.ColonyCenter(liveMap);
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
            AnySapper = hostiles.Any(IsSapper),
            EnemyAtBase = hostiles.Any(p => (p.Position - center).LengthHorizontal <= atBaseRadius),
            HostileCount = hostiles.Count,
            BigRaid = hostiles.Count >= settings.mobilizationBigRaidThreshold,
        };
    }

    // An animal a colonist is actively HUNTING is not a colony threat — the fight was provoked by us and is
    // already handled by the hunter. Without this, going hunting made a retaliating animal read as an attack:
    // the colony mobilized, drafted the hunter, and dragged them off to muster while the animal mauled them,
    // then "stood down" the moment the animal dropped and the pawn wandered off. Only wild animals can be a hunt
    // target (a humanlike raider never is), so this never suppresses a real raid.
    private static bool IsHuntedByColony(Pawn animal, Map map)
    {
        try
        {
            if (animal?.RaceProps?.Animal != true)
            {
                return false;
            }

            var colonists = map.mapPawns?.FreeColonistsSpawned;
            if (colonists == null)
            {
                return false;
            }

            foreach (var c in colonists)
            {
                var job = c?.CurJob;
                // Any way the PLAYER provoked this fight: the hunt work-job, or a manual drafted order to attack
                // or tame the animal. In all of these the player is already handling it — do not mobilize over it.
                if (job != null && job.targetA.Thing == animal
                    && (job.def == JobDefOf.Hunt || job.def == JobDefOf.AttackMelee
                        || job.def == JobDefOf.AttackStatic || job.def == JobDefOf.Tame))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    // A raider actively breaching/sapping the wall — flagged so the threat grades Serious and the muster line
    // releases immediately (holding a forward line is pointless once the wall is coming down). AnySapper was
    // previously hardcoded false, so the entire sapper fast-path was dead.
    private static bool IsSapper(Pawn p)
    {
        try
        {
            var dutyDef = p?.mindState?.duty?.def;
            if (dutyDef != null && (dutyDef == DutyDefOf.Sapper || dutyDef == DutyDefOf.Breaching))
            {
                return true;
            }

            return p?.CurJob?.def == JobDefOf.Mine;
        }
        catch
        {
            return false;
        }
    }

    // A dormant threat (an un-woken mech cluster / Anomaly entity / hive) is hostile-by-faction but not an
    // active danger — vanilla's DangerWatcher ignored it, so we must too, or the colony stays mobilized
    // forever staring at a sleeping cluster.
    private static bool IsAwakeThreat(Pawn p)
    {
        try
        {
            var dormant = p.GetComp<CompCanBeDormant>();
            if (dormant != null && !dormant.Awake)
            {
                return false;
            }

            // Anomaly entities (nociosphere, fleshmass heart) use a SEPARATE passive/active mechanism —
            // CompActivity, not CompCanBeDormant. While still "charging" (Passive) they are hostile-by-faction
            // but harmless and wandering; without this they held the colony at Serious the whole charge-up.
            if (ModsConfig.AnomalyActive)
            {
                var activity = p.GetComp<CompActivity>();
                if (activity != null && activity.IsDormant)
                {
                    return false;
                }
            }

            return true;
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
        // Persist the release timeout counter + arm flag so a mid-raid reload does not silently restart the
        // hold timer; and the tier/debounce so external readers (gizmos, other patches) don't see a false
        // "all clear" for the ~250-tick window before the first post-load recheck.
        Scribe_Values.Look(ref musterHoldRechecks, "livingWorld_musterHoldRechecks", 0);
        Scribe_Values.Look(ref musterGateArmed, "livingWorld_musterGateArmed", false);
        Scribe_Values.Look(ref currentTier, "livingWorld_currentTier", ThreatTier.None);
        Scribe_Values.Look(ref belowCount, "livingWorld_belowCount", 0);

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
