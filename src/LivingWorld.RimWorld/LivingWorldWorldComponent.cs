using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

public sealed class LivingWorldWorldComponent : WorldComponent
{
    private const int TicksPerDay = 60_000;
    private const int MaxCatchUpSimulationDays = 7;
    private const int BirthIntervalDays = 10;
    private const int AgeIntervalDays = 30;
    private const int NaturalDeathAge = 85;
    private const int MaxNaturalDeathsPerDay = 5;
    private const int PlayerCaravanMarkerContactCheckIntervalTicks = 250;
    private const string FoodResourceKey = "PackagedSurvivalMeal";
    private const string SteelResourceKey = "Steel";
    private const string MedicineResourceKey = "MedicineIndustrial";
    private const string ComponentResourceKey = "ComponentIndustrial";
    private const string SilverResourceKey = "Silver";

    private readonly World rimWorld;
    private bool bootstrapped;
    private bool migratedDrifterReservoir;
    private bool appliedEconomicDiversity;
    private bool migratedVisibleDynamics;
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
    private List<PendingApproachingRaid> approachingRaids = new();
    private int nextApproachRaidId;
    private List<MechClusterNode> mechClusters = new();
    private int nextMechClusterId;
    private List<int> mechClusterSiteNodeIds = new();
    private List<int> mechClusterSiteWorldObjectIds = new();
    private List<PendingApproachingGroup> approachingGroups = new();
    private int nextApproachGroupId;
    private List<LivingWorldSettlementExpansionWorldBinding> settlementExpansionWorldBindings = new();
    private List<string> notifiedPlayerCaravanMarkerContacts = new();
    private int lastPlayerCaravanMarkerContactCheckTick;
    private int lastLivingWorldMarkerContactCheckTick;

    // Collapses the "Ledger initialized" log across the many throwaway component instances RimWorld
    // builds during world-generation previews, so a new game does not spam a dozen identical lines.
    private static string? lastLoggedLedgerSummary;

    public LivingWorldWorldComponent(World world)
        : base(world)
    {
        rimWorld = world;
        Instance = this;
        State = new WorldState(ResolveWorldSeed(rimWorld));
        SettlementSync = new SettlementSyncCoordinator(this);
    }

    public static LivingWorldWorldComponent? Instance { get; private set; }

    public WorldState State { get; private set; }

    public SettlementSyncCoordinator SettlementSync { get; }

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

