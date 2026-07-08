using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

public sealed class LivingWorldWorldComponent : WorldComponent
{
    private const int TicksPerDay = 60_000;
    private const int MaxCatchUpSimulationDays = 7;
    private const int BirthIntervalDays = 30;
    private const int AgeIntervalDays = 60;
    private const int NaturalDeathAge = 85;
    private const int MaxNaturalDeathsPerDay = 5;
    private const string FoodResourceKey = "PackagedSurvivalMeal";
    private const string SteelResourceKey = "Steel";
    private const string MedicineResourceKey = "MedicineIndustrial";
    private const string ComponentResourceKey = "ComponentIndustrial";

    private readonly World rimWorld;
    private bool bootstrapped;
    private bool migratedDrifterReservoir;
    private string serializedState = string.Empty;
    private int lastSimulatedDay;
    private int cachedWorldPopulation;
    private int cachedTargetPopulation;
    private bool? rimWarActive;
    private bool? empireActive;
    private int lastWorldWarLetterTick = int.MinValue;
    private int notifiedCaptureCount;
    private List<long> notifiedResolvedRaidArmyIds = new();
    private List<long> notifiedRaidWarningFactIds = new();
    private List<long> notifiedConflictIds = new();
    private List<long> rewardedVictoryConflictIds = new();
    private List<long> ruinSiteIds = new();
    private List<long> offeredAllianceConflictIds = new();

    // Collapses the "Ledger initialized" log across the many throwaway component instances RimWorld
    // builds during world-generation previews, so a new game does not spam a dozen identical lines.
    private static string? lastLoggedLedgerSummary;

    public LivingWorldWorldComponent(World world)
        : base(world)
    {
        rimWorld = world;
        Instance = this;
        State = new WorldState(ResolveWorldSeed(rimWorld));
    }

    public static LivingWorldWorldComponent? Instance { get; private set; }

    public WorldState State { get; private set; }

    public bool IsBootstrapped => bootstrapped;

    public int LastWorldSettlementSourceCount { get; private set; }

    public int TotalWorldObjects { get; private set; }

    public int VanillaSettlementSourceCount { get; private set; }

    public int FactionWorldObjectSourceCount { get; private set; }

    public int ImportableWorldObjectSourceCount { get; private set; }

    public int ScanErrorCount { get; private set; }

    public string RejectedWorldObjectTypes { get; private set; } = "none";

    public string LastBootstrapError { get; private set; } = string.Empty;

    public string LastBootstrapSource { get; private set; } = "not-started";

    public string LastBootstrapStatus { get; private set; } = "not-started";

    public bool WantsDrifterArrival
    {
        get
        {
            if (!bootstrapped)
            {
                return false;
            }

            var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
            if (!settings.drifterFlowEnabled)
            {
                return false;
            }

            return State.Drifters.Count > 0;
        }
    }

    // True when Rim War is driving world factions, so the Living World world-war loop stays off.
    public bool IsRimWarActive => RimWarIsActive;

    // True when Empire is active. Empire manages the player's own empire (player-faction
    // settlements, which K4 already keeps out of the ledger), so there is nothing to disable —
    // this is surfaced only so the UI can tell the player who owns what.
    public bool IsEmpireActive => EmpireIsActive;

    public string GetSummary()
    {
        return "LW_SummaryLine".Translate(
            State.Settlements.Count.Named("settlements"),
            State.Citizens.Count.Named("citizens"),
            State.Armies.Count.Named("armies"),
            State.ProductionProfiles.Count.Named("productionProfiles"),
            State.IntelReports.Count.Named("intelReports"),
            State.RaidOpportunities.Count(opportunity => opportunity.Status == RaidOpportunityStatus.Active).Named("activeRaidOpportunities"),
            State.DrifterArrivalReservoir.Named("drifterReservoir"),
            State.Events.Count.Named("events"));
    }

    public string GetDiagnosticSummary()
    {
        return "LW_DiagnosticLine".Translate(
            bootstrapped.Named("bootstrapped"),
            LastBootstrapSource.Named("source"),
            LastBootstrapStatus.Named("status"),
            LastWorldSettlementSourceCount.Named("worldObjects"),
            TotalWorldObjects.Named("totalWorldObjects"),
            VanillaSettlementSourceCount.Named("vanillaSettlements"),
            FactionWorldObjectSourceCount.Named("factionWorldObjects"),
            ImportableWorldObjectSourceCount.Named("importableWorldObjects"),
            ScanErrorCount.Named("scanErrors"),
            LastBootstrapError.Named("error"),
            RejectedWorldObjectTypes.Named("rejectedTypes"));
    }

    public override void FinalizeInit(bool fromLoad)
    {
        base.FinalizeInit(fromLoad);
        BootstrapFromRimWorldSettlements();
        MigrateDrifterReservoirForLegacySave();
        RepairMissingProductionProfilesFromRimWorldSettlements();
        // Reconcile world-map army markers with the loaded ledger so stale markers from before the
        // save are dropped and surviving movements keep their icon.
        SyncArmyWorldObjects();
        EnsureRuinSites();
    }

    public override void WorldComponentTick()
    {
        base.WorldComponentTick();

        if (!bootstrapped)
        {
            return;
        }

        var currentTick = Find.TickManager?.TicksGame ?? 0;
        var currentDay = currentTick / TicksPerDay;
        if (currentDay <= 0 || currentDay <= lastSimulatedDay)
        {
            return;
        }

        var simulatedDays = 0;
        while (lastSimulatedDay < currentDay && simulatedDays < MaxCatchUpSimulationDays)
        {
            lastSimulatedDay++;
            SimulateWorldDay(lastSimulatedDay);
            simulatedDays++;
        }

        if (lastSimulatedDay < currentDay && (LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging)
        {
            Log.Warning($"[LivingWorld] Daily simulation catch-up capped at {MaxCatchUpSimulationDays} days. Remaining days will continue next ticks.");
        }

        // One rate-limited letter AFTER the whole catch-up loop — never one per simulated day.
        MaybeSendWorldWarLetter(currentTick);
        MaybeSendRaidConsequenceLetters();
        MaybeSendRaidWarnings();
        MaybeSendConflictLetters();
        MaybeSendAllianceOffers();
        MaybeGrantVictoryRewards();

        LogSimulationDebugSnapshot(simulatedDays, currentTick);
    }

    // Surfaces the war arc as a felt event: when a war breaks out that the player is not in, a neutral
    // participant's envoy proposes an alliance against its enemy via an accept/decline letter. One offer
    // per catch-up, persisted per conflict so it is never re-offered. Accepting bridges to real relations.
    private void MaybeSendAllianceOffers()
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        if (!settings.worldWarEnabled || RimWarIsActive || State.IsInitialWorldSeedingActive)
        {
            return;
        }

        var playerId = State.PlayerFactionId;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return;
        }