    // True when Empire (Matathias.Empire) is active. Empire manages the player's vassal colonies under the
    // "PColony" faction, which is NOT Faction.OfPlayer, so the plain IsPlayer bootstrap filter does NOT keep
    // it out on its own — the import eligibility predicate (SettlementImportEligibility) is what excludes it,
    // and BootstrapFromRimWorldSettlements re-checks it here as defense-in-depth. Surfaced so the UI can tell
    // the player who owns what.
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
        MigrateEconomicDiversityForLegacySave();
        MigrateVisibleDynamicsForLegacySave();
        // Reconcile world-map army markers with the loaded ledger so stale markers from before the
        // save are dropped and surviving movements keep their icon.
        SyncArmyWorldObjects();
        EnsureRuinSites();
        SyncApproachingRaidMarkers();
        EnsureMechClusterSites();
        SyncMechClusterMarkers();
        SyncApproachingGroupMarkers();
        if (fromLoad)
        {
            LivingWorldOrphanedLordReferenceCleaner.CleanAllMaps();
        }
        // One-shot: clear ghost ledger entries for settlements removed by other mods before this
        // fix existed, and import/re-faction any that drifted while saved. Safe at load time — every
        // real settlement is present and scannable.
        SettlementSync.ReconcileWithDestructions();
    }

    public override void WorldComponentTick()
    {
        base.WorldComponentTick();

        if (!bootstrapped)
        {
            return;
        }

        // Materialize any travelling raids that have reached the colony — every tick, ahead of the
        // daily-simulation gate below, so arrival lands on time rather than on the next day rollover.
        ProcessApproachingRaidArrivals(Find.TickManager?.TicksGame ?? 0);
        ProcessApproachingGroupArrivals(Find.TickManager?.TicksGame ?? 0);

        var currentTick = Find.TickManager?.TicksGame ?? 0;
        CheckPlayerCaravanMarkerContacts(currentTick);
        CheckLivingWorldMarkerContacts(currentTick);
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

        // Safety net for any add/capture the hooks missed (mods bypassing the standard API). Never
        // destroys — removal is driven solely by the authoritative Remove hook — so a transiently
        // unscannable settlement is never wrongly ruined.
        SettlementSync.ReconcileNonDestructive();

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
        SimulateMechClusters(currentTick, Math.Max(1, simulatedDays));

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
        var activeCropProjects = State.CropStrainProjects.Count(project => project.Status == CropStrainProjectStatus.Active);
        var technologyRecords = State.SettlementTechnologies.Count;
        var playerIntel = State.RaidIntelFacts.Count(fact =>
            fact.TargetKind == RaidIntelTargetKind.PlayerColony && !fact.IsExpired(currentTick));

        Log.Message(
            $"[LivingWorld] day {lastSimulatedDay} (+{simulatedDays}d): settlements {activeSettlements}/{State.Settlements.Count}"
            + $" | pop {pop} | facilities {facilities} | projects {activeProjects} active"
            + $" | animals {animalCohorts} cohorts | breeding {activeBreedingProjects} active"
            + $" | crops {activeCropProjects} active | tech {technologyRecords} records"
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

            var warningText = "LW_RaidWarningLetterText".Translate(
                factionName.Named("faction"),
                sourcePhrase.Named("source")).ToString();
            // Telegraph a hard raid when the faction is wealthy/well-armed (the economy -> raid bridge),
            // so the warning conveys not just "a raid may come" but "and it will hit hard".
            if (FactionRaidStrengthService.WealthRaidMultiplier(State, fact.FactionId) >= 1.15f)
            {
                warningText += " " + "LW_RaidWarningWellArmed".Translate();
            }

            Find.LetterStack?.ReceiveLetter(
                "LW_RaidWarningLetterLabel".Translate(),
                warningText,
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
        CropStrainDriver.SimulateDay(
            State,
            new CropStrainDriverRequest(
                day * TicksPerDay,
                FoodResourceKey,
                MedicineResourceKey));
        TechnologyDiffusionService.SimulateDay(
            State,
            new TechnologyDiffusionRequest(
                day * TicksPerDay));
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
            var flowTarget = PopulationFlowTargetService.Calculate(
                State,
                new PopulationFlowTargetRequest(Math.Max(0, settings.drifterHardCeiling)));

            DrifterArrivalService.SimulateArrivals(
                State,
                new DrifterArrivalRequest(dayTick, flowTarget.TargetPopulation, flowTarget.HardCeiling, settings.maxDrifterArrivalsPerDay));
            DrifterFoundingService.SimulateFounding(
                State,
                new DrifterFoundingRequest(dayTick, settings.drifterMinFounders, settings.drifterLeaderAptitudeThreshold));
            DrifterAssimilationService.SimulateAssimilation(
                State,
                new DrifterAssimilationRequest(dayTick, settings.maxDrifterAssimilationsPerDay));

            cachedTargetPopulation = flowTarget.TargetPopulation;
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
    // factions. The settler action asset ("World/LivingWorld_Settler") is intentionally not drawn
    // here yet because Core expansion currently founds its destination immediately rather than
    // dispatching a travelling settler expedition. Positions animate every frame inside the marker's
    // DrawPos, so this only manages membership.
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
            if (worldObject is WorldObject_LivingWorldArmy marker
                && !string.IsNullOrEmpty(marker.MarkerKey)
                && !marker.MarkerKey.StartsWith(ApproachingRaidRuntime.MarkerKeyPrefix, StringComparison.Ordinal)
                && !marker.MarkerKey.StartsWith(ApproachingGroupRuntime.MarkerKeyPrefix, StringComparison.Ordinal)
                && !marker.MarkerKey.StartsWith(LivingWorldSettlementExpansionWorldBridge.MarkerKeyPrefix, StringComparison.Ordinal))
            {
                // Approaching-raid markers are managed by SyncApproachingRaidMarkers (they are keyed to
                // RW incident state, not ledger travels); this ledger reconcile must not remove them.
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

        LivingWorldSettlementExpansionWorldBridge.Synchronize(State, settlementExpansionWorldBindings);
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
        marker.Tile = originTile;
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

    private void CheckPlayerCaravanMarkerContacts(int now)
    {
        if (now - lastPlayerCaravanMarkerContactCheckTick < PlayerCaravanMarkerContactCheckIntervalTicks)
        {
            return;
        }

        lastPlayerCaravanMarkerContactCheckTick = now;
        var worldObjects = Find.WorldObjects;
        if (worldObjects == null || Current.ProgramState != ProgramState.Playing)
        {
            return;
        }

        var playerCaravans = worldObjects.AllWorldObjects
            .OfType<Caravan>()
            .Where(caravan => caravan.Faction == Faction.OfPlayer)
            .ToList();
        if (playerCaravans.Count == 0)
        {
            return;
        }

        var markers = worldObjects.AllWorldObjects
            .OfType<WorldObject_LivingWorldArmy>()
            .Where(marker => !string.IsNullOrWhiteSpace(marker.MarkerKey))
            .ToList();
        var liveMarkerKeys = new HashSet<string>(
            markers.Select(marker => marker.MarkerKey),
            StringComparer.Ordinal);
        notifiedPlayerCaravanMarkerContacts.RemoveAll(contact =>
        {
            var separator = contact.IndexOf(':');
            return separator < 0
                || separator == contact.Length - 1
                || !liveMarkerKeys.Contains(contact.Substring(separator + 1));
        });
        foreach (var caravan in playerCaravans)
        {
            foreach (var marker in markers)
            {
                var contactKey = $"{caravan.ID}:{marker.MarkerKey}";
                if (notifiedPlayerCaravanMarkerContacts.Contains(contactKey))
                {
                    continue;
                }

                var contactDistance = marker.Tile.Layer.AverageTileSize * 0.75f;
                if (Vector3.Distance(caravan.DrawPos, marker.TravelPosition) > contactDistance)
                {
                    continue;
                }

                notifiedPlayerCaravanMarkerContacts.Add(contactKey);
                var details = marker.DetailsText;
                LivingWorldPlayerCaravanContactService.Handle(State, caravan, marker);
                if ((LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging)
                {
                    Log.Message($"[LivingWorld] Player caravan {caravan.ID} contacted world marker {marker.MarkerKey}: {details}");
                }
            }
        }
    }

    private void CheckLivingWorldMarkerContacts(int now)
    {
        if (now - lastLivingWorldMarkerContactCheckTick < PlayerCaravanMarkerContactCheckIntervalTicks)
        {
            return;
        }

        lastLivingWorldMarkerContactCheckTick = now;
        var worldObjects = Find.WorldObjects;
        if (worldObjects == null || Current.ProgramState != ProgramState.Playing)
        {
            return;
        }

        var markers = worldObjects.AllWorldObjects
            .OfType<WorldObject_LivingWorldArmy>()
            .Where(marker => marker.MarkerKey.StartsWith("army:", StringComparison.Ordinal)
                || marker.MarkerKey.StartsWith("caravan:", StringComparison.Ordinal)
                || marker.MarkerKey.StartsWith("mission:", StringComparison.Ordinal))
            .OrderBy(marker => marker.MarkerKey, StringComparer.Ordinal)
            .ToList();
        var consumed = new HashSet<string>(StringComparer.Ordinal);
        for (var leftIndex = 0; leftIndex < markers.Count; leftIndex++)
        {
            var left = markers[leftIndex];
            if (consumed.Contains(left.MarkerKey))
            {
                continue;
            }

            for (var rightIndex = leftIndex + 1; rightIndex < markers.Count; rightIndex++)
            {
                var right = markers[rightIndex];
                if (consumed.Contains(right.MarkerKey) || !MarkersOverlap(left, right))
                {
                    continue;
                }

                if (!TryResolveMarkerContact(left.MarkerKey, right.MarkerKey, now))
                {
                    continue;
                }

                consumed.Add(left.MarkerKey);
                consumed.Add(right.MarkerKey);
                break;
            }
        }

        if (consumed.Count > 0)
        {
            SyncArmyWorldObjects();
        }
    }

    private bool TryResolveMarkerContact(string leftKey, string rightKey, int now)
    {
        if (TryParseMarkerId(leftKey, "army:", EntityKind.Army, out var leftArmy)
            && TryParseMarkerId(rightKey, "army:", EntityKind.Army, out var rightArmy))
        {
            return ArmyInterceptionService.TryResolvePhysicalContact(State, leftArmy, rightArmy, now);
        }

        if (TryParseMarkerId(leftKey, "army:", EntityKind.Army, out var army)
            && TryParseTrafficMarkerId(rightKey, out var traffic))
        {
            return TransitEncounterService.TryResolvePhysicalContact(State, army, traffic, now);
        }

        if (TryParseMarkerId(rightKey, "army:", EntityKind.Army, out army)
            && TryParseTrafficMarkerId(leftKey, out traffic))
        {
            return TransitEncounterService.TryResolvePhysicalContact(State, army, traffic, now);
        }

        return false;
    }

    private static bool MarkersOverlap(WorldObject_LivingWorldArmy left, WorldObject_LivingWorldArmy right)
    {
        var contactDistance = Math.Min(left.Tile.Layer.AverageTileSize, right.Tile.Layer.AverageTileSize) * 0.55f;
        return Vector3.Distance(left.TravelPosition, right.TravelPosition) <= contactDistance;
    }

    private static bool TryParseTrafficMarkerId(string key, out EntityId id)
    {
        return TryParseMarkerId(key, "caravan:", EntityKind.Caravan, out id)
            || TryParseMarkerId(key, "mission:", EntityKind.Mission, out id);
    }

    private static bool TryParseMarkerId(string key, string prefix, EntityKind kind, out EntityId id)
    {
        id = default;
        return key.StartsWith(prefix, StringComparison.Ordinal)
            && long.TryParse(key.Substring(prefix.Length), out var value)
            && value > 0
            && (id = EntityId.Create(kind, value)).Value > 0;
    }

    // Called by IncidentWorker_LivingWorldFactionRaid when the storyteller fires a faction raid. Turns
    // the raid into a warband marching from the faction's nearest settlement to the colony tile, shown
    // as a world-map marker, and defers the actual raid until it arrives. Returns false (so the raid
    // fires immediately, unchanged) when travelling raids are off, there is no map target, or the
    // faction has no settlement to march from — a raid is never cancelled.
    public bool TryLaunchApproachingRaid(IncidentParms parms, Faction faction)
    {
        try
        {
            var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
            if (!settings.travelingRaidsEnabled)
            {
                return false;
            }

            if (parms?.target is not Map map)
            {
                return false;
            }

            var factionId = faction?.def?.defName;
            if (string.IsNullOrWhiteSpace(factionId))
            {
                return false;
            }

            int targetTile = map.Tile;
            var originTile = NearestFactionSettlementTile(faction!, targetTile);
            if (originTile < 0)
            {
                return false;
            }

            var distance = Find.WorldGrid?.ApproxDistanceInTiles(originTile, targetTile) ?? 0f;
            var travelTicks = ApproachingRaidRuntime.TravelTicksFor(distance);
            var now = Find.TickManager?.TicksGame ?? 0;

            var pending = new PendingApproachingRaid
            {
                FactionDefName = factionId!,
                Points = parms.points,
                TargetTile = targetTile,
                OriginTile = originTile,
                DepartTick = now,
                ArrivalTick = now + travelTicks,
                MarkerKey = $"{ApproachingRaidRuntime.MarkerKeyPrefix}{nextApproachRaidId++}",
                TargetLabel = ResolveColonyLabel(map),
            };
            approachingRaids.Add(pending);
            SyncApproachingRaidMarkers();

            if (Current.ProgramState == ProgramState.Playing)
            {
                var days = Mathf.Max(1, Mathf.RoundToInt(travelTicks / (float)TicksPerDay));
                Find.LetterStack?.ReceiveLetter(
                    "LW_RaidApproachingLabel".Translate(),
                    "LW_RaidApproachingText".Translate(
                        (faction!.Name ?? factionId!).Named("faction"),
                        pending.TargetLabel.Named("colony"),
                        days.Named("days")),
                    LetterDefOf.NegativeEvent,
                    new LookTargets((PlanetTile)originTile));
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Approaching-raid launch failed, firing raid immediately: {ex.Message}");
            return false;
        }
    }

    // Fires any travelling raids that have reached the colony. Cheap: the pending list is at most a
    // handful of entries and this only acts when arrivalTick is reached.
    private void ProcessApproachingRaidArrivals(int now)
    {
        if (approachingRaids.Count == 0)
        {
            return;
        }

        List<PendingApproachingRaid>? arrived = null;
        foreach (var raid in approachingRaids)
        {
            if (raid.ArrivalTick <= now)
            {
                (arrived ??= new List<PendingApproachingRaid>()).Add(raid);
            }
        }

        if (arrived == null)
        {
            return;
        }

        foreach (var raid in arrived)
        {
            approachingRaids.Remove(raid);
            FireArrivedRaid(raid);
        }

        SyncApproachingRaidMarkers();
    }

    // Re-fires the Living World faction raid at its destination map with FiringArrival set so the
    // incident worker skips the travel branch and spawns raiders now. Fail-open: if the map is gone
    // (colony abandoned) or anything throws, the raid simply does not land — nothing is left dangling.
    private void FireArrivedRaid(PendingApproachingRaid raid)
    {
        try
        {
            var map = Find.Maps?.FirstOrDefault(candidate => (int)candidate.Tile == raid.TargetTile);
            if (map == null)
            {
                return;
            }

            var faction = Find.FactionManager?.AllFactionsListForReading
                .FirstOrDefault(candidate => candidate.def?.defName == raid.FactionDefName);
            if (faction == null)
            {
                return;
            }

            var def = DefDatabase<IncidentDef>.GetNamedSilentFail("LivingWorld_FactionRaid");
            if (def?.Worker == null)
            {
                return;
            }

            var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            parms.faction = faction;
            if (raid.Points > 0f)
            {
                parms.points = raid.Points;
            }

            parms.target = map;

            ApproachingRaidRuntime.FiringArrival = true;
            try
            {
                def.Worker.TryExecute(parms);
            }
            finally
            {
                ApproachingRaidRuntime.FiringArrival = false;
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Arrived raid failed to materialize: {ex.Message}");
        }
    }

    // Reconciles the world-map markers for travelling raids with the pending list: a warband marker per
    // in-flight raid, dropped once the raid has landed (or been cancelled). Positions animate each frame
    // inside the marker's DrawPos, so this only manages membership. Kept separate from SyncArmyWorldObjects
    // because these markers track RW incident state, not ledger travels.
    private void SyncApproachingRaidMarkers()
    {
        var worldObjects = Find.WorldObjects;
        if (worldObjects == null)
        {
            return;
        }

        var existing = new Dictionary<string, WorldObject_LivingWorldArmy>(StringComparer.Ordinal);
        foreach (var worldObject in worldObjects.AllWorldObjects)
        {
            if (worldObject is WorldObject_LivingWorldArmy marker
                && marker.MarkerKey.StartsWith(ApproachingRaidRuntime.MarkerKeyPrefix, StringComparison.Ordinal))
            {
                existing[marker.MarkerKey] = marker;
            }
        }

        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        var markerDef = settings.travelingRaidsEnabled
            ? DefDatabase<WorldObjectDef>.GetNamedSilentFail("LivingWorld_ArmyMarker")
            : null;

        var live = new HashSet<string>(StringComparer.Ordinal);
        if (markerDef != null)
        {
            foreach (var raid in approachingRaids)
            {
                if (raid.TargetTile < 0 || raid.OriginTile < 0)
                {
                    continue;
                }

                live.Add(raid.MarkerKey);

                var faction = Find.FactionManager?.AllFactionsListForReading
                    .FirstOrDefault(candidate => candidate.def?.defName == raid.FactionDefName);

                var isNew = !existing.TryGetValue(raid.MarkerKey, out var marker);
                marker ??= (WorldObject_LivingWorldArmy)WorldObjectMaker.MakeWorldObject(markerDef);
                marker.Tile = raid.OriginTile;
                if (faction != null)
                {
                    marker.SetFaction(faction);
                }

                marker.Configure(
                    raid.MarkerKey,
                    "World/LivingWorld_Warband",
                    "LW_MissionKind_RaidParty".Translate(),
                    raid.OriginTile,
                    raid.TargetTile,
                    raid.DepartTick,
                    raid.ArrivalTick,
                    faction?.Name ?? raid.FactionDefName,
                    raid.TargetLabel,
                    0,
                    0,
                    string.Empty,
                    "LW_MissionReason_Raid".Translate().ToString());
                if (isNew)
                {
                    worldObjects.Add(marker);
                }
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

    private WorldObject_LivingWorldArmy? FindApproachMarker(string key)
    {
        return Find.WorldObjects?.AllWorldObjects
            .OfType<WorldObject_LivingWorldArmy>()
            .FirstOrDefault(marker => marker.MarkerKey == key);
    }

    private static int NearestFactionSettlementTile(Faction faction, int targetTile)
    {
        var worldObjects = Find.WorldObjects;
        var grid = Find.WorldGrid;
        if (worldObjects == null || grid == null)
        {
            return -1;
        }

        var best = -1;
        var bestDistance = float.MaxValue;
        foreach (var settlement in worldObjects.Settlements)
        {
            if (settlement?.Faction != faction)
            {
                continue;
            }

            int tile = settlement.Tile;
            if (tile < 0)
            {
                continue;
            }

            var distance = grid.ApproxDistanceInTiles(tile, targetTile);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = tile;
            }
        }

        return best;
    }

    private static string ResolveColonyLabel(Map map)
    {
        var label = map?.Parent?.Label;
        return string.IsNullOrWhiteSpace(label) ? "LW_YourColony".Translate().ToString() : label!;
    }

    // Daily driver for mechanoid complexes: makes sure a couple exist, accumulates awakening pressure
    // from the player colony's wealth and each complex's proximity (the "wealth + proximity" cause), and
    // rouses a complex once its pressure crosses the threshold (with a telegraph letter). Fail-open.
    private void SimulateMechClusters(int tick, int days)
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        if (!settings.mechClustersEnabled)
        {
            SyncMechClusterMarkers();
            return;
        }

        try
        {
            EnsureMechClusters();
            EnsureMechClusterSites();

            var playerMap = Find.AnyPlayerHomeMap;
            var grid = Find.WorldGrid;
            if (playerMap != null && grid != null)
            {
                var wealth = playerMap.wealthWatcher?.WealthTotal ?? 0f;
                int playerTile = playerMap.Tile;
                foreach (var cluster in mechClusters)
                {
                    if (cluster.Awake || cluster.Tile < 0)
                    {
                        continue;
                    }

                    var distance = grid.ApproxDistanceInTiles(cluster.Tile, playerTile);
                    cluster.Pressure += MechClusterRuntime.DailyPressure(wealth, distance) * Math.Max(1, days);
                    if (MechClusterRuntime.ShouldAwaken(cluster.Pressure))
                    {
                        AwakenMechCluster(cluster, tick);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Mech cluster simulation skipped safely: {ex.Message}");
        }

        SyncMechClusterMarkers();
    }

    // Called from the raid patch when a mechanoid raid actually fired: gives it a source. If a complex is
    // already awake the cause is established (its telegraph already fired) and the raid is simply "from"
    // it. Otherwise the nearest dormant complex is roused now — this raid is its awakening.
    public void NotifyMechanoidRaid(IncidentParms parms)
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        if (!settings.mechClustersEnabled)
        {
            return;
        }

        EnsureMechClusters();
        EnsureMechClusterSites();
        if (mechClusters.Count == 0 || mechClusters.Any(cluster => cluster.Awake))
        {
            return;
        }

        var map = parms?.target as Map ?? Find.AnyPlayerHomeMap;
        var grid = Find.WorldGrid;
        if (map == null || grid == null)
        {
            return;
        }

        int playerTile = map.Tile;
        MechClusterNode? nearest = null;
        var best = float.MaxValue;
        foreach (var cluster in mechClusters)
        {
            if (cluster.Tile < 0)
            {
                continue;
            }

            var distance = grid.ApproxDistanceInTiles(cluster.Tile, playerTile);
            if (distance < best)
            {
                best = distance;
                nearest = cluster;
            }
        }

        if (nearest != null)
        {
            AwakenMechCluster(nearest, Find.TickManager?.TicksGame ?? 0);
            EnsureMechClusterSites();
            SyncMechClusterMarkers();
        }
    }

    private void AwakenMechCluster(MechClusterNode cluster, int tick)
    {
        cluster.Awake = true;
        cluster.AwakenTick = tick;

        if (Current.ProgramState == ProgramState.Playing)
        {
            Find.LetterStack?.ReceiveLetter(
                "LW_MechClusterAwakenLabel".Translate(),
                "LW_MechClusterAwakenText".Translate(),
                LetterDefOf.ThreatSmall,
                new LookTargets((PlanetTile)cluster.Tile));
        }
    }

    private void EnsureMechClusters()
    {
        if (mechClusters.Count >= MechClusterRuntime.MaxClusters)
        {
            return;
        }

        var playerMap = Find.AnyPlayerHomeMap;
        if (playerMap == null)
        {
            return;
        }

        int playerTile = playerMap.Tile;
        var guard = 0;
        while (mechClusters.Count < MechClusterRuntime.MaxClusters && guard++ < MechClusterRuntime.MaxClusters + 3)
        {
            if (!TryFindMechClusterTile(playerTile, out var tile))
            {
                break;
            }

            mechClusters.Add(new MechClusterNode
            {
                Id = nextMechClusterId++,
                Tile = tile,
                Awake = false,
            });
        }
    }

    // Finds a valid land tile at a believable distance from the colony. Deterministic-ish scan (seeded by
    // the world seed) over the tile grid, skipping water/impassable/unbuildable tiles and tiles too near
    // or too far, and any already hosting a complex. Bounded so it can never spin.
    private bool TryFindMechClusterTile(int playerTile, out int tile)
    {
        tile = -1;
        var grid = Find.WorldGrid;
        if (grid == null || playerTile < 0)
        {
            return false;
        }

        var count = grid.TilesCount;
        if (count <= 0)
        {
            return false;
        }

        var start = (int)(Math.Abs((State.WorldSeed * 2654435761L) + (nextMechClusterId * 40503L)) % count);
        for (var i = 0; i < 500; i++)
        {
            var candidate = (int)(((long)start + (i * 7919L)) % count);
            var candidateTile = grid[candidate];
            var biome = candidateTile?.PrimaryBiome;
            if (biome == null || candidateTile!.WaterCovered || biome.impassable || !biome.canBuildBase)
            {
                continue;
            }

            var distance = grid.ApproxDistanceInTiles(candidate, playerTile);
            if (distance < MechClusterRuntime.MinClusterDistanceFromPlayer
                || distance > MechClusterRuntime.MaxClusterDistanceFromPlayer)
            {
                continue;
            }

            if (mechClusters.Any(cluster => cluster.Tile == candidate))
            {
                continue;
            }

            tile = candidate;
            return true;
        }

        return false;
    }

    private void EnsureMechClusterSites()
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        if (!settings.mechClustersEnabled)
        {
            return;
        }

        var worldObjects = Find.WorldObjects;
        if (worldObjects == null)
        {
            return;
        }

        var sitePart = ResolveMechClusterSitePart();
        if (sitePart == null)
        {
            return;
        }

        PruneMissingMechClusterSites(worldObjects);

        foreach (var cluster in mechClusters)
        {
            if (cluster.Tile < 0 || HasMechClusterSite(cluster.Id))
            {
                continue;
            }

            try
            {
                var site = SiteMaker.MakeSite(
                    sitePart,
                    cluster.Tile,
                    faction: null,
                    ifHostileThenMustRemainHostile: false,
                    threatPoints: MechClusterThreatPoints(cluster),
                    worldObjectDef: null);
                site.Tile = cluster.Tile;
                worldObjects.Add(site);
                mechClusterSiteNodeIds.Add(cluster.Id);
                mechClusterSiteWorldObjectIds.Add(site.ID);
            }
            catch (Exception ex)
            {
                Log.Warning($"[LivingWorld] Could not create actionable mechanoid complex site at tile {cluster.Tile}: {ex.Message}");
            }
        }
    }

    private static SitePartDef? ResolveMechClusterSitePart()
    {
        return DefDatabase<SitePartDef>.GetNamedSilentFail("MechClusterForceNoConditionCauser")
            ?? DefDatabase<SitePartDef>.GetNamedSilentFail("MechCluster");
    }

    private static float MechClusterThreatPoints(MechClusterNode cluster)
    {
        var pressureBonus = Math.Min(600f, Math.Max(0f, cluster.Pressure) * 0.35f);
        return Math.Max(400f, (cluster.Awake ? 850f : 450f) + pressureBonus);
    }

    private bool HasMechClusterSite(int nodeId)
    {
        return mechClusterSiteNodeIds.Contains(nodeId);
    }

    private void PruneMissingMechClusterSites(WorldObjectsHolder worldObjects)
    {
        for (var i = mechClusterSiteNodeIds.Count - 1; i >= 0; i--)
        {
            var siteObjectId = i < mechClusterSiteWorldObjectIds.Count
                ? mechClusterSiteWorldObjectIds[i]
                : -1;
            var worldObject = siteObjectId >= 0
                ? worldObjects.AllWorldObjects.FirstOrDefault(candidate => candidate.ID == siteObjectId)
                : null;
            var stillExists = worldObject != null && IsMechClusterSite(worldObject);
            if (!stillExists)
            {
                if (worldObject != null)
                {
                    worldObjects.Remove(worldObject);
                }

                mechClusterSiteNodeIds.RemoveAt(i);
                if (i < mechClusterSiteWorldObjectIds.Count)
                {
                    mechClusterSiteWorldObjectIds.RemoveAt(i);
                }
            }
        }

        while (mechClusterSiteWorldObjectIds.Count > mechClusterSiteNodeIds.Count)
        {
            mechClusterSiteWorldObjectIds.RemoveAt(mechClusterSiteWorldObjectIds.Count - 1);
        }
    }

    private static bool IsMechClusterSite(WorldObject worldObject)
    {
        if (worldObject is not Site site)
        {
            return false;
        }

        var partsField = typeof(Site).GetField(
            "parts",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (partsField?.GetValue(site) is not IEnumerable<SitePart> parts)
        {
            return false;
        }

        return parts.Any(part =>
            string.Equals(part.def?.defName, "MechClusterForceNoConditionCauser", StringComparison.Ordinal)
            || string.Equals(part.def?.defName, "MechCluster", StringComparison.Ordinal));
    }

    // Removes legacy fallback markers for mechanoid complexes. A mech cluster must be a real vanilla site
    // with a MechCluster gen step; if that cannot be created, we do not show a fake clickable marker.
    private void SyncMechClusterMarkers()
    {
        var worldObjects = Find.WorldObjects;
        if (worldObjects == null)
        {
            return;
        }

        PruneMissingMechClusterSites(worldObjects);

        foreach (var marker in worldObjects.AllWorldObjects.OfType<WorldObject_MechCluster>().ToList())
        {
            worldObjects.Remove(marker);
        }
    }

    private WorldObject? FindMechClusterSite(int nodeId)
    {
        var index = mechClusterSiteNodeIds.IndexOf(nodeId);
        if (index < 0 || index >= mechClusterSiteWorldObjectIds.Count)
        {
            return null;
        }

        var siteObjectId = mechClusterSiteWorldObjectIds[index];
        return Find.WorldObjects?.AllWorldObjects.FirstOrDefault(worldObject => worldObject.ID == siteObjectId);
    }

    // Called by the group-travel patch when the storyteller fires a neutral group arrival (visitors, and
    // later traders/travellers). Turns it into a group marching from one of the faction's settlements to
    // the colony, shown as a world-map marker, and defers the arrival until it gets there. Returns false
    // (so the incident fires immediately, unchanged) when travelling arrivals are off, there is no map
    // target, or the faction has no settlement to travel from — an arrival is never lost.
    public bool TryLaunchApproachingGroup(IncidentDef? incidentDef, IncidentParms parms, string kindKey)
    {
        try
        {
            var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
            if (!settings.arrivalsTravelEnabled)
            {
                return false;
            }

            var defName = incidentDef?.defName;
            if (string.IsNullOrWhiteSpace(defName) || parms?.target is not Map map)
            {
                return false;
            }

            var faction = parms.faction ?? PickNeutralFactionWithSettlement();
            var factionId = faction?.def?.defName;
            if (faction == null || string.IsNullOrWhiteSpace(factionId))
            {
                return false;
            }

            int targetTile = map.Tile;
            var originTile = NearestFactionSettlementTile(faction, targetTile);
            if (originTile < 0)
            {
                return false;
            }

            var distance = Find.WorldGrid?.ApproxDistanceInTiles(originTile, targetTile) ?? 0f;
            var travelTicks = ApproachingGroupRuntime.TravelTicksFor(distance);
            var now = Find.TickManager?.TicksGame ?? 0;

            var pending = new PendingApproachingGroup
            {
                IncidentDefName = defName!,
                FactionDefName = factionId!,
                Points = parms.points,
                TargetTile = targetTile,
                OriginTile = originTile,
                DepartTick = now,
                ArrivalTick = now + travelTicks,
                MarkerKey = $"{ApproachingGroupRuntime.MarkerKeyPrefix}{nextApproachGroupId++}",
                KindKey = string.IsNullOrWhiteSpace(kindKey) ? "LW_ArrivalKind_Visitors" : kindKey,
                TargetLabel = ResolveColonyLabel(map),
            };
            approachingGroups.Add(pending);
            SyncApproachingGroupMarkers();

            if (Current.ProgramState == ProgramState.Playing)
            {
                var days = Mathf.Max(1, Mathf.RoundToInt(travelTicks / (float)TicksPerDay));
                Find.LetterStack?.ReceiveLetter(
                    "LW_GroupApproachingLabel".Translate(),
                    "LW_GroupApproachingText".Translate(
                        (faction.Name ?? factionId!).Named("faction"),
                        pending.KindKey.Translate().Named("kind"),
                        pending.TargetLabel.Named("colony"),
                        days.Named("days")),
                    LetterDefOf.NeutralEvent,
                    new LookTargets((PlanetTile)originTile));
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Approaching-group launch failed, firing incident immediately: {ex.Message}");
            return false;
        }
    }

    private void ProcessApproachingGroupArrivals(int now)
    {
        if (approachingGroups.Count == 0)
        {
            return;
        }

        List<PendingApproachingGroup>? arrived = null;
        foreach (var group in approachingGroups)
        {
            if (group.ArrivalTick <= now)
            {
                (arrived ??= new List<PendingApproachingGroup>()).Add(group);
            }
        }

        if (arrived == null)
        {
            return;
        }

        foreach (var group in arrived)
        {
            approachingGroups.Remove(group);
            FireArrivedGroup(group);
        }

        SyncApproachingGroupMarkers();
    }

    // Re-fires the neutral-group incident at its destination map with FiringArrival set so the travel
    // patch lets the vanilla worker run and spawn the group now. Fail-open: if the map is gone or anything
    // throws, the group simply does not land.
    private void FireArrivedGroup(PendingApproachingGroup group)
    {
        try
        {
            var map = Find.Maps?.FirstOrDefault(candidate => (int)candidate.Tile == group.TargetTile);
            if (map == null)
            {
                return;
            }

            var faction = Find.FactionManager?.AllFactionsListForReading
                .FirstOrDefault(candidate => candidate.def?.defName == group.FactionDefName);

            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(group.IncidentDefName);
            if (def?.Worker == null)
            {
                return;
            }

            var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
            if (faction != null)
            {
                parms.faction = faction;
            }

            if (group.Points > 0f)
            {
                parms.points = group.Points;
            }

            parms.target = map;

            ApproachingGroupRuntime.FiringArrival = true;
            try
            {
                def.Worker.TryExecute(parms);
            }
            finally
            {
                ApproachingGroupRuntime.FiringArrival = false;
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Arrived group failed to materialize: {ex.Message}");
        }
    }

    private void SyncApproachingGroupMarkers()
    {
        var worldObjects = Find.WorldObjects;
        if (worldObjects == null)
        {
            return;
        }

        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        var markerDef = settings.arrivalsTravelEnabled
            ? DefDatabase<WorldObjectDef>.GetNamedSilentFail("LivingWorld_ArmyMarker")
            : null;

        var existing = new Dictionary<string, WorldObject_LivingWorldArmy>(StringComparer.Ordinal);
        foreach (var worldObject in worldObjects.AllWorldObjects)
        {
            if (worldObject is WorldObject_LivingWorldArmy marker
                && marker.MarkerKey.StartsWith(ApproachingGroupRuntime.MarkerKeyPrefix, StringComparison.Ordinal))
            {
                existing[marker.MarkerKey] = marker;
            }
        }

        var live = new HashSet<string>(StringComparer.Ordinal);
        if (markerDef != null)
        {
            foreach (var group in approachingGroups)
            {
                if (group.TargetTile < 0 || group.OriginTile < 0)
                {
                    continue;
                }

                live.Add(group.MarkerKey);

                var faction = Find.FactionManager?.AllFactionsListForReading
                    .FirstOrDefault(candidate => candidate.def?.defName == group.FactionDefName);

                var isNew = !existing.TryGetValue(group.MarkerKey, out var marker);
                marker ??= (WorldObject_LivingWorldArmy)WorldObjectMaker.MakeWorldObject(markerDef);
                marker.Tile = group.TargetTile;
                if (faction != null)
                {
                    marker.SetFaction(faction);
                }

                marker.Configure(
                    group.MarkerKey,
                    "World/LivingWorld_Trader",
                    group.KindKey.Translate(),
                    group.OriginTile,
                    group.TargetTile,
                    group.DepartTick,
                    group.ArrivalTick,
                    faction?.Name ?? group.FactionDefName,
                    group.TargetLabel,
                    0,
                    0,
                    string.Empty,
                    "LW_MissionReason_Visit".Translate().ToString());
                if (isNew)
                {
                    worldObjects.Add(marker);
                }
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

    private WorldObject_LivingWorldArmy? FindApproachGroupMarker(string key)
    {
        return Find.WorldObjects?.AllWorldObjects
            .OfType<WorldObject_LivingWorldArmy>()
            .FirstOrDefault(marker => marker.MarkerKey == key);
    }

    private static Faction? PickNeutralFactionWithSettlement()
    {
        var worldObjects = Find.WorldObjects;
        if (worldObjects == null)
        {
            return null;
        }

        var player = Faction.OfPlayer;
        foreach (var settlement in worldObjects.Settlements)
        {
            var faction = settlement?.Faction;
            if (faction != null
                && !faction.IsPlayer
                && faction.def?.humanlikeFaction == true
                && (player == null || !faction.HostileTo(player)))
            {
                return faction;
            }
        }

        return null;
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
        return SettlementSlug.ParseTile(slug);
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

                // Surface the ruin as a loot opportunity the player can act on (jump to it), not a
                // silent marker. Only in-game, so loading a save never re-announces old ruins.
                if (Current.ProgramState == ProgramState.Playing)
                {
                    Find.LetterStack?.ReceiveLetter(
                        "LW_RuinSiteLetterLabel".Translate(),
                        "LW_RuinSiteLetterText".Translate(ResolveFactionLabel(ruin.FormerFactionId).Named("faction")),
                        LetterDefOf.PositiveEvent,
                        new LookTargets((PlanetTile)tile));
                }

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
        Scribe_Values.Look(ref appliedEconomicDiversity, "livingWorld_appliedEconomicDiversity", false);
        Scribe_Values.Look(ref migratedVisibleDynamics, "livingWorld_migratedVisibleDynamics", false);
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
        Scribe_Collections.Look(ref approachingRaids, "livingWorld_approachingRaids", LookMode.Deep);
        approachingRaids ??= new List<PendingApproachingRaid>();
        Scribe_Values.Look(ref nextApproachRaidId, "livingWorld_nextApproachRaidId", 0);
        Scribe_Collections.Look(ref mechClusters, "livingWorld_mechClusters", LookMode.Deep);
        mechClusters ??= new List<MechClusterNode>();
        Scribe_Values.Look(ref nextMechClusterId, "livingWorld_nextMechClusterId", 0);
        Scribe_Collections.Look(ref mechClusterSiteNodeIds, "livingWorld_mechClusterSiteNodeIds", LookMode.Value);
        mechClusterSiteNodeIds ??= new List<int>();
        Scribe_Collections.Look(ref mechClusterSiteWorldObjectIds, "livingWorld_mechClusterSiteWorldObjectIds", LookMode.Value);
        mechClusterSiteWorldObjectIds ??= new List<int>();
        Scribe_Collections.Look(ref approachingGroups, "livingWorld_approachingGroups", LookMode.Deep);
        approachingGroups ??= new List<PendingApproachingGroup>();
        Scribe_Values.Look(ref nextApproachGroupId, "livingWorld_nextApproachGroupId", 0);
        Scribe_Collections.Look(
            ref settlementExpansionWorldBindings,
            "livingWorld_settlementExpansionWorldBindings",
            LookMode.Deep);
        settlementExpansionWorldBindings ??= new List<LivingWorldSettlementExpansionWorldBinding>();
        Scribe_Collections.Look(ref notifiedPlayerCaravanMarkerContacts, "livingWorld_notifiedPlayerCaravanMarkerContacts", LookMode.Value);
        notifiedPlayerCaravanMarkerContacts ??= new List<string>();
        Scribe_Values.Look(ref lastPlayerCaravanMarkerContactCheckTick, "livingWorld_lastPlayerCaravanMarkerContactCheckTick", 0);

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
        var citizenCount = SettlementPopulationSeedingService.CalculateAdultCount(
            State.WorldSeed,
            settlement.Id.Value,
            Math.Max(6, settings.baselineHumanSettlementAdults),
            6,
            Math.Max(6, settings.maxSettlementAdults));
        State.RecordSettlementProductionProfile(ApplyEconomicCharacter(SettlementProductionProfile.FromEnvironment(
            settlement.Id,
            new SettlementProductionEnvironment(
                "TemperateForest",
                "SmallHills",
                "Industrial",
                55,
                850,
                21))));

        for (var i = 0; i < citizenCount; i++)
        {
            var sex = i % 2 == 0 ? Sex.Male : Sex.Female;
            var age = SettlementPopulationSeedingService.CalculateAdultAge(State.WorldSeed, settlement.Id.Value, i);
            State.CreateCitizen(
                "LW_DebugCitizenName".Translate((i + 1).Named("index")).ToString(),
                age,
                sex,
                "settler",
                settlement.Id);
        }

        State.AddResource(settlement.Id, FoodResourceKey, citizenCount * Math.Max(1, settings.foodPerCitizen));
        State.AddResource(
            settlement.Id,
            SteelResourceKey,
            ScaleEconomicEndowment(settlement.Id, citizenCount * Math.Max(1, settings.steelPerCitizen)));
        var debugSilver = EconomicSilverEndowment(settlement.Id, citizenCount);
        if (debugSilver > 0)
        {
            State.AddResource(settlement.Id, SilverResourceKey, debugSilver);
        }

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

            // Defense-in-depth for Empire (Matathias.Empire): the scanner's eligibility predicate already
            // excludes the player's "PColony" vassal colonies, but re-check here so a future importer
            // regression can never seed a player-owned settlement into the ledger — where the world war /
            // demography would then simulate famine in, attack, or "capture" a base the player actually owns.
            // Only engages when Empire is active; otherwise the scan candidates pass through untouched.
            var candidates = EmpireIsActive
                ? scan.Candidates
                    .Where(candidate => !string.Equals(
                        candidate.FactionId,
                        SettlementImportEligibility.EmpirePlayerColonyFactionDefName,
                        StringComparison.Ordinal))
                    .ToList()
                : (IReadOnlyList<WorldObjectSettlementCandidate>)scan.Candidates;

            if (EmpireIsActive && candidates.Count != scan.Candidates.Count && settings.debugLogging)
            {
                Log.Warning($"[LivingWorld] Empire active: excluded {scan.Candidates.Count - candidates.Count} player-owned PColony vassal settlement(s) from the Living World ledger.");
            }

            if (candidates.Count == 0)
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

            // Iterate the Empire-filtered `candidates` list (not raw scan.Candidates) so bootstrap keeps
            // main's defense-in-depth PColony exclusion, while reusing the extracted SeedImportedSettlement.
            foreach (var candidate in candidates)
            {
                SeedImportedSettlement(candidate, settings);
            }

            var initialDrifterReservoir = Math.Max(
                0,
                State.Settlements.Count * Math.Max(0, settings.targetWorldPopulationPerSettlement));
            State.RunInitialWorldSeeding(() =>
            {
                State.AddDrifterArrivalReservoir(initialDrifterReservoir, "initial outside-world population reserve");
            });
            SettlementWealthService.RefreshAll(State, SettlementWealthService.DefaultPriceBook);

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

    public void SeedImportedSettlement(WorldObjectSettlementCandidate candidate, LivingWorldSettings settings)
    {
        var faction = Find.FactionManager.AllFactionsListForReading
            .FirstOrDefault(f => f.def?.defName == candidate.FactionId);
        var configuredAdults = faction?.def?.humanlikeFaction == true
            ? settings.baselineHumanSettlementAdults
            : settings.baselineNonHumanSettlementAdults;
        var worldSettlement = State.CreateSettlement(candidate.StableKey, candidate.Name, candidate.FactionId);
        var settlementStableSeed = SettlementPopulationSeedingService.StableSettlementSeed(candidate.StableKey);
        var baselineAdults = SettlementPopulationSeedingService.CalculateAdultCount(
            State.WorldSeed,
            settlementStableSeed,
            configuredAdults,
            settings.minSettlementAdults,
            settings.maxSettlementAdults);
        var baselineChildren = faction?.def?.humanlikeFaction == true
            ? SettlementPopulationSeedingService.CalculateChildCount(
                State.WorldSeed,
                settlementStableSeed,
                baselineAdults)
            : 0;
        var productionProfile = ApplyEconomicCharacter(RimWorldSettlementProductionProfileFactory.Create(
            candidate,
            worldSettlement.Id,
            faction));

        State.RunInitialWorldSeeding(() =>
        {
            // initial world seeding is bulk ledger setup, not runtime world history.
            State.RecordSettlementProductionProfile(productionProfile);

            for (var i = 0; i < baselineAdults; i++)
            {
                var sex = i % 2 == 0 ? Sex.Male : Sex.Female;
                var age = SettlementPopulationSeedingService.CalculateAdultAge(State.WorldSeed, settlementStableSeed, i);
                State.CreateCitizen($"{candidate.Name} citizen {i + 1}", age, sex, "settler", worldSettlement.Id);
            }

            for (var i = 0; i < baselineChildren; i++)
            {
                var sex = i % 2 == 0 ? Sex.Female : Sex.Male;
                var age = SettlementPopulationSeedingService.CalculateChildAge(State.WorldSeed, settlementStableSeed, i);
                State.CreateCitizen($"{candidate.Name} child {i + 1}", age, sex, "child", worldSettlement.Id);
            }

            if (settings.foodPerCitizen > 0)
            {
                State.AddResource(worldSettlement.Id, FoodResourceKey, (baselineAdults + baselineChildren) * settings.foodPerCitizen);
            }

            if (settings.steelPerCitizen > 0)
            {
                State.AddResource(
                    worldSettlement.Id,
                    SteelResourceKey,
                    ScaleEconomicEndowment(worldSettlement.Id, baselineAdults * settings.steelPerCitizen));
            }

            var silverEndowment = EconomicSilverEndowment(worldSettlement.Id, baselineAdults);
            if (silverEndowment > 0)
            {
                State.AddResource(worldSettlement.Id, SilverResourceKey, silverEndowment);
            }

            SettlementBootstrapPrimer.PrimeSettlement(
                State,
                new SettlementBootstrapPrimerRequest(
                    Tick: 0,
                    SettlementId: worldSettlement.Id,
                    FoodResourceKey: FoodResourceKey,
                    SteelResourceKey: SteelResourceKey,
                    ComponentResourceKey: ComponentResourceKey));
        });

        PlayerKnowledgeService.RecordPublicSettlementInfo(
            State,
            worldSettlement.Id,
            "settlement public disclosure");
    }

    // Stamps a freshly created production profile with its deterministic economic character (archetype +
    // economy scale) so settlements diverge economically. Gated by the settings toggle; a no-op when off.
    private SettlementProductionProfile ApplyEconomicCharacter(SettlementProductionProfile profile)
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        return settings.economicDiversityEnabled
            ? SettlementEconomicCharacterService.Apply(profile, State.WorldSeed)
            : profile;
    }

    // Scales a baseline seeding endowment by the settlement's economic character, so prosperous
    // settlements start richer. Off → the baseline is used unchanged.
    private int ScaleEconomicEndowment(EntityId settlementId, int baseline)
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        return settings.economicDiversityEnabled
            ? SettlementEconomicCharacterService.ScaleEndowment(baseline, State.WorldSeed, settlementId.Value)
            : baseline;
    }

    // The starting silver reserve for a settlement, scaled by its economic character. Prosperous
    // settlements begin with real silver, poor ones with little — an immediate day-one wealth spread.
    private int EconomicSilverEndowment(EntityId settlementId, int adults)
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        return settings.economicDiversityEnabled
            ? SettlementEconomicCharacterService.SilverEndowment(adults, State.WorldSeed, settlementId.Value)
            : 0;
    }

    // One-time upgrade for saves created before economic diversity existed: every settlement's profile is
    // still at the untouched default (Balanced, scale 100), so the world reads flat. Apply the rolled
    // character to those default profiles once. Deterministic and idempotent; profiles a system already
    // raised above default (crop tech) are preserved by Apply.
    private void MigrateEconomicDiversityForLegacySave()
    {
        if (appliedEconomicDiversity)
        {
            return;
        }

        appliedEconomicDiversity = true;

        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        if (!settings.economicDiversityEnabled || !bootstrapped)
        {
            return;
        }

        try
        {
            foreach (var profile in State.ProductionProfiles.ToList())
            {
                if (profile.Archetype == ProductionArchetype.Balanced && profile.EconomyScalePercent == 100)
                {
                    State.RecordSettlementProductionProfile(
                        SettlementEconomicCharacterService.Apply(profile, State.WorldSeed));
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Economic-diversity migration skipped safely: {ex.Message}");
        }
    }

    // One-time upgrade for saves bootstrapped before settlements had day-one visible dynamics.
    // It is additive: existing citizens/resources/events are preserved, while missing starter
    // facilities, animal cohorts, wealth snapshots and child cohorts are seeded deterministically.
    private void MigrateVisibleDynamicsForLegacySave()
    {
        if (migratedVisibleDynamics)
        {
            return;
        }

        migratedVisibleDynamics = true;

        if (!bootstrapped || State.Settlements.Count == 0)
        {
            return;
        }

        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        var upgradedSettlements = 0;
        var addedCitizens = 0;

        try
        {
            State.RunInitialWorldSeeding(() =>
            {
                foreach (var settlement in State.Settlements.OrderBy(candidate => candidate.Id.Value))
                {
                    SettlementBootstrapPrimer.PrimeSettlement(
                        State,
                        new SettlementBootstrapPrimerRequest(
                            Tick: 0,
                            SettlementId: settlement.Id,
                            FoodResourceKey: FoodResourceKey,
                            SteelResourceKey: SteelResourceKey,
                            ComponentResourceKey: ComponentResourceKey));

                    addedCitizens += AddMissingLegacyPopulation(settlement, settings);
                    upgradedSettlements++;
                }

                SettlementWealthService.RefreshAll(State, SettlementWealthService.DefaultPriceBook);
            });

            if (settings.debugLogging)
            {
                Log.Message($"[LivingWorld] Migrated legacy save visible dynamics for {upgradedSettlements} settlements, added {addedCitizens} citizens.");
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Visible-dynamics migration skipped safely: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private int AddMissingLegacyPopulation(WorldSettlement settlement, LivingWorldSettings settings)
    {
        var current = State.GetSettlementPopulation(settlement.Id);
        var faction = Find.FactionManager.AllFactionsListForReading
            .FirstOrDefault(candidate => candidate.def?.defName == settlement.FactionId);
        if (faction?.def?.humanlikeFaction != true)
        {
            return 0;
        }

        var stableSeed = SettlementPopulationSeedingService.StableSettlementSeed(settlement.Slug);
        var targetAdults = SettlementPopulationSeedingService.CalculateAdultCount(
            State.WorldSeed,
            stableSeed,
            settings.baselineHumanSettlementAdults,
            settings.minSettlementAdults,
            settings.maxSettlementAdults);
        var targetChildren = SettlementPopulationSeedingService.CalculateChildCount(
            State.WorldSeed,
            stableSeed,
            Math.Max(targetAdults, current.Adults));
        var added = 0;
        var addedAdults = 0;

        for (var i = current.Adults; i < targetAdults; i++)
        {
            var sex = i % 2 == 0 ? Sex.Male : Sex.Female;
            var age = SettlementPopulationSeedingService.CalculateAdultAge(State.WorldSeed, stableSeed, i);
            State.CreateCitizen($"{settlement.Name} citizen {i + 1}", age, sex, "settler", settlement.Id);
            added++;
            addedAdults++;
        }

        for (var i = current.Children; i < targetChildren; i++)
        {
            var sex = i % 2 == 0 ? Sex.Female : Sex.Male;
            var age = SettlementPopulationSeedingService.CalculateChildAge(State.WorldSeed, stableSeed, i);
            State.CreateCitizen($"{settlement.Name} child {i + 1}", age, sex, "child", settlement.Id);
            added++;
        }

        if (settings.foodPerCitizen > 0 && added > 0)
        {
            State.AddResource(settlement.Id, FoodResourceKey, added * settings.foodPerCitizen);
        }

        if (settings.steelPerCitizen > 0 && addedAdults > 0)
        {
            State.AddResource(
                settlement.Id,
                SteelResourceKey,
                ScaleEconomicEndowment(settlement.Id, addedAdults * settings.steelPerCitizen));
        }

        return added;
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
                    ApplyEconomicCharacter(RimWorldSettlementProductionProfileFactory.Create(
                        candidate,
                        settlement.Id,
                        faction)));
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