        var offerDef = DefDatabase<LetterDef>.GetNamedSilentFail("LivingWorld_AllianceOffer");
        if (offerDef == null)
        {
            return;
        }

        var offered = new HashSet<long>(offeredAllianceConflictIds);
        foreach (var conflict in State.Conflicts
            .Where(conflict => conflict.Status == WorldConflictStatus.Active
                && !conflict.Involves(playerId!)
                && !offered.Contains(conflict.Id.Value))
            .OrderByDescending(conflict => conflict.WarExhaustionA + conflict.WarExhaustionB)
            .ThenBy(conflict => conflict.Id.Value))
        {
            var ally = AlliableParticipant(conflict, playerId!);
            if (ally == null)
            {
                continue;
            }

            var enemy = string.Equals(ally, conflict.FactionA, StringComparison.Ordinal)
                ? conflict.FactionB
                : conflict.FactionA;

            offeredAllianceConflictIds.Add(conflict.Id.Value);
            SendAllianceOffer(offerDef, ally, enemy);
            return; // One offer per catch-up, never a flood.
        }
    }

    private string? AlliableParticipant(WorldConflict conflict, string playerId)
    {
        foreach (var faction in new[] { conflict.FactionA, conflict.FactionB })
        {
            if (!string.Equals(faction, playerId, StringComparison.Ordinal)
                && !AllianceService.IsAlliedWithPlayer(State, faction)
                && DiplomacyService.GetStance(State, playerId, faction) == RelationStance.Neutral)
            {
                return faction;
            }
        }

        return null;
    }

    private void SendAllianceOffer(LetterDef offerDef, string allyFactionId, string enemyFactionId)
    {
        var relatedFaction = Find.FactionManager?.AllFactionsListForReading
            .FirstOrDefault(candidate => candidate?.def?.defName == allyFactionId);
        var letter = (ChoiceLetter_LivingWorldAlliance)LetterMaker.MakeLetter(
            "LW_AllianceOfferLabel".Translate(),
            "LW_AllianceOfferText".Translate(
                ResolveFactionLabel(allyFactionId).Named("ally"),
                ResolveFactionLabel(enemyFactionId).Named("enemy")),
            offerDef,
            relatedFaction,
            (Quest?)null);
        letter.allyFactionId = allyFactionId;
        letter.enemyFactionId = enemyFactionId;
        Find.LetterStack?.ReceiveLetter(letter);
    }

    // Player war participation Slice 4: when a war the player joined has resolved in the ally's favour,
    // grant the shared-victory windfall and announce it. Persisted per conflict so each win pays once.
    private void MaybeGrantVictoryRewards()
    {
        var rewarded = new HashSet<long>(rewardedVictoryConflictIds);
        var victories = PlayerVictoryService.GrantVictoryRewards(State, rewarded, State.CurrentTick);
        if (victories.Count == 0)
        {
            return;
        }

        foreach (var victory in victories)
        {
            rewardedVictoryConflictIds.Add(victory.ConflictId);

            // Bridge to REAL RimWorld relations: the shared victory actually raises the player's
            // standing with the victorious ally, not just the ledger's own goodwill.
            LivingWorldFactionRelations.ApplyGoodwill(victory.AllyFactionId, PlayerVictoryService.VictoryGoodwill);

            Find.LetterStack?.ReceiveLetter(
                "LW_PlayerVictoryLetterLabel".Translate(),
                "LW_PlayerVictoryLetterText".Translate(
                    ResolveFactionLabel(victory.AllyFactionId).Named("ally"),
                    ResolveFactionLabel(victory.EnemyFactionId).Named("enemy")),
                LetterDefOf.PositiveEvent);
        }
    }

    private int lastLoggedEventCount = -1;

    // Concise per-catch-up snapshot of the otherwise-invisible NPC world, written to Player.log when
    // the debug-logging setting is on. The player cannot enter NPC settlements to watch them evolve,
    // so this is the observability window: state counts + the raid-intel that drives incoming raids +
    // the new ledger events (facility built, war declared, collapse, capture, ...) since last time.
    private void LogSimulationDebugSnapshot(int simulatedDays, int currentTick)
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        if (!settings.debugLogging)
        {
            return;
        }

        var activeSettlements = State.Settlements.Count(settlement => settlement.IsActive);
        var pop = State.Citizens.Count(citizen => citizen.Status == CitizenStatus.Alive);
        var facilities = State.SettlementFacilities.Count;
        var activeProjects = State.SettlementProjects.Count(project => project.Status == SettlementProjectStatus.Active);
        var activeConflicts = State.Conflicts.Count(conflict => conflict.Status != WorldConflictStatus.Resolved);
        var activeRuins = State.Ruins.Count(ruin => ruin.Status == RuinStatus.Active);
        var animalCohorts = State.AnimalCohorts.Count;
        var activeBreedingProjects = State.AnimalBreedingProjects.Count(project => project.Status == AnimalBreedingProjectStatus.Active);
        var playerIntel = State.RaidIntelFacts.Count(fact =>
            fact.TargetKind == RaidIntelTargetKind.PlayerColony && !fact.IsExpired(currentTick));

        Log.Message(
            $"[LivingWorld] day {lastSimulatedDay} (+{simulatedDays}d): settlements {activeSettlements}/{State.Settlements.Count}"
            + $" | pop {pop} | facilities {facilities} | projects {activeProjects} active"
            + $" | animals {animalCohorts} cohorts | breeding {activeBreedingProjects} active"
            + $" | conflicts {activeConflicts} | ruins {activeRuins} | player-raid-intel {playerIntel}");

        var events = State.Events;
        if (lastLoggedEventCount < 0)
        {
            // First snapshot of the session: don't replay the bootstrap history, just set the mark.
            lastLoggedEventCount = events.Count;
            return;
        }

        if (events.Count > lastLoggedEventCount)
        {
            var byKind = events
                .Skip(lastLoggedEventCount)
                .GroupBy(worldEvent => worldEvent.Kind)
                .Select(group => $"{group.Key} x{group.Count()}");
            Log.Message($"[LivingWorld]   new events: {string.Join(", ", byKind)}");
        }

        lastLoggedEventCount = events.Count;
    }

    // A believable early warning: when a hostile faction has fresh raid intel about the player's
    // colony (a scout sighting, a trader's word, a rumor...), tell the player a raid may follow and
    // name the source. Deterministic (derived from ledger intel facts, not a random roll),
    // rate-limited, and persisted per intel fact so a save/load never re-warns. Only while Living
    // World actually drives raids (silent when ceded to Rim War).
    private void MaybeSendRaidWarnings()
    {
        if (State.IsInitialWorldSeedingActive || IsRimWarActive)
        {
            return;
        }

        var active = State.RaidIntelFacts
            .Where(fact => fact.TargetKind == RaidIntelTargetKind.PlayerColony && !fact.IsExpired(State.CurrentTick))
            .ToList();

        var currentIds = new HashSet<long>(active.Select(fact => fact.Id.Value));
        notifiedRaidWarningFactIds.RemoveAll(id => !currentIds.Contains(id));

        var sent = 0;
        foreach (var fact in active
            .Where(fact => !notifiedRaidWarningFactIds.Contains(fact.Id.Value))
            .OrderByDescending(fact => fact.ValueBand)
            .ThenByDescending(fact => fact.Confidence)
            .ThenBy(fact => fact.Id.Value))
        {
            notifiedRaidWarningFactIds.Add(fact.Id.Value);
            if (sent >= 2)
            {
                // Cap warnings per pass so a burst of intel never floods the player.
                continue;
            }

            var factionName = Find.FactionManager?.AllFactionsListForReading
                .FirstOrDefault(candidate => candidate.def?.defName == fact.FactionId)?.Name
                ?? fact.FactionId;
            var sourcePhrase = RaidWarningSourceKey(fact.SourceKind).Translate();

            Find.LetterStack?.ReceiveLetter(
                "LW_RaidWarningLetterLabel".Translate(),
                "LW_RaidWarningLetterText".Translate(
                    factionName.Named("faction"),
                    sourcePhrase.Named("source")),
                LetterDefOf.ThreatSmall);
            sent++;

            if ((LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging)
            {
                Log.Message(
                    $"[LivingWorld] raid warning sent: faction={fact.FactionId} source={fact.SourceKind}"
                    + $" band={fact.ValueBand} confidence={fact.Confidence}");
            }
        }
    }

    private static string RaidWarningSourceKey(IntelSourceKind source)
    {
        return source switch
        {
            IntelSourceKind.Scout => "LW_RaidWarningSource_Scout",
            IntelSourceKind.Trade => "LW_RaidWarningSource_Trade",
            IntelSourceKind.Rumor => "LW_RaidWarningSource_Rumor",
            IntelSourceKind.Prisoner => "LW_RaidWarningSource_Prisoner",
            IntelSourceKind.Survivor => "LW_RaidWarningSource_Survivor",
            IntelSourceKind.Refugee => "LW_RaidWarningSource_Refugee",
            IntelSourceKind.DirectVisit => "LW_RaidWarningSource_DirectVisit",
            _ => "LW_RaidWarningSource_Public",
        };
    }

    // Closes the loop on a Living World raid the player just fought: once a raid's reserved citizens
    // are all reconciled (dead/captured/returned/missing), tell the player where it came from and
    // that the source settlement is now weaker — the visible payoff of "the raid used real people".
    // Losses the player directly observed on their own map, so exact numbers are legitimate here.
    private void MaybeSendRaidConsequenceLetters()
    {
        if (State.IsInitialWorldSeedingActive)
        {
            return;
        }

        var resolved = State.RaidOutcomes
            .Where(outcome => outcome.IsResolved)
            .ToList();

        var currentIds = new HashSet<long>(resolved.Select(outcome => outcome.ArmyId.Value));
        notifiedResolvedRaidArmyIds.RemoveAll(id => !currentIds.Contains(id));

        var sent = 0;
        foreach (var outcome in resolved
            .Where(outcome => !notifiedResolvedRaidArmyIds.Contains(outcome.ArmyId.Value))
            .OrderBy(outcome => outcome.Tick)
            .ThenBy(outcome => outcome.ArmyId.Value))
        {
            notifiedResolvedRaidArmyIds.Add(outcome.ArmyId.Value);
            if (sent >= 3)
            {
                // Cap letters per pass so a long catch-up never floods the player.
                continue;
            }

            var settlement = State.GetSettlement(outcome.SourceSettlementId);
            var settlementName = settlement?.Name ?? outcome.FactionId;
            var factionName = Find.FactionManager?.AllFactionsListForReading
                .FirstOrDefault(candidate => candidate.def?.defName == outcome.FactionId)?.Name
                ?? outcome.FactionId;
            var lost = outcome.Dead + outcome.Prisoner + outcome.Missing;

            Find.LetterStack?.ReceiveLetter(
                "LW_RaidConsequenceLetterLabel".Translate(),
                "LW_RaidConsequenceLetterText".Translate(
                    settlementName.Named("settlement"),
                    factionName.Named("faction"),
                    outcome.Sent.Named("sent"),
                    outcome.Returned.Named("returned"),
                    lost.Named("lost")),
                LetterDefOf.NeutralEvent);
            sent++;
        }
    }

    // Surfaces the world war to the player as an occasional, rate-limited letter that summarizes
    // settlements that changed hands. Silent only when the loop itself is off (disabled or ceded
    // to Rim War) or during initial seeding; captures accumulate across the cooldown so nothing is
    // lost, and the whole daily catch-up produces at most one letter (no flood).
    private void MaybeSendWorldWarLetter(int currentTick)
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        if (!settings.worldWarEnabled || IsRimWarActive || State.IsInitialWorldSeedingActive)
        {
            return;
        }

        var captureCount = State.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.SettlementCaptured);
        var newCaptures = captureCount - notifiedCaptureCount;
        if (newCaptures <= 0)
        {
            return;
        }

        var cooldownTicks = Math.Max(0, settings.worldWarLetterCooldownDays) * TicksPerDay;
        if (currentTick - lastWorldWarLetterTick < cooldownTicks)
        {
            // Within cooldown: hold off, let captures accumulate for the next letter.
            return;
        }

        Find.LetterStack?.ReceiveLetter(
            "LW_WorldWarLetterLabel".Translate(),
            "LW_WorldWarLetterText".Translate(newCaptures.Named("captures")),
            LetterDefOf.NeutralEvent);
        lastWorldWarLetterTick = currentTick;
        notifiedCaptureCount = captureCount;
    }

    // Announces newly-declared wars between NPC factions — the political companion to the capture
    // letter (which reports territory changes, not declarations). One batched letter per catch-up
    // (headline war + count of any others that started in the same window) so a busy war-day never
    // floods the player; each conflict is persisted so a save/load never re-announces it. Silent
    // when the world war is off, ceded to Rim War, or during initial seeding.
    private void MaybeSendConflictLetters()
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        if (!settings.worldWarEnabled || RimWarIsActive || State.IsInitialWorldSeedingActive)
        {
            return;
        }

        var known = new HashSet<long>(notifiedConflictIds);
        var newConflicts = State.Conflicts
            .Where(conflict => conflict.Status == WorldConflictStatus.Active
                && !known.Contains(conflict.Id.Value))
            .OrderBy(conflict => conflict.StartedTick)
            .ThenBy(conflict => conflict.Id.Value)
            .ToList();
        if (newConflicts.Count == 0)
        {
            return;
        }

        var headline = newConflicts[0];
        Find.LetterStack?.ReceiveLetter(
            "LW_ConflictLetterLabel".Translate(),
            "LW_ConflictLetterText".Translate(
                ResolveFactionLabel(headline.FactionA).Named("factionA"),
                ResolveFactionLabel(headline.FactionB).Named("factionB"),
                newConflicts.Count.Named("count")),
            LetterDefOf.NeutralEvent);

        foreach (var conflict in newConflicts)
        {
            notifiedConflictIds.Add(conflict.Id.Value);
        }
    }

    private void SimulateWorldDay(int day)
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        // Release stale raid preparations and materialization leases whose window elapsed, so
        // reserved citizens/supplies return to their settlements instead of leaking. Cheap: both only
        // touch active records past their expiry.
        RaidPreparationService.ReleaseExpiredPreparations(State, day * TicksPerDay);
        MaterializationLeaseService.ReleaseExpiredLeases(State, day * TicksPerDay);
        SettlementProductionService.SimulateDay(
            State,
            new SettlementProductionRequest(
                day * TicksPerDay,
                FoodResourceKey,
                SteelResourceKey,
                MedicineResourceKey,
                ComponentResourceKey));
        SettlementDailySimulationService.SimulateDay(
            State,
            new SettlementDailySimulationRequest(
                day * TicksPerDay,
                FoodResourceKey,
                settings.foodPerCitizen > 0 ? 1 : 0,
                BirthIntervalDays));
        AnimalEcologyDriver.SimulateDay(
            State,
            new AnimalEcologyDriverRequest(
                day * TicksPerDay,
                FoodResourceKey,
                settings.foodPerCitizen > 0 ? 1 : 0));
        AnimalProductionService.SimulateDay(
            State,
            new AnimalProductionRequest(
                day * TicksPerDay,
                FoodResourceKey,
                RanchOutputPerHealthyAnimal: 1,
                WildHarvestDivisor: 4,
                MaxWildAnimalsHarvestedPerCohort: 2));
        AnimalBreedingDriver.SimulateDay(
            State,
            new AnimalBreedingDriverRequest(
                day * TicksPerDay,
                FoodResourceKey,
                MedicineResourceKey,
                ComponentResourceKey));
        DemographyService.SimulateDay(
            State,
            new DemographySimulationRequest(
                day * TicksPerDay,
                AgeIntervalDays,
                NaturalDeathAge,
                MaxNaturalDeathsPerDay));
        MigrationService.SimulateDay(
            State,
            new MigrationSimulationRequest(
                day * TicksPerDay,
                FoodResourceKey,
                settings.foodPerCitizen > 0 ? 1 : 0,
                50,
                1));

        if (settings.settlementDevelopmentEnabled && !State.IsInitialWorldSeedingActive)
        {
            SettlementDevelopmentService.SimulateDay(
                State,
                new SettlementDevelopmentRequest(
                    day * TicksPerDay,
                    FoodResourceKey,
                    settings.settlementHousingHeadroom,
                    settings.settlementDevelopmentStep,
                    settings.maxSettlementAdults));

            // Settlements invest their surplus materials into facilities: complete ready projects,
            // repair damaged facilities, then start building the ones they lack. This is what makes
            // the Task 4 facilities UI light up in-game; it consumes real Steel/Components so the
            // ledger stays conservative. Gated with development so disabling that setting also stops
            // facility churn.
            SettlementInfrastructureDriver.SimulateDay(
                State,
                new SettlementInfrastructureDriverRequest(day * TicksPerDay));
        }

        if (settings.drifterFlowEnabled && !State.IsInitialWorldSeedingActive)
        {
            var dayTick = day * TicksPerDay;
            var target = Math.Max(0, State.Settlements.Count * Math.Max(0, settings.targetWorldPopulationPerSettlement));
            var ceiling = Math.Max(target, Math.Max(0, settings.drifterHardCeiling));

            DrifterArrivalService.SimulateArrivals(
                State,
                new DrifterArrivalRequest(dayTick, target, ceiling, settings.maxDrifterArrivalsPerDay));
            DrifterFoundingService.SimulateFounding(
                State,
                new DrifterFoundingRequest(dayTick, settings.drifterMinFounders, settings.drifterLeaderAptitudeThreshold));
            DrifterAssimilationService.SimulateAssimilation(
                State,
                new DrifterAssimilationRequest(dayTick, settings.maxDrifterAssimilationsPerDay));

            cachedTargetPopulation = target;
            cachedWorldPopulation = State.Citizens.Count(citizen => citizen.Status == CitizenStatus.Alive) + State.Drifters.Count;
        }

        // World war runs after population flow and before collapse checks, so faction
        // extinction accounts for the day's battle casualties. It is mutually exclusive with
        // Rim War: if that mod is driving factions, Living World stands down to avoid double
        // driving the same world.
        if (settings.worldWarEnabled && !RimWarIsActive && !State.IsInitialWorldSeedingActive)
        {
            EnsureFactionBehaviors();
            WorldWarService.SimulateDay(
                State,
                new WorldWarRequest(
                    day * TicksPerDay,
                    settings.worldWarTravelDays,
                    settings.worldWarRaidCombatants,
                    settings.worldWarWarbandCooldownDays));
        }

        // Faction extinction, then its physical consequences: collapsed non-player settlements
        // become ruins, starving non-player settlements relocate to a stable sibling, and stale
        // ruins are pruned. ResolveFactionCollapses folds the collapse pass into the driver (same
        // reason and daily timing as the previous bare call) so collapse still runs exactly once.
        SettlementLifecycleDriver.SimulateDay(
            State,
            new SettlementLifecycleDriverRequest(
                day * TicksPerDay,
                FoodResourceKey,
                settings.foodPerCitizen > 0 ? 1 : 0)
            {
                ResolveFactionCollapses = true,
            });

        // End wars that have been decided (large exhaustion gap, or one side wiped out) — runs after
        // collapses so a faction that just lost its last settlement loses its wars too. Without this
        // no war ever concludes, so player victory rewards would never fire.
        ConflictResolutionService.SimulateDay(State, day * TicksPerDay);

        // Refresh the economy wealth snapshots from end-of-day stock so the economy UI (main tab
        // bands, the population/economy table) reads real silver + material value instead of a
        // fallback. Deterministic and conservation-safe (writes only snapshots).
        SettlementWealthService.RefreshAll(State, SettlementWealthService.DefaultPriceBook);

        // Visualize the day's world-war army movements on the globe (display only — the ledger
        // remains the source of truth). Cheap: it only touches active traveling movements.
        SyncArmyWorldObjects();

        // Mark the ruins of settlements destroyed by faction collapse (display only). Reconciled the
        // same way as army markers: a marker per active ruin, dropped when the ruin is reclaimed or
        // pruned from the ledger.
        EnsureRuinSites();
    }

    // Reconciles the world-map mission markers with the ledger's active travels: a marker per
    // marching warband and per traveling caravan, each with its own icon, dropping markers whose
    // travel has resolved and clearing everything when the world war is off or Rim War is driving
    // factions. Positions animate every frame inside the marker's DrawPos, so this only manages
    // membership.
    private void SyncArmyWorldObjects()
    {
        var worldObjects = Find.WorldObjects;
        if (worldObjects == null)
        {
            return;
        }

        var existing = new Dictionary<string, WorldObject_LivingWorldArmy>(StringComparer.Ordinal);
        foreach (var worldObject in worldObjects.AllWorldObjects)
        {
            if (worldObject is WorldObject_LivingWorldArmy marker && !string.IsNullOrEmpty(marker.MarkerKey))
            {
                existing[marker.MarkerKey] = marker;
            }
        }

        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        var warVisible = settings.worldWarEnabled && !RimWarIsActive;

        var live = new HashSet<string>(StringComparer.Ordinal);
        var markerDef = warVisible ? DefDatabase<WorldObjectDef>.GetNamedSilentFail("LivingWorld_ArmyMarker") : null;
        if (markerDef != null)
        {
            // Warbands marching to battle.
            foreach (var movement in State.ArmyMovements)
            {
                if (movement.Status != ArmyMovementStatus.Traveling)
                {
                    continue;
                }

                var army = State.GetArmy(movement.ArmyId);
                if (army == null)
                {
                    continue;
                }

                EnsureMissionMarker(
                    worldObjects, markerDef, existing, live,
                    $"army:{movement.ArmyId.Value}",
                    "World/LivingWorld_Warband",
                    "LW_MissionKind_Warband".Translate(),
                    army.FactionId,
                    army.SourceSettlementId,
                    movement.TargetSettlementId,
                    movement.DepartTick,
                    movement.ArrivalTick,
                    BuildWarbandMarkerDetails(army));
            }

            // Caravans hauling goods between settlements (Core's caravan travel system).
            foreach (var caravan in State.Caravans)
            {
                if (caravan.Status != CaravanStatus.Traveling)
                {
                    continue;
                }

                EnsureMissionMarker(
                    worldObjects, markerDef, existing, live,
                    $"caravan:{caravan.Id.Value}",
                    "World/LivingWorld_Trader",
                    "LW_MissionKind_Trader".Translate(),
                    caravan.FactionId,
                    caravan.SourceSettlementId,
                    caravan.TargetSettlementId,
                    caravan.DepartTick,
                    caravan.ArrivalTick,
                    BuildCaravanMarkerDetails(caravan));
            }

            // Scout / diplomat missions travelling to a target settlement.
            foreach (var mission in State.Missions)
            {
                if (mission.Status != WorldMissionStatus.Traveling)
                {
                    continue;
                }

                var texture = mission.Kind == WorldMissionKind.Scout
                    ? "World/LivingWorld_Scout"
                    : "World/LivingWorld_Diplomat";
                var kindKey = mission.Kind == WorldMissionKind.Scout
                    ? "LW_MissionKind_Scout"
                    : "LW_MissionKind_Diplomat";

                EnsureMissionMarker(
                    worldObjects, markerDef, existing, live,
                    $"mission:{mission.Id.Value}",
                    texture,
                    kindKey.Translate(),
                    mission.FactionId,
                    mission.OriginSettlementId,
                    mission.TargetSettlementId,
                    mission.DepartTick,
                    mission.ArrivalTick,
                    BuildMissionMarkerDetails(mission));
            }
        }

        foreach (var pair in existing)
        {
            if (!live.Contains(pair.Key))
            {
                worldObjects.Remove(pair.Value);
            }
        }
    }

    private void EnsureMissionMarker(
        WorldObjectsHolder worldObjects,
        WorldObjectDef markerDef,
        Dictionary<string, WorldObject_LivingWorldArmy> existing,
        HashSet<string> live,
        string key,
        string texture,
        string kindNoun,
        string factionId,
        EntityId originSettlementId,
        EntityId targetSettlementId,
        int departTick,
        int arrivalTick,
        MissionMarkerDetails details)
    {
        var target = State.GetSettlement(targetSettlementId);
        if (target == null)
        {
            return;
        }

        var targetTile = ParseSettlementTile(target.Slug);
        if (targetTile < 0)
        {
            return;
        }

        live.Add(key);

        var origin = State.GetSettlement(originSettlementId);
        var originTile = origin != null ? ParseSettlementTile(origin.Slug) : targetTile;
        if (originTile < 0)
        {
            originTile = targetTile;
        }

        var faction = Find.FactionManager?.AllFactionsListForReading
            .FirstOrDefault(candidate => candidate.def?.defName == factionId);

        var isNew = !existing.TryGetValue(key, out var marker);
        marker ??= (WorldObject_LivingWorldArmy)WorldObjectMaker.MakeWorldObject(markerDef);
        marker.Tile = targetTile;
        if (faction != null)
        {
            marker.SetFaction(faction);
        }

        marker.Configure(
            key,
            texture,
            kindNoun,
            originTile,
            targetTile,
            departTick,
            arrivalTick,
            faction?.Name ?? factionId,
            target.Name,
            details.Combatants,
            details.Strength,
            details.ResourceSummary,
            details.Reason);
        if (isNew)
        {
            worldObjects.Add(marker);
        }
    }

    private MissionMarkerDetails BuildWarbandMarkerDetails(WorldArmy army)
    {
        var combatants = State.Citizens.Count(citizen =>
            citizen.Status == CitizenStatus.Alive
            && citizen.IsAdult
            && State.GetOwner(citizen.Id) == army.Id);
        var strength = SettlementPowerService.CombatPowerOf(combatants);
        var resources = FormatResourceSummary(ResourceLedgerService.GetResources(State, army.Id));
        var reason = "LW_MissionReason_Warband".Translate().ToString();
        return new MissionMarkerDetails(combatants, strength, resources, reason);
    }

    private MissionMarkerDetails BuildCaravanMarkerDetails(WorldCaravan caravan)
    {
        var resources = FormatResourceSummary(ResourceLedgerService.GetResources(State, caravan.Id));
        var reason = "LW_MissionReason_Caravan".Translate().ToString();
        return new MissionMarkerDetails(0, 0, resources, reason);
    }

    private MissionMarkerDetails BuildMissionMarkerDetails(WorldMission mission)
    {
        var reasonKey = mission.Kind == WorldMissionKind.Scout
            ? "LW_MissionReason_Scout"
            : "LW_MissionReason_Diplomat";
        var reason = reasonKey.Translate(mission.Amount.Named("amount")).ToString();
        return new MissionMarkerDetails(0, 0, string.Empty, reason);
    }

    private static string FormatResourceSummary(IReadOnlyList<ResourceStack> resources)
    {
        var visible = resources
            .Where(resource => resource.Quantity > 0)
            .OrderByDescending(resource => resource.Quantity)
            .ThenBy(resource => resource.ResourceKey, StringComparer.Ordinal)
            .Take(3)
            .Select(resource => $"{resource.ResourceKey} x{resource.Quantity}")
            .ToList();

        return visible.Count == 0
            ? string.Empty
            : string.Join(", ", visible);
    }

    private sealed class MissionMarkerDetails
    {
        public MissionMarkerDetails(int combatants, int strength, string resourceSummary, string reason)
        {
            Combatants = combatants;
            Strength = strength;
            ResourceSummary = resourceSummary ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public int Combatants { get; }

        public int Strength { get; }

        public string ResourceSummary { get; }

        public string Reason { get; }
    }

    // Ledger settlement slugs are "worldobject:{defName}:{tile}:{factionId}", so the RimWorld world
    // tile is embedded even though Core itself has no tile geometry. Returns -1 when unparseable.
    private static int ParseSettlementTile(string? slug)
    {
        if (string.IsNullOrEmpty(slug))
        {
            return -1;
        }

        var parts = slug!.Split(':');
        return parts.Length >= 3 && int.TryParse(parts[2], out var tile) ? tile : -1;
    }

    // Turns the ledger's active ruins into REAL, lootable RimWorld sites (abandoned settlements the
    // player can caravan to and clear for salvage) instead of display-only markers. Built once per
    // ruin (tracked in ruinSiteIds); vanilla owns the site's lifecycle afterwards. Fail-open: a site
    // that cannot be built is skipped, never throwing inside the daily tick.
    public void EnsureRuinSites()
    {
        var worldObjects = Find.WorldObjects;
        var sitePart = SitePartDefOf.AbandonedSettlement;
        if (worldObjects == null || sitePart == null)
        {
            return;
        }

        var alreadyBuilt = new HashSet<long>(ruinSiteIds);
        foreach (var ruin in State.Ruins)
        {
            if (ruin.Status != RuinStatus.Active || alreadyBuilt.Contains(ruin.Id.Value))
            {
                continue;
            }

            var tile = ParseSettlementTile(ruin.Slug);
            if (tile < 0)
            {
                continue;
            }

            // Mark attempted before building so a failure never retries every tick.
            ruinSiteIds.Add(ruin.Id.Value);
            try
            {
                var site = SiteMaker.MakeSite(
                    sitePart,
                    tile,
                    faction: null,
                    ifHostileThenMustRemainHostile: false,
                    threatPoints: RuinThreatPoints(ruin.DangerBand),
                    worldObjectDef: null);
                if (site == null)
                {
                    continue;
                }

                site.Tile = tile;
                worldObjects.Add(site);
                if ((LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging)
                {
                    Log.Message(
                        $"[LivingWorld] ruin site created for '{ruin.Name}' (former {ruin.FormerFactionId}) at tile {tile}.");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[LivingWorld] ruin site creation failed safely: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static int RuinThreatPoints(RuinDangerBand band)
    {
        return band switch
        {
            RuinDangerBand.High => 800,
            RuinDangerBand.Medium => 400,
            _ => 150,
        };
    }

    private static string ResolveFactionLabel(string factionId)
    {
        if (string.IsNullOrEmpty(factionId))
        {
            return factionId ?? string.Empty;
        }

        var faction = Find.FactionManager?.AllFactionsListForReading
            .FirstOrDefault(candidate => candidate.def?.defName == factionId);
        return faction?.Name ?? factionId;
    }

    // Rim War (Torann.RimWar) drives world factions the same way; when it is active Living
    // World's own world-war loop stays off so the two never fight over the same world.
    private bool RimWarIsActive => rimWarActive ??= ModsConfig.IsActive("Torann.RimWar");

    // Empire (Matathias.Empire): the player-empire manager. Detected only to inform the player.
    private bool EmpireIsActive => empireActive ??= ModsConfig.IsActive("Matathias.Empire");

    // Gives every ledger faction a behavior once, derived from its RimWorld faction: permanent
    // enemies (pirates) become irreconcilable warmongers, the player is passive, other humanlike
    // factions fight, and non-humanlike factions (mechanoids, insectoids) are excluded.
    private void EnsureFactionBehaviors()
    {
        foreach (var factionId in State.Settlements
            .Select(settlement => settlement.FactionId)
            .Distinct(StringComparer.Ordinal))
        {
            if (State.GetFactionBehavior(factionId) != FactionBehavior.Undefined)
            {
                continue;
            }

            var faction = Find.FactionManager?.AllFactionsListForReading
                .FirstOrDefault(candidate => candidate.def?.defName == factionId);

            if (faction?.def == null || !faction.def.humanlikeFaction)
            {
                State.AssignFactionBehavior(factionId, FactionBehavior.Excluded);
            }
            else if (faction.IsPlayer)
            {
                State.AssignFactionBehavior(factionId, FactionBehavior.Player);
            }
            else if (faction.def.permanentEnemy)
            {
                State.AssignFactionBehavior(factionId, FactionBehavior.Warmonger);
                State.MarkFactionIrreconcilable(factionId);
            }
            else
            {
                State.AssignFactionBehavior(factionId, FactionBehavior.Aggressive);
            }
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();

        if (Scribe.mode == LoadSaveMode.Saving)
        {
            serializedState = WorldStateCodec.Serialize(State);
        }

        Scribe_Values.Look(ref bootstrapped, "livingWorld_bootstrapped", false);
        Scribe_Values.Look(ref serializedState, "livingWorld_serializedState", string.Empty);
        Scribe_Values.Look(ref lastSimulatedDay, "livingWorld_lastSimulatedDay", 0);
        Scribe_Values.Look(ref lastWorldWarLetterTick, "livingWorld_lastWorldWarLetterTick", int.MinValue);
        Scribe_Values.Look(ref notifiedCaptureCount, "livingWorld_notifiedCaptureCount", 0);
        Scribe_Values.Look(ref migratedDrifterReservoir, "livingWorld_migratedDrifterReservoir", false);
        Scribe_Collections.Look(ref notifiedResolvedRaidArmyIds, "livingWorld_notifiedResolvedRaidArmyIds", LookMode.Value);
        notifiedResolvedRaidArmyIds ??= new List<long>();
        Scribe_Collections.Look(ref notifiedRaidWarningFactIds, "livingWorld_notifiedRaidWarningFactIds", LookMode.Value);
        notifiedRaidWarningFactIds ??= new List<long>();
        Scribe_Collections.Look(ref notifiedConflictIds, "livingWorld_notifiedConflictIds", LookMode.Value);
        notifiedConflictIds ??= new List<long>();
        Scribe_Collections.Look(ref rewardedVictoryConflictIds, "livingWorld_rewardedVictoryConflictIds", LookMode.Value);
        rewardedVictoryConflictIds ??= new List<long>();
        Scribe_Collections.Look(ref ruinSiteIds, "livingWorld_ruinSiteIds", LookMode.Value);
        ruinSiteIds ??= new List<long>();
        Scribe_Collections.Look(ref offeredAllianceConflictIds, "livingWorld_offeredAllianceConflictIds", LookMode.Value);
        offeredAllianceConflictIds ??= new List<long>();

        if (Scribe.mode == LoadSaveMode.LoadingVars && !string.IsNullOrWhiteSpace(serializedState))
        {
            State = WorldStateCodec.Deserialize(serializedState);
            LastBootstrapSource = "save";
            LastBootstrapStatus = "loaded";
            LastWorldSettlementSourceCount = State.Settlements.Count;
            ImportableWorldObjectSourceCount = State.Settlements.Count;
        }
    }

    public void RetryBootstrapFromRimWorldSettlements()
    {
        bootstrapped = false;
        BootstrapFromRimWorldSettlements();
    }

    public void CreateDebugLedger()
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        State = new WorldState(ResolveWorldSeed(rimWorld));
        var settlement = State.CreateSettlement("debug-settlement", "LW_DebugSettlementName".Translate().ToString(), "LivingWorldDebug");
        var citizenCount = Math.Max(6, settings.baselineHumanSettlementAdults);
        State.RecordSettlementProductionProfile(SettlementProductionProfile.FromEnvironment(
            settlement.Id,
            new SettlementProductionEnvironment(
                "TemperateForest",
                "SmallHills",
                "Industrial",
                55,
                850,
                21)));

        for (var i = 0; i < citizenCount; i++)
        {
            var sex = i % 2 == 0 ? Sex.Male : Sex.Female;
            var age = 18 + (i % 42);
            State.CreateCitizen(
                "LW_DebugCitizenName".Translate((i + 1).Named("index")).ToString(),
                age,
                sex,
                "settler",
                settlement.Id);
        }

        State.AddResource(settlement.Id, FoodResourceKey, citizenCount * Math.Max(1, settings.foodPerCitizen));
        State.AddResource(settlement.Id, SteelResourceKey, citizenCount * Math.Max(1, settings.steelPerCitizen));
        PlayerKnowledgeService.RecordPublicSettlementInfo(
            State,
            settlement.Id,
            "debug settlement public disclosure");

        bootstrapped = true;
        LastWorldSettlementSourceCount = 0;
        TotalWorldObjects = 0;
        VanillaSettlementSourceCount = 0;
        FactionWorldObjectSourceCount = 0;
        ImportableWorldObjectSourceCount = 1;
        ScanErrorCount = 0;
        RejectedWorldObjectTypes = "debug-ledger";
        LastBootstrapError = string.Empty;
        LastBootstrapSource = "debug-ledger";
        LastBootstrapStatus = "created";

        if (settings.debugLogging)
        {
            Log.Message($"[LivingWorld] Debug ledger created. {GetSummary()}");
        }
    }

    // Legacy saves predate the drifter-arrival reservoir: they load already bootstrapped with the
    // reservoir at 0, bootstrap early-returns, and — with no replenishment path yet — drifter arrivals
    // would stop permanently and silently. Seed the reservoir once for such saves (matching a fresh
    // world), guarded by a persisted flag so an intentionally-depleted reservoir is never refilled on
    // reload.
    private void MigrateDrifterReservoirForLegacySave()
    {
        if (migratedDrifterReservoir || !bootstrapped)
        {
            return;
        }

        migratedDrifterReservoir = true;

        if (State.DrifterArrivalReservoir > 0 || State.Settlements.Count == 0)
        {
            return;
        }

        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        var reserve = State.Settlements.Count * Math.Max(0, settings.targetWorldPopulationPerSettlement);
        if (reserve <= 0)
        {
            return;
        }

        State.RunInitialWorldSeeding(() =>
        {
            State.AddDrifterArrivalReservoir(reserve, "legacy save drifter reservoir migration");
        });

        if (settings.debugLogging)
        {
            Log.Message($"[LivingWorld] Migrated legacy save: seeded drifter arrival reservoir to {reserve}.");
        }
    }

    public void BootstrapFromRimWorldSettlements()
    {
        if (bootstrapped)
        {
            return;
        }

        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        if (!settings.bootstrapLedgerDuringWorldGeneration)
        {
            bootstrapped = true;
            LastBootstrapSource = "settings";
            LastBootstrapStatus = "disabled";
            if (settings.debugLogging)
            {
                Log.Message("[LivingWorld] Ledger bootstrap skipped by world generation settings.");
            }

            return;
        }

        try
        {
            State = new WorldState(ResolveWorldSeed(rimWorld));
            LastBootstrapError = string.Empty;

            var scan = new WorldObjectScanner().Scan();
            TotalWorldObjects = scan.Summary.TotalWorldObjects;
            VanillaSettlementSourceCount = scan.Summary.VanillaSettlementSourceCount;
            FactionWorldObjectSourceCount = scan.Summary.FactionWorldObjectSourceCount;
            ImportableWorldObjectSourceCount = scan.Summary.ImportableWorldObjectSourceCount;
            ScanErrorCount = scan.Summary.ScanErrorCount;
            RejectedWorldObjectTypes = scan.Summary.RejectedWorldObjectTypes;
            LastWorldSettlementSourceCount = scan.Summary.VanillaSettlementSourceCount;

            if (scan.Candidates.Count == 0)
            {
                LastBootstrapSource = "world-objects";
                LastBootstrapStatus = "empty-source";
                bootstrapped = false;

                if (settings.debugLogging)
                {
                    Log.Warning($"[LivingWorld] Ledger bootstrap found 0 importable world objects. Total={TotalWorldObjects}, vanilla settlements={VanillaSettlementSourceCount}, faction objects={FactionWorldObjectSourceCount}, scan errors={ScanErrorCount}, rejected={RejectedWorldObjectTypes}.");
                }

                return;
            }

            foreach (var settlement in scan.Candidates)
            {
                var faction = Find.FactionManager.AllFactionsListForReading
                    .FirstOrDefault(candidate => candidate.def?.defName == settlement.FactionId);
                var configuredAdults = faction?.def?.humanlikeFaction == true
                    ? settings.baselineHumanSettlementAdults
                    : settings.baselineNonHumanSettlementAdults;
                var baselineAdults = Math.Max(
                    settings.minSettlementAdults,
                    Math.Min(settings.maxSettlementAdults, configuredAdults));
                var worldSettlement = State.CreateSettlement(settlement.StableKey, settlement.Name, settlement.FactionId);
                var productionProfile = RimWorldSettlementProductionProfileFactory.Create(
                    settlement,
                    worldSettlement.Id,
                    faction);

                State.RunInitialWorldSeeding(() =>
                {
                    // initial world seeding is bulk ledger setup, not runtime world history.
                    State.RecordSettlementProductionProfile(productionProfile);

                    for (var i = 0; i < baselineAdults; i++)
                    {
                        var sex = i % 2 == 0 ? Sex.Male : Sex.Female;
                        var age = 18 + (i % 42);
                        State.CreateCitizen($"{settlement.Name} citizen {i + 1}", age, sex, "settler", worldSettlement.Id);
                    }

                    if (settings.foodPerCitizen > 0)
                    {
                        State.AddResource(worldSettlement.Id, FoodResourceKey, baselineAdults * settings.foodPerCitizen);
                    }

                    if (settings.steelPerCitizen > 0)
                    {
                        State.AddResource(worldSettlement.Id, SteelResourceKey, baselineAdults * settings.steelPerCitizen);
                    }
                });

                PlayerKnowledgeService.RecordPublicSettlementInfo(
                    State,
                    worldSettlement.Id,
                    "settlement public disclosure");
            }

            var initialDrifterReservoir = Math.Max(
                0,
                State.Settlements.Count * Math.Max(0, settings.targetWorldPopulationPerSettlement));
            State.RunInitialWorldSeeding(() =>
            {
                State.AddDrifterArrivalReservoir(initialDrifterReservoir, "initial outside-world population reserve");
            });

            // A freshly-generated world is seeded here, so it never needs the legacy migration.
            migratedDrifterReservoir = true;

            bootstrapped = true;
            LastBootstrapSource = "world-objects";
            LastBootstrapStatus = "initialized";
            if (settings.debugLogging)
            {
                var summary = GetSummary();
                if (summary != lastLoggedLedgerSummary)
                {
                    lastLoggedLedgerSummary = summary;
                    Log.Message($"[LivingWorld] Ledger initialized. {summary}");
                }
            }
        }
        catch (Exception ex)
        {
            bootstrapped = false;
            LastBootstrapSource = "world-objects";
            LastBootstrapStatus = "bootstrap-error";
            LastBootstrapError = $"{ex.GetType().Name}: {ex.Message}";
            Log.Error($"[LivingWorld] Ledger bootstrap failed safely: {LastBootstrapError}");
        }
    }

    private void RepairMissingProductionProfilesFromRimWorldSettlements()
    {
        if (!bootstrapped
            || State.Settlements.Count == 0
            || State.ProductionProfiles.Count >= State.Settlements.Count)
        {
            return;
        }

        try
        {
            var scan = new WorldObjectScanner().Scan();
            var repaired = 0;
            foreach (var candidate in scan.Candidates)
            {
                var settlement = State.Settlements.FirstOrDefault(existing => existing.Slug == candidate.StableKey);
                if (settlement == null || State.GetSettlementProductionProfile(settlement.Id) != null)
                {
                    continue;
                }

                var faction = Find.FactionManager.AllFactionsListForReading
                    .FirstOrDefault(existing => existing.def?.defName == candidate.FactionId);
                State.RecordSettlementProductionProfile(
                    RimWorldSettlementProductionProfileFactory.Create(
                        candidate,
                        settlement.Id,
                        faction));
                repaired++;
            }

            if (repaired > 0)
            {
                LastBootstrapStatus = LastBootstrapStatus == "loaded"
                    ? "loaded+production-repair"
                    : LastBootstrapStatus;
                if ((LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging)
                {
                    Log.Message($"[LivingWorld] Repaired {repaired} missing production profiles.");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Production profile repair skipped safely: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static int ResolveWorldSeed(World world)
    {
        var seedString = world.info.seedString;
        return string.IsNullOrWhiteSpace(seedString)
            ? StableSeedFromString("LivingWorld")
            : StableSeedFromString(seedString);
    }

    private static int StableSeedFromString(string seedString)
    {
        unchecked
        {
            const int offsetBasis = (int)2166136261;
            const int prime = 16777619;
            var hash = offsetBasis;
            foreach (var character in seedString)
            {
                hash ^= character;
                hash *= prime;
            }

            return hash == int.MinValue
                ? int.MaxValue
                : Math.Abs(hash);
        }
    }
}
