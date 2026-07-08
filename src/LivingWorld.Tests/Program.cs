using LivingWorld.Core;

var tests = new List<(string Name, Action Test)>
{
    ("allocates stable sequential citizen ids", TestSequentialCitizenIds),
    ("stores citizens as lightweight records instead of pawns", TestCitizenRecord),
    ("records append-only events when citizens are created", TestCitizenCreatedEvent),
    ("can suppress bulk bootstrap events", TestBulkEventSuppression),
    ("tracks settlement population from assigned citizens", TestSettlementPopulation),
    ("settlement population counts only alive citizens owned by settlement", TestSettlementPopulationExcludesNonAliveOrForeignOwnedCitizens),
    ("queries settlements through settlement query service", TestSettlementQueryService),
    ("reports validation errors for orphan citizens", TestValidationErrors),
    ("reports validation errors for broken ownership and ledgers", TestValidationCatchesOwnershipAndLedgerInvariants),
    ("queries population through public api", TestPublicApiPopulationQuery),
    ("assigns settlement ownership when citizen is created", TestCitizenOwnership),
    ("tracks resource stack ownership", TestResourceOwnership),
    ("tracks resources through resource ledger service", TestResourceLedgerService),
    ("uses typed world resource keys", TestWorldResourceKey),
    ("transfers owned resources to army", TestResourceTransferToArmy),
    ("rejects transfer when owner lacks quantity", TestInsufficientResourceTransfer),
    ("transfers assets through ownership service", TestOwnershipServiceTransfersAssets),
    ("tracks animal cohorts as owned lightweight records", TestAnimalCohortOwnership),
    ("simulates animal ecology growth and pressure", TestAnimalEcologyGrowthAndPressure),
    ("seeds animal cohorts from settlement environment", TestAnimalEcologyDriverSeedsFromSettlementEnvironment),
    ("animal ecology driver is idempotent and feeds domesticated cohorts", TestAnimalEcologyDriverIsIdempotentAndFeedsDomesticatedCohorts),
    ("animal production converts herds and hunting into food", TestAnimalProductionConvertsHerdsAndHuntingIntoFood),
    ("migrates animal cohorts without duplicating population", TestAnimalCohortMigrationConservesPopulation),
    ("serializes animal cohorts", TestAnimalCohortSerialization),
    ("animal selection projects improve cohorts over time", TestAnimalSelectionProjectsImproveCohorts),
    ("animal breeding projects require settlement capabilities", TestAnimalBreedingProjectsRequireCapabilities),
    ("animal incubation projects create ledger cohorts", TestAnimalIncubationProjectsCreateLedgerCohorts),
    ("animal breeding driver starts and completes projects", TestAnimalBreedingDriverStartsAndCompletesProjects),
    ("serializes animal breeding projects", TestAnimalBreedingProjectSerialization),
    ("simulates daily settlement food and births", TestSettlementDailySimulationConsumesFoodAndBirths),
    ("records food shortage and blocks births during starvation", TestSettlementDailySimulationRecordsFoodShortage),
    ("blocks births when housing is full", TestSettlementDailySimulationBlocksBirthsWhenHousingIsFull),
    ("ages citizens and records natural deaths", TestDemographyServiceAgesAndKillsElders),
    ("builds settlement production profile from terrain and technology", TestSettlementProductionProfileUsesTerrainAndTechnology),
    ("calculates and caches settlement and faction wealth", TestSettlementWealthServiceCachesWealth),
    ("refreshes all faction wealth with the default price book", TestSettlementWealthRefreshAllUsesDefaultPrices),
    ("deep production uses archetype labor scale and complexity", TestSettlementProductionUsesDepthModifiers),
    ("virtual trade conserves goods and silver", TestVirtualTradeTransfersGoodsAndSilver),
    ("produces owned resources every day from settlement profile", TestSettlementProductionAddsOwnedResources),
    ("settlement facilities modify production output", TestSettlementFacilitiesModifyProductionOutput),
    ("settlement production status reports effective daily output", TestSettlementProductionStatusUsesEffectiveOutput),
    ("settlement projects consume resources and complete facilities", TestSettlementProjectsConsumeResourcesAndCompleteFacilities),
    ("damaged facilities reduce output and repairs consume resources", TestDamagedFacilitiesReduceOutputAndRepairsConsumeResources),
    ("daily infrastructure driver invests in and completes facilities", TestInfrastructureDriverInvestsInAndCompletesFacilities),
    ("daily infrastructure driver repairs damaged facilities first", TestInfrastructureDriverRepairsDamagedFacilitiesFirst),
    ("serializes settlement production profiles", TestSettlementProductionProfileSerialization),
    ("serializes settlement facilities and projects", TestSettlementFacilityProjectSerialization),
    ("records settlement capabilities and specialist pools", TestSettlementCapabilityAndSpecialistLedger),
    ("develops settlement tier and specialists deliberately", TestSettlementDevelopmentUpgradesTierAndSpecialists),
    ("answers settlement readiness from capabilities and specialists", TestSettlementCapabilityReadiness),
    ("serializes settlement capabilities and specialist pools", TestSettlementCapabilitySerialization),
    ("prevents starving settlements from launching raids", TestStarvingSettlementCannotLaunchRaid),
    ("creates refugees from starving settlements", TestMigrationPressureCreatesRefugee),
    ("moves refugees into stable settlements", TestMigrationCompletesToStableSettlement),
    ("creates migration groups before completing migration", TestMigrationCreatesTravelingGroup),
    ("serializes migration groups", TestMigrationGroupSerialization),
    ("queries ownership through public api", TestPublicApiOwnershipQuery),
    ("plans raid army from real citizens and resources", TestRaidPlannerAllocatesRealAssets),
    ("rejects raid when combatants are insufficient", TestRaidPlannerRejectsInsufficientCombatants),
    ("rejects raid when supplies are insufficient", TestRaidPlannerRejectsInsufficientSupplies),
    ("reserves vanilla raid combatants from faction population", TestRaidPopulationAllocatorReservesFactionCombatants),
    ("caps vanilla raid combatants to available adults", TestRaidPopulationAllocatorCapsToAvailableAdults),
    ("creates raid opportunity from valuable trade intel", TestTradeIntelCreatesRaidOpportunity),
    ("consumes raid opportunity before vanilla raid allocation", TestRaidOpportunityConsumesOnce),
    ("records faction knowledge from trade intel without exact values", TestFactionKnowledgeRecordsTradeIntelAboutPlayer),
    ("queries active faction raid intel facts", TestFactionKnowledgeServiceListsActiveRaidIntelFacts),
    ("expired raid intel does not create new raid intent", TestRaidIntelExpiresAndStopsCreatingNewIntent),
    ("raid preparation reserves real citizens and supplies", TestRaidPreparationReservesRealCitizensAndSupplies),
    ("raid preparation fails without leaking citizens when supplies are insufficient", TestRaidPreparationFailsWithoutLeakingCitizensWhenSuppliesInsufficient),
    ("stale raid preparation returns reserved citizens and supplies", TestStaleRaidPreparationReturnsReservedCitizensAndResources),
    ("launched raid preparation is not expired as stale", TestLaunchedRaidPreparationIsNotExpiredAsStale),
    ("raid preparation terminal lifecycle cannot be reversed", TestRaidPreparationTerminalLifecycleCannotBeReversed),
    ("raid preparation survives save load", TestRaidPreparationSurvivesSaveLoad),
    ("materialization lease reserves concrete citizens", TestMaterializationLeaseReservesConcreteCitizens),
    ("materialization lease blocks double active leasing", TestMaterializationLeaseBlocksDoubleActiveLeasing),
    ("expired materialization lease releases reserved citizens", TestExpiredMaterializationLeaseReleasesReservedCitizen),
    ("materialization lease reconciles pawn fate into ledger", TestMaterializationLeaseReconcilesPawnFate),
    ("pawn fate sync resolves active materialization lease", TestPawnFateSyncResolvesMaterializationLease),
    ("materialization lease survives save load", TestMaterializationLeaseSurvivesSaveLoad),
    ("records sold goods into faction settlement ledger", TestTradeLedgerSettlementReceivesSoldGoods),
    ("records purchased goods leaving faction settlement ledger", TestTradeLedgerSettlementProvidesPurchasedGoods),
    ("keeps trade intel when faction has no ledger settlement", TestTradeLedgerNoSettlementFallsBackToIntel),
    ("records public settlement knowledge as estimates", TestPublicSettlementKnowledgeUsesEstimates),
    ("updates settlement knowledge from traders with confidence", TestTraderSettlementKnowledgeUpdatesConfidence),
    ("records production knowledge without exact values unless directly visited", TestProductionKnowledgeVisibility),
    ("does not downgrade direct settlement knowledge with weaker intel", TestSettlementKnowledgeDoesNotDowngradeDirectVisit),
    ("marks old settlement knowledge as stale", TestSettlementKnowledgeFreshness),
    ("binds generated raid pawns to reserved citizens", TestRaidPawnBindingLinksPawnsToCitizens),
    ("marks bound raid citizen dead when pawn dies", TestRaidPawnDeathMarksCitizenDead),
    ("marks bound raid citizen prisoner when captured", TestRaidPawnCaptureMarksCitizenPrisoner),
    ("returns surviving raid citizen to source settlement when pawn exits", TestRaidPawnReturnMovesCitizenHome),
    ("records a resolved raid outcome after all bound pawns are resolved", TestRaidOutcomeRecordedWhenRaidResolves),
    ("does not count a downed raider despawn as a safe return", TestDownedRaiderExitIsNotCountedAsReturn),
    ("marks a lost downed raider as missing and resolves the raid", TestRaidPawnMissingMarksCitizenMissingAndResolvesRaid),
    ("returns undeployed reserved combatants to their settlement", TestUndeployedReservedCombatantsReturnHome),
    ("leaves deployed and resolved raiders alone when releasing reserves", TestReleaseUndeployedReservesLeavesDeployedAndResolvedAlone),
    ("returns orphaned reserves from an aborted raid", TestAbortedRaidReleaseReturnsOrphanedReserves),
    ("runs a vanilla raid from real population without an intel opportunity", TestVanillaRaidInterceptedWithoutOpportunity),
    ("leaves a vanilla raid untouched when the faction has no living world population", TestVanillaRaidPassesThroughWithoutPopulation),
    ("consumes a standing opportunity when intercepting a vanilla raid", TestVanillaRaidConsumesOpportunityWhenPresent),
    ("frees orphaned reserves before intercepting a new vanilla raid", TestVanillaRaidInterceptionFreesPriorOrphans),
    ("drifter arrival spends a finite external reservoir", TestDrifterArrivalSpendsFiniteReservoir),
    ("drifter reservoir survives save load", TestDrifterReservoirSerializationRoundTrip),
    ("adds drifters toward the target world population in metered steps", TestDrifterArrivalFillsTowardTarget),
    ("idles the arrival tap when the world already meets its target", TestDrifterArrivalIdlesWhenWorldPopulated),
    ("never pushes world population past the hard ceiling", TestDrifterArrivalRespectsHardCeiling),
    ("adds no drifters when the arrival tap is disabled", TestDrifterArrivalDisabled),
    ("records arrivals as unaffiliated drifters with events", TestDrifterArrivalRecordsUnaffiliated),
    ("materializes a pooled drifter and drains the pool", TestMaterializeDrifterDrainsPool),
    ("records an unpooled arrival with net-zero pool change", TestMaterializeNewArrivalRecordsWithoutGrowingPool),
    ("assimilates drifters into settlements as citizens", TestDrifterAssimilationJoinsSettlement),
    ("spreads assimilation toward the least populated settlement", TestDrifterAssimilationSpreadsAcrossSettlements),
    ("leaves drifters in the pool when there is no settlement to join", TestDrifterAssimilationWithoutSettlement),
    ("respects the per-step assimilation cap", TestDrifterAssimilationRespectsCap),
    ("keeps world population conserved across arrival then assimilation", TestDrifterArrivalThenAssimilationConservesPopulation),
    ("founds a settlement when a capable organizer leads enough drifters", TestDrifterFoundingCreatesSettlement),
    ("founds a raider band when the capable leader is a fighter", TestDrifterFoundingCreatesRaiderBand),
    ("does not found without a capable leader", TestDrifterFoundingNeedsCapableLeader),
    ("does not found without enough drifters", TestDrifterFoundingNeedsEnoughDrifters),
    ("collapses factions with no living citizens", TestFactionLifecycleCollapsesEmptyFaction),
    ("keeps factions alive while prisoners or migrants exist", TestFactionLifecycleCountsNonResidentSurvivors),
    ("collapses each faction only once", TestFactionLifecycleIsIdempotent),
    ("serializes collapsed faction records", TestFactionLifecycleSerializationRoundTrip),
    ("keeps the player faction out of lifecycle collapse", TestFactionLifecycleSkipsPlayerFaction),
    ("serializes player faction identity", TestPlayerFactionIdentitySerialization),
    ("computes settlement combat power from living adults", TestSettlementPowerFromLivingAdults),
    ("keeps derived population aggregates in sync with citizen lifecycle", TestDerivedAggregatesTrackPopulationLifecycle),
    ("keeps derived combat aggregates in sync with raid lifecycle", TestDerivedAggregatesTrackRaidLifecycle),
    ("prospering settlements develop housing over time", TestSettlementDevelopmentGrowsHousing),
    ("wires settlement development into the daily tick", TestRimWorldSettlementDevelopmentWiring),
    ("wires stale raid preparation cleanup into the daily tick", TestRimWorldDailyTickReleasesExpiredRaidPreparations),
    ("diminishes settlement combat power past the threshold", TestSettlementPowerDiminishesPastThreshold),
    ("maps faction behavior archetypes to profiles", TestFactionBehaviorProfiles),
    ("excludes passive faction behaviors from world war", TestFactionBehaviorNonParticipants),
    ("army arrives at its target when the eta passes", TestArmyMovementArrivesOnEta),
    ("army movement survives a save/load round trip", TestArmyMovementSerializationRoundTrip),
    ("prunes old resolved army movements without losing history", TestArmyMovementPrunesOldResolvedMovements),
    ("attacker captures a weaker settlement and conserves population", TestBattleAttackerCapturesWeakSettlement),
    ("attacker survivors occupy captured settlement", TestBattleAttackerSurvivorsOccupyCapturedSettlement),
    ("defender holds and the beaten army stands down", TestBattleDefenderHoldsAndArmyStandsDown),
    ("attacker survivors return home after failed attack", TestBattleAttackerSurvivorsReturnHomeAfterDefeat),
    ("battle applies faction combat behavior multiplier", TestBattleAppliesFactionCombatBehaviorMultiplier),
    ("world battle against the player faction is blocked for materialization", TestBattleAgainstPlayerFactionIsBlocked),
    ("opposing armies intercept each other in transit", TestOpposingArmiesInterceptInTransit),
    ("faction behavior survives a save/load round trip", TestFactionBehaviorPersists),
    ("warmonger with power and an enemy plans a warband", TestFactionActionPlannerWarband),
    ("warmonger scouts before attacking an unknown enemy", TestFactionActionPlannerScoutsBeforeUnknownWarTarget),
    ("warmongers spread targets instead of dogpiling the lowest id", TestFactionActionPlannerSpreadsEnemyTargets),
    ("warmonger does not target allied settlements", TestFactionActionPlannerSkipsAlliedTargets),
    ("warmonger does not target the player faction", TestFactionActionPlannerSkipsPlayerFactionTarget),
    ("non-combat world actions do not target the player faction", TestWorldWarNonCombatActionsSkipPlayerFaction),
    ("cautious faction can choose deliberate development", TestFactionActionPlannerChoosesDevelop),
    ("action planner skips passive and powerless factions", TestFactionActionPlannerFiltersPassive),
    ("irreconcilable factions stay hostile despite goodwill", TestDiplomacyIrreconcilableStaysHostile),
    ("faction goodwill drifts back toward neutral", TestDiplomacyGoodwillDrifts),
    ("aggression and relations survive a save/load round trip", TestDiplomacyPersists),
    ("world war launches a warband and resolves it into a capture", TestWorldWarLaunchesAndResolvesWarband),
    ("repeated losses increase faction war exhaustion", TestRepeatedLossesIncreaseFactionWarExhaustion),
    ("conflict claim tracks captured settlement", TestConflictClaimTracksCapturedSettlement),
    ("player attack pressures the victim's wars as a third party", TestPlayerInterventionPressuresVictimWars),
    ("alliance forms with an at-war faction and credits on player attack", TestAllianceFormsAndCreditsOnPlayerAttack),
    ("a decided war resolves and rewards the player's ally victory", TestWarResolvesAndRewardsPlayerVictory),
    ("daily tick drives war resolution and victory rewards", TestRimWorldWarResolutionAndVictoryWiring),
    ("truce prevents new warbands until expired", TestTrucePreventsNewWarbandsUntilExpired),
    ("war refugees enter finite population flow", TestWarRefugeesEnterFinitePopulationFlow),
    ("warband cooldown paces a faction's attacks", TestWorldWarWarbandCooldownThrottlesLaunches),
    ("expansionist faction founds a colony from its population", TestWorldWarExpansionistFoundsColony),
    ("expansion rejects empty or insufficient colonies", TestExpandSettlementRejectsEmptyOrInsufficientSettlers),
    ("expansion creates unique colony slugs", TestWorldWarExpansionUsesUniqueColonySlugs),
    ("world war caravan transfers real settlement goods", TestWorldWarCaravanTransfersRealGoods),
    ("persistent caravan preserves cargo through save load", TestPersistentCaravanSerialization),
    ("destroying a persistent caravan removes its cargo", TestDestroyPersistentCaravanRemovesCargo),
    ("prunes terminal caravans after the retention window", TestCaravanPruneRemovesTerminalCaravans),
    ("world mission survives save load and arrives", TestWorldMissionSurvivesSaveLoadAndArrives),
    ("persistent caravan still delivers after load", TestPersistentCaravanArrivesAfterLoad),
    ("derived aggregates track capture and expansion", TestDerivedAggregatesTrackCaptureAndExpansion),
    ("destroyed settlement leaves ruin and refugees", TestDestroyedSettlementLeavesRuinAndRefugees),
    ("relocation moves citizens and resources through migration group", TestRelocationMovesCitizensAndResourcesThroughMigrationGroup),
    ("ruin can be reclaimed without duplicating resources", TestRuinCanBeReclaimedWithoutDuplicatingResources),
    ("old inactive ruins can be pruned after history is recorded", TestOldInactiveRuinsCanBePrunedAfterHistoryIsRecorded),
    ("daily lifecycle driver relocates starving settlements", TestLifecycleDriverRelocatesStarvingSettlements),
    ("daily lifecycle driver destroys settlements after faction collapse", TestLifecycleDriverDestroysCollapsedFactionSettlements),
    ("daily lifecycle driver prunes inactive ruins", TestLifecycleDriverPrunesInactiveRuins),
    ("migrates the drifter reservoir for legacy saves", TestRimWorldDrifterReservoirLegacyMigration),
    ("world war develop action invests in a settlement", TestWorldWarDevelopActionInvestsInSettlement),
    ("world war scouting records settlement intel", TestWorldWarScoutingRecordsIntel),
    ("world war diplomat changes faction goodwill", TestWorldWarDiplomatChangesGoodwill),
    ("world war non-warband effects survive save load", TestWorldWarNonWarbandEffectsPersistThroughSaveLoad),
    ("world war service is split into action executors", TestWorldWarServiceSplitExecutors),
    ("wires world war into the daily tick behind the rim war flag", TestRimWorldWorldWarIntegration),
    ("shows world war consequences in the main tab", TestRimWorldWorldWarMainTab),
    ("sends rate-limited world war letters behind the flag", TestRimWorldWorldWarNotifications),
    ("announces newly declared NPC wars with a persisted letter", TestRimWorldConflictLetters),
    ("sends a raid consequence letter after a raid resolves", TestRimWorldRaidConsequenceLetter),
    ("routes the custom faction raid through a prepared expedition", TestRimWorldRaidRoutesThroughPreparation),
    ("warns the player from player-targeted raid intel", TestRimWorldRaidWarningFromIntel),
    ("releases stale raid preparations and materialization leases daily", TestRimWorldReleasesStaleReservations),
    ("drives infrastructure and lifecycle simulation from the daily tick", TestRimWorldDailyTickRunsSimulationDrivers),
    ("drives animal ecology from the daily tick", TestRimWorldDailyTickRunsAnimalEcologyDriver),
    ("adds a safe world-map speed test override", TestRimWorldWorldMapSpeedTestOverride),
    ("detects Empire and surfaces the interop note", TestRimWorldEmpireInterop),
    ("shows world economy bands in the main tab", TestRimWorldWorldEconomyMainTab),
    ("summarizes world conflicts in the main tab", TestRimWorldWorldConflictsSection),
    ("shows factions watching the player in the main tab", TestRimWorldWatchersSection),
    ("draws faction icons in the main tab", TestRimWorldMainTabFactionIcons),
    ("serializes and restores Living World state", TestWorldStateSerializationRoundTrip),
    ("serializes citizens in compact save block", TestWorldStateSerializesCitizensCompactly),
    ("serializes ownership and events in compact save blocks", TestWorldStateSerializesOwnershipAndEventsCompactly),
    ("loads legacy per-citizen save blocks", TestWorldStateLoadsLegacyCitizenElements),
    ("serializes and restores drifters", TestDrifterSerializationRoundTrip),
    ("defines RimWorld source mod metadata", TestRimWorldSourceModMetadata),
    ("defines RimWorld 1.6 load folders", TestRimWorldLoadFolders),
    ("defines RimWorld loader project", TestRimWorldLoaderProject),
    ("defines RimWorld Mod entrypoint", TestRimWorldModEntrypoint),
    ("defines RimWorld local install script", TestRimWorldInstallScript),
    ("multi-targets Core for RimWorld runtime", TestCoreTargetsRimWorldRuntime),
    ("wires RimWorld loader to Core", TestRimWorldLoaderReferencesCore),
    ("defines Living World world component", TestRimWorldWorldComponent),
    ("defines Living World main tab", TestRimWorldMainTab),
    ("limits Living World main tab rendering work", TestRimWorldMainTabLimitsRenderingWork),
    ("adds Living World settlement population to inspect panel", TestRimWorldSettlementInspectPatch),
    ("surfaces facilities and projects in the inspect panel with fog-of-war gating", TestRimWorldSettlementFacilitiesInspection),
    ("surfaces settlement animal cohorts in the inspect panel", TestRimWorldSettlementAnimalsInspection),
    ("patches vanilla enemy raids into Living World population", TestRimWorldRaidIncidentPatch),
    ("patches generated raid pawns into Living World citizens", TestRimWorldRaidPawnGenerationPatch),
    ("patches pawn death into Living World casualties", TestRimWorldPawnKillPatch),
    ("patches pawn capture into Living World prisoners", TestRimWorldPawnCapturePatch),
    ("patches pawn exit into Living World raid returns", TestRimWorldPawnExitPatch),
    ("defines identity based pawn sync service", TestRimWorldPawnIdentityService),
    ("pawn death sync checks identity comp before thing id", TestRimWorldPawnKillPatchUsesIdentity),
    ("pawn capture sync checks identity comp before thing id", TestRimWorldPawnCapturePatchUsesIdentity),
    ("pawn exit sync checks identity comp before thing id", TestRimWorldPawnExitPatchUsesIdentity),
    ("defines core pawn fate sync service", TestCorePawnFateSyncService),
    ("rimworld inbound patches use pawn sync service", TestRimWorldInboundPatchesUsePawnSyncService),
    ("records trade intel from RimWorld trade dialog", TestRimWorldTradeIntelPatch),
    ("defines Living World main button def", TestRimWorldMainButtonDef),
    ("defines a pawn identity comp round-tripping the ledger id", TestRimWorldIdentityComp),
    ("registers pawn identity comp on human ThingDef", TestRimWorldIdentityCompThingDefPatch),
    ("declares Harmony dependency and reference", TestRimWorldHarmonyDependency),
    ("patches world generation settings page", TestRimWorldWorldGenSettingsPatch),
    ("defines world generation settings window", TestRimWorldWorldGenSettingsWindow),
    ("groups mod settings by player intent", TestLivingWorldSettingsAreGroupedByPlayerIntent),
    ("surfaces compatibility cede state in settings", TestCompatibilitySettingsSurfaceCedenceState),
    ("has EN/RU keys for grouped settings", TestLivingWorldSettingsHaveRussianAndEnglishKeys),
    ("shows world-war armies as world-map markers", TestRimWorldWorldArmyMarker),
    ("shows destroyed settlements as world-map ruin markers", TestRimWorldRuinWorldObjectMarker),
    ("shows a columnar population and economy table", TestRimWorldEconomyWindow),
    ("shows a settlement observer window for growth and projects", TestRimWorldSettlementObserverWindow),
    ("defines drifter-flow settings persisted in ExposeData", TestRimWorldDrifterFlowSettings),
    ("draws drifter-flow settings with localized labels", TestRimWorldDrifterFlowDrawer),
    ("uses world generation settings during bootstrap", TestWorldComponentUsesWorldGenSettings),
    ("uses RimWorld world seed for deterministic state", TestWorldComponentUsesRimWorldWorldSeed),
    ("catches up missed daily simulations with a cap", TestWorldComponentCatchesUpMissedSimulationDays),
    ("marks bootstrap as initial world seeding", TestWorldComponentUsesInitialWorldSeedingBoundary),
    ("suppresses noisy bootstrap citizen events", TestWorldComponentSuppressesBootstrapEventSpam),
    ("explains empty Living World ledger in main tab", TestRimWorldMainTabExplainsEmptyLedger),
    ("scans RimWorld world objects for bootstrap candidates", TestRimWorldWorldObjectScanner),
    ("imports only whitelisted world object types", TestRimWorldWorldObjectScannerUsesImporterWhitelist),
    ("uses explicit world object scanner sorting", TestRimWorldWorldObjectScannerUsesExplicitSorting),
    ("keeps Living World bootstrap failures inside diagnostics", TestRimWorldBootstrapFailureDiagnostics),
    ("reports detailed world object bootstrap diagnostics", TestRimWorldDetailedBootstrapDiagnostics),
    ("wires Living World state into RimWorld saves", TestRimWorldWorldComponentPersistsLedger),
    ("defines English and Russian keyed translations", TestRimWorldKeyedTranslations),
    ("keeps English and Russian keyed translations in parity", TestKeyedLanguageParity),
    ("keeps Odyssey Russian faction namer grammar valid", TestRimWorldRussianOdysseyRulePackOverride),
    ("uses translations in RimWorld UI", TestRimWorldUiUsesTranslations),
    ("defines the drifter arrival incident def", TestRimWorldDrifterArrivalIncidentDef),
    ("defines the drifter arrival incident worker", TestRimWorldDrifterArrivalWorker),
    ("drifter arrival requires a pooled drifter before firing", TestDrifterArrivalGateRequiresPooledDrifter),
    ("drifter arrival validates edge spawn cells", TestRimWorldDrifterArrivalWorkerValidatesSpawnCell),
    ("defines Russian incident def localization", TestRimWorldIncidentDefRussianLocalization),
    ("defines the faction raid incident def", TestRimWorldFactionRaidIncidentDef),
    ("defines the faction raid incident worker", TestRimWorldFactionRaidWorker),
    ("disables the faction raid incident while Rim War is active", TestRimWorldFactionRaidHonorsRimWarGuard),
    ("binds custom raid pawns to identity comp", TestRimWorldRaidPawnGenerationAttachesIdentity),
    ("materializes settlement visitors as ledger citizens via leases", TestRimWorldSettlementVisitMaterialization),
    ("player defeat of an NPC settlement registers a conflict", TestRimWorldPlayerAttackRegistersConflict),
    ("settlement visit lease resolves through the pawn fate sync", TestSettlementVisitLeaseResolvesThroughPawnSync),
    ("localizes faction raid incident", TestRimWorldFactionRaidLocalization),
    ("documents custom raid primary path and legacy fallback", TestRaidPrimaryPathAndFallbackContract),
};

var failures = new List<string>();

foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{name}: {ex.Message}");
        Console.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

if (failures.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine($"{failures.Count} test(s) failed.");
    Environment.Exit(1);
}

Console.WriteLine();
Console.WriteLine($"{tests.Count} test(s) passed.");

static void TestSequentialCitizenIds()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "pirates");

    var first = state.CreateCitizen("Ada", 20, Sex.Female, "farmer", settlement.Id);
    var second = state.CreateCitizen("Borin", 31, Sex.Male, "soldier", settlement.Id);

    AssertEqual("Citizen:1", first.Id.ToString());
    AssertEqual("Citizen:2", second.Id.ToString());
}

static void TestCitizenRecord()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("evos", "Evos Village", "civil");
    var citizen = state.CreateCitizen("Nara", 42, Sex.Female, "doctor", settlement.Id);

    AssertEqual("Nara", citizen.Name);
    AssertEqual(42, citizen.Age);
    AssertEqual(Sex.Female, citizen.Sex);
    AssertEqual("doctor", citizen.Profession);
    AssertEqual(CitizenStatus.Alive, citizen.Status);
    AssertEqual(settlement.Id, citizen.SettlementId);
}

static void TestCitizenCreatedEvent()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "pirates");
    var citizen = state.CreateCitizen("Kira", 27, Sex.Female, "soldier", settlement.Id);

    var created = state.Events.Single(e => e.Kind == WorldEventKind.CitizenCreated);

    AssertEqual(citizen.Id, created.SubjectId);
    AssertEqual("CitizenCreated", created.Kind.ToString());
    AssertEqual(0, created.Tick);
}

static void TestBulkEventSuppression()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "pirates");
    var eventsBeforeBulk = state.Events.Count;

    state.RunWithoutEvents(() =>
    {
        state.CreateCitizen("Quiet Citizen", 27, Sex.Female, "soldier", settlement.Id);
        state.AddResource(settlement.Id, "Steel", 100);
    });

    AssertEqual(eventsBeforeBulk, state.Events.Count);
    AssertEqual(1, state.Citizens.Count);
    AssertEqual(100, state.GetOwnedResourceQuantity(settlement.Id, "Steel"));
}

static void TestSettlementPopulation()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "pirates");

    state.CreateCitizen("Child", 8, Sex.Male, "child", settlement.Id);
    state.CreateCitizen("Adult", 40, Sex.Female, "farmer", settlement.Id);
    state.CreateCitizen("Elder", 71, Sex.Male, "elder", settlement.Id);

    var population = state.GetSettlementPopulation(settlement.Id);

    AssertEqual(3, population.Total);
    AssertEqual(1, population.Children);
    AssertEqual(1, population.Adults);
    AssertEqual(1, population.Elderly);
}

static void TestSettlementPopulationExcludesNonAliveOrForeignOwnedCitizens()
{
    var settlementId = EntityId.Create(EntityKind.Settlement, 1);
    var armyId = EntityId.Create(EntityKind.Army, 1);
    var citizens = new[]
    {
        new WorldCitizen(EntityId.Create(EntityKind.Citizen, 1), "Alive", 30, Sex.Female, "settler", settlementId, CitizenStatus.Alive),
        new WorldCitizen(EntityId.Create(EntityKind.Citizen, 2), "Dead", 31, Sex.Male, "settler", settlementId, CitizenStatus.Dead),
        new WorldCitizen(EntityId.Create(EntityKind.Citizen, 3), "Missing", 32, Sex.Female, "settler", settlementId, CitizenStatus.Missing),
        new WorldCitizen(EntityId.Create(EntityKind.Citizen, 4), "Prisoner", 33, Sex.Male, "settler", settlementId, CitizenStatus.Prisoner),
        new WorldCitizen(EntityId.Create(EntityKind.Citizen, 5), "Deployed", 34, Sex.Female, "soldier", settlementId, CitizenStatus.Alive)
    };
    var state = WorldState.FromSnapshot(new WorldStateSnapshot(
        12345,
        0,
        new[] { new WorldSettlement(settlementId, "north-camp", "Northern Camp", "pirates") },
        citizens,
        new[] { new WorldArmy(armyId, "Raid", "pirates", settlementId) },
        Array.Empty<WorldMigrationGroup>(),
        Array.Empty<WorldIntelReport>(),
        Array.Empty<KnownSettlementInfo>(),
        Array.Empty<RaidOpportunity>(),
        Array.Empty<RaidPawnLink>(),
        Array.Empty<WorldRaidOutcome>(),
        Array.Empty<SettlementProductionProfile>(),
        Array.Empty<WorldFactionRecord>(),
        new[]
        {
            new OwnershipRecord(citizens[0].Id, settlementId),
            new OwnershipRecord(citizens[1].Id, settlementId),
            new OwnershipRecord(citizens[2].Id, settlementId),
            new OwnershipRecord(citizens[3].Id, settlementId),
            new OwnershipRecord(citizens[4].Id, armyId),
            new OwnershipRecord(armyId, settlementId)
        },
        Array.Empty<ResourceStack>(),
        Array.Empty<WorldEvent>(),
        Array.Empty<Drifter>()));

    var population = state.GetSettlementPopulation(settlementId);

    AssertEqual(1, population.Total);
    AssertEqual(1, population.Adults);
}

static void TestSettlementQueryService()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "pirates");
    state.CreateCitizen("Worker", 30, Sex.Female, "settler", settlement.Id);
    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 6);
    state.RecordSettlementProductionProfile(SettlementProductionProfile.FromEnvironment(
        settlement.Id,
        new SettlementProductionEnvironment("TemperateForest", "SmallHills", "Industrial", 55, 850, 21)));

    var population = SettlementQueryService.GetPopulation(state, settlement.Id);
    var food = SettlementQueryService.GetFoodStatus(state, settlement.Id, "PackagedSurvivalMeal", 1);
    var migration = SettlementQueryService.GetMigrationStatus(state, settlement.Id, "PackagedSurvivalMeal", 1);
    var production = SettlementQueryService.GetProductionStatus(state, settlement.Id);

    AssertEqual(1, population.Total);
    AssertEqual(6, food.Food);
    AssertEqual(false, migration.ShouldCreateRefugees);
    AssertEqual(1, production.AdultWorkers);
}

static void TestValidationErrors()
{
    var state = new WorldState(12345);
    var missingSettlement = EntityId.Create(EntityKind.Settlement, 404);

    state.ImportCitizen(new WorldCitizen(
        EntityId.Create(EntityKind.Citizen, 1),
        "Orphan",
        30,
        Sex.Male,
        "wanderer",
        missingSettlement,
        CitizenStatus.Alive));

    var errors = state.Validate().ToList();

    AssertEqual(2, errors.Count);
    AssertContains("Citizen Citizen:1 references missing settlement Settlement:404.", string.Join(Environment.NewLine, errors));
    AssertContains("Ownership owner Settlement:404 does not exist.", string.Join(Environment.NewLine, errors));
}

static void TestValidationCatchesOwnershipAndLedgerInvariants()
{
    var settlementId = EntityId.Create(EntityKind.Settlement, 1);
    var duplicateSettlementId = EntityId.Create(EntityKind.Settlement, 2);
    var armyId = EntityId.Create(EntityKind.Army, 1);
    var citizenId = EntityId.Create(EntityKind.Citizen, 1);
    var missingCitizenId = EntityId.Create(EntityKind.Citizen, 99);
    var missingSettlementId = EntityId.Create(EntityKind.Settlement, 99);
    var state = WorldState.FromSnapshot(new WorldStateSnapshot(
        12345,
        0,
        new[]
        {
            new WorldSettlement(settlementId, "north-camp", "Northern Camp", "pirates"),
            new WorldSettlement(duplicateSettlementId, "north-camp", "Duplicate Northern Camp", "pirates")
        },
        new[] { new WorldCitizen(citizenId, "Alive", 30, Sex.Female, "settler", settlementId, CitizenStatus.Alive) },
        new[] { new WorldArmy(armyId, "Raid", "pirates", settlementId) },
        Array.Empty<WorldMigrationGroup>(),
        Array.Empty<WorldIntelReport>(),
        Array.Empty<KnownSettlementInfo>(),
        Array.Empty<RaidOpportunity>(),
        Array.Empty<RaidPawnLink>(),
        new[] { new WorldRaidOutcome(armyId, settlementId, "pirates", 0, 3, 0, 1, 1, 0, 0) },
        Array.Empty<SettlementProductionProfile>(),
        Array.Empty<WorldFactionRecord>(),
        new[]
        {
            new OwnershipRecord(missingCitizenId, settlementId),
            new OwnershipRecord(armyId, missingSettlementId)
        },
        new[] { new ResourceStack(settlementId, "Steel", -5) },
        Array.Empty<WorldEvent>(),
        Array.Empty<Drifter>()));
    var combinedErrors = string.Join(Environment.NewLine, state.Validate());

    AssertContains("Alive citizen Citizen:1 does not have an owner.", combinedErrors);
    AssertContains("Ownership asset Citizen:99 does not exist.", combinedErrors);
    AssertContains("Ownership owner Settlement:99 does not exist.", combinedErrors);
    AssertContains("Resource Steel owned by Settlement:1 has negative quantity -5.", combinedErrors);
    AssertContains("Duplicate settlement slug north-camp.", combinedErrors);
    AssertContains("Raid outcome Army:1 totals do not balance", combinedErrors);
}

static void TestPublicApiPopulationQuery()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "pirates");
    state.CreateCitizen("Raider", 28, Sex.Male, "soldier", settlement.Id);

    ILivingWorldApi api = new LivingWorldApi(state);
    var population = api.GetSettlementPopulation(settlement.Id);

    AssertEqual(1, population.Total);
}

static void TestCitizenOwnership()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "pirates");
    var citizen = state.CreateCitizen("Ada", 20, Sex.Female, "farmer", settlement.Id);

    var owner = state.GetOwner(citizen.Id);

    AssertEqual(settlement.Id, owner);
}

static void TestResourceOwnership()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "pirates");

    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 350);

    AssertEqual(350, state.GetOwnedResourceQuantity(settlement.Id, "PackagedSurvivalMeal"));
}

static void TestResourceLedgerService()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "pirates");

    ResourceLedgerService.AddResource(state, settlement.Id, "Steel", 100);
    var consumed = ResourceLedgerService.ConsumeResource(state, settlement.Id, "Steel", 35, "construction");

    AssertEqual(35, consumed);
    AssertEqual(65, ResourceLedgerService.GetQuantity(state, settlement.Id, "Steel"));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.ResourceAdded));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.ResourceConsumed));
}

static void TestWorldResourceKey()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "pirates");
    var steel = new WorldResourceKey(
        "Steel",
        ResourceCategory.Material,
        false,
        1.9f,
        0.5f);

    ResourceLedgerService.AddResource(state, settlement.Id, steel, 50);
    var consumed = ResourceLedgerService.ConsumeResource(state, settlement.Id, steel, 15, "construction");

    AssertEqual("Steel", steel.DefName);
    AssertEqual(ResourceCategory.Material, steel.Category);
    AssertEqual(false, steel.IsPerishable);
    AssertEqual(15, consumed);
    AssertEqual(35, ResourceLedgerService.GetQuantity(state, settlement.Id, steel));
}

static void TestResourceTransferToArmy()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "pirates");
    var army = state.CreateArmy("raid-1", "pirates", settlement.Id);

    state.AddResource(settlement.Id, "Steel", 500);
    var result = state.TransferResource(settlement.Id, army.Id, "Steel", 300, "raid launch");

    AssertEqual(OwnershipTransferStatus.Success, result.Status);
    AssertEqual(200, state.GetOwnedResourceQuantity(settlement.Id, "Steel"));
    AssertEqual(300, state.GetOwnedResourceQuantity(army.Id, "Steel"));
}

static void TestInsufficientResourceTransfer()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "pirates");
    var army = state.CreateArmy("raid-1", "pirates", settlement.Id);

    state.AddResource(settlement.Id, "Steel", 500);
    var result = state.TransferResource(settlement.Id, army.Id, "Steel", 501, "raid launch");

    AssertEqual(OwnershipTransferStatus.InsufficientOwnedAssets, result.Status);
    AssertEqual("Owner Settlement:1 has 500 Steel but transfer requested 501.", result.Reason);
    AssertEqual(500, state.GetOwnedResourceQuantity(settlement.Id, "Steel"));
    AssertEqual(0, state.GetOwnedResourceQuantity(army.Id, "Steel"));
}

static void TestOwnershipServiceTransfersAssets()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "pirates");
    var army = state.CreateArmy("raid-1", "pirates", settlement.Id);
    var citizen = state.CreateCitizen("Ada", 20, Sex.Female, "soldier", settlement.Id);

    var result = OwnershipService.TransferAsset(
        state,
        citizen.Id,
        settlement.Id,
        army.Id,
        "raid launch");

    AssertEqual(OwnershipTransferStatus.Success, result.Status);
    AssertEqual(army.Id, state.GetOwner(citizen.Id));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.OwnershipTransferred));
}

static void TestAnimalCohortOwnership()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("ranch", "Ranch", "Outlander");

    var herd = state.CreateAnimalCohort(
        settlement.Id,
        "Muffalo",
        AnimalCohortType.Domesticated,
        count: 12,
        healthPercent: 90,
        fertilityPercent: 80,
        carryingCapacity: 30,
        tick: 10_000);

    AssertEqual(EntityKind.Animal, herd.Id.Kind);
    AssertEqual(settlement.Id, herd.OwnerId);
    AssertEqual(settlement.Id, state.GetOwner(herd.Id));
    AssertEqual(12, state.GetAnimalCohort(herd.Id)!.Count);
    AssertEqual(1, state.AnimalCohorts.Count);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.AnimalCohortCreated));
    AssertEqual(0, state.Validate().Count());
}

static void TestAnimalEcologyGrowthAndPressure()
{
    var state = new WorldState(12345);
    var ranch = state.CreateSettlement("ranch", "Ranch", "Outlander");
    var herd = state.CreateAnimalCohort(
        ranch.Id,
        "Muffalo",
        AnimalCohortType.Domesticated,
        count: 10,
        healthPercent: 100,
        fertilityPercent: 100,
        carryingCapacity: 40,
        tick: 0);
    state.AddResource(ranch.Id, "Hay", 20);

    var growing = AnimalEcologyService.SimulateDay(
        state,
        new AnimalEcologyRequest(60_000, "Hay", 1));

    AssertEqual(2, growing.Births);
    AssertEqual(0, growing.Deaths);
    AssertEqual(12, state.GetAnimalCohort(herd.Id)!.Count);
    AssertEqual(10, state.GetOwnedResourceQuantity(ranch.Id, "Hay"));

    var starving = AnimalEcologyService.SimulateDay(
        state,
        new AnimalEcologyRequest(120_000, "Hay", 1));

    AssertEqual(0, starving.Births);
    AssertEqual(1, starving.Deaths);
    AssertEqual(11, state.GetAnimalCohort(herd.Id)!.Count);
    AssertEqual(0, state.GetOwnedResourceQuantity(ranch.Id, "Hay"));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.AnimalCohortGrew));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.AnimalCohortDeclined));
}

static void TestAnimalEcologyDriverSeedsFromSettlementEnvironment()
{
    var state = new WorldState(12345);
    var desert = state.CreateSettlement("desert-ranch", "Desert Ranch", "Outlander");
    var boreal = state.CreateSettlement("boreal-camp", "Boreal Camp", "Outlander");

    state.RecordSettlementProductionProfile(SettlementProductionProfile.FromEnvironment(
        desert.Id,
        new SettlementProductionEnvironment("ExtremeDesert", "Flat", "Industrial", 5, 80, 39)));
    state.RecordSettlementCapability(new SettlementCapability(
        desert.Id,
        HousingCapacity: 20,
        FoodStorageCapacity: 20,
        MedicineStorageCapacity: 5,
        PowerCapacity: 0,
        LaboratoryCapacity: 0,
        AnimalCapacity: 12,
        CropCapacity: 0,
        ResearchCapacity: 0,
        MechanicalCapacity: 0,
        PollutionHandling: 0));

    state.RecordSettlementProductionProfile(SettlementProductionProfile.FromEnvironment(
        boreal.Id,
        new SettlementProductionEnvironment("BorealForest", "LargeHills", "Medieval", 25, 700, -8)));
    state.RecordSettlementCapability(new SettlementCapability(
        boreal.Id,
        HousingCapacity: 20,
        FoodStorageCapacity: 20,
        MedicineStorageCapacity: 5,
        PowerCapacity: 0,
        LaboratoryCapacity: 0,
        AnimalCapacity: 8,
        CropCapacity: 0,
        ResearchCapacity: 0,
        MechanicalCapacity: 0,
        PollutionHandling: 0));

    var result = AnimalEcologyDriver.SimulateDay(
        state,
        new AnimalEcologyDriverRequest(60_000, "Food", FeedPerDomesticatedAnimal: 0));

    var desertCohorts = state.GetAnimalCohorts(desert.Id);
    var borealCohorts = state.GetAnimalCohorts(boreal.Id);

    AssertEqual(4, result.CohortsSeeded);
    AssertEqual(true, desertCohorts.Any(cohort => cohort.Type == AnimalCohortType.Domesticated && cohort.AnimalKind == "Dromedary"));
    AssertEqual(true, desertCohorts.Any(cohort => cohort.Type == AnimalCohortType.Wild && cohort.AnimalKind == "Ibex"));
    AssertEqual(true, borealCohorts.Any(cohort => cohort.Type == AnimalCohortType.Domesticated && cohort.AnimalKind == "Muffalo"));
    AssertEqual(true, borealCohorts.Any(cohort => cohort.Type == AnimalCohortType.Wild && cohort.AnimalKind == "Caribou"));
    AssertEqual(12, desertCohorts.Single(cohort => cohort.Type == AnimalCohortType.Domesticated).CarryingCapacity);
    AssertEqual(0, state.Validate().Count());
}

static void TestAnimalEcologyDriverIsIdempotentAndFeedsDomesticatedCohorts()
{
    var state = new WorldState(12345);
    var ranch = state.CreateSettlement("temperate-ranch", "Temperate Ranch", "Outlander");
    state.RecordSettlementProductionProfile(SettlementProductionProfile.FromEnvironment(
        ranch.Id,
        new SettlementProductionEnvironment("TemperateForest", "SmallHills", "Industrial", 55, 850, 21)));
    state.RecordSettlementCapability(new SettlementCapability(
        ranch.Id,
        HousingCapacity: 20,
        FoodStorageCapacity: 20,
        MedicineStorageCapacity: 5,
        PowerCapacity: 0,
        LaboratoryCapacity: 0,
        AnimalCapacity: 8,
        CropCapacity: 0,
        ResearchCapacity: 0,
        MechanicalCapacity: 0,
        PollutionHandling: 0));
    state.AddResource(ranch.Id, "Food", 20);

    var dayOne = AnimalEcologyDriver.SimulateDay(
        state,
        new AnimalEcologyDriverRequest(60_000, "Food", FeedPerDomesticatedAnimal: 1));
    var countAfterDayOne = state.GetAnimalCohorts(ranch.Id).Count;
    var foodAfterDayOne = state.GetOwnedResourceQuantity(ranch.Id, "Food");

    var dayTwo = AnimalEcologyDriver.SimulateDay(
        state,
        new AnimalEcologyDriverRequest(120_000, "Food", FeedPerDomesticatedAnimal: 1));

    AssertEqual(2, dayOne.CohortsSeeded);
    AssertEqual(0, dayTwo.CohortsSeeded);
    AssertEqual(countAfterDayOne, state.GetAnimalCohorts(ranch.Id).Count);
    AssertEqual(true, foodAfterDayOne < 20);
    AssertEqual(true, state.GetOwnedResourceQuantity(ranch.Id, "Food") < foodAfterDayOne);
    AssertEqual(0, state.Validate().Count());
}

static void TestAnimalProductionConvertsHerdsAndHuntingIntoFood()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("ranch", "Ranch", "Outlander");
    state.CreateAnimalCohort(
        settlement.Id,
        "Muffalo",
        AnimalCohortType.Domesticated,
        count: 12,
        healthPercent: 80,
        fertilityPercent: 70,
        carryingCapacity: 20,
        tick: 0);
    var deer = state.CreateAnimalCohort(
        settlement.Id,
        "Deer",
        AnimalCohortType.Wild,
        count: 10,
        healthPercent: 90,
        fertilityPercent: 70,
        carryingCapacity: 14,
        tick: 0);

    var result = AnimalProductionService.SimulateDay(
        state,
        new AnimalProductionRequest(
            Tick: 60_000,
            FoodResourceKey: "PackagedSurvivalMeal",
            RanchOutputPerHealthyAnimal: 1,
            WildHarvestDivisor: 4,
            MaxWildAnimalsHarvestedPerCohort: 3));

    AssertEqual(2, result.RanchFoodProduced);
    AssertEqual(3, result.WildAnimalsHarvested);
    AssertEqual(6, result.HuntingFoodProduced);
    AssertEqual(8, state.GetOwnedResourceQuantity(settlement.Id, "PackagedSurvivalMeal"));
    AssertEqual(7, state.GetAnimalCohort(deer.Id)!.Count);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.AnimalProductsHarvested));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.AnimalHunted));
    AssertEqual(0, state.Validate().Count());
}

static void TestAnimalCohortMigrationConservesPopulation()
{
    var state = new WorldState(12345);
    var source = state.CreateSettlement("source", "Source", "Outlander");
    var target = state.CreateSettlement("target", "Target", "Outlander");
    var herd = state.CreateAnimalCohort(
        source.Id,
        "Deer",
        AnimalCohortType.Wild,
        count: 20,
        healthPercent: 80,
        fertilityPercent: 70,
        carryingCapacity: 10,
        tick: 0);
    state.CreateAnimalCohort(
        target.Id,
        "Deer",
        AnimalCohortType.Wild,
        count: 4,
        healthPercent: 80,
        fertilityPercent: 70,
        carryingCapacity: 20,
        tick: 0);

    var result = AnimalEcologyService.MigratePressure(
        state,
        new AnimalMigrationRequest(60_000, source.Id, target.Id, "Deer", MaxCount: 5));

    AssertEqual(5, result.Migrated);
    AssertEqual(15, state.GetAnimalCohort(herd.Id)!.Count);
    AssertEqual(9, state.GetAnimalCohorts(target.Id).Single(cohort => cohort.AnimalKind == "Deer").Count);
    AssertEqual(24, state.GetAnimalCohorts(source.Id).Sum(cohort => cohort.Count)
        + state.GetAnimalCohorts(target.Id).Sum(cohort => cohort.Count));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.AnimalCohortMigrated));
    AssertEqual(0, state.Validate().Count());
}

static void TestAnimalCohortSerialization()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("ranch", "Ranch", "Outlander");
    var herd = state.CreateAnimalCohort(
        settlement.Id,
        "Muffalo",
        AnimalCohortType.Domesticated,
        count: 8,
        healthPercent: 75,
        fertilityPercent: 60,
        carryingCapacity: 18,
        tick: 99);

    var restored = WorldStateCodec.Deserialize(WorldStateCodec.Serialize(state));
    var restoredHerd = restored.GetAnimalCohort(herd.Id)!;

    AssertEqual(herd, restoredHerd);
    AssertEqual(settlement.Id, restored.GetOwner(herd.Id));
    AssertEqual(0, restored.Validate().Count());
}

static void TestAnimalSelectionProjectsImproveCohorts()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("ranch", "Ranch", "Outlander");
    state.RecordSettlementCapability(new SettlementCapability(settlement.Id, 10, 10, 10, 0, 0, 12, 0, 0, 0, 0));
    state.RecordSpecialistPool(new SpecialistPool(settlement.Id, 0, 2, 0, 0, 0, 0, 0, 0, 0));
    var herd = state.CreateAnimalCohort(settlement.Id, "Muffalo", AnimalCohortType.Domesticated, 8, 70, 60, 18, 0);
    state.AddResource(settlement.Id, "Hay", 40);
    state.AddResource(settlement.Id, "MedicineIndustrial", 4);
    state.AddResource(settlement.Id, "ComponentIndustrial", 3);

    var started = AnimalBreedingService.StartSelectionProject(
        state,
        new AnimalBreedingStartRequest(
            Tick: 60_000,
            SettlementId: settlement.Id,
            SourceCohortId: herd.Id,
            Trait: AnimalBreedingTrait.Fertility,
            DurationTicks: 60_000,
            FeedResourceKey: "Hay",
            FeedCost: 12,
            MedicineResourceKey: "MedicineIndustrial",
            MedicineCost: 1,
            ComponentResourceKey: "ComponentIndustrial",
            ComponentCost: 1));

    AssertEqual(AnimalBreedingStartStatus.Success, started.Status);
    AssertEqual(28, state.GetOwnedResourceQuantity(settlement.Id, "Hay"));
    AssertEqual(3, state.GetOwnedResourceQuantity(settlement.Id, "MedicineIndustrial"));
    AssertEqual(2, state.GetOwnedResourceQuantity(settlement.Id, "ComponentIndustrial"));
    AssertEqual(AnimalBreedingProjectStatus.Active, state.GetAnimalBreedingProject(started.Project!.Id)!.Status);

    var early = AnimalBreedingService.CompleteReadyProjects(state, 90_000);
    AssertEqual(0, early.CompletedProjects);
    AssertEqual(60, state.GetAnimalCohort(herd.Id)!.FertilityPercent);

    var completed = AnimalBreedingService.CompleteReadyProjects(state, 120_000);
    AssertEqual(1, completed.CompletedProjects);
    AssertEqual(70, state.GetAnimalCohort(herd.Id)!.FertilityPercent);
    AssertEqual(AnimalBreedingProjectStatus.Completed, state.GetAnimalBreedingProject(started.Project.Id)!.Status);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.AnimalBreedingProjectStarted));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.AnimalBreedingProjectCompleted));
    AssertEqual(0, state.Validate().Count());
}

static void TestAnimalBreedingProjectsRequireCapabilities()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("camp", "Camp", "Outlander");
    var herd = state.CreateAnimalCohort(settlement.Id, "Muffalo", AnimalCohortType.Domesticated, 8, 70, 60, 18, 0);
    state.AddResource(settlement.Id, "Hay", 40);
    state.AddResource(settlement.Id, "MedicineIndustrial", 4);
    state.AddResource(settlement.Id, "ComponentIndustrial", 3);

    var result = AnimalBreedingService.StartSelectionProject(
        state,
        new AnimalBreedingStartRequest(
            60_000,
            settlement.Id,
            herd.Id,
            AnimalBreedingTrait.Health,
            60_000,
            "Hay",
            12,
            "MedicineIndustrial",
            1,
            "ComponentIndustrial",
            1));

    AssertEqual(AnimalBreedingStartStatus.InsufficientCapability, result.Status);
    AssertEqual(40, state.GetOwnedResourceQuantity(settlement.Id, "Hay"));
    AssertEqual(0, state.AnimalBreedingProjects.Count);
}

static void TestAnimalIncubationProjectsCreateLedgerCohorts()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("lab-ranch", "Lab Ranch", "Outlander");
    state.RecordSettlementCapability(new SettlementCapability(settlement.Id, 10, 10, 10, 5, 2, 12, 0, 2, 0, 0));
    state.RecordSpecialistPool(new SpecialistPool(settlement.Id, 0, 2, 0, 1, 0, 1, 0, 0, 0));
    var herd = state.CreateAnimalCohort(settlement.Id, "Muffalo", AnimalCohortType.Domesticated, 8, 80, 80, 18, 0);
    state.AddResource(settlement.Id, "Hay", 40);
    state.AddResource(settlement.Id, "MedicineIndustrial", 4);
    state.AddResource(settlement.Id, "ComponentIndustrial", 3);

    var started = AnimalBreedingService.StartIncubationProject(
        state,
        new AnimalBreedingStartRequest(
            60_000,
            settlement.Id,
            herd.Id,
            AnimalBreedingTrait.Health,
            60_000,
            "Hay",
            10,
            "MedicineIndustrial",
            2,
            "ComponentIndustrial",
            2));

    AnimalBreedingService.CompleteReadyProjects(state, 120_000);

    AssertEqual(AnimalBreedingStartStatus.Success, started.Status);
    AssertEqual(2, state.GetAnimalCohorts(settlement.Id).Count);
    var incubated = state.GetAnimalCohorts(settlement.Id).OrderByDescending(cohort => cohort.Id.Value).First();
    AssertEqual("Muffalo", incubated.AnimalKind);
    AssertEqual(1, incubated.Count);
    AssertEqual(90, incubated.HealthPercent);
    AssertEqual(settlement.Id, state.GetOwner(incubated.Id));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.AnimalCohortIncubated));
    AssertEqual(0, state.Validate().Count());
}

static void TestAnimalBreedingDriverStartsAndCompletesProjects()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("ranch", "Ranch", "Outlander");
    state.RecordSettlementCapability(new SettlementCapability(settlement.Id, 10, 10, 10, 0, 0, 12, 0, 0, 0, 0));
    state.RecordSpecialistPool(new SpecialistPool(settlement.Id, 0, 2, 0, 0, 0, 0, 0, 0, 0));
    var herd = state.CreateAnimalCohort(settlement.Id, "Muffalo", AnimalCohortType.Domesticated, 8, 90, 50, 18, 0);
    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 40);
    state.AddResource(settlement.Id, "MedicineIndustrial", 4);
    state.AddResource(settlement.Id, "ComponentIndustrial", 3);

    var started = AnimalBreedingDriver.SimulateDay(
        state,
        new AnimalBreedingDriverRequest(
            Tick: 60_000,
            FeedResourceKey: "PackagedSurvivalMeal",
            MedicineResourceKey: "MedicineIndustrial",
            ComponentResourceKey: "ComponentIndustrial"));

    var early = AnimalBreedingDriver.SimulateDay(
        state,
        new AnimalBreedingDriverRequest(
            Tick: 90_000,
            FeedResourceKey: "PackagedSurvivalMeal",
            MedicineResourceKey: "MedicineIndustrial",
            ComponentResourceKey: "ComponentIndustrial"));

    var completed = AnimalBreedingDriver.SimulateDay(
        state,
        new AnimalBreedingDriverRequest(
            Tick: 120_000,
            FeedResourceKey: "PackagedSurvivalMeal",
            MedicineResourceKey: "MedicineIndustrial",
            ComponentResourceKey: "ComponentIndustrial"));

    AssertEqual(1, started.ProjectsStarted);
    AssertEqual(0, early.ProjectsStarted);
    AssertEqual(0, early.ProjectsCompleted);
    AssertEqual(1, completed.ProjectsCompleted);
    AssertEqual(60, state.GetAnimalCohort(herd.Id)!.FertilityPercent);
    AssertEqual(1, state.AnimalBreedingProjects.Count(project => project.Status == AnimalBreedingProjectStatus.Completed));
    AssertEqual(0, state.Validate().Count());
}

static void TestAnimalBreedingProjectSerialization()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("ranch", "Ranch", "Outlander");
    state.RecordSettlementCapability(new SettlementCapability(settlement.Id, 10, 10, 10, 0, 0, 12, 0, 0, 0, 0));
    state.RecordSpecialistPool(new SpecialistPool(settlement.Id, 0, 2, 0, 0, 0, 0, 0, 0, 0));
    var herd = state.CreateAnimalCohort(settlement.Id, "Muffalo", AnimalCohortType.Domesticated, 8, 70, 60, 18, 0);
    state.AddResource(settlement.Id, "Hay", 40);
    state.AddResource(settlement.Id, "MedicineIndustrial", 4);
    state.AddResource(settlement.Id, "ComponentIndustrial", 3);

    var started = AnimalBreedingService.StartSelectionProject(
        state,
        new AnimalBreedingStartRequest(60_000, settlement.Id, herd.Id, AnimalBreedingTrait.Capacity, 60_000, "Hay", 12, "MedicineIndustrial", 1, "ComponentIndustrial", 1));

    var restored = WorldStateCodec.Deserialize(WorldStateCodec.Serialize(state));

    AssertEqual(started.Project, restored.GetAnimalBreedingProject(started.Project!.Id));
    AssertEqual(0, restored.Validate().Count());
}

static void TestSettlementDailySimulationConsumesFoodAndBirths()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "Pirate");
    state.CreateCitizen("Parent 1", 28, Sex.Female, "settler", settlement.Id);
    state.CreateCitizen("Parent 2", 30, Sex.Male, "settler", settlement.Id);
    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 12);

    var dayOne = SettlementDailySimulationService.SimulateDay(
        state,
        new SettlementDailySimulationRequest(60_000, "PackagedSurvivalMeal", 1, 3));

    AssertEqual(2, dayOne.FoodConsumed);
    AssertEqual(0, dayOne.Births);
    AssertEqual(10, state.GetOwnedResourceQuantity(settlement.Id, "PackagedSurvivalMeal"));

    var dayThree = SettlementDailySimulationService.SimulateDay(
        state,
        new SettlementDailySimulationRequest(180_000, "PackagedSurvivalMeal", 1, 3));

    AssertEqual(2, dayThree.FoodConsumed);
    AssertEqual(1, dayThree.Births);
    AssertEqual(8, state.GetOwnedResourceQuantity(settlement.Id, "PackagedSurvivalMeal"));
    AssertEqual(3, state.GetSettlementPopulation(settlement.Id).Total);
    AssertEqual(1, state.GetSettlementPopulation(settlement.Id).Children);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.CitizenBorn));
}

static void TestSettlementDailySimulationRecordsFoodShortage()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "Pirate");
    state.CreateCitizen("Parent 1", 28, Sex.Female, "settler", settlement.Id);
    state.CreateCitizen("Parent 2", 30, Sex.Male, "settler", settlement.Id);
    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 1);

    var result = SettlementDailySimulationService.SimulateDay(
        state,
        new SettlementDailySimulationRequest(180_000, "PackagedSurvivalMeal", 1, 3));
    var foodStatus = state.GetSettlementFoodStatus(settlement.Id, "PackagedSurvivalMeal", 1);

    AssertEqual(1, result.FoodConsumed);
    AssertEqual(1, result.FoodShortages);
    AssertEqual(0, result.Births);
    AssertEqual(0, state.GetOwnedResourceQuantity(settlement.Id, "PackagedSurvivalMeal"));
    AssertEqual(true, foodStatus.IsShortage);
    AssertEqual(0, foodStatus.FoodDays);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.FoodShortage));
    AssertEqual(0, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.CitizenBorn));
}

static void TestSettlementDailySimulationBlocksBirthsWhenHousingIsFull()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("full-house", "Full House", "Settlers");
    state.CreateCitizen("Parent 1", 28, Sex.Female, "settler", settlement.Id);
    state.CreateCitizen("Parent 2", 30, Sex.Male, "settler", settlement.Id);
    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 20);
    state.RecordSettlementCapability(new SettlementCapability(
        settlement.Id,
        HousingCapacity: 2,
        FoodStorageCapacity: 100,
        MedicineStorageCapacity: 0,
        PowerCapacity: 0,
        LaboratoryCapacity: 0,
        AnimalCapacity: 0,
        CropCapacity: 0,
        ResearchCapacity: 0,
        MechanicalCapacity: 0,
        PollutionHandling: 0));

    var result = SettlementDailySimulationService.SimulateDay(
        state,
        new SettlementDailySimulationRequest(180_000, "PackagedSurvivalMeal", 1, 3));

    AssertEqual(0, result.Births);
    AssertEqual(2, state.GetSettlementPopulation(settlement.Id).Total);
    AssertEqual(0, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.CitizenBorn));
}

static void TestDemographyServiceAgesAndKillsElders()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "Pirate");
    var child = state.CreateCitizen("Young", 17, Sex.Female, "settler", settlement.Id);
    var elder = state.CreateCitizen("Old", 84, Sex.Male, "settler", settlement.Id);

    var result = DemographyService.SimulateDay(
        state,
        new DemographySimulationRequest(
            3_600_000,
            60,
            85,
            1));

    AssertEqual(2, result.CitizensAged);
    AssertEqual(1, result.NaturalDeaths);
    AssertEqual(18, state.GetCitizen(child.Id)!.Age);
    AssertEqual(CitizenStatus.Alive, state.GetCitizen(child.Id)!.Status);
    AssertEqual(CitizenStatus.Dead, state.GetCitizen(elder.Id)!.Status);
    AssertEqual(1, state.GetSettlementPopulation(settlement.Id).Total);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.CitizenAged));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.CitizenDied));
}

static void TestSettlementProductionProfileUsesTerrainAndTechnology()
{
    var settlementId = EntityId.Create(EntityKind.Settlement, 7);
    var fertileIndustrial = SettlementProductionProfile.FromEnvironment(
        settlementId,
        new SettlementProductionEnvironment(
            "TemperateForest",
            "SmallHills",
            "Industrial",
            55,
            850,
            21));
    var desertNeolithic = SettlementProductionProfile.FromEnvironment(
        settlementId,
        new SettlementProductionEnvironment(
            "ExtremeDesert",
            "Flat",
            "Neolithic",
            5,
            80,
            39));

    AssertEqual(settlementId, fertileIndustrial.SettlementId);
    AssertEqual(4, fertileIndustrial.FoodPerAdult);
    AssertEqual(2, fertileIndustrial.SteelPerAdult);
    AssertEqual(1, fertileIndustrial.MedicinePerAdult);
    AssertEqual(1, fertileIndustrial.ComponentPerAdult);
    AssertEqual(0, desertNeolithic.FoodPerAdult);
    AssertEqual(0, desertNeolithic.ComponentPerAdult);
}

static void TestSettlementWealthRefreshAllUsesDefaultPrices()
{
    var state = new WorldState(999);
    var camp = state.CreateSettlement("camp", "Camp", "Pirates");
    var town = state.CreateSettlement("town", "Town", "Traders");
    state.AddResource(camp.Id, "Silver", 50);
    state.AddResource(camp.Id, "Steel", 100);               // 100 * 2 = 200 material
    state.AddResource(town.Id, "PackagedSurvivalMeal", 10); // 10 * 14 = 140 material
    state.AddResource(town.Id, "ComponentIndustrial", 2);   // 2 * 24 = 48 material

    SettlementWealthService.RefreshAll(state, SettlementWealthService.DefaultPriceBook);

    var pirates = state.GetFactionWealth("Pirates")!;
    AssertEqual(50, pirates.Silver);
    AssertEqual(200, pirates.MaterialWealth);
    AssertEqual(250, pirates.TotalWealth);

    var traders = state.GetFactionWealth("Traders")!;
    AssertEqual(0, traders.Silver);
    AssertEqual(188, traders.MaterialWealth);
    AssertEqual(188, traders.TotalWealth);

    // Per-settlement snapshots are recorded too, not just the faction rollup.
    AssertEqual(250, state.GetSettlementWealth(camp.Id)!.TotalWealth);

    // The daily world tick drives the refresh with the canonical price book.
    var component = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldWorldComponent.cs"));
    AssertContains("SettlementWealthService.RefreshAll(State, SettlementWealthService.DefaultPriceBook)", component);
}

static void TestSettlementWealthServiceCachesWealth()
{
    var state = new WorldState(12345);
    var market = state.CreateSettlement("market", "Market", "Traders");
    var mine = state.CreateSettlement("mine", "Mine", "Traders");
    state.AddResource(market.Id, "Silver", 120);
    state.AddResource(market.Id, "Steel", 10);
    state.AddResource(market.Id, "ComponentIndustrial", 1);
    state.AddResource(mine.Id, "Silver", 30);
    state.AddResource(mine.Id, "Steel", 5);

    var prices = ResourcePriceBook.FromSilver(
        "Silver",
        new Dictionary<string, int>
        {
            ["Steel"] = 2,
            ["ComponentIndustrial"] = 12,
        });

    var settlement = SettlementWealthService.RefreshSettlement(state, market.Id, prices);
    var faction = SettlementWealthService.RefreshFaction(state, "Traders", prices);

    AssertEqual(152, settlement.TotalWealth);
    AssertEqual(120, settlement.Silver);
    AssertEqual(32, settlement.MaterialWealth);
    AssertEqual(settlement, state.GetSettlementWealth(market.Id));
    AssertEqual(192, faction.TotalWealth);
    AssertEqual(150, faction.Silver);
    AssertEqual(42, faction.MaterialWealth);
    AssertEqual(faction, state.GetFactionWealth("Traders"));
}

static void TestSettlementProductionUsesDepthModifiers()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("deep-mine", "Deep Mine", "Miners");
    for (var i = 0; i < 4; i++)
    {
        state.CreateCitizen($"Miner {i + 1}", 30 + i, Sex.Male, "miner", settlement.Id);
    }

    var profile = SettlementProductionProfile.FromEnvironment(
        settlement.Id,
        new SettlementProductionEnvironment(
            "AridShrubland",
            "LargeHills",
            "Industrial",
            20,
            300,
            15)) with
    {
        Archetype = ProductionArchetype.Miner,
        LaborEfficiencyPercent = 150,
        EconomyScalePercent = 125,
        ComplexityPenaltyPercent = 80,
    };
    state.RecordSettlementProductionProfile(profile);

    var result = SettlementProductionService.SimulateDay(
        state,
        new SettlementProductionRequest(
            60_000,
            "PackagedSurvivalMeal",
            "Steel",
            "MedicineIndustrial",
            "ComponentIndustrial"));

    AssertEqual(4, result.FoodProduced);
    AssertEqual(27, result.SteelProduced);
    AssertEqual(0, result.MedicineProduced);
    AssertEqual(7, result.ComponentsProduced);
    AssertEqual(27, state.GetOwnedResourceQuantity(settlement.Id, "Steel"));
    AssertEqual(7, state.GetOwnedResourceQuantity(settlement.Id, "ComponentIndustrial"));
}

static void TestVirtualTradeTransfersGoodsAndSilver()
{
    var state = new WorldState(12345);
    var seller = state.CreateSettlement("steelworks", "Steelworks", "Miners");
    var buyer = state.CreateSettlement("city", "City", "Traders");
    state.AddResource(seller.Id, "Steel", 100);
    state.AddResource(buyer.Id, "Silver", 500);

    var request = new VirtualTradeRequest(
        seller.Id,
        buyer.Id,
        "Steel",
        RequestedQuantity: 20,
        SilverResourceKey: "Silver",
        BaseUnitPrice: 4);
    var quote = VirtualTradeService.GetQuote(state, request);
    var result = VirtualTradeService.Execute(state, request);

    AssertEqual(5, quote.UnitPrice);
    AssertEqual(20, result.QuantityTransferred);
    AssertEqual(100, result.SilverTransferred);
    AssertEqual(80, state.GetOwnedResourceQuantity(seller.Id, "Steel"));
    AssertEqual(100, state.GetOwnedResourceQuantity(seller.Id, "Silver"));
    AssertEqual(20, state.GetOwnedResourceQuantity(buyer.Id, "Steel"));
    AssertEqual(400, state.GetOwnedResourceQuantity(buyer.Id, "Silver"));
    AssertEqual(100, state.GetOwnedResourceQuantity(seller.Id, "Steel") + state.GetOwnedResourceQuantity(buyer.Id, "Steel"));
    AssertEqual(500, state.GetOwnedResourceQuantity(seller.Id, "Silver") + state.GetOwnedResourceQuantity(buyer.Id, "Silver"));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.SettlementTradeRecorded));
}

static void TestSettlementProductionAddsOwnedResources()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("forge-town", "Forge Town", "Outlander");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen($"Worker {i + 1}", 24 + i, Sex.Male, "worker", settlement.Id);
    }

    state.RecordSettlementProductionProfile(SettlementProductionProfile.FromEnvironment(
        settlement.Id,
        new SettlementProductionEnvironment(
            "TemperateForest",
            "SmallHills",
            "Industrial",
            55,
            850,
            21)));

    var result = SettlementProductionService.SimulateDay(
        state,
        new SettlementProductionRequest(
            60_000,
            "PackagedSurvivalMeal",
            "Steel",
            "MedicineIndustrial",
            "ComponentIndustrial"));

    AssertEqual(12, result.FoodProduced);
    AssertEqual(6, result.SteelProduced);
    AssertEqual(3, result.MedicineProduced);
    AssertEqual(3, result.ComponentsProduced);
    AssertEqual(12, state.GetOwnedResourceQuantity(settlement.Id, "PackagedSurvivalMeal"));
    AssertEqual(6, state.GetOwnedResourceQuantity(settlement.Id, "Steel"));
    AssertEqual(3, state.GetOwnedResourceQuantity(settlement.Id, "MedicineIndustrial"));
    AssertEqual(3, state.GetOwnedResourceQuantity(settlement.Id, "ComponentIndustrial"));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.SettlementProductionUpdated));
}

static void TestSettlementFacilitiesModifyProductionOutput()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("farm-town", "Farm Town", "Outlander");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen($"Farmer {i + 1}", 24 + i, Sex.Female, "farmer", settlement.Id);
    }

    state.RecordSettlementProductionProfile(SettlementProductionProfile.FromEnvironment(
        settlement.Id,
        new SettlementProductionEnvironment(
            "TemperateForest",
            "SmallHills",
            "Industrial",
            55,
            850,
            21)));
    state.RecordSettlementFacility(new SettlementFacility(
        EntityId.Create(EntityKind.SettlementFacility, 1),
        settlement.Id,
        SettlementFacilityKind.Farm,
        Level: 2,
        ConditionPercent: 100,
        BuiltTick: 0));

    var result = SettlementProductionService.SimulateDay(
        state,
        new SettlementProductionRequest(
            60_000,
            "PackagedSurvivalMeal",
            "Steel",
            "MedicineIndustrial",
            "ComponentIndustrial"));

    AssertEqual(16, result.FoodProduced);
    AssertEqual(6, result.SteelProduced);
    AssertEqual(16, state.GetOwnedResourceQuantity(settlement.Id, "PackagedSurvivalMeal"));
}

static void TestSettlementProductionStatusUsesEffectiveOutput()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("farm-status", "Farm Status", "Outlander");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen($"Farmer {i + 1}", 24 + i, Sex.Female, "farmer", settlement.Id);
    }

    state.RecordSettlementProductionProfile(SettlementProductionProfile.FromEnvironment(
        settlement.Id,
        new SettlementProductionEnvironment(
            "TemperateForest",
            "SmallHills",
            "Industrial",
            55,
            850,
            21)));
    state.RecordSettlementFacility(new SettlementFacility(
        EntityId.Create(EntityKind.SettlementFacility, 1),
        settlement.Id,
        SettlementFacilityKind.Farm,
        Level: 2,
        ConditionPercent: 100,
        BuiltTick: 0));

    var status = state.GetSettlementProductionStatus(settlement.Id);

    AssertEqual(3, status.AdultWorkers);
    AssertEqual(16, status.FoodPerDay);
    AssertEqual(6, status.SteelPerDay);
    AssertEqual(3, status.MedicinePerDay);
    AssertEqual(3, status.ComponentsPerDay);
}

static void TestSettlementProjectsConsumeResourcesAndCompleteFacilities()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("forge-town", "Forge Town", "Outlander");
    state.AddResource(settlement.Id, "Steel", 120);
    state.AddResource(settlement.Id, "ComponentIndustrial", 8);

    var result = SettlementProjectService.StartBuildFacility(
        state,
        settlement.Id,
        SettlementFacilityKind.Workshop,
        level: 1,
        startTick: 1_000,
        durationTicks: 60_000,
        steelCost: 80,
        componentCost: 4);

    AssertEqual(SettlementProjectStartStatus.Success, result.Status);
    AssertEqual(40, state.GetOwnedResourceQuantity(settlement.Id, "Steel"));
    AssertEqual(4, state.GetOwnedResourceQuantity(settlement.Id, "ComponentIndustrial"));
    AssertEqual(0, state.GetSettlementFacilities(settlement.Id).Count);
    AssertEqual(SettlementProjectStatus.Active, state.GetSettlementProject(result.Project!.Id)!.Status);

    var completedEarly = SettlementProjectService.CompleteReadyProjects(state, 30_000);
    AssertEqual(0, completedEarly.CompletedProjects);
    AssertEqual(0, state.GetSettlementFacilities(settlement.Id).Count);

    var completed = SettlementProjectService.CompleteReadyProjects(state, 61_000);
    var facility = state.GetSettlementFacilities(settlement.Id).Single();

    AssertEqual(1, completed.CompletedProjects);
    AssertEqual(SettlementFacilityKind.Workshop, facility.Kind);
    AssertEqual(100, facility.ConditionPercent);
    AssertEqual(SettlementProjectStatus.Completed, state.GetSettlementProject(result.Project.Id)!.Status);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.SettlementProjectStarted));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.SettlementFacilityBuilt));
}

static void TestDamagedFacilitiesReduceOutputAndRepairsConsumeResources()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("steel-town", "Steel Town", "Outlander");
    for (var i = 0; i < 4; i++)
    {
        state.CreateCitizen($"Worker {i + 1}", 30 + i, Sex.Male, "worker", settlement.Id);
    }

    state.RecordSettlementProductionProfile(SettlementProductionProfile.FromEnvironment(
        settlement.Id,
        new SettlementProductionEnvironment(
            "AridShrubland",
            "LargeHills",
            "Industrial",
            20,
            300,
            15)));
    var facility = state.RecordSettlementFacility(new SettlementFacility(
        EntityId.Create(EntityKind.SettlementFacility, 1),
        settlement.Id,
        SettlementFacilityKind.Workshop,
        Level: 2,
        ConditionPercent: 100,
        BuiltTick: 0));
    facility = SettlementFacilityService.DamageFacility(state, facility.Id, 50, "test damage");

    var damagedProduction = SettlementProductionService.SimulateDay(
        state,
        new SettlementProductionRequest(
            60_000,
            "PackagedSurvivalMeal",
            "Steel",
            "MedicineIndustrial",
            "ComponentIndustrial"));

    AssertEqual(14, damagedProduction.SteelProduced);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.SettlementFacilityDamaged));

    state.AddResource(settlement.Id, "Steel", 50);
    state.AddResource(settlement.Id, "ComponentIndustrial", 2);
    var repair = SettlementProjectService.StartRepairFacility(
        state,
        facility.Id,
        startTick: 70_000,
        durationTicks: 10_000,
        steelCost: 40,
        componentCost: 1);
    SettlementProjectService.CompleteReadyProjects(state, 80_000);

    AssertEqual(SettlementProjectStartStatus.Success, repair.Status);
    AssertEqual(24, state.GetOwnedResourceQuantity(settlement.Id, "Steel"));
    AssertEqual(5, state.GetOwnedResourceQuantity(settlement.Id, "ComponentIndustrial"));
    AssertEqual(100, state.GetSettlementFacility(facility.Id)!.ConditionPercent);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.SettlementFacilityRepaired));
}

static void TestInfrastructureDriverInvestsInAndCompletesFacilities()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("farm-town", "Farm Town", "Outlander");
    for (var i = 0; i < 6; i++)
    {
        state.CreateCitizen($"Farmer {i + 1}", 24 + i, Sex.Female, "farmer", settlement.Id);
    }

    state.RecordSettlementProductionProfile(SettlementProductionProfile.FromEnvironment(
        settlement.Id,
        new SettlementProductionEnvironment(
            "TemperateForest",
            "SmallHills",
            "Industrial",
            55,
            850,
            21)));
    state.AddResource(settlement.Id, "Steel", 160);
    state.AddResource(settlement.Id, "ComponentIndustrial", 10);

    var started = SettlementInfrastructureDriver.SimulateDay(
        state,
        new SettlementInfrastructureDriverRequest(Tick: 60_000, ProjectDurationTicks: 60_000));

    AssertEqual(1, started.ProjectsStarted);
    AssertEqual(0, started.ProjectsCompleted);
    AssertEqual(80, state.GetOwnedResourceQuantity(settlement.Id, "Steel"));
    AssertEqual(6, state.GetOwnedResourceQuantity(settlement.Id, "ComponentIndustrial"));
    AssertEqual(1, state.SettlementProjects.Count(project => project.Status == SettlementProjectStatus.Active));
    AssertEqual(0, state.GetSettlementFacilities(settlement.Id).Count);

    var completed = SettlementInfrastructureDriver.SimulateDay(
        state,
        new SettlementInfrastructureDriverRequest(Tick: 120_000, ProjectDurationTicks: 60_000));

    AssertEqual(1, completed.ProjectsCompleted);
    AssertEqual(1, completed.ProjectsStarted);
    AssertEqual(1, state.SettlementProjects.Count(project => project.Status == SettlementProjectStatus.Active));
    var facility = state.GetSettlementFacilities(settlement.Id).Single();
    AssertEqual(SettlementFacilityKind.Farm, facility.Kind);
    AssertEqual(100, facility.ConditionPercent);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.SettlementFacilityBuilt));
}

static void TestInfrastructureDriverRepairsDamagedFacilitiesFirst()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("forge-town", "Forge Town", "Outlander");
    for (var i = 0; i < 5; i++)
    {
        state.CreateCitizen($"Worker {i + 1}", 30 + i, Sex.Male, "worker", settlement.Id);
    }

    state.RecordSettlementProductionProfile(SettlementProductionProfile.FromEnvironment(
        settlement.Id,
        new SettlementProductionEnvironment(
            "AridShrubland",
            "LargeHills",
            "Industrial",
            20,
            300,
            15)));
    var damaged = state.RecordSettlementFacility(new SettlementFacility(
        EntityId.Create(EntityKind.SettlementFacility, 42),
        settlement.Id,
        SettlementFacilityKind.Workshop,
        Level: 2,
        ConditionPercent: 45,
        BuiltTick: 0));
    state.AddResource(settlement.Id, "Steel", 200);
    state.AddResource(settlement.Id, "ComponentIndustrial", 10);

    var started = SettlementInfrastructureDriver.SimulateDay(
        state,
        new SettlementInfrastructureDriverRequest(Tick: 60_000, ProjectDurationTicks: 30_000));

    AssertEqual(1, started.ProjectsStarted);
    var project = state.SettlementProjects.Single(project => project.Status == SettlementProjectStatus.Active);
    AssertEqual(SettlementProjectKind.RepairFacility, project.Kind);
    AssertEqual(damaged.Id, project.TargetFacilityId);
    AssertEqual(160, state.GetOwnedResourceQuantity(settlement.Id, "Steel"));
    AssertEqual(9, state.GetOwnedResourceQuantity(settlement.Id, "ComponentIndustrial"));

    SettlementInfrastructureDriver.SimulateDay(
        state,
        new SettlementInfrastructureDriverRequest(Tick: 90_000, ProjectDurationTicks: 30_000));

    AssertEqual(100, state.GetSettlementFacility(damaged.Id)!.ConditionPercent);
    AssertEqual(1, state.GetSettlementFacilities(settlement.Id).Count);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.SettlementFacilityRepaired));
}

static void TestSettlementProductionProfileSerialization()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("forge-town", "Forge Town", "Outlander");
    var profile = SettlementProductionProfile.FromEnvironment(
        settlement.Id,
        new SettlementProductionEnvironment(
            "BorealForest",
            "LargeHills",
            "Spacer",
            30,
            600,
            8));

    state.RecordSettlementProductionProfile(profile);

    var restored = WorldStateCodec.Deserialize(WorldStateCodec.Serialize(state));
    var restoredProfile = restored.GetSettlementProductionProfile(settlement.Id);

    AssertEqual(profile, restoredProfile);
}

static void TestSettlementFacilityProjectSerialization()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("factory", "Factory", "Outlander");
    state.AddResource(settlement.Id, "Steel", 200);
    state.AddResource(settlement.Id, "ComponentIndustrial", 10);
    var facility = state.RecordSettlementFacility(new SettlementFacility(
        EntityId.Create(EntityKind.SettlementFacility, 10),
        settlement.Id,
        SettlementFacilityKind.Workshop,
        Level: 2,
        ConditionPercent: 40,
        BuiltTick: 12));
    var project = SettlementProjectService.StartRepairFacility(
        state,
        facility.Id,
        startTick: 100,
        durationTicks: 500,
        steelCost: 30,
        componentCost: 2).Project!;

    var restored = WorldStateCodec.Deserialize(WorldStateCodec.Serialize(state));

    AssertEqual(facility, restored.GetSettlementFacility(facility.Id));
    AssertEqual(project, restored.GetSettlementProject(project.Id));
    AssertEqual(170, restored.GetOwnedResourceQuantity(settlement.Id, "Steel"));
    AssertEqual(8, restored.GetOwnedResourceQuantity(settlement.Id, "ComponentIndustrial"));
}

static void TestSettlementCapabilityAndSpecialistLedger()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("lab-town", "Lab Town", "Outlander");
    var capability = new SettlementCapability(
        settlement.Id,
        HousingCapacity: 80,
        FoodStorageCapacity: 1200,
        MedicineStorageCapacity: 90,
        PowerCapacity: 2000,
        LaboratoryCapacity: 4,
        AnimalCapacity: 60,
        CropCapacity: 40,
        ResearchCapacity: 3,
        MechanicalCapacity: 2,
        PollutionHandling: 1);
    var specialists = new SpecialistPool(
        settlement.Id,
        Farmers: 10,
        Handlers: 6,
        Doctors: 3,
        Researchers: 4,
        Engineers: 5,
        Geneticists: 1,
        Mechanitors: 1,
        Soldiers: 12,
        Diplomats: 2);

    state.RecordSettlementCapability(capability);
    state.RecordSpecialistPool(specialists);

    AssertEqual(capability, state.GetSettlementCapability(settlement.Id));
    AssertEqual(specialists, state.GetSpecialistPool(settlement.Id));
}

static void TestSettlementCapabilityReadiness()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("frontier-lab", "Frontier Lab", "Outlander");
    for (var i = 0; i < 12; i++)
    {
        state.CreateCitizen($"Worker {i + 1}", 20 + i, Sex.Female, "worker", settlement.Id);
    }

    state.RecordSettlementCapability(new SettlementCapability(
        settlement.Id,
        HousingCapacity: 10,
        FoodStorageCapacity: 600,
        MedicineStorageCapacity: 50,
        PowerCapacity: 900,
        LaboratoryCapacity: 1,
        AnimalCapacity: 20,
        CropCapacity: 30,
        ResearchCapacity: 2,
        MechanicalCapacity: 0,
        PollutionHandling: 0));
    state.RecordSpecialistPool(new SpecialistPool(
        settlement.Id,
        Farmers: 4,
        Handlers: 1,
        Doctors: 1,
        Researchers: 2,
        Engineers: 0,
        Geneticists: 0,
        Mechanitors: 0,
        Soldiers: 3,
        Diplomats: 1));

    var status = state.GetSettlementCapabilityStatus(settlement.Id);

    AssertEqual(12, status.Population);
    AssertEqual(-2, status.FreeHousing);
    AssertEqual(false, status.HasHousingForPopulation);
    AssertEqual(true, status.CanSupportCropProgram);
    AssertEqual(true, status.CanSupportAnimalProgram);
    AssertEqual(true, status.CanRunBasicLab);
    AssertEqual(false, status.CanRunMechanicalProduction);
}

static void TestSettlementCapabilitySerialization()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("bio-town", "Bio Town", "Outlander");
    var capability = new SettlementCapability(
        settlement.Id,
        HousingCapacity: 70,
        FoodStorageCapacity: 900,
        MedicineStorageCapacity: 80,
        PowerCapacity: 1500,
        LaboratoryCapacity: 3,
        AnimalCapacity: 50,
        CropCapacity: 45,
        ResearchCapacity: 4,
        MechanicalCapacity: 2,
        PollutionHandling: 2);
    var specialists = new SpecialistPool(
        settlement.Id,
        Farmers: 7,
        Handlers: 5,
        Doctors: 2,
        Researchers: 4,
        Engineers: 3,
        Geneticists: 2,
        Mechanitors: 1,
        Soldiers: 8,
        Diplomats: 1);

    state.RecordSettlementCapability(capability);
    state.RecordSpecialistPool(specialists);

    var snapshotRestored = WorldState.FromSnapshot(state.CreateSnapshot());
    var restored = WorldStateCodec.Deserialize(WorldStateCodec.Serialize(state));

    AssertEqual(capability, snapshotRestored.GetSettlementCapability(settlement.Id));
    AssertEqual(specialists, snapshotRestored.GetSpecialistPool(settlement.Id));
    AssertEqual(capability, restored.GetSettlementCapability(settlement.Id));
    AssertEqual(specialists, restored.GetSpecialistPool(settlement.Id));
    AssertEqual(true, restored.GetSettlementCapabilityStatus(settlement.Id).CanRunBasicLab);
}

static void TestStarvingSettlementCannotLaunchRaid()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "Pirate");
    for (var i = 0; i < 4; i++)
    {
        state.CreateCitizen($"Raider {i + 1}", 24 + i, Sex.Male, "soldier", settlement.Id);
    }

    var result = RaidPopulationAllocator.ReserveForRaid(
        state,
        new RaidPopulationAllocationRequest("Pirate", "starving raid", 2));

    AssertEqual(RaidPopulationAllocationStatus.NoAvailableCombatants, result.Status);
    AssertEqual(0, result.AvailableCombatants);
    AssertEqual(0, state.Armies.Count);
    AssertEqual(4, state.GetSettlementPopulation(settlement.Id).Adults);
}

static void TestMigrationPressureCreatesRefugee()
{
    var state = new WorldState(12345);
    var source = state.CreateSettlement("starving-camp", "Starving Camp", "Pirate");
    for (var i = 0; i < 4; i++)
    {
        state.CreateCitizen($"Citizen {i + 1}", 20 + i, Sex.Male, "settler", source.Id);
    }

    var statusBefore = state.GetSettlementMigrationStatus(source.Id, "PackagedSurvivalMeal", 1);
    var result = MigrationService.SimulateDay(
        state,
        new MigrationSimulationRequest(240_000, "PackagedSurvivalMeal", 1, 50, 1));
    var refugee = state.Citizens.Single(citizen => citizen.Status == CitizenStatus.Refugee);
    var statusAfter = state.GetSettlementMigrationStatus(source.Id, "PackagedSurvivalMeal", 1);

    AssertEqual(true, statusBefore.ShouldCreateRefugees);
    AssertEqual(1, result.RefugeesCreated);
    AssertEqual(0, result.MigrationGroupsCreated);
    AssertEqual(0, result.MigrationsCompleted);
    AssertEqual(source.Id, refugee.SettlementId);
    AssertEqual(refugee.Id, state.GetOwner(refugee.Id));
    AssertEqual(3, state.GetSettlementPopulation(source.Id).Total);
    AssertEqual(1, statusAfter.Refugees);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.RefugeeCreated));
    AssertEqual(0, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.MigrationStarted));
}

static void TestMigrationCompletesToStableSettlement()
{
    var state = new WorldState(12345);
    var source = state.CreateSettlement("starving-camp", "Starving Camp", "Pirate");
    var target = state.CreateSettlement("safe-camp", "Safe Camp", "Pirate");
    for (var i = 0; i < 4; i++)
    {
        state.CreateCitizen($"Source Citizen {i + 1}", 20 + i, Sex.Male, "settler", source.Id);
    }

    state.CreateCitizen("Safe Citizen", 30, Sex.Female, "settler", target.Id);
    state.AddResource(target.Id, "PackagedSurvivalMeal", 20);

    MigrationService.SimulateDay(
        state,
        new MigrationSimulationRequest(240_000, "PackagedSurvivalMeal", 1, 50, 1));
    var result = MigrationService.SimulateDay(
        state,
        new MigrationSimulationRequest(300_000, "PackagedSurvivalMeal", 1, 50, 0));

    AssertEqual(0, result.RefugeesCreated);
    AssertEqual(0, result.MigrationGroupsCreated);
    AssertEqual(1, result.MigrationsCompleted);
    AssertEqual(0, state.Citizens.Count(citizen => citizen.Status == CitizenStatus.Refugee));
    AssertEqual(2, state.GetSettlementPopulation(target.Id).Total);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.MigrationCompleted));
}

static void TestMigrationCreatesTravelingGroup()
{
    var state = new WorldState(12345);
    var source = state.CreateSettlement("starving-camp", "Starving Camp", "Pirate");
    var target = state.CreateSettlement("safe-camp", "Safe Camp", "Pirate");
    state.CreateCitizen("Source Citizen", 20, Sex.Male, "settler", source.Id);
    state.CreateCitizen("Safe Citizen", 30, Sex.Female, "settler", target.Id);
    state.AddResource(target.Id, "PackagedSurvivalMeal", 20);

    var result = MigrationService.SimulateDay(
        state,
        new MigrationSimulationRequest(240_000, "PackagedSurvivalMeal", 1, 50, 1, 120_000));
    var group = state.MigrationGroups.Single();
    var migrant = state.Citizens.Single(citizen => citizen.Status == CitizenStatus.Migrating);

    AssertEqual(1, result.RefugeesCreated);
    AssertEqual(1, result.MigrationGroupsCreated);
    AssertEqual(0, result.MigrationsCompleted);
    AssertEqual(MigrationGroupStatus.Traveling, group.Status);
    AssertEqual(source.Id, group.SourceSettlementId);
    AssertEqual(target.Id, group.TargetSettlementId);
    AssertEqual(360_000, group.ArrivalTick);
    AssertEqual(group.Id, state.GetOwner(migrant.Id));
    AssertEqual(1, state.GetSettlementPopulation(target.Id).Total);
}

static void TestMigrationGroupSerialization()
{
    var state = new WorldState(12345);
    var source = state.CreateSettlement("starving-camp", "Starving Camp", "Pirate");
    var target = state.CreateSettlement("safe-camp", "Safe Camp", "Pirate");
    state.CreateMigrationGroup(source.Id, target.Id, "Pirate", 240_000, 360_000, "starvation");

    var restored = WorldStateCodec.Deserialize(WorldStateCodec.Serialize(state));
    var group = restored.MigrationGroups.Single();

    AssertEqual(EntityKind.MigrationGroup, group.Id.Kind);
    AssertEqual(source.Id, group.SourceSettlementId);
    AssertEqual(target.Id, group.TargetSettlementId);
    AssertEqual(MigrationGroupStatus.Traveling, group.Status);
    AssertEqual(360_000, group.ArrivalTick);
}

static void TestPublicApiOwnershipQuery()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "pirates");
    var citizen = state.CreateCitizen("Raider", 28, Sex.Male, "soldier", settlement.Id);

    ILivingWorldApi api = new LivingWorldApi(state);

    AssertEqual(settlement.Id, api.GetOwner(citizen.Id));
}

static void TestRaidPlannerAllocatesRealAssets()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "pirates");
    for (var i = 0; i < 5; i++)
    {
        state.CreateCitizen($"Raider {i + 1}", 20 + i, Sex.Male, "soldier", settlement.Id);
    }

    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 40);
    state.AddResource(settlement.Id, "Steel", 100);

    var result = RaidPlanner.PlanRaid(
        state,
        new RaidPlanRequest(settlement.Id, "test raid", 3, 12, 30));

    AssertEqual(RaidPlanStatus.Success, result.Status);
    AssertEqual(1, state.Armies.Count);
    AssertEqual(2, state.GetSettlementPopulation(settlement.Id).Total);
    AssertEqual(3, state.Citizens.Count(citizen => state.GetOwner(citizen.Id) == result.Army!.Id));
    AssertEqual(28, state.GetOwnedResourceQuantity(settlement.Id, "PackagedSurvivalMeal"));
    AssertEqual(70, state.GetOwnedResourceQuantity(settlement.Id, "Steel"));
    AssertEqual(12, state.GetOwnedResourceQuantity(result.Army!.Id, "PackagedSurvivalMeal"));
    AssertEqual(30, state.GetOwnedResourceQuantity(result.Army.Id, "Steel"));
}

static void TestRaidPlannerRejectsInsufficientCombatants()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "pirates");
    state.CreateCitizen("Raider 1", 24, Sex.Male, "soldier", settlement.Id);
    state.CreateCitizen("Raider 2", 25, Sex.Female, "soldier", settlement.Id);
    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 40);
    state.AddResource(settlement.Id, "Steel", 100);

    var result = RaidPlanner.PlanRaid(
        state,
        new RaidPlanRequest(settlement.Id, "too large raid", 3, 12, 30));

    AssertEqual(RaidPlanStatus.InsufficientCombatants, result.Status);
    AssertEqual(0, state.Armies.Count);
    AssertEqual(2, state.GetSettlementPopulation(settlement.Id).Total);
    AssertEqual(40, state.GetOwnedResourceQuantity(settlement.Id, "PackagedSurvivalMeal"));
    AssertEqual(100, state.GetOwnedResourceQuantity(settlement.Id, "Steel"));
}

static void TestRaidPlannerRejectsInsufficientSupplies()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "pirates");
    for (var i = 0; i < 5; i++)
    {
        state.CreateCitizen($"Raider {i + 1}", 20 + i, Sex.Male, "soldier", settlement.Id);
    }

    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 4);
    state.AddResource(settlement.Id, "Steel", 100);

    var result = RaidPlanner.PlanRaid(
        state,
        new RaidPlanRequest(settlement.Id, "under supplied raid", 3, 12, 30));

    AssertEqual(RaidPlanStatus.InsufficientResources, result.Status);
    AssertEqual(0, state.Armies.Count);
    AssertEqual(5, state.GetSettlementPopulation(settlement.Id).Total);
    AssertEqual(4, state.GetOwnedResourceQuantity(settlement.Id, "PackagedSurvivalMeal"));
    AssertEqual(100, state.GetOwnedResourceQuantity(settlement.Id, "Steel"));
}

static void TestRaidPopulationAllocatorReservesFactionCombatants()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "Pirate");
    for (var i = 0; i < 5; i++)
    {
        state.CreateCitizen($"Raider {i + 1}", 20 + i, Sex.Male, "soldier", settlement.Id);
    }
    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 10);

    var result = RaidPopulationAllocator.ReserveForRaid(
        state,
        new RaidPopulationAllocationRequest("Pirate", "vanilla raid", 3));

    AssertEqual(RaidPopulationAllocationStatus.Success, result.Status);
    AssertEqual(3, result.ReservedCombatants);
    AssertEqual(5, result.AvailableCombatants);
    AssertEqual(1, state.Armies.Count);
    AssertEqual(2, state.GetSettlementPopulation(settlement.Id).Total);
    AssertEqual(3, state.Citizens.Count(citizen => state.GetOwner(citizen.Id) == result.Army!.Id));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.RaidLaunched));
}

static void TestRaidPopulationAllocatorCapsToAvailableAdults()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "Pirate");
    state.CreateCitizen("Raider 1", 24, Sex.Male, "soldier", settlement.Id);
    state.CreateCitizen("Raider 2", 25, Sex.Female, "soldier", settlement.Id);
    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 4);

    var result = RaidPopulationAllocator.ReserveForRaid(
        state,
        new RaidPopulationAllocationRequest("Pirate", "large vanilla raid", 5));

    AssertEqual(RaidPopulationAllocationStatus.Success, result.Status);
    AssertEqual(2, result.ReservedCombatants);
    AssertEqual(2, result.AvailableCombatants);
    AssertEqual(0, state.GetSettlementPopulation(settlement.Id).Total);
    AssertEqual(2, state.Citizens.Count(citizen => state.GetOwner(citizen.Id) == result.Army!.Id));
}

static void TestTradeIntelCreatesRaidOpportunity()
{
    var state = new WorldState(12345);

    var result = RaidIntelService.RecordTradeIntel(
        state,
        new TradeIntelRequest("Pirate", 6000, 2, "gold and psychite trade"));

    AssertEqual(IntelReportStatus.Accepted, result.Status);
    AssertEqual(1, state.IntelReports.Count);
    AssertEqual(1, state.RaidOpportunities.Count);
    AssertEqual(IntelSourceKind.Trade, result.IntelReport!.SourceKind);
    AssertEqual("Pirate", result.RaidOpportunity!.FactionId);
    AssertEqual(RaidOpportunityStatus.Active, result.RaidOpportunity.Status);
    AssertEqual(12, result.RaidOpportunity.CombatantDemand);
}

static void TestRaidOpportunityConsumesOnce()
{
    var state = new WorldState(12345);

    RaidIntelService.RecordTradeIntel(
        state,
        new TradeIntelRequest("Pirate", 1200, 1, "small gold sale"));

    var first = RaidOpportunityService.TryConsumeBestOpportunity(state, "Pirate", out var opportunity);
    var second = RaidOpportunityService.TryConsumeBestOpportunity(state, "Pirate", out _);

    AssertEqual(true, first);
    AssertEqual(RaidOpportunityStatus.Consumed, state.GetRaidOpportunity(opportunity!.Id)!.Status);
    AssertEqual(false, second);
}

static void TestFactionKnowledgeRecordsTradeIntelAboutPlayer()
{
    var state = new WorldState(12345);
    state.AdvanceToTick(60_000);

    var result = RaidIntelService.RecordTradeIntel(
        state,
        new TradeIntelRequest("Pirate", 6000, 2, "gold and psychite trade"));

    AssertEqual(IntelReportStatus.Accepted, result.Status);
    AssertEqual(1, state.RaidIntelFacts.Count);
    AssertEqual(IntelSourceKind.Trade, result.RaidIntelFact!.SourceKind);
    AssertEqual("Pirate", result.RaidIntelFact.FactionId);
    AssertEqual(RaidIntelTargetKind.PlayerColony, result.RaidIntelFact.TargetKind);
    AssertEqual(RaidIntelValueBand.High, result.RaidIntelFact.ValueBand);
    AssertEqual(70, result.RaidIntelFact.Confidence);
    AssertEqual(60_000, result.RaidIntelFact.CreatedTick);
    AssertEqual(60_000 + RaidIntelService.DefaultTradeIntelLifetimeTicks, result.RaidIntelFact.ExpiresTick);
    AssertEqual(false, result.RaidIntelFact.Summary.Contains("6000", StringComparison.Ordinal));
}

static void TestRaidIntelExpiresAndStopsCreatingNewIntent()
{
    var state = new WorldState(12345);
    state.AdvanceToTick(10);

    var result = RaidIntelService.RecordTradeIntel(
        state,
        new TradeIntelRequest("Pirate", 2000, 0, "gold sale"));

    state.AdvanceToTick(result.RaidIntelFact!.ExpiresTick + 1);
    var created = RaidIntentService.TryCreateBestIntent(
        state,
        new RaidIntentRequest("Pirate", FactionHostility.Neutral),
        out var intent);

    AssertEqual(false, created);
    AssertEqual(null, intent);
}

static void TestFactionKnowledgeServiceListsActiveRaidIntelFacts()
{
    var state = new WorldState(12345);
    state.AdvanceToTick(100);
    var first = RaidIntelService.RecordTradeIntel(
        state,
        new TradeIntelRequest("Pirate", 1500, 0, "gold sale")).RaidIntelFact!;
    RaidIntelService.RecordTradeIntel(
        state,
        new TradeIntelRequest("Outlander", 3000, 0, "weapon sale"));
    state.AdvanceToTick(first.ExpiresTick + 1);
    RaidIntelService.RecordTradeIntel(
        state,
        new TradeIntelRequest("Pirate", 2500, 0, "new gold sale"));

    var active = FactionKnowledgeService.GetActiveRaidIntelFacts(state, "Pirate").ToList();

    AssertEqual(1, active.Count);
    AssertEqual(RaidIntelValueBand.High, active[0].ValueBand);
    AssertEqual(false, active[0].IsExpired(state.CurrentTick));
}

static void TestRaidPreparationReservesRealCitizensAndSupplies()
{
    var state = new WorldState(12345);
    state.AdvanceToTick(100);
    var settlement = state.CreateSettlement("pirate-den-101", "Pirate Den", "Pirate");
    for (var i = 0; i < 5; i++)
    {
        state.CreateCitizen($"Raider {i}", 25 + i, i % 2 == 0 ? Sex.Male : Sex.Female, "raider", settlement.Id);
    }

    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 10);
    RaidIntelService.RecordTradeIntel(state, new TradeIntelRequest("Pirate", 1500, 0, "gold sale"));
    var intentCreated = RaidIntentService.TryCreateBestIntent(
        state,
        new RaidIntentRequest("Pirate", FactionHostility.Hostile),
        out var intent);

    var preparation = RaidPreparationService.PrepareRaid(
        state,
        new RaidPreparationRequest(intent!, "PackagedSurvivalMeal", 1, 60_000));

    AssertEqual(true, intentCreated);
    AssertEqual(RaidPreparationStatus.Ready, preparation.Status);
    AssertEqual(settlement.Id, preparation.SourceSettlementId);
    AssertEqual(3, preparation.ReservedCombatants);
    AssertEqual(3, preparation.ReservedSupplies);
    AssertEqual(3, state.Citizens.Count(citizen => state.GetOwner(citizen.Id) == preparation.ArmyId));
    AssertEqual(3, state.GetOwnedResourceQuantity(preparation.ArmyId, "PackagedSurvivalMeal"));
    AssertEqual(7, state.GetOwnedResourceQuantity(settlement.Id, "PackagedSurvivalMeal"));
}

static void TestStaleRaidPreparationReturnsReservedCitizensAndResources()
{
    var state = new WorldState(12345);
    state.AdvanceToTick(100);
    var settlement = state.CreateSettlement("pirate-den-101", "Pirate Den", "Pirate");
    for (var i = 0; i < 4; i++)
    {
        state.CreateCitizen($"Raider {i}", 30, Sex.Male, "raider", settlement.Id);
    }

    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 10);
    RaidIntelService.RecordTradeIntel(state, new TradeIntelRequest("Pirate", 1000, 0, "gold sale"));
    RaidIntentService.TryCreateBestIntent(
        state,
        new RaidIntentRequest("Pirate", FactionHostility.Hostile),
        out var intent);
    var preparation = RaidPreparationService.PrepareRaid(
        state,
        new RaidPreparationRequest(intent!, "PackagedSurvivalMeal", 1, 10));

    state.AdvanceToTick(preparation.ExpiresTick + 1);
    var released = RaidPreparationService.ReleaseExpiredPreparations(state, state.CurrentTick);

    AssertEqual(1, released);
    AssertEqual(RaidPreparationStatus.Released, state.GetRaidPreparation(preparation.Id)!.Status);
    AssertEqual(4, state.GetSettlementPopulation(settlement.Id).Adults);
    AssertEqual(4, state.Citizens.Count(citizen => state.GetOwner(citizen.Id) == settlement.Id));
    AssertEqual(10, state.GetOwnedResourceQuantity(settlement.Id, "PackagedSurvivalMeal"));
    AssertEqual(0, state.GetOwnedResourceQuantity(preparation.ArmyId, "PackagedSurvivalMeal"));
}

static void TestLaunchedRaidPreparationIsNotExpiredAsStale()
{
    var state = new WorldState(12345);
    state.AdvanceToTick(100);
    var settlement = state.CreateSettlement("pirate-den-101", "Pirate Den", "Pirate");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen($"Raider {i}", 30, Sex.Male, "raider", settlement.Id);
    }

    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 10);
    RaidIntelService.RecordTradeIntel(state, new TradeIntelRequest("Pirate", 1000, 0, "gold sale"));
    RaidIntentService.TryCreateBestIntent(
        state,
        new RaidIntentRequest("Pirate", FactionHostility.Hostile),
        out var intent);
    var preparation = RaidPreparationService.PrepareRaid(
        state,
        new RaidPreparationRequest(intent!, "PackagedSurvivalMeal", 1, 10));

    var launched = state.LaunchRaidPreparation(preparation.Id);
    state.AdvanceToTick(preparation.ExpiresTick + 1);
    var released = RaidPreparationService.ReleaseExpiredPreparations(state, state.CurrentTick);

    AssertEqual(RaidPreparationStatus.Launched, launched.Status);
    AssertEqual(0, released);
    AssertEqual(RaidPreparationStatus.Launched, state.GetRaidPreparation(preparation.Id)!.Status);
    AssertEqual(2, state.Citizens.Count(citizen => state.GetOwner(citizen.Id) == preparation.ArmyId));
    AssertEqual(2, state.GetOwnedResourceQuantity(preparation.ArmyId, "PackagedSurvivalMeal"));
}

static void TestRaidPreparationTerminalLifecycleCannotBeReversed()
{
    var state = new WorldState(12345);
    state.AdvanceToTick(100);
    var settlement = state.CreateSettlement("pirate-den-101", "Pirate Den", "Pirate");
    for (var i = 0; i < 4; i++)
    {
        state.CreateCitizen($"Raider {i}", 30, Sex.Male, "raider", settlement.Id);
    }

    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 10);
    RaidIntelService.RecordTradeIntel(state, new TradeIntelRequest("Pirate", 1000, 0, "gold sale"));
    RaidIntentService.TryCreateBestIntent(
        state,
        new RaidIntentRequest("Pirate", FactionHostility.Hostile),
        out var intent);
    var launchedPrep = RaidPreparationService.PrepareRaid(
        state,
        new RaidPreparationRequest(intent!, "PackagedSurvivalMeal", 1, 60_000));
    var releasedPrep = RaidPreparationService.PrepareRaid(
        state,
        new RaidPreparationRequest(intent!, "PackagedSurvivalMeal", 1, 60_000));

    state.LaunchRaidPreparation(launchedPrep.Id);
    state.ReleaseRaidPreparation(launchedPrep.Id);
    state.ReleaseRaidPreparation(releasedPrep.Id);
    state.LaunchRaidPreparation(releasedPrep.Id);

    AssertEqual(RaidPreparationStatus.Launched, state.GetRaidPreparation(launchedPrep.Id)!.Status);
    AssertEqual(RaidPreparationStatus.Released, state.GetRaidPreparation(releasedPrep.Id)!.Status);
}

static void TestRaidPreparationFailsWithoutLeakingCitizensWhenSuppliesInsufficient()
{
    var state = new WorldState(12345);
    state.AdvanceToTick(100);
    var settlement = state.CreateSettlement("pirate-den-101", "Pirate Den", "Pirate");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen($"Raider {i}", 30, Sex.Male, "raider", settlement.Id);
    }

    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 1);
    RaidIntelService.RecordTradeIntel(state, new TradeIntelRequest("Pirate", 1500, 0, "gold sale"));
    RaidIntentService.TryCreateBestIntent(
        state,
        new RaidIntentRequest("Pirate", FactionHostility.Hostile),
        out var intent);

    AssertThrows<InvalidOperationException>(() =>
        RaidPreparationService.PrepareRaid(
            state,
            new RaidPreparationRequest(intent!, "PackagedSurvivalMeal", 1, 60_000)));

    AssertEqual(0, state.RaidPreparations.Count);
    AssertEqual(3, state.Citizens.Count(citizen => state.GetOwner(citizen.Id) == settlement.Id));
    AssertEqual(1, state.GetOwnedResourceQuantity(settlement.Id, "PackagedSurvivalMeal"));
}

static void TestRaidPreparationSurvivesSaveLoad()
{
    var state = new WorldState(12345);
    state.AdvanceToTick(100);
    var settlement = state.CreateSettlement("pirate-den-101", "Pirate Den", "Pirate");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen($"Raider {i}", 28, Sex.Female, "raider", settlement.Id);
    }

    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 5);
    RaidIntelService.RecordTradeIntel(state, new TradeIntelRequest("Pirate", 1000, 0, "gold sale"));
    RaidIntentService.TryCreateBestIntent(
        state,
        new RaidIntentRequest("Pirate", FactionHostility.Hostile),
        out var intent);
    var preparation = RaidPreparationService.PrepareRaid(
        state,
        new RaidPreparationRequest(intent!, "PackagedSurvivalMeal", 1, 60_000));

    var restored = WorldStateCodec.Deserialize(WorldStateCodec.Serialize(state));
    var savedPreparation = restored.GetRaidPreparation(preparation.Id)!;

    AssertEqual(preparation.Id, savedPreparation.Id);
    AssertEqual(RaidPreparationStatus.Ready, savedPreparation.Status);
    AssertEqual(preparation.ArmyId, savedPreparation.ArmyId);
    AssertEqual(1, restored.RaidIntelFacts.Count);
    AssertEqual(1, restored.RaidPreparations.Count);
    AssertEqual(2, restored.GetOwnedResourceQuantity(savedPreparation.ArmyId, "PackagedSurvivalMeal"));
}

static void TestMaterializationLeaseReservesConcreteCitizens()
{
    var state = new WorldState(12345);
    state.AdvanceToTick(100);
    var settlement = state.CreateSettlement("visitor-town", "Visitor Town", "Outlander");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen($"Visitor {i}", 24 + i, Sex.Female, "settler", settlement.Id);
    }

    var result = MaterializationLeaseService.CreateLeases(
        state,
        new MaterializationLeaseRequest(
            settlement.Id,
            settlement.Id,
            MaterializationPurpose.SettlementVisit,
            "visit:player-map",
            2,
            600));

    AssertEqual(MaterializationLeaseStatus.Success, result.Status);
    AssertEqual(2, result.Leases.Count);
    AssertEqual(2, state.MaterializationLeases.Count);
    AssertEqual(2, state.Events.Count(e => e.Kind == WorldEventKind.MaterializationLeaseCreated));
    AssertEqual(settlement.Id, result.Leases[0].SourceOwnerId);
    AssertEqual(settlement.Id, result.Leases[0].ReturnOwnerId);
    AssertEqual(MaterializationLeaseLifecycle.Reserved, result.Leases[0].Lifecycle);
    AssertEqual(700, result.Leases[0].ExpiresTick);
}

static void TestMaterializationLeaseBlocksDoubleActiveLeasing()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("visitor-town", "Visitor Town", "Outlander");
    state.CreateCitizen("Visitor", 30, Sex.Male, "settler", settlement.Id);

    var first = MaterializationLeaseService.CreateLeases(
        state,
        new MaterializationLeaseRequest(
            settlement.Id,
            settlement.Id,
            MaterializationPurpose.SettlementVisit,
            "visit:first",
            1,
            600));
    var second = MaterializationLeaseService.CreateLeases(
        state,
        new MaterializationLeaseRequest(
            settlement.Id,
            settlement.Id,
            MaterializationPurpose.TradeCaravan,
            "trade:second",
            1,
            600));

    AssertEqual(MaterializationLeaseStatus.Success, first.Status);
    AssertEqual(MaterializationLeaseStatus.InsufficientCitizens, second.Status);
    AssertEqual(1, state.MaterializationLeases.Count);
}

static void TestExpiredMaterializationLeaseReleasesReservedCitizen()
{
    var state = new WorldState(12345);
    state.AdvanceToTick(100);
    var settlement = state.CreateSettlement("visitor-town", "Visitor Town", "Outlander");
    state.CreateCitizen("Visitor", 30, Sex.Male, "settler", settlement.Id);

    MaterializationLeaseService.CreateLeases(
        state,
        new MaterializationLeaseRequest(
            settlement.Id,
            settlement.Id,
            MaterializationPurpose.SettlementVisit,
            "visit:first",
            1,
            10));
    state.AdvanceToTick(111);
    var released = MaterializationLeaseService.ReleaseExpiredLeases(state, state.CurrentTick);
    var second = MaterializationLeaseService.CreateLeases(
        state,
        new MaterializationLeaseRequest(
            settlement.Id,
            settlement.Id,
            MaterializationPurpose.TradeCaravan,
            "trade:second",
            1,
            600));

    AssertEqual(1, released);
    AssertEqual(MaterializationLeaseStatus.Success, second.Status);
    AssertEqual(2, state.MaterializationLeases.Count);
    AssertEqual(1, state.MaterializationLeases.Count(lease => lease.Lifecycle == MaterializationLeaseLifecycle.Released));
}

static void TestMaterializationLeaseReconcilesPawnFate()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("visitor-town", "Visitor Town", "Outlander");
    state.CreateCitizen("Dead Visitor", 30, Sex.Male, "settler", settlement.Id);
    state.CreateCitizen("Returned Visitor", 31, Sex.Female, "settler", settlement.Id);
    state.CreateCitizen("Missing Visitor", 32, Sex.Male, "settler", settlement.Id);
    state.CreateCitizen("Prisoner Visitor", 33, Sex.Female, "settler", settlement.Id);

    var leases = MaterializationLeaseService.CreateLeases(
        state,
        new MaterializationLeaseRequest(
            settlement.Id,
            settlement.Id,
            MaterializationPurpose.SettlementVisit,
            "visit:player-map",
            4,
            600)).Leases;

    MaterializationLeaseService.BindPawn(state, leases[0].Id, 101);
    MaterializationLeaseService.BindPawn(state, leases[1].Id, 102);
    MaterializationLeaseService.BindPawn(state, leases[2].Id, 103);
    MaterializationLeaseService.BindPawn(state, leases[3].Id, 104);

    AssertEqual(MaterializationLeaseResolveStatus.Success, MaterializationLeaseService.Resolve(
        state,
        new MaterializationLeaseResolveRequest(leases[0].Id, PawnFateKind.Dead, "killed on player map")).Status);
    AssertEqual(MaterializationLeaseResolveStatus.Success, MaterializationLeaseService.Resolve(
        state,
        new MaterializationLeaseResolveRequest(leases[1].Id, PawnFateKind.Returned, "left the map alive")).Status);
    AssertEqual(MaterializationLeaseResolveStatus.Success, MaterializationLeaseService.Resolve(
        state,
        new MaterializationLeaseResolveRequest(leases[2].Id, PawnFateKind.Missing, "map despawned while downed")).Status);
    AssertEqual(MaterializationLeaseResolveStatus.Success, MaterializationLeaseService.Resolve(
        state,
        new MaterializationLeaseResolveRequest(leases[3].Id, PawnFateKind.Prisoner, "captured by player")).Status);

    AssertEqual(CitizenStatus.Dead, state.GetCitizen(leases[0].CitizenId)!.Status);
    AssertEqual(CitizenStatus.Alive, state.GetCitizen(leases[1].CitizenId)!.Status);
    AssertEqual(CitizenStatus.Missing, state.GetCitizen(leases[2].CitizenId)!.Status);
    AssertEqual(CitizenStatus.Prisoner, state.GetCitizen(leases[3].CitizenId)!.Status);
    AssertEqual(settlement.Id, state.GetOwner(leases[1].CitizenId));
    AssertEqual(MaterializationLeaseLifecycle.Returned, state.GetMaterializationLease(leases[1].Id)!.Lifecycle);
    AssertEqual(MaterializationLeaseResolveStatus.AlreadyResolved, MaterializationLeaseService.Resolve(
        state,
        new MaterializationLeaseResolveRequest(leases[1].Id, PawnFateKind.Returned, "again")).Status);
}

static void TestPawnFateSyncResolvesMaterializationLease()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("visitor-town", "Visitor Town", "Outlander");
    state.CreateCitizen("Visitor", 30, Sex.Female, "settler", settlement.Id);
    var lease = MaterializationLeaseService.CreateLeases(
        state,
        new MaterializationLeaseRequest(
            settlement.Id,
            settlement.Id,
            MaterializationPurpose.SettlementVisit,
            "visit:player-map",
            1,
            600)).Leases[0];
    MaterializationLeaseService.BindPawn(state, lease.Id, 777);

    var result = LivingWorldPawnSyncService.Apply(
        state,
        new PawnFateSyncRequest(lease.CitizenId, PawnFateKind.Missing, "lost on generated map"));

    AssertEqual(PawnFateSyncStatus.Success, result.Status);
    AssertEqual(CitizenStatus.Missing, state.GetCitizen(lease.CitizenId)!.Status);
    AssertEqual(MaterializationLeaseLifecycle.Missing, state.GetMaterializationLease(lease.Id)!.Lifecycle);
}

static void TestMaterializationLeaseSurvivesSaveLoad()
{
    var state = new WorldState(12345);
    state.AdvanceToTick(100);
    var settlement = state.CreateSettlement("visitor-town", "Visitor Town", "Outlander");
    state.CreateCitizen("Visitor", 30, Sex.Female, "settler", settlement.Id);
    var lease = MaterializationLeaseService.CreateLeases(
        state,
        new MaterializationLeaseRequest(
            settlement.Id,
            settlement.Id,
            MaterializationPurpose.SettlementVisit,
            "visit:player-map",
            1,
            600)).Leases[0];
    MaterializationLeaseService.BindPawn(state, lease.Id, 777);

    var restored = WorldStateCodec.Deserialize(WorldStateCodec.Serialize(state));
    var restoredLease = restored.GetMaterializationLease(lease.Id)!;

    AssertEqual(1, restored.MaterializationLeases.Count);
    AssertEqual(lease.CitizenId, restoredLease.CitizenId);
    AssertEqual(777, restoredLease.PawnThingId);
    AssertEqual(MaterializationLeaseLifecycle.Materialized, restoredLease.Lifecycle);
    AssertEqual(MaterializationPurpose.SettlementVisit, restoredLease.Purpose);
}

static void TestTradeLedgerSettlementReceivesSoldGoods()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("market-town", "Market Town", "Outlander");

    var result = SettlementTradeLedgerService.RecordTrade(
        state,
        new SettlementTradeLedgerRequest(
            "Outlander",
            "Gold",
            5,
            SettlementTradeDirection.SettlementReceives,
            1250,
            5,
            "player sold gold"));

    AssertEqual(SettlementTradeLedgerStatus.Success, result.Status);
    AssertEqual(settlement.Id, result.SettlementId);
    AssertEqual(5, result.QuantityApplied);
    AssertEqual(5, state.GetOwnedResourceQuantity(settlement.Id, "Gold"));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.SettlementTradeRecorded));
    AssertEqual(1, state.IntelReports.Count);
    AssertEqual(1, state.RaidOpportunities.Count);
}

static void TestTradeLedgerSettlementProvidesPurchasedGoods()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("market-town", "Market Town", "Outlander");
    state.AddResource(settlement.Id, "MedicineIndustrial", 8);

    var result = SettlementTradeLedgerService.RecordTrade(
        state,
        new SettlementTradeLedgerRequest(
            "Outlander",
            "MedicineIndustrial",
            5,
            SettlementTradeDirection.SettlementProvides,
            250,
            0,
            "player bought medicine"));

    AssertEqual(SettlementTradeLedgerStatus.Success, result.Status);
    AssertEqual(5, result.QuantityApplied);
    AssertEqual(3, state.GetOwnedResourceQuantity(settlement.Id, "MedicineIndustrial"));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.SettlementTradeRecorded));
    AssertEqual(1, state.IntelReports.Count);
    AssertEqual(0, state.RaidOpportunities.Count);
}

static void TestTradeLedgerNoSettlementFallsBackToIntel()
{
    var state = new WorldState(12345);

    var result = SettlementTradeLedgerService.RecordTrade(
        state,
        new SettlementTradeLedgerRequest(
            "UnknownFaction",
            "Gold",
            4,
            SettlementTradeDirection.SettlementReceives,
            1000,
            4,
            "trade with untracked faction"));

    AssertEqual(SettlementTradeLedgerStatus.NoSettlement, result.Status);
    AssertEqual(null, result.SettlementId);
    AssertEqual(0, result.QuantityApplied);
    AssertEqual(0, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.SettlementTradeRecorded));
    AssertEqual(1, state.IntelReports.Count);
    AssertEqual(1, state.RaidOpportunities.Count);
}

static void TestPublicSettlementKnowledgeUsesEstimates()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "Pirate");
    for (var i = 0; i < 23; i++)
    {
        state.CreateCitizen($"Citizen {i + 1}", 20 + i % 30, Sex.Male, "settler", settlement.Id);
    }

    var known = PlayerKnowledgeService.RecordPublicSettlementInfo(
        state,
        settlement.Id,
        "public settlement notice");

    AssertEqual(settlement.Id, known.SettlementId);
    AssertEqual(IntelSourceKind.Public, known.SourceKind);
    AssertEqual(KnowledgeConfidence.Medium, known.Confidence);
    AssertEqual(SettlementPopulationBand.Medium, known.PopulationBand);
    AssertEqual(SettlementFoodKnowledge.Shortage, known.Food);
    AssertEqual(SettlementProductionKnowledge.Unknown, known.Production);
    AssertEqual(false, known.ExactValuesVisible);
    AssertEqual(1, state.KnownSettlementInfos.Count);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.SettlementIntelUpdated));
}

static void TestTraderSettlementKnowledgeUpdatesConfidence()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "Pirate");
    for (var i = 0; i < 6; i++)
    {
        state.CreateCitizen($"Citizen {i + 1}", 20 + i, Sex.Male, "settler", settlement.Id);
    }
    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 60);
    state.AdvanceToTick(120_000);

    PlayerKnowledgeService.RecordPublicSettlementInfo(state, settlement.Id, "public rumor");
    var known = PlayerKnowledgeService.RecordTraderSettlementInfo(
        state,
        settlement.Id,
        "trader saw full granaries");
    var roundTripped = WorldStateCodec.Deserialize(WorldStateCodec.Serialize(state));
    var savedKnown = roundTripped.GetKnownSettlementInfo(settlement.Id)!;

    AssertEqual(IntelSourceKind.Trade, known.SourceKind);
    AssertEqual(KnowledgeConfidence.High, known.Confidence);
    AssertEqual(SettlementFoodKnowledge.Stable, known.Food);
    AssertEqual(SettlementProductionKnowledge.Unknown, known.Production);
    AssertEqual(120_000, known.Tick);
    AssertEqual("trader saw full granaries", known.Summary);
    AssertEqual(IntelSourceKind.Trade, savedKnown.SourceKind);
    AssertEqual(KnowledgeConfidence.High, savedKnown.Confidence);
    AssertEqual(SettlementProductionKnowledge.Unknown, savedKnown.Production);
}

static void TestProductionKnowledgeVisibility()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("forge-town", "Forge Town", "Outlander");
    for (var i = 0; i < 4; i++)
    {
        state.CreateCitizen($"Worker {i + 1}", 24 + i, Sex.Male, "worker", settlement.Id);
    }

    state.RecordSettlementProductionProfile(SettlementProductionProfile.FromEnvironment(
        settlement.Id,
        new SettlementProductionEnvironment("TemperateForest", "SmallHills", "Industrial", 55, 850, 21)));

    var publicInfo = PlayerKnowledgeService.RecordPublicSettlementInfo(
        state,
        settlement.Id,
        "public market notice");
    var directInfo = PlayerKnowledgeService.RecordDirectVisitSettlementInfo(
        state,
        settlement.Id,
        "player visited settlement map");
    var roundTripped = WorldStateCodec.Deserialize(WorldStateCodec.Serialize(state));
    var savedKnown = roundTripped.GetKnownSettlementInfo(settlement.Id)!;

    AssertEqual(SettlementProductionKnowledge.Strong, publicInfo.Production);
    AssertEqual(false, publicInfo.ExactValuesVisible);
    AssertEqual(SettlementProductionKnowledge.Strong, directInfo.Production);
    AssertEqual(true, directInfo.ExactValuesVisible);
    AssertEqual(KnowledgeConfidence.Confirmed, directInfo.Confidence);
    AssertEqual(IntelSourceKind.DirectVisit, directInfo.SourceKind);
    AssertEqual(SettlementProductionKnowledge.Strong, savedKnown.Production);
    AssertEqual(true, savedKnown.ExactValuesVisible);
}

static void TestSettlementKnowledgeDoesNotDowngradeDirectVisit()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("forge-town", "Forge Town", "Outlander");
    state.CreateCitizen("Worker", 31, Sex.Female, "worker", settlement.Id);
    state.RecordSettlementProductionProfile(SettlementProductionProfile.FromEnvironment(
        settlement.Id,
        new SettlementProductionEnvironment("TemperateForest", "SmallHills", "Industrial", 55, 850, 21)));

    var direct = PlayerKnowledgeService.RecordDirectVisitSettlementInfo(
        state,
        settlement.Id,
        "direct visit");
    state.AdvanceToTick(240_000);
    var weaker = PlayerKnowledgeService.RecordTraderSettlementInfo(
        state,
        settlement.Id,
        "later trader rumor");
    var current = state.GetKnownSettlementInfo(settlement.Id)!;

    AssertEqual(direct, weaker);
    AssertEqual(IntelSourceKind.DirectVisit, current.SourceKind);
    AssertEqual(KnowledgeConfidence.Confirmed, current.Confidence);
    AssertEqual(true, current.ExactValuesVisible);
    AssertEqual("direct visit", current.Summary);
}

static void TestSettlementKnowledgeFreshness()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "Pirate");
    state.CreateCitizen("Citizen", 31, Sex.Female, "settler", settlement.Id);
    state.AdvanceToTick(60_000);

    var known = PlayerKnowledgeService.RecordTraderSettlementInfo(
        state,
        settlement.Id,
        "trader report");
    var fresh = PlayerKnowledgeService.GetFreshness(known, currentTick: 120_000, staleAfterTicks: 180_000);
    var stale = PlayerKnowledgeService.GetFreshness(known, currentTick: 300_001, staleAfterTicks: 180_000);

    AssertEqual(60_000, fresh.AgeTicks);
    AssertEqual(1, fresh.AgeDays);
    AssertEqual(false, fresh.IsStale);
    AssertEqual(240_001, stale.AgeTicks);
    AssertEqual(4, stale.AgeDays);
    AssertEqual(true, stale.IsStale);
}

static void TestRaidPawnBindingLinksPawnsToCitizens()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "Pirate");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen($"Raider {i + 1}", 22 + i, Sex.Male, "soldier", settlement.Id);
    }
    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 6);

    var allocation = RaidPopulationAllocator.ReserveForRaid(
        state,
        new RaidPopulationAllocationRequest("Pirate", "vanilla raid", 2));

    var result = RaidPawnBindingService.BindRaidPawns(
        state,
        allocation.Army!.Id,
        new[] { 101, 102, 103 });

    AssertEqual(RaidPawnBindingStatus.Success, result.Status);
    AssertEqual(2, result.BoundCount);
    AssertEqual(2, state.RaidPawnLinks.Count);
    AssertEqual(allocation.Army.Id, state.GetRaidPawnLink(101)!.ArmyId);
    AssertEqual(101, state.GetRaidPawnLink(101)!.PawnThingId);
    AssertEqual(RaidPawnLinkStatus.Active, state.GetRaidPawnLink(102)!.Status);
    AssertEqual(null, state.GetRaidPawnLink(103));
}

static void TestRaidPawnDeathMarksCitizenDead()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "Pirate");
    state.CreateCitizen("Raider 1", 24, Sex.Male, "soldier", settlement.Id);
    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 2);

    var allocation = RaidPopulationAllocator.ReserveForRaid(
        state,
        new RaidPopulationAllocationRequest("Pirate", "vanilla raid", 1));
    RaidPawnBindingService.BindRaidPawns(state, allocation.Army!.Id, new[] { 101 });

    var result = RaidPawnBindingService.MarkPawnDead(state, 101, "killed on player map");
    var link = state.GetRaidPawnLink(101)!;
    var citizen = state.GetCitizen(link.CitizenId)!;

    AssertEqual(RaidPawnCasualtyStatus.Success, result.Status);
    AssertEqual(CitizenStatus.Dead, citizen.Status);
    AssertEqual(RaidPawnLinkStatus.Dead, link.Status);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.CitizenDied));
}

static void TestRaidPawnCaptureMarksCitizenPrisoner()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "Pirate");
    state.CreateCitizen("Raider 1", 24, Sex.Male, "soldier", settlement.Id);
    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 2);

    var allocation = RaidPopulationAllocator.ReserveForRaid(
        state,
        new RaidPopulationAllocationRequest("Pirate", "vanilla raid", 1));
    RaidPawnBindingService.BindRaidPawns(state, allocation.Army!.Id, new[] { 101 });

    var result = RaidPawnBindingService.MarkPawnPrisoner(state, 101, "captured by player");
    var returnResult = RaidPawnBindingService.MarkPawnReturned(state, 101, "despawn after capture");
    var link = state.GetRaidPawnLink(101)!;

    AssertEqual(RaidPawnPrisonerStatus.Success, result.Status);
    AssertEqual(RaidPawnReturnStatus.AlreadyResolved, returnResult.Status);
    AssertEqual(RaidPawnLinkStatus.Prisoner, link.Status);
    AssertEqual(allocation.Army.Id, state.GetOwner(link.CitizenId));
    AssertEqual(CitizenStatus.Prisoner, state.GetCitizen(link.CitizenId)!.Status);
    AssertEqual(0, state.GetSettlementPopulation(settlement.Id).Total);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.RaidPawnCaptured));
}

static void TestRaidPawnReturnMovesCitizenHome()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "Pirate");
    state.CreateCitizen("Raider 1", 24, Sex.Male, "soldier", settlement.Id);
    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 2);

    var allocation = RaidPopulationAllocator.ReserveForRaid(
        state,
        new RaidPopulationAllocationRequest("Pirate", "vanilla raid", 1));
    RaidPawnBindingService.BindRaidPawns(state, allocation.Army!.Id, new[] { 101 });

    var result = RaidPawnBindingService.MarkPawnReturned(state, 101, "pawn exited map alive");
    var link = state.GetRaidPawnLink(101)!;

    AssertEqual(RaidPawnReturnStatus.Success, result.Status);
    AssertEqual(RaidPawnLinkStatus.Returned, link.Status);
    AssertEqual(settlement.Id, state.GetOwner(link.CitizenId));
    AssertEqual(CitizenStatus.Alive, state.GetCitizen(link.CitizenId)!.Status);
    AssertEqual(1, state.GetSettlementPopulation(settlement.Id).Total);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.RaidPawnReturned));
}

static void TestRaidOutcomeRecordedWhenRaidResolves()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "Pirate");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen($"Raider {i + 1}", 24 + i, Sex.Male, "soldier", settlement.Id);
    }
    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 6);

    var allocation = RaidPopulationAllocator.ReserveForRaid(
        state,
        new RaidPopulationAllocationRequest("Pirate", "vanilla raid", 3));
    RaidPawnBindingService.BindRaidPawns(state, allocation.Army!.Id, new[] { 101, 102, 103 });

    RaidPawnBindingService.MarkPawnDead(state, 101, "killed on player map");
    RaidPawnBindingService.MarkPawnReturned(state, 102, "fled the map");

    AssertEqual(0, state.RaidOutcomes.Count);

    RaidPawnBindingService.MarkPawnPrisoner(state, 103, "captured by player");
    RaidOutcomeService.TryRecordResolvedRaidOutcome(state, allocation.Army.Id);

    var outcome = state.RaidOutcomes.Single();

    AssertEqual(allocation.Army.Id, outcome.ArmyId);
    AssertEqual(settlement.Id, outcome.SourceSettlementId);
    AssertEqual(3, outcome.Sent);
    AssertEqual(0, outcome.Active);
    AssertEqual(1, outcome.Dead);
    AssertEqual(1, outcome.Returned);
    AssertEqual(1, outcome.Prisoner);
    AssertEqual(true, outcome.IsResolved);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.RaidResolved));

    RaidOutcomeService.TryRecordResolvedRaidOutcome(state, allocation.Army.Id);

    AssertEqual(1, state.RaidOutcomes.Count);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.RaidResolved));
}

static void TestDownedRaiderExitIsNotCountedAsReturn()
{
    // A raider that despawns while still downed has not made it home: it is lost
    // (Miss), not recorded as a safe return that revives the citizen.
    AssertEqual(RaidPawnExitAction.Miss, RaidPawnExitPolicy.Resolve(isDead: false, isPrisoner: false, isDowned: true));

    // A pawn walking off the map edge under its own power is a genuine return.
    AssertEqual(RaidPawnExitAction.Return, RaidPawnExitPolicy.Resolve(isDead: false, isPrisoner: false, isDowned: false));

    // Prisoner status wins even if the pawn is also downed when carried off.
    AssertEqual(RaidPawnExitAction.Capture, RaidPawnExitPolicy.Resolve(isDead: false, isPrisoner: true, isDowned: true));

    // Death is resolved by the kill patch, so the exit hook must ignore dead pawns.
    AssertEqual(RaidPawnExitAction.Ignore, RaidPawnExitPolicy.Resolve(isDead: true, isPrisoner: false, isDowned: false));
}

static void TestRaidPawnMissingMarksCitizenMissingAndResolvesRaid()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "Pirate");
    for (var i = 0; i < 2; i++)
    {
        state.CreateCitizen($"Raider {i + 1}", 24 + i, Sex.Male, "soldier", settlement.Id);
    }
    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 4);

    var allocation = RaidPopulationAllocator.ReserveForRaid(
        state,
        new RaidPopulationAllocationRequest("Pirate", "vanilla raid", 2));
    RaidPawnBindingService.BindRaidPawns(state, allocation.Army!.Id, new[] { 101, 102 });

    RaidPawnBindingService.MarkPawnDead(state, 101, "killed on player map");
    var result = RaidPawnBindingService.MarkPawnMissing(state, 102, "downed raider lost when map despawned");
    var link = state.GetRaidPawnLink(102)!;

    // A lost raider is neither dead, home, nor a prisoner: the citizen is Missing,
    // no longer counts toward its settlement, and does not revive.
    AssertEqual(RaidPawnCasualtyStatus.Success, result.Status);
    AssertEqual(RaidPawnLinkStatus.Missing, link.Status);
    AssertEqual(CitizenStatus.Missing, state.GetCitizen(link.CitizenId)!.Status);
    AssertEqual(0, state.GetSettlementPopulation(settlement.Id).Total);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.RaidPawnMissing));

    // Marking the last pawn resolves the raid: no active links remain.
    var outcome = state.RaidOutcomes.Single();
    AssertEqual(2, outcome.Sent);
    AssertEqual(0, outcome.Active);
    AssertEqual(1, outcome.Dead);
    AssertEqual(1, outcome.Missing);
    AssertEqual(true, outcome.IsResolved);

    // Re-marking a resolved pawn is a no-op.
    AssertEqual(RaidPawnCasualtyStatus.AlreadyResolved, RaidPawnBindingService.MarkPawnMissing(state, 102, "again").Status);
}

static void TestUndeployedReservedCombatantsReturnHome()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "Pirate");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen($"Raider {i + 1}", 24 + i, Sex.Male, "soldier", settlement.Id);
    }
    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 6);

    var allocation = RaidPopulationAllocator.ReserveForRaid(
        state,
        new RaidPopulationAllocationRequest("Pirate", "vanilla raid", 3));

    // Vanilla generated only one pawn: one citizen deploys, two stay reserved-but-idle.
    RaidPawnBindingService.BindRaidPawns(state, allocation.Army!.Id, new[] { 101 });

    var released = RaidReconciliationService.ReleaseUndeployedReserves(state, allocation.Army.Id);

    AssertEqual(2, released);
    // The two who never deployed are home again and counted by their settlement.
    AssertEqual(2, state.GetSettlementPopulation(settlement.Id).Total);
    // The one that deployed keeps its active link and stays with the army.
    AssertEqual(1, state.RaidPawnLinks.Count(link =>
        link.ArmyId == allocation.Army.Id && link.Status == RaidPawnLinkStatus.Active));
    AssertEqual(1, state.Citizens.Count(citizen => state.GetOwner(citizen.Id) == allocation.Army.Id));
    // Idempotent: nothing left to release.
    AssertEqual(0, RaidReconciliationService.ReleaseUndeployedReserves(state, allocation.Army.Id));
}

static void TestReleaseUndeployedReservesLeavesDeployedAndResolvedAlone()
{
    var state = new WorldState(777);
    var settlement = state.CreateSettlement("camp", "Camp", "Pirate");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen($"R{i + 1}", 25, Sex.Male, "soldier", settlement.Id);
    }
    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 6);

    var allocation = RaidPopulationAllocator.ReserveForRaid(
        state,
        new RaidPopulationAllocationRequest("Pirate", "raid", 3));
    // Two deploy, one stays reserved-but-idle.
    RaidPawnBindingService.BindRaidPawns(state, allocation.Army!.Id, new[] { 101, 102 });
    RaidPawnBindingService.MarkPawnDead(state, 101, "killed on player map");

    var released = RaidReconciliationService.ReleaseUndeployedReserves(state, allocation.Army.Id);

    // Only the never-deployed reservist returns; the dead and active raiders are untouched.
    AssertEqual(1, released);
    var deadLink = state.GetRaidPawnLink(101)!;
    AssertEqual(RaidPawnLinkStatus.Dead, deadLink.Status);
    AssertEqual(CitizenStatus.Dead, state.GetCitizen(deadLink.CitizenId)!.Status);
    AssertEqual(allocation.Army.Id, state.GetOwner(deadLink.CitizenId));
    AssertEqual(RaidPawnLinkStatus.Active, state.GetRaidPawnLink(102)!.Status);
}

static void TestAbortedRaidReleaseReturnsOrphanedReserves()
{
    var state = new WorldState(999);
    var settlement = state.CreateSettlement("camp", "Camp", "Pirate");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen($"R{i + 1}", 25, Sex.Male, "soldier", settlement.Id);
    }
    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 6);

    // Raid reserved combatants but was aborted before any pawn was generated/bound.
    RaidPopulationAllocator.ReserveForRaid(
        state,
        new RaidPopulationAllocationRequest("Pirate", "aborted raid", 3));
    AssertEqual(0, state.GetSettlementPopulation(settlement.Id).Total);

    var released = RaidReconciliationService.ReleaseAllUndeployedReserves(state);

    AssertEqual(3, released);
    AssertEqual(3, state.GetSettlementPopulation(settlement.Id).Total);
}

static void TestDrifterArrivalSpendsFiniteReservoir()
{
    var state = new WorldState(4242);
    state.AddDrifterArrivalReservoir(3, "test reservoir");
    var request = new DrifterArrivalRequest(
        Tick: 0,
        TargetWorldPopulation: 10,
        HardCeiling: 100,
        MaxArrivalsPerStep: 2);

    AssertEqual(2, DrifterArrivalService.SimulateArrivals(state, request with { Tick = 60_000 }).Arrived);
    AssertEqual(1, state.DrifterArrivalReservoir);
    AssertEqual(1, DrifterArrivalService.SimulateArrivals(state, request with { Tick = 120_000 }).Arrived);
    AssertEqual(0, state.DrifterArrivalReservoir);
    AssertEqual(0, DrifterArrivalService.SimulateArrivals(state, request with { Tick = 180_000 }).Arrived);
    AssertEqual(3, state.Drifters.Count);
}

static void TestDrifterReservoirSerializationRoundTrip()
{
    var state = new WorldState(4242);
    state.AddDrifterArrivalReservoir(7, "test reservoir");

    var restored = WorldStateCodec.Deserialize(WorldStateCodec.Serialize(state));

    AssertEqual(7, restored.DrifterArrivalReservoir);
    var result = DrifterArrivalService.SimulateArrivals(
        restored,
        new DrifterArrivalRequest(60_000, TargetWorldPopulation: 10, HardCeiling: 100, MaxArrivalsPerStep: 3));
    AssertEqual(3, result.Arrived);
    AssertEqual(4, restored.DrifterArrivalReservoir);
}

static void TestDrifterArrivalFillsTowardTarget()
{
    var state = new WorldState(4242);
    state.AddDrifterArrivalReservoir(10, "test reservoir");
    var request = new DrifterArrivalRequest(
        Tick: 0,
        TargetWorldPopulation: 5,
        HardCeiling: 100,
        MaxArrivalsPerStep: 2);

    // Empty world: metered arrivals of 2 fill toward the target, then a final 1,
    // then the tap idles at the target.
    AssertEqual(2, DrifterArrivalService.SimulateArrivals(state, request with { Tick = 60_000 }).Arrived);
    AssertEqual(2, DrifterArrivalService.SimulateArrivals(state, request with { Tick = 120_000 }).Arrived);
    var third = DrifterArrivalService.SimulateArrivals(state, request with { Tick = 180_000 });
    AssertEqual(1, third.Arrived);
    AssertEqual(5, third.PoolSize);
    AssertEqual(0, DrifterArrivalService.SimulateArrivals(state, request with { Tick = 240_000 }).Arrived);
    AssertEqual(5, state.Drifters.Count);
}

static void TestDrifterArrivalIdlesWhenWorldPopulated()
{
    var state = new WorldState(4242);
    var settlement = state.CreateSettlement("camp", "Camp", "Outlander");
    for (var i = 0; i < 6; i++)
    {
        state.CreateCitizen($"Settler {i + 1}", 25, Sex.Male, "settler", settlement.Id);
    }

    // Existing settlement population counts toward the target, so the tap stays idle.
    var result = DrifterArrivalService.SimulateArrivals(
        state,
        new DrifterArrivalRequest(60_000, TargetWorldPopulation: 5, HardCeiling: 100, MaxArrivalsPerStep: 2));

    AssertEqual(0, result.Arrived);
    AssertEqual(0, state.Drifters.Count);
}

static void TestDrifterArrivalRespectsHardCeiling()
{
    var state = new WorldState(4242);
    state.AddDrifterArrivalReservoir(10, "test reservoir");
    var request = new DrifterArrivalRequest(
        Tick: 0,
        TargetWorldPopulation: 100,
        HardCeiling: 4,
        MaxArrivalsPerStep: 2);

    AssertEqual(2, DrifterArrivalService.SimulateArrivals(state, request with { Tick = 60_000 }).Arrived);
    AssertEqual(2, DrifterArrivalService.SimulateArrivals(state, request with { Tick = 120_000 }).Arrived);
    // At the ceiling the tap stops even though the target is far higher.
    AssertEqual(0, DrifterArrivalService.SimulateArrivals(state, request with { Tick = 180_000 }).Arrived);
    AssertEqual(4, state.Drifters.Count);
}

static void TestDrifterArrivalDisabled()
{
    var state = new WorldState(4242);
    state.AddDrifterArrivalReservoir(10, "test reservoir");
    var result = DrifterArrivalService.SimulateArrivals(
        state,
        new DrifterArrivalRequest(60_000, TargetWorldPopulation: 50, HardCeiling: 100, MaxArrivalsPerStep: 0));

    AssertEqual(0, result.Arrived);
    AssertEqual(0, state.Drifters.Count);
}

static void TestDrifterArrivalRecordsUnaffiliated()
{
    var state = new WorldState(4242);
    state.AddDrifterArrivalReservoir(10, "test reservoir");
    var result = DrifterArrivalService.SimulateArrivals(
        state,
        new DrifterArrivalRequest(60_000, TargetWorldPopulation: 3, HardCeiling: 100, MaxArrivalsPerStep: 2));

    AssertEqual(2, result.Arrived);
    AssertEqual(2, state.Drifters.Count);
    AssertEqual(2, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.DrifterArrived));

    var drifter = state.Drifters.OrderBy(d => d.Id.Value).First();
    AssertEqual(EntityKind.Drifter, drifter.Id.Kind);
    AssertEqual(true, !string.IsNullOrWhiteSpace(drifter.Name));
    // A drifter belongs to no settlement and no owner yet.
    AssertEqual(null, state.GetOwner(drifter.Id));
}

static void TestMaterializeDrifterDrainsPool()
{
    var state = new WorldState(4242);
    var drifter = state.CreateDrifter("Wanderer", 30, Sex.Male);
    var before = state.Drifters.Count;

    var materialized = state.MaterializeDrifter(drifter.Id, pawnThingId: 777, tick: 60_000);

    AssertEqual(drifter.Id, materialized.Id);
    AssertEqual(before - 1, state.Drifters.Count);
    AssertEqual(null, state.GetDrifter(drifter.Id));
    AssertEqual(1, state.Events.Count(e => e.Kind == WorldEventKind.DrifterMaterialized));
}

static void TestMaterializeNewArrivalRecordsWithoutGrowingPool()
{
    var state = new WorldState(4242);
    var before = state.Drifters.Count;

    state.MaterializeNewArrival(pawnThingId: 555, tick: 60_000, name: "Stray", age: 25, sex: Sex.Female);

    AssertEqual(before, state.Drifters.Count);
    AssertEqual(1, state.Events.Count(e => e.Kind == WorldEventKind.DrifterMaterialized));
    AssertEqual(0, state.Events.Count(e => e.Kind == WorldEventKind.DrifterArrived));
}

static void TestDrifterAssimilationJoinsSettlement()
{
    var state = new WorldState(4242);
    var settlement = state.CreateSettlement("camp", "Camp", "Outlander");
    state.AddDrifterArrivalReservoir(10, "test reservoir");
    DrifterArrivalService.SimulateArrivals(
        state,
        new DrifterArrivalRequest(60_000, TargetWorldPopulation: 2, HardCeiling: 100, MaxArrivalsPerStep: 2));
    var drifter = state.Drifters.OrderBy(d => d.Id.Value).First();

    var result = DrifterAssimilationService.SimulateAssimilation(
        state,
        new DrifterAssimilationRequest(120_000, MaxAssimilationsPerStep: 5));

    AssertEqual(2, result.Assimilated);
    AssertEqual(0, state.Drifters.Count);
    AssertEqual(2, state.GetSettlementPopulation(settlement.Id).Total);
    AssertEqual(2, state.Events.Count(e => e.Kind == WorldEventKind.DrifterAssimilated));
    // The new citizen carries the drifter's identity into the settlement.
    var citizen = state.Citizens.First(c => c.Name == drifter.Name);
    AssertEqual(drifter.Age, citizen.Age);
    AssertEqual(drifter.Sex, citizen.Sex);
    AssertEqual(settlement.Id, state.GetOwner(citizen.Id));
}

static void TestDrifterAssimilationSpreadsAcrossSettlements()
{
    var state = new WorldState(4242);
    var first = state.CreateSettlement("a", "A", "Outlander");
    var second = state.CreateSettlement("b", "B", "Outlander");
    state.AddDrifterArrivalReservoir(10, "test reservoir");
    DrifterArrivalService.SimulateArrivals(
        state,
        new DrifterArrivalRequest(60_000, TargetWorldPopulation: 2, HardCeiling: 100, MaxArrivalsPerStep: 2));

    DrifterAssimilationService.SimulateAssimilation(
        state,
        new DrifterAssimilationRequest(120_000, MaxAssimilationsPerStep: 2));

    // Two empty settlements get one drifter each, not both into one.
    AssertEqual(1, state.GetSettlementPopulation(first.Id).Total);
    AssertEqual(1, state.GetSettlementPopulation(second.Id).Total);
}

static void TestDrifterAssimilationWithoutSettlement()
{
    var state = new WorldState(4242);
    state.AddDrifterArrivalReservoir(10, "test reservoir");
    DrifterArrivalService.SimulateArrivals(
        state,
        new DrifterArrivalRequest(60_000, TargetWorldPopulation: 2, HardCeiling: 100, MaxArrivalsPerStep: 2));

    var result = DrifterAssimilationService.SimulateAssimilation(
        state,
        new DrifterAssimilationRequest(120_000, MaxAssimilationsPerStep: 5));

    AssertEqual(0, result.Assimilated);
    AssertEqual(2, state.Drifters.Count);
}

static void TestDrifterAssimilationRespectsCap()
{
    var state = new WorldState(4242);
    var settlement = state.CreateSettlement("camp", "Camp", "Outlander");
    state.AddDrifterArrivalReservoir(10, "test reservoir");
    DrifterArrivalService.SimulateArrivals(
        state,
        new DrifterArrivalRequest(60_000, TargetWorldPopulation: 3, HardCeiling: 100, MaxArrivalsPerStep: 3));

    var result = DrifterAssimilationService.SimulateAssimilation(
        state,
        new DrifterAssimilationRequest(120_000, MaxAssimilationsPerStep: 1));

    AssertEqual(1, result.Assimilated);
    AssertEqual(2, state.Drifters.Count);
    AssertEqual(1, state.GetSettlementPopulation(settlement.Id).Total);
}

static void TestDrifterArrivalThenAssimilationConservesPopulation()
{
    var state = new WorldState(4242);
    var settlement = state.CreateSettlement("camp", "Camp", "Outlander");
    state.AddDrifterArrivalReservoir(10, "test reservoir");

    // Arrive 2 drifters, then assimilate them: world population (alive citizens +
    // drifters) is conserved at 2 — nobody appears or vanishes.
    DrifterArrivalService.SimulateArrivals(
        state,
        new DrifterArrivalRequest(60_000, TargetWorldPopulation: 2, HardCeiling: 100, MaxArrivalsPerStep: 2));
    DrifterAssimilationService.SimulateAssimilation(
        state,
        new DrifterAssimilationRequest(120_000, MaxAssimilationsPerStep: 2));

    var worldPopulation = state.Citizens.Count(c => c.Status == CitizenStatus.Alive) + state.Drifters.Count;
    AssertEqual(2, worldPopulation);
    AssertEqual(0, state.Drifters.Count);

    // The tap now idles because the settlement already holds the target population.
    var refill = DrifterArrivalService.SimulateArrivals(
        state,
        new DrifterArrivalRequest(180_000, TargetWorldPopulation: 2, HardCeiling: 100, MaxArrivalsPerStep: 2));
    AssertEqual(0, refill.Arrived);
}

static void TestDrifterFoundingCreatesSettlement()
{
    var state = new WorldState(4242);
    state.CreateDrifter("Organizer", 34, Sex.Female, combatAptitude: 15, organizationAptitude: 80);
    state.CreateDrifter("Hand 1", 26, Sex.Male, 10, 12);
    state.CreateDrifter("Hand 2", 29, Sex.Male, 8, 14);

    var result = DrifterFoundingService.SimulateFounding(
        state,
        new DrifterFoundingRequest(60_000, MinFounders: 3, LeaderAptitudeThreshold: 60));

    AssertEqual(true, result.Founded);
    AssertEqual(false, result.IsRaiderBand);
    AssertEqual(3, result.FounderCount);
    AssertEqual(0, state.Drifters.Count);
    // A brand-new settlement with its own faction now exists, holding the founders.
    var settlement = state.GetSettlement(result.SettlementId!.Value)!;
    AssertEqual(result.FactionId, settlement.FactionId);
    AssertEqual(3, state.GetSettlementPopulation(settlement.Id).Total);
    AssertEqual(1, state.Events.Count(e => e.Kind == WorldEventKind.SettlementFounded));
    // The capable organizer leads the new community.
    AssertEqual(true, state.Citizens.Any(c => c.SettlementId == settlement.Id && c.Profession == "leader"));
}

static void TestDrifterFoundingCreatesRaiderBand()
{
    var state = new WorldState(4242);
    state.CreateDrifter("Warlord", 31, Sex.Male, combatAptitude: 90, organizationAptitude: 20);
    state.CreateDrifter("Thug 1", 24, Sex.Male, 40, 10);
    state.CreateDrifter("Thug 2", 27, Sex.Male, 35, 12);

    var result = DrifterFoundingService.SimulateFounding(
        state,
        new DrifterFoundingRequest(60_000, MinFounders: 3, LeaderAptitudeThreshold: 60));

    AssertEqual(true, result.Founded);
    AssertEqual(true, result.IsRaiderBand);
    AssertEqual(3, state.GetSettlementPopulation(result.SettlementId!.Value).Total);
}

static void TestDrifterFoundingNeedsCapableLeader()
{
    var state = new WorldState(4242);
    state.CreateDrifter("Nobody 1", 30, Sex.Male, 20, 25);
    state.CreateDrifter("Nobody 2", 28, Sex.Female, 18, 22);
    state.CreateDrifter("Nobody 3", 33, Sex.Male, 15, 30);

    var result = DrifterFoundingService.SimulateFounding(
        state,
        new DrifterFoundingRequest(60_000, MinFounders: 3, LeaderAptitudeThreshold: 60));

    AssertEqual(false, result.Founded);
    AssertEqual(3, state.Drifters.Count);
    AssertEqual(0, state.Settlements.Count);
}

static void TestDrifterFoundingNeedsEnoughDrifters()
{
    var state = new WorldState(4242);
    state.CreateDrifter("Organizer", 34, Sex.Female, 15, 80);
    state.CreateDrifter("Hand", 26, Sex.Male, 10, 12);

    var result = DrifterFoundingService.SimulateFounding(
        state,
        new DrifterFoundingRequest(60_000, MinFounders: 3, LeaderAptitudeThreshold: 60));

    AssertEqual(false, result.Founded);
    AssertEqual(2, state.Drifters.Count);
}

static void TestFactionLifecycleCollapsesEmptyFaction()
{
    var state = new WorldState(4242);
    state.CreateSettlement("dead-camp", "Dead Camp", "Pirates");

    var result = FactionLifecycleService.SimulateCollapses(
        state,
        new FactionLifecycleRequest(60_000, new[] { "Pirates" }));

    AssertEqual(1, result.CollapsedFactions);
    var record = state.GetFactionRecord("Pirates")!;
    AssertEqual(WorldFactionStatus.Collapsed, record.Status);
    AssertEqual(60_000, record.Tick);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.FactionCollapsed));
}

static void TestFactionLifecycleCountsNonResidentSurvivors()
{
    var settlementId = EntityId.Create(EntityKind.Settlement, 1);
    var prisonerId = EntityId.Create(EntityKind.Citizen, 1);
    var playerSettlementId = EntityId.Create(EntityKind.Settlement, 2);
    var state = WorldState.FromSnapshot(new WorldStateSnapshot(
        4242,
        0,
        new[]
        {
            new WorldSettlement(settlementId, "captured-camp", "Captured Camp", "Pirates"),
            new WorldSettlement(playerSettlementId, "player-colony", "Player Colony", "Player")
        },
        new[] { new WorldCitizen(prisonerId, "Captured Raider", 33, Sex.Female, "raider", settlementId, CitizenStatus.Prisoner) },
        Array.Empty<WorldArmy>(),
        Array.Empty<WorldMigrationGroup>(),
        Array.Empty<WorldIntelReport>(),
        Array.Empty<KnownSettlementInfo>(),
        Array.Empty<RaidOpportunity>(),
        Array.Empty<RaidPawnLink>(),
        Array.Empty<WorldRaidOutcome>(),
        Array.Empty<SettlementProductionProfile>(),
        Array.Empty<WorldFactionRecord>(),
        new[] { new OwnershipRecord(prisonerId, playerSettlementId) },
        Array.Empty<ResourceStack>(),
        Array.Empty<WorldEvent>(),
        Array.Empty<Drifter>()));

    var result = FactionLifecycleService.SimulateCollapses(
        state,
        new FactionLifecycleRequest(60_000, new[] { "Pirates" }));

    AssertEqual(0, result.CollapsedFactions);
    AssertEqual(null, state.GetFactionRecord("Pirates"));
}

static void TestFactionLifecycleIsIdempotent()
{
    var state = new WorldState(4242);
    state.CreateSettlement("empty-camp", "Empty Camp", "Pirates");

    FactionLifecycleService.SimulateCollapses(state, new FactionLifecycleRequest(60_000));
    FactionLifecycleService.SimulateCollapses(state, new FactionLifecycleRequest(120_000));

    AssertEqual(1, state.FactionRecords.Count);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.FactionCollapsed));
    AssertEqual(60_000, state.GetFactionRecord("Pirates")!.Tick);
}

static void TestSettlementDevelopmentGrowsHousing()
{
    var state = new WorldState(4242);
    var town = state.CreateSettlement("town", "Town", "Settlers");
    for (var i = 0; i < 10; i++)
    {
        state.CreateCitizen("C" + i, 30, Sex.Male, "settler", town.Id);
    }

    state.AddResource(town.Id, "PackagedSurvivalMeal", 50); // fed (>= 10 population)

    // A starving settlement never develops.
    var camp = state.CreateSettlement("camp", "Camp", "Settlers");
    for (var i = 0; i < 5; i++)
    {
        state.CreateCitizen("D" + i, 30, Sex.Male, "settler", camp.Id);
    }

    var r1 = SettlementDevelopmentService.SimulateDay(
        state, new SettlementDevelopmentRequest(60_000, "PackagedSurvivalMeal", HousingHeadroom: 4, DevelopmentStep: 5, MaxHousing: 80));
    AssertEqual(1, r1.SettlementsDeveloped);
    AssertEqual(5, state.GetSettlementCapability(town.Id)!.HousingCapacity); // 0 -> +5
    AssertEqual(null, state.GetSettlementCapability(camp.Id)); // starving camp untouched

    // Keeps building toward population + headroom = 14, then stops.
    SettlementDevelopmentService.SimulateDay(
        state, new SettlementDevelopmentRequest(120_000, "PackagedSurvivalMeal", 4, 5, 80));
    SettlementDevelopmentService.SimulateDay(
        state, new SettlementDevelopmentRequest(180_000, "PackagedSurvivalMeal", 4, 5, 80));
    AssertEqual(14, state.GetSettlementCapability(town.Id)!.HousingCapacity); // 5 -> 10 -> 14 (capped)

    var r4 = SettlementDevelopmentService.SimulateDay(
        state, new SettlementDevelopmentRequest(240_000, "PackagedSurvivalMeal", 4, 5, 80));
    AssertEqual(0, r4.SettlementsDeveloped); // already developed enough
    AssertEqual(3, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.SettlementDeveloped));
}

static void TestSettlementDevelopmentUpgradesTierAndSpecialists()
{
    var state = new WorldState(4242);
    var town = state.CreateSettlement("builders", "Builders", "Settlers");
    for (var i = 0; i < 12; i++)
    {
        state.CreateCitizen("Builder " + i, 30, Sex.Male, "builder", town.Id);
    }

    state.AddResource(town.Id, "PackagedSurvivalMeal", 100);
    state.AddResource(town.Id, "Silver", 500);
    state.RecordSettlementCapability(new SettlementCapability(
        town.Id,
        HousingCapacity: 28,
        FoodStorageCapacity: 28,
        MedicineStorageCapacity: 0,
        PowerCapacity: 0,
        LaboratoryCapacity: 0,
        AnimalCapacity: 0,
        CropCapacity: 0,
        ResearchCapacity: 0,
        MechanicalCapacity: 0,
        PollutionHandling: 0));
    state.RecordSpecialistPool(new SpecialistPool(
        town.Id,
        Farmers: 1,
        Handlers: 0,
        Doctors: 0,
        Researchers: 0,
        Engineers: 0,
        Geneticists: 0,
        Mechanitors: 0,
        Soldiers: 1,
        Diplomats: 0));

    var before = SettlementDevelopmentService.GetTier(state, town.Id);
    var result = SettlementDevelopmentService.SimulateDay(
        state,
        new SettlementDevelopmentRequest(60_000, "PackagedSurvivalMeal", 20, 8, 120)
        {
            SilverResourceKey = "Silver",
            DevelopmentSilverCost = 120,
            SpecialistGrowthStep = 2,
        });

    var after = SettlementDevelopmentService.GetTier(state, town.Id);
    var specialists = state.GetSpecialistPool(town.Id)!;

    AssertEqual(SettlementTier.Village, before);
    AssertEqual(SettlementTier.Town, after);
    AssertEqual(1, result.SettlementsDeveloped);
    AssertEqual(1, result.TierUpgrades);
    AssertEqual(380, state.GetOwnedResourceQuantity(town.Id, "Silver"));
    AssertEqual(3, specialists.Farmers);
    AssertEqual(2, specialists.Engineers);
    AssertEqual(3, specialists.Soldiers);
}

static void TestRimWorldSettlementDevelopmentWiring()
{
    var component = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldWorldComponent.cs"));
    AssertContains("SettlementDevelopmentService.SimulateDay", component);
    AssertContains("settings.settlementDevelopmentEnabled", component);

    var settings = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldSettings.cs"));
    AssertContains("public bool settlementDevelopmentEnabled", settings);
    AssertContains("Scribe_Values.Look(ref settlementDevelopmentEnabled", settings);
    AssertContains("public int settlementDevelopmentStep", settings);
    AssertContains("public int settlementHousingHeadroom", settings);
}

static void TestFactionLifecycleSkipsPlayerFaction()
{
    var state = new WorldState(4242);
    state.SetPlayerFactionId("PlayerFaction");
    state.CreateSettlement("player-base", "Player Base", "PlayerFaction");
    state.CreateSettlement("empty-raiders", "Empty Raiders", "Raiders");

    var result = FactionLifecycleService.SimulateCollapses(
        state,
        new FactionLifecycleRequest(60_000));

    AssertEqual(1, result.CollapsedFactions);
    AssertEqual(false, state.IsFactionCollapsed("PlayerFaction"));
    AssertEqual(true, state.IsFactionCollapsed("Raiders"));
}

static void TestPlayerFactionIdentitySerialization()
{
    var state = new WorldState(4242);
    state.SetPlayerFactionId("PlayerFaction");

    var restored = WorldStateCodec.Deserialize(WorldStateCodec.Serialize(state));

    AssertEqual("PlayerFaction", restored.PlayerFactionId);
    AssertEqual(true, restored.IsPlayerFaction("PlayerFaction"));
}

static void TestSettlementPowerFromLivingAdults()
{
    var state = new WorldState(4242);
    var settlement = state.CreateSettlement("camp", "Camp", "Pirates");

    // Empty settlement has no combat power.
    AssertEqual(0, SettlementPowerService.GetSettlementPower(state, settlement.Id).CombatPower);

    // Children are not combatants.
    state.CreateCitizen("Kid", 10, Sex.Male, "child", settlement.Id);
    AssertEqual(0, SettlementPowerService.GetSettlementPower(state, settlement.Id).Combatants);

    // Living adult residents are combatants.
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen("Adult" + i, 30, Sex.Male, "settler", settlement.Id);
    }

    var power = SettlementPowerService.GetSettlementPower(state, settlement.Id);
    AssertEqual(3, power.Combatants);
    AssertEqual(300, power.CombatPower);
}

static void TestSettlementPowerDiminishesPastThreshold()
{
    var state = new WorldState(4242);
    var settlement = state.CreateSettlement("city", "City", "Empire");

    for (var i = 0; i < 60; i++)
    {
        state.CreateCitizen("Adult" + i, 30, Sex.Male, "settler", settlement.Id);
    }

    var power = SettlementPowerService.GetSettlementPower(state, settlement.Id);
    AssertEqual(60, power.Combatants);
    // 50 * 100 + 10 * 50 = 5500: diminishing returns past the 50-combatant threshold
    // keep a huge settlement from producing unbounded, linear military power.
    AssertEqual(5500, power.CombatPower);
}

static void TestDerivedAggregatesTrackPopulationLifecycle()
{
    var state = new WorldState(12345);
    var source = state.CreateSettlement("source", "Source", "Pirate");
    var target = state.CreateSettlement("target", "Target", "Pirate");

    var child = state.CreateCitizen("Child", 12, Sex.Female, "settler", source.Id);
    var adult = state.CreateCitizen("Adult", 30, Sex.Male, "settler", source.Id);
    state.CreateCitizen("Elder", 70, Sex.Female, "settler", source.Id);

    AssertAggregateMatchesFullScan(state, source.Id);
    AssertAggregateMatchesFullScan(state, target.Id);
    AssertFactionAggregateMatchesFullScan(state, "Pirate");

    state.MarkCitizenDead(adult.Id, "test casualty");
    AssertAggregateMatchesFullScan(state, source.Id);
    AssertFactionAggregateMatchesFullScan(state, "Pirate");

    state.MarkCitizenRefugee(child.Id, "starvation");
    AssertAggregateMatchesFullScan(state, source.Id);
    AssertFactionAggregateMatchesFullScan(state, "Pirate");

    state.CompleteCitizenMigration(child.Id, target.Id, "stable destination");
    AssertAggregateMatchesFullScan(state, source.Id);
    AssertAggregateMatchesFullScan(state, target.Id);
    AssertFactionAggregateMatchesFullScan(state, "Pirate");
}

static void TestDerivedAggregatesTrackRaidLifecycle()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("camp", "Camp", "Pirate");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen($"Raider {i + 1}", 24 + i, Sex.Male, "soldier", settlement.Id);
    }
    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 8);

    AssertAggregateMatchesFullScan(state, settlement.Id);
    AssertFactionAggregateMatchesFullScan(state, "Pirate");

    var allocation = RaidPopulationAllocator.ReserveForRaid(
        state,
        new RaidPopulationAllocationRequest("Pirate", "raid", 3));
    AssertAggregateMatchesFullScan(state, settlement.Id);
    AssertFactionAggregateMatchesFullScan(state, "Pirate");

    RaidPawnBindingService.BindRaidPawns(state, allocation.Army!.Id, new[] { 101, 102 });
    RaidReconciliationService.ReleaseUndeployedReserves(state, allocation.Army.Id);
    AssertAggregateMatchesFullScan(state, settlement.Id);
    AssertFactionAggregateMatchesFullScan(state, "Pirate");

    RaidPawnBindingService.MarkPawnReturned(state, 101, "returned");
    AssertAggregateMatchesFullScan(state, settlement.Id);
    AssertFactionAggregateMatchesFullScan(state, "Pirate");

    RaidPawnBindingService.MarkPawnMissing(state, 102, "lost");
    AssertAggregateMatchesFullScan(state, settlement.Id);
    AssertFactionAggregateMatchesFullScan(state, "Pirate");

    var secondAllocation = RaidPopulationAllocator.ReserveForRaid(
        state,
        new RaidPopulationAllocationRequest("Pirate", "second raid", 1));
    RaidPawnBindingService.BindRaidPawns(state, secondAllocation.Army!.Id, new[] { 103 });
    RaidPawnBindingService.MarkPawnPrisoner(state, 103, "captured");
    AssertAggregateMatchesFullScan(state, settlement.Id);
    AssertFactionAggregateMatchesFullScan(state, "Pirate");
}

static void TestFactionBehaviorProfiles()
{
    // Warmonger reaches furthest and hits hardest; Cautious/Merchant stay close.
    var warmonger = FactionBehaviorService.GetProfile(FactionBehavior.Warmonger);
    AssertEqual(4, warmonger.EngagementRange);
    AssertEqual(1.2f, warmonger.CombatMultiplier);
    AssertEqual(true, warmonger.ParticipatesInWorldWar);

    AssertEqual(3, FactionBehaviorService.GetProfile(FactionBehavior.Aggressive).EngagementRange);
    AssertEqual(2, FactionBehaviorService.GetProfile(FactionBehavior.Expansionist).EngagementRange);
    AssertEqual(1, FactionBehaviorService.GetProfile(FactionBehavior.Cautious).EngagementRange);
    AssertEqual(1, FactionBehaviorService.GetProfile(FactionBehavior.Merchant).EngagementRange);

    // Merchant grows fastest but fights weakest among the active archetypes.
    AssertEqual(1.05f, FactionBehaviorService.GetProfile(FactionBehavior.Merchant).GrowthMultiplier);
    AssertEqual(0.85f, FactionBehaviorService.GetProfile(FactionBehavior.Merchant).CombatMultiplier);
}

static void TestFactionBehaviorNonParticipants()
{
    // Player-controlled, vassal, excluded and unassigned factions never drive world war,
    // and have a neutral (range 0, all-1.0) profile so nothing is fabricated for them.
    foreach (var behavior in new[]
    {
        FactionBehavior.Player,
        FactionBehavior.Vassal,
        FactionBehavior.Excluded,
        FactionBehavior.Undefined,
    })
    {
        var profile = FactionBehaviorService.GetProfile(behavior);
        AssertEqual(false, profile.ParticipatesInWorldWar);
        AssertEqual(0, profile.EngagementRange);
        AssertEqual(1f, profile.CombatMultiplier);
    }

    // Every one of the six active archetypes participates.
    foreach (var behavior in new[]
    {
        FactionBehavior.Expansionist,
        FactionBehavior.Cautious,
        FactionBehavior.Merchant,
        FactionBehavior.Aggressive,
        FactionBehavior.Warmonger,
        FactionBehavior.Random,
    })
    {
        AssertEqual(true, FactionBehaviorService.GetProfile(behavior).ParticipatesInWorldWar);
    }
}

static void TestArmyMovementArrivesOnEta()
{
    var state = new WorldState(4242);
    var source = state.CreateSettlement("home", "Home", "Pirates");
    var target = state.CreateSettlement("prey", "Prey", "Outlanders");
    var army = state.CreateArmy("Raiders", "Pirates", source.Id);

    state.DispatchArmy(army.Id, target.Id, arrivalTick: 5 * 60_000);

    // Before the ETA the army is still travelling.
    var early = ArmyMovementService.SimulateDay(state, new ArmyMovementRequest(2 * 60_000));
    AssertEqual(0, early.Arrived);
    AssertEqual(ArmyMovementStatus.Traveling, state.GetArmyMovement(army.Id)!.Status);

    // At/after the ETA it arrives, exactly once.
    var onTime = ArmyMovementService.SimulateDay(state, new ArmyMovementRequest(5 * 60_000));
    AssertEqual(1, onTime.Arrived);
    AssertEqual(ArmyMovementStatus.Arrived, state.GetArmyMovement(army.Id)!.Status);

    // Idempotent: an already-arrived army is not re-processed.
    var again = ArmyMovementService.SimulateDay(state, new ArmyMovementRequest(6 * 60_000));
    AssertEqual(0, again.Arrived);
}

static void TestArmyMovementSerializationRoundTrip()
{
    var state = new WorldState(4242);
    var source = state.CreateSettlement("home", "Home", "Pirates");
    var target = state.CreateSettlement("prey", "Prey", "Outlanders");
    var army = state.CreateArmy("Raiders", "Pirates", source.Id);
    state.DispatchArmy(army.Id, target.Id, arrivalTick: 5 * 60_000);

    var restored = WorldStateCodec.Deserialize(WorldStateCodec.Serialize(state));

    var movement = restored.GetArmyMovement(army.Id)!;
    AssertEqual(target.Id, movement.TargetSettlementId);
    AssertEqual(5 * 60_000, movement.ArrivalTick);
    AssertEqual(ArmyMovementStatus.Traveling, movement.Status);
}

static void TestArmyMovementPrunesOldResolvedMovements()
{
    var state = new WorldState(4242);
    var source = state.CreateSettlement("home", "Home", "Pirates");
    var target = state.CreateSettlement("prey", "Prey", "Outlanders");

    var oldResolved = state.CreateArmy("Old", "Pirates", source.Id);
    state.DispatchArmy(oldResolved.Id, target.Id, arrivalTick: 60_000);
    state.AdvanceToTick(2 * 60_000);
    state.SetArmyMovementStatus(oldResolved.Id, ArmyMovementStatus.Disbanded);

    var recentResolved = state.CreateArmy("Recent", "Pirates", source.Id);
    state.DispatchArmy(recentResolved.Id, target.Id, arrivalTick: 8 * 60_000);
    state.AdvanceToTick(9 * 60_000);
    state.SetArmyMovementStatus(recentResolved.Id, ArmyMovementStatus.Recalled);

    var traveling = state.CreateArmy("Traveling", "Pirates", source.Id);
    state.DispatchArmy(traveling.Id, target.Id, arrivalTick: 20 * 60_000);
    var historyEventsBeforePrune = state.Events.Count;

    var result = ArmyMovementPruneService.Prune(
        state,
        new ArmyMovementPruneRequest(CurrentTick: 10 * 60_000, RetentionDays: 5));

    AssertEqual(1, result.Pruned);
    AssertEqual(null, state.GetArmyMovement(oldResolved.Id));
    AssertEqual(ArmyMovementStatus.Recalled, state.GetArmyMovement(recentResolved.Id)!.Status);
    AssertEqual(ArmyMovementStatus.Traveling, state.GetArmyMovement(traveling.Id)!.Status);
    AssertEqual(historyEventsBeforePrune, state.Events.Count);
    AssertEqual(true, state.Events.Any(worldEvent => worldEvent.Kind == WorldEventKind.WarbandLaunched));
}

static void TestBattleAttackerCapturesWeakSettlement()
{
    var state = new WorldState(4242);
    var source = state.CreateSettlement("home", "Home", "Pirates");
    for (var i = 0; i < 12; i++)
    {
        state.CreateCitizen("P" + i, 30, Sex.Male, "raider", source.Id);
    }

    var target = state.CreateSettlement("prey", "Prey", "Outlanders");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen("O" + i, 30, Sex.Male, "settler", target.Id);
    }

    var reservation = RaidPopulationAllocator.ReserveForRaid(
        state, new RaidPopulationAllocationRequest("Pirates", "Raiders", 8, FoodPerCitizen: 0));
    var army = reservation.Army!;
    AssertEqual(8, reservation.ReservedCombatants);

    var totalCitizens = state.Citizens.Count;

    state.DispatchArmy(army.Id, target.Id, 0);
    state.SetArmyMovementStatus(army.Id, ArmyMovementStatus.Arrived);

    var outcome = WorldBattleService.Resolve(state, army.Id);

    AssertEqual(BattleWinner.Attacker, outcome.Winner);
    AssertEqual(true, outcome.Captured);
    // Captured: the settlement now belongs to the attacking faction (survivors come with it).
    AssertEqual("Pirates", state.GetSettlement(target.Id)!.FactionId);
    // 8 attackers * 20% = 1 loss; 3 defenders * 60% = 1 loss.
    AssertEqual(1, outcome.AttackerLosses);
    AssertEqual(1, outcome.DefenderLosses);
    // Population is conserved: no citizen appears or vanishes; exactly the losses turn Dead.
    AssertEqual(totalCitizens, state.Citizens.Count);
    AssertEqual(2, state.Citizens.Count(citizen => citizen.Status == CitizenStatus.Dead));
}

static void TestBattleAttackerSurvivorsOccupyCapturedSettlement()
{
    var state = new WorldState(4242);
    var source = state.CreateSettlement("home", "Home", "Pirates");
    for (var i = 0; i < 12; i++)
    {
        state.CreateCitizen("P" + i, 30, Sex.Male, "raider", source.Id);
    }

    var target = state.CreateSettlement("prey", "Prey", "Outlanders");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen("O" + i, 30, Sex.Male, "settler", target.Id);
    }

    var reservation = RaidPopulationAllocator.ReserveForRaid(
        state, new RaidPopulationAllocationRequest("Pirates", "Raiders", 8, FoodPerCitizen: 0));
    var army = reservation.Army!;
    var attackerIds = state.Citizens
        .Where(citizen => state.GetOwner(citizen.Id) == army.Id)
        .Select(citizen => citizen.Id)
        .ToList();

    state.DispatchArmy(army.Id, target.Id, 0);
    state.SetArmyMovementStatus(army.Id, ArmyMovementStatus.Arrived);

    WorldBattleService.Resolve(state, army.Id);

    var survivingAttackers = attackerIds
        .Where(id => state.GetCitizen(id)!.Status == CitizenStatus.Alive)
        .ToList();
    AssertEqual(7, survivingAttackers.Count);
    foreach (var attackerId in survivingAttackers)
    {
        AssertEqual(target.Id, state.GetOwner(attackerId));
        AssertEqual(target.Id, state.GetCitizen(attackerId)!.SettlementId);
    }

    AssertEqual(9, state.GetSettlementPopulation(target.Id).Adults);
    AssertAggregateMatchesFullScan(state, target.Id);
    AssertFactionAggregateMatchesFullScan(state, "Pirates");
}

static void TestBattleDefenderHoldsAndArmyStandsDown()
{
    var state = new WorldState(4242);
    var source = state.CreateSettlement("home", "Home", "Pirates");
    for (var i = 0; i < 6; i++)
    {
        state.CreateCitizen("P" + i, 30, Sex.Male, "raider", source.Id);
    }

    var target = state.CreateSettlement("fortress", "Fortress", "Outlanders");
    for (var i = 0; i < 20; i++)
    {
        state.CreateCitizen("O" + i, 30, Sex.Male, "settler", target.Id);
    }

    var reservation = RaidPopulationAllocator.ReserveForRaid(
        state, new RaidPopulationAllocationRequest("Pirates", "Raiders", 2, FoodPerCitizen: 0));
    var army = reservation.Army!;

    var totalCitizens = state.Citizens.Count;

    state.DispatchArmy(army.Id, target.Id, 0);
    state.SetArmyMovementStatus(army.Id, ArmyMovementStatus.Arrived);

    var outcome = WorldBattleService.Resolve(state, army.Id);

    AssertEqual(BattleWinner.Defender, outcome.Winner);
    AssertEqual(false, outcome.Captured);
    AssertEqual("Outlanders", state.GetSettlement(target.Id)!.FactionId);
    AssertEqual(ArmyMovementStatus.Disbanded, state.GetArmyMovement(army.Id)!.Status);
    // Population is conserved: only the combined losses turn Dead.
    AssertEqual(totalCitizens, state.Citizens.Count);
    AssertEqual(
        outcome.AttackerLosses + outcome.DefenderLosses,
        state.Citizens.Count(citizen => citizen.Status == CitizenStatus.Dead));
}

static void TestBattleAttackerSurvivorsReturnHomeAfterDefeat()
{
    var state = new WorldState(4242);
    var source = state.CreateSettlement("home", "Home", "Pirates");
    for (var i = 0; i < 6; i++)
    {
        state.CreateCitizen("P" + i, 30, Sex.Male, "raider", source.Id);
    }

    var target = state.CreateSettlement("fortress", "Fortress", "Outlanders");
    for (var i = 0; i < 20; i++)
    {
        state.CreateCitizen("O" + i, 30, Sex.Male, "settler", target.Id);
    }

    var reservation = RaidPopulationAllocator.ReserveForRaid(
        state, new RaidPopulationAllocationRequest("Pirates", "Raiders", 2, FoodPerCitizen: 0));
    var army = reservation.Army!;
    var attackerIds = state.Citizens
        .Where(citizen => state.GetOwner(citizen.Id) == army.Id)
        .Select(citizen => citizen.Id)
        .ToList();

    state.DispatchArmy(army.Id, target.Id, 0);
    state.SetArmyMovementStatus(army.Id, ArmyMovementStatus.Arrived);

    WorldBattleService.Resolve(state, army.Id);

    var survivingAttackers = attackerIds
        .Where(id => state.GetCitizen(id)!.Status == CitizenStatus.Alive)
        .ToList();
    AssertEqual(1, survivingAttackers.Count);
    foreach (var attackerId in survivingAttackers)
    {
        AssertEqual(source.Id, state.GetOwner(attackerId));
    }
}

static void TestBattleAppliesFactionCombatBehaviorMultiplier()
{
    var state = new WorldState(4242);
    var source = state.CreateSettlement("home", "Home", "Pirates");
    for (var i = 0; i < 6; i++)
    {
        state.CreateCitizen("P" + i, 30, Sex.Male, "raider", source.Id);
    }

    var target = state.CreateSettlement("fortress", "Fortress", "Outlanders");
    for (var i = 0; i < 4; i++)
    {
        state.CreateCitizen("O" + i, 30, Sex.Male, "settler", target.Id);
    }

    state.AssignFactionBehavior("Pirates", FactionBehavior.Warmonger);
    var reservation = RaidPopulationAllocator.ReserveForRaid(
        state, new RaidPopulationAllocationRequest("Pirates", "Raiders", 4, FoodPerCitizen: 0));
    var army = reservation.Army!;

    state.DispatchArmy(army.Id, target.Id, 0);
    state.SetArmyMovementStatus(army.Id, ArmyMovementStatus.Arrived);

    var outcome = WorldBattleService.Resolve(state, army.Id);

    AssertEqual(BattleWinner.Attacker, outcome.Winner);
    AssertEqual(480, outcome.AttackerPower);
    AssertEqual(400, outcome.DefenderPower);
}

static void TestBattleAgainstPlayerFactionIsBlocked()
{
    var state = new WorldState(4242);
    var source = state.CreateSettlement("home", "Home", "Pirates");
    for (var i = 0; i < 8; i++)
    {
        state.CreateCitizen("P" + i, 30, Sex.Male, "raider", source.Id);
    }

    var playerBase = state.CreateSettlement("player-base", "Player Base", "PlayerFaction");
    for (var i = 0; i < 2; i++)
    {
        state.CreateCitizen("C" + i, 30, Sex.Female, "colonist", playerBase.Id);
    }

    state.SetPlayerFactionId("PlayerFaction");
    var reservation = RaidPopulationAllocator.ReserveForRaid(
        state, new RaidPopulationAllocationRequest("Pirates", "Raiders", 4, FoodPerCitizen: 0));
    var army = reservation.Army!;
    state.DispatchArmy(army.Id, playerBase.Id, 0);
    state.SetArmyMovementStatus(army.Id, ArmyMovementStatus.Arrived);

    var result = WorldBattleService.TryResolve(state, army.Id);

    AssertEqual(BattleResolutionStatus.BlockedPlayerSettlement, result.Status);
    AssertEqual(null, result.Outcome);
    AssertEqual("PlayerFaction", state.GetSettlement(playerBase.Id)!.FactionId);
    AssertEqual(ArmyMovementStatus.Disbanded, state.GetArmyMovement(army.Id)!.Status);
    foreach (var citizen in state.Citizens.Where(citizen => citizen.Name.StartsWith("P", StringComparison.Ordinal)))
    {
        AssertEqual(source.Id, state.GetOwner(citizen.Id));
    }
    AssertEqual(0, state.Citizens.Count(citizen => citizen.Status == CitizenStatus.Dead));
}

static void TestOpposingArmiesInterceptInTransit()
{
    var state = new WorldState(4242);
    var redHome = state.CreateSettlement("red-home", "Red Home", "Red");
    var blueHome = state.CreateSettlement("blue-home", "Blue Home", "Blue");
    for (var i = 0; i < 10; i++)
    {
        state.CreateCitizen("R" + i, 30, Sex.Male, "fighter", redHome.Id);
        state.CreateCitizen("B" + i, 30, Sex.Male, "fighter", blueHome.Id);
    }

    var redReserve = RaidPopulationAllocator.ReserveForRaid(
        state,
        new RaidPopulationAllocationRequest("Red", "Red warband", 6, FoodPerCitizen: 0));
    var blueReserve = RaidPopulationAllocator.ReserveForRaid(
        state,
        new RaidPopulationAllocationRequest("Blue", "Blue warband", 6, FoodPerCitizen: 0));
    var redArmy = redReserve.Army!;
    var blueArmy = blueReserve.Army!;

    state.DispatchArmy(redArmy.Id, blueHome.Id, arrivalTick: 5 * 60_000);
    state.DispatchArmy(blueArmy.Id, redHome.Id, arrivalTick: 5 * 60_000);

    var result = ArmyInterceptionService.SimulateDay(state, new ArmyInterceptionRequest(2 * 60_000));

    AssertEqual(1, result.Interceptions);
    AssertEqual(0, state.ArmyMovements.Count(movement => movement.Status == ArmyMovementStatus.Arrived));
    AssertEqual(1, state.ArmyMovements.Count(movement => movement.Status == ArmyMovementStatus.Traveling));
    AssertEqual(1, state.ArmyMovements.Count(movement => movement.Status == ArmyMovementStatus.Recalled));
    AssertEqual(true, state.Conflicts.Any(conflict => conflict.Involves("Red") && conflict.Involves("Blue")));
    AssertEqual(true, state.Citizens.Count(citizen => citizen.Status == CitizenStatus.Dead) > 0);
    AssertEqual(0, state.Validate().Count());
}

static void TestFactionBehaviorPersists()
{
    var state = new WorldState(4242);
    state.CreateSettlement("horde", "Horde", "Raiders");
    state.AssignFactionBehavior("Raiders", FactionBehavior.Warmonger);

    var restored = WorldStateCodec.Deserialize(WorldStateCodec.Serialize(state));

    AssertEqual(FactionBehavior.Warmonger, restored.GetFactionBehavior("Raiders"));
    // Unassigned factions default to Undefined.
    AssertEqual(FactionBehavior.Undefined, restored.GetFactionBehavior("Nobody"));
}

static void TestFactionActionPlannerWarband()
{
    var state = new WorldState(4242);
    var home = state.CreateSettlement("horde", "Horde", "Raiders");
    for (var i = 0; i < 6; i++)
    {
        state.CreateCitizen("R" + i, 30, Sex.Male, "raider", home.Id);
    }

    var victim = state.CreateSettlement("village", "Village", "Settlers");
    state.AssignFactionBehavior("Raiders", FactionBehavior.Warmonger);
    state.RecordFactionSettlementIntel("Raiders", victim.Id, IntelSourceKind.Scout, 0, confidence: 80);

    var plan = FactionActionPlanner.Plan(state, "Raiders", 60_000);

    AssertEqual(WarAction.Warband, plan.Action);
    AssertEqual(victim.Id, plan.TargetSettlementId);
}

static void TestFactionActionPlannerScoutsBeforeUnknownWarTarget()
{
    var state = new WorldState(4242);
    var home = state.CreateSettlement("horde", "Horde", "Raiders");
    for (var i = 0; i < 6; i++)
    {
        state.CreateCitizen("R" + i, 30, Sex.Male, "raider", home.Id);
    }

    var victim = state.CreateSettlement("village", "Village", "Settlers");
    state.AssignFactionBehavior("Raiders", FactionBehavior.Warmonger);

    var unknown = FactionActionPlanner.Plan(state, "Raiders", 60_000);
    AssertEqual(WarAction.ScoutingParty, unknown.Action);
    AssertEqual(null, unknown.TargetSettlementId);

    WorldWarService.SimulateDay(state, new WorldWarRequest(60_000, TravelDays: 1, RaidCombatants: 3));
    AssertEqual(0, state.ArmyMovements.Count);
    AssertEqual(1, state.Missions.Count(mission => mission.Kind == WorldMissionKind.Scout));

    WorldWarService.SimulateDay(state, new WorldWarRequest(120_000, TravelDays: 1, RaidCombatants: 3));
    AssertEqual(true, state.HasFactionSettlementIntel("Raiders", victim.Id));

    var known = FactionActionPlanner.Plan(state, "Raiders", 180_000);
    AssertEqual(WarAction.Warband, known.Action);
    AssertEqual(victim.Id, known.TargetSettlementId);
}

static void TestFactionActionPlannerSpreadsEnemyTargets()
{
    var state = new WorldState(4242);
    var attackers = new[] { "ToxicPeople", "LeagueOfCrods", "OrangeDominion", "DustJackals" };
    foreach (var faction in attackers)
    {
        var home = state.CreateSettlement($"home-{faction}", $"{faction} Home", faction);
        for (var i = 0; i < 6; i++)
        {
            state.CreateCitizen($"{faction}-{i}", 30, Sex.Male, "fighter", home.Id);
        }

        state.AssignFactionBehavior(faction, FactionBehavior.Warmonger);
    }

    // Keep attackers from targeting each other so every faction sees the same target pool. The old
    // Id-only target ordering dogpiles all of them onto Target One.
    for (var i = 0; i < attackers.Length; i++)
    {
        for (var j = i + 1; j < attackers.Length; j++)
        {
            DiplomacyService.AdjustGoodwill(state, attackers[i], attackers[j], 80);
        }
    }

    var targetOne = state.CreateSettlement("target-one", "Target One", "TargetOneFaction");
    var targetTwo = state.CreateSettlement("target-two", "Target Two", "TargetTwoFaction");
    var targetThree = state.CreateSettlement("target-three", "Target Three", "TargetThreeFaction");
    var targetIds = new[] { targetOne.Id, targetTwo.Id, targetThree.Id };
    foreach (var faction in attackers)
    {
        foreach (var targetId in targetIds)
        {
            state.RecordFactionSettlementIntel(faction, targetId, IntelSourceKind.Scout, 0, confidence: 80);
        }
    }

    var chosenTargets = attackers
        .Select(faction => FactionActionPlanner.Plan(state, faction, 60_000).TargetSettlementId)
        .ToList();

    AssertEqual(true, chosenTargets.All(target => target.HasValue && targetIds.Contains(target.Value)));
    AssertEqual(true, chosenTargets.Distinct().Count() > 1);
}

static void TestFactionActionPlannerSkipsAlliedTargets()
{
    var state = new WorldState(4242);
    var home = state.CreateSettlement("horde", "Horde", "Raiders");
    for (var i = 0; i < 6; i++)
    {
        state.CreateCitizen("R" + i, 30, Sex.Male, "raider", home.Id);
    }

    state.CreateSettlement("ally", "Ally", "Settlers");
    state.AssignFactionBehavior("Raiders", FactionBehavior.Warmonger);
    DiplomacyService.AdjustGoodwill(state, "Raiders", "Settlers", 80);

    var plan = FactionActionPlanner.Plan(state, "Raiders", 60_000);

    AssertEqual(WarAction.ScoutingParty, plan.Action);
    AssertEqual(null, plan.TargetSettlementId);
}

static void TestFactionActionPlannerSkipsPlayerFactionTarget()
{
    var state = new WorldState(4242);
    var home = state.CreateSettlement("horde", "Horde", "Raiders");
    for (var i = 0; i < 6; i++)
    {
        state.CreateCitizen("R" + i, 30, Sex.Male, "raider", home.Id);
    }

    state.CreateSettlement("player-base", "Player Base", "PlayerFaction");
    state.SetPlayerFactionId("PlayerFaction");
    state.AssignFactionBehavior("Raiders", FactionBehavior.Warmonger);

    var plan = FactionActionPlanner.Plan(state, "Raiders", 60_000);

    AssertEqual(WarAction.ScoutingParty, plan.Action);
    AssertEqual(null, plan.TargetSettlementId);
}

static void TestWorldWarNonCombatActionsSkipPlayerFaction()
{
    var state = new WorldState(4242);
    var playerBase = state.CreateSettlement("player-base", "Player Base", "PlayerFaction");
    state.SetPlayerFactionId("PlayerFaction");
    for (var i = 0; i < 6; i++)
    {
        state.CreateCitizen("C" + i, 30, Sex.Female, "colonist", playerBase.Id);
    }

    var village = state.CreateSettlement("village", "Village", "Villagers");
    for (var i = 0; i < 6; i++)
    {
        state.CreateCitizen("V" + i, 30, Sex.Female, "settler", village.Id);
    }

    var market = state.CreateSettlement("market", "Market", "Traders");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen("T" + i, 30, Sex.Male, "merchant", market.Id);
    }

    state.AddResource(market.Id, "Steel", 40);
    state.AssignFactionBehavior("Traders", FactionBehavior.Merchant);
    WorldWarService.SimulateDay(state, new WorldWarRequest(60_000, TravelDays: 1, RaidCombatants: 3));

    state.AssignFactionBehavior("Traders", FactionBehavior.Excluded);
    WorldWarService.SimulateDay(state, new WorldWarRequest(120_000, TravelDays: 1, RaidCombatants: 3));

    AssertEqual(0, state.GetOwnedResourceQuantity(playerBase.Id, "Steel"));
    AssertEqual(10, state.GetOwnedResourceQuantity(village.Id, "Steel"));

    var watch = state.CreateSettlement("watch", "Watch", "Scouts");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen("S" + i, 30, Sex.Male, "scout", watch.Id);
    }

    state.AssignFactionBehavior("Scouts", FactionBehavior.Cautious);
    WorldWarService.SimulateDay(state, new WorldWarRequest(180_000, TravelDays: 1, RaidCombatants: 3));
    var scoutMission = state.Missions.Single(mission => mission.Kind == WorldMissionKind.Scout);
    AssertEqual(village.Id, scoutMission.TargetSettlementId);
    AssertEqual(null, state.GetKnownSettlementInfo(playerBase.Id));

    var envoys = state.CreateSettlement("envoys", "Envoys", "Envoys");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen("E" + i, 30, Sex.Female, "diplomat", envoys.Id);
    }

    state.AssignFactionBehavior("Scouts", FactionBehavior.Excluded);
    state.AssignFactionBehavior("Envoys", FactionBehavior.Random);
    WorldWarService.SimulateDay(state, new WorldWarRequest(240_000, TravelDays: 1, RaidCombatants: 3));
    AssertEqual(IntelSourceKind.Scout, state.GetKnownSettlementInfo(village.Id)!.SourceKind);
    var diplomaticMission = state.Missions.Single(mission => mission.Kind == WorldMissionKind.Diplomat);
    AssertEqual(village.Id, diplomaticMission.TargetSettlementId);
    AssertEqual("Villagers", diplomaticMission.TargetFactionId);
    AssertEqual(0, DiplomacyService.GetGoodwill(state, "Envoys", "PlayerFaction"));

    state.AssignFactionBehavior("Envoys", FactionBehavior.Excluded);
    WorldWarService.SimulateDay(state, new WorldWarRequest(300_000, TravelDays: 1, RaidCombatants: 3));
    AssertEqual(5, DiplomacyService.GetGoodwill(state, "Envoys", "Villagers"));
}

static void TestFactionActionPlannerChoosesDevelop()
{
    var state = new WorldState(4242);
    var home = state.CreateSettlement("builders", "Builders", "Builders");
    for (var i = 0; i < 6; i++)
    {
        state.CreateCitizen("B" + i, 30, Sex.Male, "builder", home.Id);
    }

    state.AddResource(home.Id, "Silver", 300);
    state.RecordSettlementCapability(new SettlementCapability(
        home.Id,
        HousingCapacity: 5,
        FoodStorageCapacity: 5,
        MedicineStorageCapacity: 0,
        PowerCapacity: 0,
        LaboratoryCapacity: 0,
        AnimalCapacity: 0,
        CropCapacity: 0,
        ResearchCapacity: 0,
        MechanicalCapacity: 0,
        PollutionHandling: 0));
    state.AssignFactionBehavior("Builders", FactionBehavior.Cautious);

    var plan = FactionActionPlanner.Plan(state, "Builders", 60_000);

    AssertEqual(WarAction.Develop, plan.Action);
    AssertEqual(home.Id, plan.TargetSettlementId);
}

static void TestFactionActionPlannerFiltersPassive()
{
    var state = new WorldState(4242);
    var home = state.CreateSettlement("horde", "Horde", "Raiders");
    for (var i = 0; i < 6; i++)
    {
        state.CreateCitizen("R" + i, 30, Sex.Male, "raider", home.Id);
    }

    state.CreateSettlement("village", "Village", "Settlers");
    state.AssignFactionBehavior("Raiders", FactionBehavior.Warmonger);
    state.AssignFactionBehavior("Settlers", FactionBehavior.Player);

    var plans = FactionActionPlanner.PlanDay(state, 60_000);

    // Only the warmonger acts; the player-controlled faction is excluded.
    AssertEqual(1, plans.Count);
    AssertEqual("Raiders", plans[0].FactionId);
    AssertEqual(WarAction.ScoutingParty, plans[0].Action);
}

static void TestDiplomacyIrreconcilableStaysHostile()
{
    var state = new WorldState(4242);
    state.MarkFactionIrreconcilable("Pirates");

    // No gesture can lift an irreconcilable faction out of hostility.
    DiplomacyService.AdjustGoodwill(state, "Pirates", "Outlanders", 80);

    AssertEqual(-100, DiplomacyService.GetGoodwill(state, "Pirates", "Outlanders"));
    AssertEqual(RelationStance.Hostile, DiplomacyService.GetStance(state, "Outlanders", "Pirates"));
}

static void TestDiplomacyGoodwillDrifts()
{
    var state = new WorldState(4242);
    DiplomacyService.AdjustGoodwill(state, "Alpha", "Beta", -30);

    DiplomacyService.SimulateDay(state, 60_000);
    AssertEqual(-28, DiplomacyService.GetGoodwill(state, "Alpha", "Beta"));

    for (var day = 2; day <= 15; day++)
    {
        DiplomacyService.SimulateDay(state, day * 60_000);
    }

    // -30 drifting +2/day reaches neutral and stops there (queried the other way to prove symmetry).
    AssertEqual(0, DiplomacyService.GetGoodwill(state, "Beta", "Alpha"));
}

static void TestDiplomacyPersists()
{
    var state = new WorldState(4242);
    state.MarkFactionIrreconcilable("Pirates");
    DiplomacyService.RecordAggression(state, "Raiders", "Settlers", 40);

    var restored = WorldStateCodec.Deserialize(WorldStateCodec.Serialize(state));

    AssertEqual(-40, DiplomacyService.GetGoodwill(restored, "Raiders", "Settlers"));
    AssertEqual(true, restored.IsFactionIrreconcilable("Pirates"));
}

static void TestWorldWarLaunchesAndResolvesWarband()
{
    var state = new WorldState(4242);
    var horde = state.CreateSettlement("horde", "Horde", "Raiders");
    for (var i = 0; i < 10; i++)
    {
        state.CreateCitizen("R" + i, 30, Sex.Male, "raider", horde.Id);
    }

    var village = state.CreateSettlement("village", "Village", "Settlers");
    for (var i = 0; i < 2; i++)
    {
        state.CreateCitizen("S" + i, 30, Sex.Male, "settler", village.Id);
    }

    state.AssignFactionBehavior("Raiders", FactionBehavior.Warmonger);
    state.AssignFactionBehavior("Settlers", FactionBehavior.Cautious);
    state.RecordFactionSettlementIntel("Raiders", village.Id, IntelSourceKind.Scout, 0, confidence: 80);

    var totalCitizens = state.Citizens.Count;

    // Day 1: the warmonger plans and launches a warband (2 days travel).
    var day1 = WorldWarService.SimulateDay(state, new WorldWarRequest(1 * 60_000, TravelDays: 2, RaidCombatants: 6));
    AssertEqual(1, day1.WarbandsLaunched);
    AssertEqual(0, day1.BattlesResolved);

    // Day 2: still travelling; the faction already has an army in flight, so nothing new launches.
    var day2 = WorldWarService.SimulateDay(state, new WorldWarRequest(2 * 60_000, TravelDays: 2, RaidCombatants: 6));
    AssertEqual(0, day2.WarbandsLaunched);
    AssertEqual(0, day2.BattlesResolved);

    // Day 3: the army arrives and the stronger warband captures the village.
    var day3 = WorldWarService.SimulateDay(state, new WorldWarRequest(3 * 60_000, TravelDays: 2, RaidCombatants: 6));
    AssertEqual(1, day3.BattlesResolved);
    AssertEqual(1, day3.SettlementsCaptured);
    AssertEqual("Raiders", state.GetSettlement(village.Id)!.FactionId);

    // The attack soured relations, and population is conserved (only battle losses turned Dead).
    AssertEqual(-WorldWarService.AggressionSeverity, DiplomacyService.GetGoodwill(state, "Raiders", "Settlers"));
    AssertEqual(totalCitizens, state.Citizens.Count);

    // The war is legible in world history: the warband set out and the settlement fell.
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.WarbandLaunched));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.SettlementCaptured));
}

static void TestRepeatedLossesIncreaseFactionWarExhaustion()
{
    var state = new WorldState(4242);
    var conflict = ConflictService.GetOrCreateConflict(state, "Raiders", "Settlers", 60_000);

    ConflictService.RecordBattleOutcome(
        state,
        conflict.Id,
        attackerFactionId: "Raiders",
        defenderFactionId: "Settlers",
        attackerLosses: 2,
        defenderLosses: 5,
        capturedSettlementId: null,
        tick: 120_000);
    ConflictService.RecordBattleOutcome(
        state,
        conflict.Id,
        attackerFactionId: "Raiders",
        defenderFactionId: "Settlers",
        attackerLosses: 3,
        defenderLosses: 4,
        capturedSettlementId: null,
        tick: 180_000);

    var updated = state.GetConflict(conflict.Id)!;
    AssertEqual(5, updated.GetWarExhaustion("Raiders"));
    AssertEqual(9, updated.GetWarExhaustion("Settlers"));
    AssertEqual(1, state.Conflicts.Count);
    AssertEqual(2, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.ConflictUpdated));
}

static void TestConflictClaimTracksCapturedSettlement()
{
    var state = new WorldState(4242);
    var source = state.CreateSettlement("home", "Home", "Raiders");
    for (var i = 0; i < 8; i++)
    {
        state.CreateCitizen("R" + i, 30, Sex.Male, "raider", source.Id);
    }

    var target = state.CreateSettlement("village", "Village", "Settlers");
    for (var i = 0; i < 2; i++)
    {
        state.CreateCitizen("S" + i, 30, Sex.Female, "settler", target.Id);
    }

    var reservation = RaidPopulationAllocator.ReserveForRaid(
        state,
        new RaidPopulationAllocationRequest("Raiders", "Raiders", 6, FoodPerCitizen: 0));
    var army = reservation.Army!;
    state.DispatchArmy(army.Id, target.Id, 60_000);
    state.SetArmyMovementStatus(army.Id, ArmyMovementStatus.Arrived);

    WorldBattleService.Resolve(state, army.Id);

    var conflict = state.Conflicts.Single();
    var claim = state.ConflictClaims.Single();
    AssertEqual(target.Id, claim.SettlementId);
    AssertEqual("Raiders", claim.ClaimantFactionId);
    AssertEqual(conflict.Id, claim.ConflictId);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.ConflictClaimRecorded));

    var restored = WorldStateCodec.Deserialize(WorldStateCodec.Serialize(state));
    AssertEqual(conflict, restored.GetConflict(conflict.Id));
    AssertEqual(claim, restored.ConflictClaims.Single());
}

// Slice 1 of player war participation: a player attack pressures the VICTIM faction in every war it
// is fighting (not the player, who is never made a belligerent or ally), claims a captured settlement
// once, and — with no active war — records a standalone aggression event.
static void TestPlayerInterventionPressuresVictimWars()
{
    var state = new WorldState(4242);
    state.SetPlayerFactionId("Player");
    var enemyTown = state.CreateSettlement("enemy-town", "Enemy Town", "Raiders");
    state.CreateSettlement("rival-town", "Rival Town", "Settlers");

    // The victim (Raiders) is already at war with Settlers.
    ConflictService.GetOrCreateConflict(state, "Raiders", "Settlers", 100);

    var result = PlayerConflictInterventionService.RecordSettlementAttack(state, "Raiders", 8, enemyTown.Id, 200);
    AssertEqual(1, result.ConflictsPressured);
    AssertEqual(false, result.AggressionRecorded);

    var war = state.Conflicts.Single();
    AssertEqual(8, war.GetWarExhaustion("Raiders"));   // victim exhaustion rises in its existing war
    AssertEqual(0, war.GetWarExhaustion("Settlers"));  // the victim's enemy is untouched
    AssertEqual(false, state.Conflicts.Any(conflict => conflict.Involves("Player"))); // player is not a belligerent
    var claim = state.ConflictClaims.Single();         // captured settlement claimed once, by the player
    AssertEqual(enemyTown.Id, claim.SettlementId);
    AssertEqual("Player", claim.ClaimantFactionId);

    // With no active war, the attack records an aggression event, not a conflict.
    var peaceful = new WorldState(7);
    peaceful.SetPlayerFactionId("Player");
    var peacefulResult = PlayerConflictInterventionService.RecordSettlementAttack(peaceful, "Tribe", 5, null, 10);
    AssertEqual(0, peacefulResult.ConflictsPressured);
    AssertEqual(true, peacefulResult.AggressionRecorded);
    AssertEqual(0, peaceful.Conflicts.Count());

    // Guards: no player faction, and attacking the player itself, are no-ops.
    var noPlayer = new WorldState(1);
    AssertEqual(false, PlayerConflictInterventionService.RecordSettlementAttack(noPlayer, "Raiders", 5, null, 1).AggressionRecorded);
    AssertEqual(0, PlayerConflictInterventionService.RecordSettlementAttack(state, "Player", 5, null, 1).ConflictsPressured);
}

// Slice 2 of player war participation: an alliance (modelled as an Ally-stance goodwill relation,
// no new save state) forms only when the ally is at war, and defeating the ally's enemy earns
// the ally's gratitude.
static void TestAllianceFormsAndCreditsOnPlayerAttack()
{
    var state = new WorldState(4242);
    state.SetPlayerFactionId("Player");
    state.CreateSettlement("ally-town", "Ally Town", "Settlers");
    state.CreateSettlement("enemy-town", "Enemy Town", "Raiders");

    // An ally that is not at war cannot be allied with.
    AssertEqual(AllianceFormStatus.AllyNotAtWar, AllianceService.FormAlliance(state, "Settlers", 100).Status);

    // Put the prospective ally at war with the enemy, then the alliance forms and reaches Ally stance.
    ConflictService.GetOrCreateConflict(state, "Settlers", "Raiders", 100);
    AssertEqual(AllianceFormStatus.Formed, AllianceService.FormAlliance(state, "Settlers", 200).Status);
    AssertEqual(true, AllianceService.IsAlliedWithPlayer(state, "Settlers"));
    AssertEqual(RelationStance.Ally, DiplomacyService.GetStance(state, "Player", "Settlers"));

    // Defeating the enemy earns the at-war ally's gratitude (goodwill toward the player).
    var before = DiplomacyService.GetGoodwill(state, "Player", "Settlers");
    var credited = AllianceService.CreditAlliesOnPlayerAttack(state, "Raiders", AllianceService.DefaultAllyGratitude, 300);
    AssertEqual(1, credited);
    AssertEqual(before + AllianceService.DefaultAllyGratitude, DiplomacyService.GetGoodwill(state, "Player", "Settlers"));

    // The player cannot ally with itself.
    AssertEqual(AllianceFormStatus.AllyIsPlayer, AllianceService.FormAlliance(state, "Player", 400).Status);
}

// Slices 3-4 foundation: a decided war resolves, and the player who allied with the winner shares in
// the victory exactly once.
static void TestWarResolvesAndRewardsPlayerVictory()
{
    var state = new WorldState(4242);
    state.SetPlayerFactionId("Player");
    state.CreateSettlement("ally-town", "Ally Town", "Settlers");
    state.CreateSettlement("enemy-town", "Enemy Town", "Raiders");

    ConflictService.GetOrCreateConflict(state, "Settlers", "Raiders", 100);
    AssertEqual(AllianceFormStatus.Formed, AllianceService.FormAlliance(state, "Settlers", 200).Status);

    // The enemy takes a decisive beating (large exhaustion lead) so the ally wins.
    ConflictService.RecordBattleOutcome(
        state, "Settlers", "Raiders", attackerLosses: 0, defenderLosses: 40, capturedSettlementId: null, tick: 300);

    // Not yet resolved before the resolution pass.
    var conflictBefore = state.Conflicts.Single();
    AssertEqual(WorldConflictStatus.Active, conflictBefore.Status);
    AssertEqual("Settlers", ConflictResolutionService.WinnerOf(conflictBefore));

    var resolution = ConflictResolutionService.SimulateDay(state, 360);
    AssertEqual(1, resolution.ResolvedConflicts);
    AssertEqual(WorldConflictStatus.Resolved, state.Conflicts.Single().Status);

    // The player shares in the ally's victory (a goodwill windfall), and only once.
    var rewarded = new HashSet<long>();
    var before = DiplomacyService.GetGoodwill(state, "Player", "Settlers");
    var victories = PlayerVictoryService.GrantVictoryRewards(state, rewarded, 360);
    AssertEqual(1, victories.Count);
    AssertEqual("Settlers", victories[0].AllyFactionId);
    AssertEqual("Raiders", victories[0].EnemyFactionId);
    AssertEqual(before + PlayerVictoryService.VictoryGoodwill, DiplomacyService.GetGoodwill(state, "Player", "Settlers"));

    foreach (var victory in victories)
    {
        rewarded.Add(victory.ConflictId);
    }

    AssertEqual(0, PlayerVictoryService.GrantVictoryRewards(state, rewarded, 400).Count);
}

// The RimWorld daily tick drives war resolution + victory rewards, and the alliance UI announces the
// war objective on formation.
static void TestRimWorldWarResolutionAndVictoryWiring()
{
    var root = FindRepoRoot();
    var component = File.ReadAllText(Path.Combine(root, "src", "LivingWorld.RimWorld", "LivingWorldWorldComponent.cs"));
    AssertContains("ConflictResolutionService.SimulateDay", component);
    AssertContains("PlayerVictoryService.GrantVictoryRewards", component);
    AssertContains("rewardedVictoryConflictIds", component);
    AssertContains("Scribe_Collections.Look(ref rewardedVictoryConflictIds", component);
    AssertContains("LW_PlayerVictoryLetterText", component);

    var mainTab = File.ReadAllText(Path.Combine(root, "src", "LivingWorld.RimWorld", "MainTabWindow_LivingWorld.cs"));
    AssertContains("LW_AllianceObjectiveLetterText", mainTab);

    var en = File.ReadAllText(Path.Combine(root, "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ru = File.ReadAllText(Path.Combine(root, "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));
    foreach (var key in new[]
    {
        "LW_PlayerVictoryLetterLabel", "LW_PlayerVictoryLetterText",
        "LW_AllianceObjectiveLetterLabel", "LW_AllianceObjectiveLetterText",
    })
    {
        AssertContains($"<{key}>", en);
        AssertContains($"<{key}>", ru);
    }
}

static void TestTrucePreventsNewWarbandsUntilExpired()
{
    var state = new WorldState(4242);
    var home = state.CreateSettlement("horde", "Horde", "Raiders");
    for (var i = 0; i < 8; i++)
    {
        state.CreateCitizen("R" + i, 30, Sex.Male, "raider", home.Id);
    }

    var target = state.CreateSettlement("village", "Village", "Settlers");
    state.AssignFactionBehavior("Raiders", FactionBehavior.Warmonger);
    state.RecordFactionSettlementIntel("Raiders", target.Id, IntelSourceKind.Scout, 0, confidence: 80);
    ConflictService.StartTruce(state, "Raiders", "Settlers", startTick: 60_000, expiresTick: 180_000);

    var blocked = FactionActionPlanner.Plan(state, "Raiders", 120_000);
    var expired = FactionActionPlanner.Plan(state, "Raiders", 240_000);

    AssertEqual(WarAction.ScoutingParty, blocked.Action);
    AssertEqual(null, blocked.TargetSettlementId);
    AssertEqual(WarAction.Warband, expired.Action);
    AssertEqual(target.Id, expired.TargetSettlementId);
}

static void TestWarRefugeesEnterFinitePopulationFlow()
{
    var state = new WorldState(4242);
    var settlement = state.CreateSettlement("frontier", "Frontier", "Settlers");
    var first = state.CreateCitizen("Ada", 31, Sex.Female, "farmer", settlement.Id);
    var second = state.CreateCitizen("Bo", 12, Sex.Male, "child", settlement.Id);
    var destroyed = SettlementLifecycleService.DestroySettlement(state, settlement.Id, 60_000, "war");

    var conflict = ConflictService.GetOrCreateConflict(state, "Raiders", "Settlers", 60_000);
    ConflictService.RecordWarRefugees(state, conflict.Id, "Settlers", destroyed.RefugeesCreated, 60_000);

    var updated = state.GetConflict(conflict.Id)!;
    AssertEqual(2, updated.RefugeesCreated);
    AssertEqual(CitizenStatus.Refugee, state.GetCitizen(first.Id)!.Status);
    AssertEqual(CitizenStatus.Refugee, state.GetCitizen(second.Id)!.Status);
    AssertEqual(2, state.Citizens.Count(citizen => citizen.Status == CitizenStatus.Refugee));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.WarRefugeesRecorded));
}

static void TestWorldWarWarbandCooldownThrottlesLaunches()
{
    var state = new WorldState(4242);
    var horde = state.CreateSettlement("horde", "Horde", "Raiders");
    for (var i = 0; i < 20; i++)
    {
        state.CreateCitizen("R" + i, 30, Sex.Male, "raider", horde.Id);
    }

    var villageOne = state.CreateSettlement("v1", "Village One", "Settlers");
    for (var i = 0; i < 2; i++)
    {
        state.CreateCitizen("A" + i, 30, Sex.Male, "settler", villageOne.Id);
    }

    var villageTwo = state.CreateSettlement("v2", "Village Two", "Settlers");
    for (var i = 0; i < 2; i++)
    {
        state.CreateCitizen("B" + i, 30, Sex.Male, "settler", villageTwo.Id);
    }

    state.AssignFactionBehavior("Raiders", FactionBehavior.Warmonger);
    state.AssignFactionBehavior("Settlers", FactionBehavior.Cautious);
    state.RecordFactionSettlementIntel("Raiders", villageOne.Id, IntelSourceKind.Scout, 0, confidence: 80);
    state.RecordFactionSettlementIntel("Raiders", villageTwo.Id, IntelSourceKind.Scout, 0, confidence: 80);

    var totalLaunched = 0;
    for (var day = 1; day <= 6; day++)
    {
        var result = WorldWarService.SimulateDay(
            state,
            new WorldWarRequest(day * 60_000, TravelDays: 1, RaidCombatants: 6, WarbandCooldownDays: 10));
        totalLaunched += result.WarbandsLaunched;
    }

    // Despite a standing second enemy village, the 10-day cooldown lets only one warband launch
    // in a 6-day window — the warmonger paces itself instead of attacking every day.
    AssertEqual(1, totalLaunched);
}

static void TestWorldWarExpansionistFoundsColony()
{
    var state = new WorldState(4242);
    var home = state.CreateSettlement("home", "Home", "Settlers");
    for (var i = 0; i < 30; i++)
    {
        state.CreateCitizen("S" + i, 30, Sex.Male, "settler", home.Id);
    }

    state.AssignFactionBehavior("Settlers", FactionBehavior.Expansionist);

    var settlementsBefore = state.Settlements.Count;
    var totalCitizens = state.Citizens.Count;

    var result = WorldWarService.SimulateDay(
        state,
        new WorldWarRequest(60_000, TravelDays: 2, RaidCombatants: 6, SettlerCount: 6));

    AssertEqual(1, result.ColoniesFounded);
    AssertEqual(settlementsBefore + 1, state.Settlements.Count);
    // Population is conserved: the settlers relocated, none were created from nothing.
    AssertEqual(totalCitizens, state.Citizens.Count);

    var colony = state.Settlements.First(settlement => settlement.Id != home.Id);
    AssertEqual(6, state.GetSettlementPopulation(colony.Id).Adults);
    AssertEqual("Settlers", state.GetSettlement(colony.Id)!.FactionId);
}

static void TestExpandSettlementRejectsEmptyOrInsufficientSettlers()
{
    var state = new WorldState(4242);
    var home = state.CreateSettlement("home", "Home", "Settlers");
    state.CreateCitizen("Only Adult", 30, Sex.Female, "settler", home.Id);

    AssertThrows<ArgumentOutOfRangeException>(
        () => state.ExpandSettlement(home.Id, "empty", "Empty", 0));
    AssertThrows<InvalidOperationException>(
        () => state.ExpandSettlement(home.Id, "underfilled", "Underfilled", 2));
    AssertEqual(1, state.Settlements.Count);
    AssertEqual(1, state.GetSettlementPopulation(home.Id).Adults);
}

static void TestWorldWarExpansionUsesUniqueColonySlugs()
{
    var state = new WorldState(4242);
    var home = state.CreateSettlement("home", "Home", "Settlers");
    for (var i = 0; i < 30; i++)
    {
        state.CreateCitizen("Home Settler " + i, 30, Sex.Male, "settler", home.Id);
    }

    var existing = state.CreateSettlement("Settlers-colony-3", "Old Colony", "Settlers");
    for (var i = 0; i < 9; i++)
    {
        state.CreateCitizen("Old Settler " + i, 30, Sex.Female, "settler", existing.Id);
    }

    state.AssignFactionBehavior("Settlers", FactionBehavior.Expansionist);

    var result = WorldWarService.SimulateDay(
        state,
        new WorldWarRequest(60_000, TravelDays: 2, RaidCombatants: 6, SettlerCount: 6));

    AssertEqual(1, result.ColoniesFounded);
    AssertEqual(true, state.Settlements.Any(settlement => settlement.Slug == "Settlers-colony-4"));
    AssertEqual(false, state.Validate().Any(error => error.Contains("Duplicate settlement slug", StringComparison.Ordinal)));
}

static void TestWorldWarCaravanTransfersRealGoods()
{
    var state = new WorldState(4242);
    var market = state.CreateSettlement("market", "Market", "Traders");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen("Trader " + i, 30, Sex.Female, "merchant", market.Id);
    }

    state.AddResource(market.Id, "Steel", 40);
    var village = state.CreateSettlement("village", "Village", "Settlers");
    state.AssignFactionBehavior("Traders", FactionBehavior.Merchant);

    WorldWarService.SimulateDay(state, new WorldWarRequest(60_000, TravelDays: 1, RaidCombatants: 3));

    AssertEqual(30, state.GetOwnedResourceQuantity(market.Id, "Steel"));
    AssertEqual(0, state.GetOwnedResourceQuantity(village.Id, "Steel"));
    var caravan = state.Caravans.Single();
    AssertEqual(CaravanStatus.Traveling, caravan.Status);
    AssertEqual(10, state.GetOwnedResourceQuantity(caravan.Id, "Steel"));

    state.AssignFactionBehavior("Traders", FactionBehavior.Excluded);
    WorldWarService.SimulateDay(state, new WorldWarRequest(120_000, TravelDays: 1, RaidCombatants: 3));

    AssertEqual(CaravanStatus.Arrived, state.GetCaravan(caravan.Id)!.Status);
    AssertEqual(10, state.GetOwnedResourceQuantity(village.Id, "Steel"));
    AssertEqual(0, state.GetOwnedResourceQuantity(caravan.Id, "Steel"));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.CaravanArrived));
    AssertEqual(2, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.OwnershipTransferred));
}

static void TestPersistentCaravanSerialization()
{
    var state = new WorldState(4242);
    var source = state.CreateSettlement("source", "Source", "Traders");
    var target = state.CreateSettlement("target", "Target", "Settlers");
    state.AddResource(source.Id, "Steel", 50);

    var caravan = state.CreateCaravan("Steel caravan", "Traders", source.Id, target.Id, 0, 60_000);
    state.TransferResource(source.Id, caravan.Id, "Steel", 20, "test cargo");

    var restored = WorldStateCodec.Deserialize(WorldStateCodec.Serialize(state));
    var restoredCaravan = restored.GetCaravan(caravan.Id)!;

    AssertEqual(CaravanStatus.Traveling, restoredCaravan.Status);
    AssertEqual(source.Id, restoredCaravan.SourceSettlementId);
    AssertEqual(target.Id, restoredCaravan.TargetSettlementId);
    AssertEqual(20, restored.GetOwnedResourceQuantity(caravan.Id, "Steel"));
}

static void TestDestroyPersistentCaravanRemovesCargo()
{
    var state = new WorldState(4242);
    var source = state.CreateSettlement("source", "Source", "Traders");
    var target = state.CreateSettlement("target", "Target", "Settlers");
    state.AddResource(source.Id, "Steel", 50);

    var caravan = state.CreateCaravan("Steel caravan", "Traders", source.Id, target.Id, 0, 60_000);
    state.TransferResource(source.Id, caravan.Id, "Steel", 20, "test cargo");

    var destroyed = state.DestroyCaravan(caravan.Id, "ambushed");

    AssertEqual(CaravanStatus.Destroyed, destroyed.Status);
    AssertEqual(0, state.GetOwnedResourceQuantity(caravan.Id, "Steel"));
    AssertEqual(30, state.GetOwnedResourceQuantity(source.Id, "Steel"));
    AssertEqual(0, state.GetOwnedResourceQuantity(target.Id, "Steel"));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.CaravanDestroyed));
}

static void TestCaravanPruneRemovesTerminalCaravans()
{
    var state = new WorldState(4242);
    var source = state.CreateSettlement("source", "Source", "Traders");
    var target = state.CreateSettlement("target", "Target", "Settlers");
    state.AddResource(source.Id, "Steel", 50);

    var caravan = state.CreateCaravan("Steel caravan", "Traders", source.Id, target.Id, 0, 60_000);
    state.TransferResource(source.Id, caravan.Id, "Steel", 20, "test cargo");
    state.MarkCaravanArrived(caravan.Id); // delivers 20 Steel to target, status Arrived at tick 60_000
    AssertEqual(1, state.Caravans.Count);

    // Within the retention window: the arrived caravan is kept.
    CaravanPruneService.Prune(state, new CaravanPruneRequest(60_000 + (10 * 60_000), 30));
    AssertEqual(1, state.Caravans.Count);

    // Past retention: pruned with its ownership entry — goods stay delivered, no dangling refs.
    var result = CaravanPruneService.Prune(state, new CaravanPruneRequest(60_000 + (31 * 60_000), 30));
    AssertEqual(1, result.Pruned);
    AssertEqual(0, state.Caravans.Count);
    AssertEqual(20, state.GetOwnedResourceQuantity(target.Id, "Steel"));
    AssertEqual(0, state.Validate().Count());
}

static void TestRimWorldDrifterReservoirLegacyMigration()
{
    var component = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldWorldComponent.cs"));
    // Legacy saves (bootstrapped, reservoir 0) get a one-time reseed, guarded by a persisted flag so an
    // intentionally-depleted reservoir is never refilled on reload; fresh worlds set the flag at bootstrap.
    AssertContains("MigrateDrifterReservoirForLegacySave", component);
    AssertContains("Scribe_Values.Look(ref migratedDrifterReservoir", component);
    AssertContains("State.DrifterArrivalReservoir > 0 || State.Settlements.Count == 0", component);
    AssertContains("migratedDrifterReservoir = true", component);
}

static void TestWorldMissionSurvivesSaveLoadAndArrives()
{
    var state = new WorldState(4242);
    var scouts = state.CreateSettlement("a", "A", "Scouts");
    var target = state.CreateSettlement("b", "B", "Settlers");
    var mission = state.DispatchMission(WorldMissionKind.Scout, "Scouts", scouts.Id, target.Id, 0, 60_000, amount: 100);

    // An in-flight mission round-trips through save/load...
    var restored = WorldStateCodec.Deserialize(WorldStateCodec.Serialize(state));
    var restoredMission = restored.GetMission(mission.Id)!;
    AssertEqual(WorldMissionKind.Scout, restoredMission.Kind);
    AssertEqual(WorldMissionStatus.Traveling, restoredMission.Status);
    AssertEqual(scouts.Id, restoredMission.OriginSettlementId);
    AssertEqual(target.Id, restoredMission.TargetSettlementId);
    AssertEqual(100, restoredMission.Amount);

    // ...and still arrives and applies its effect (and is removed) after load.
    WorldMissionService.SimulateDay(restored, new WorldMissionRequest(60_000));
    AssertEqual(IntelSourceKind.Scout, restored.GetKnownSettlementInfo(target.Id)!.SourceKind);
    AssertEqual(0, restored.Missions.Count);
}

static void TestPersistentCaravanArrivesAfterLoad()
{
    var state = new WorldState(4242);
    var source = state.CreateSettlement("source", "Source", "Traders");
    var target = state.CreateSettlement("target", "Target", "Settlers");
    state.AddResource(source.Id, "Steel", 50);
    var caravan = state.CreateCaravan("Steel caravan", "Traders", source.Id, target.Id, 0, 60_000);
    state.TransferResource(source.Id, caravan.Id, "Steel", 20, "cargo");

    // A caravan saved mid-flight must still deliver its cargo when it arrives after load.
    var restored = WorldStateCodec.Deserialize(WorldStateCodec.Serialize(state));
    var arrived = restored.MarkCaravanArrived(caravan.Id);

    AssertEqual(CaravanStatus.Arrived, arrived.Status);
    AssertEqual(20, restored.GetOwnedResourceQuantity(target.Id, "Steel"));
    AssertEqual(0, restored.GetOwnedResourceQuantity(caravan.Id, "Steel"));
    AssertEqual(0, restored.Validate().Count());
}

static void TestDerivedAggregatesTrackCaptureAndExpansion()
{
    var state = new WorldState(4242);
    var raider = state.CreateSettlement("a", "A", "Raiders");
    var settler = state.CreateSettlement("b", "B", "Settlers");
    for (var i = 0; i < 20; i++)
    {
        state.CreateCitizen("A" + i, 30, Sex.Male, "settler", raider.Id);
        state.CreateCitizen("B" + i, 30, Sex.Male, "settler", settler.Id);
    }

    // Prime the caches, then mutate faction ownership via capture — both faction aggregates must track it.
    AssertFactionAggregateMatchesFullScan(state, "Raiders");
    AssertFactionAggregateMatchesFullScan(state, "Settlers");
    state.CaptureSettlement(settler.Id, "Raiders");
    AssertFactionAggregateMatchesFullScan(state, "Raiders");
    AssertFactionAggregateMatchesFullScan(state, "Settlers");
    AssertAggregateMatchesFullScan(state, settler.Id);

    // Expansion moves adults to a new colony — the source settlement aggregate must track the drop.
    AssertAggregateMatchesFullScan(state, raider.Id);
    state.ExpandSettlement(raider.Id, "a-colony", "A Colony", 6);
    AssertAggregateMatchesFullScan(state, raider.Id);
    AssertFactionAggregateMatchesFullScan(state, "Raiders");
}

static void TestDestroyedSettlementLeavesRuinAndRefugees()
{
    var state = new WorldState(4242);
    var settlement = state.CreateSettlement("frontier", "Frontier", "Settlers");
    var adult = state.CreateCitizen("Ada", 31, Sex.Female, "farmer", settlement.Id);
    var child = state.CreateCitizen("Bo", 9, Sex.Male, "child", settlement.Id);
    state.AddResource(settlement.Id, "Steel", 90);
    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 20);

    var result = SettlementLifecycleService.DestroySettlement(
        state,
        settlement.Id,
        tick: 120_000,
        reason: "burned");

    AssertEqual(SettlementLifecycleStatus.Destroyed, state.GetSettlement(settlement.Id)!.Status);
    AssertEqual(1, state.Ruins.Count);
    AssertEqual(settlement.Id, result.Ruin.OriginalSettlementId);
    AssertEqual(RuinSalvageBand.Medium, result.Ruin.SalvageBand);
    AssertEqual(0, state.GetSettlementPopulation(settlement.Id).Total);
    AssertEqual(CitizenStatus.Refugee, state.GetCitizen(adult.Id)!.Status);
    AssertEqual(CitizenStatus.Refugee, state.GetCitizen(child.Id)!.Status);
    AssertEqual(adult.Id, state.GetOwner(adult.Id));
    AssertEqual(child.Id, state.GetOwner(child.Id));
    AssertEqual(90, state.GetOwnedResourceQuantity(result.Ruin.Id, "Steel"));
    AssertEqual(20, state.GetOwnedResourceQuantity(result.Ruin.Id, "PackagedSurvivalMeal"));
    AssertEqual(0, state.GetOwnedResourceQuantity(settlement.Id, "Steel"));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.SettlementDestroyed));
    AssertEqual(0, state.Validate().Count());
}

static void TestRelocationMovesCitizensAndResourcesThroughMigrationGroup()
{
    var state = new WorldState(4242);
    var source = state.CreateSettlement("unsafe", "Unsafe", "Settlers");
    var target = state.CreateSettlement("safe", "Safe", "Settlers");
    var first = state.CreateCitizen("Ada", 31, Sex.Female, "farmer", source.Id);
    var second = state.CreateCitizen("Bo", 34, Sex.Male, "builder", source.Id);
    state.AddResource(source.Id, "Steel", 80);
    state.AddResource(source.Id, "PackagedSurvivalMeal", 30);

    var result = SettlementLifecycleService.StartRelocation(
        state,
        source.Id,
        target.Id,
        tick: 10_000,
        arrivalTick: 70_000,
        reason: "unsafe");

    AssertEqual(SettlementLifecycleStatus.Abandoned, state.GetSettlement(source.Id)!.Status);
    AssertEqual(1, state.Ruins.Count);
    AssertEqual(MigrationGroupStatus.Traveling, result.MigrationGroup.Status);
    AssertEqual(result.MigrationGroup.Id, state.GetOwner(first.Id));
    AssertEqual(result.MigrationGroup.Id, state.GetOwner(second.Id));
    AssertEqual(80, state.GetOwnedResourceQuantity(result.MigrationGroup.Id, "Steel"));
    AssertEqual(0, state.GetOwnedResourceQuantity(source.Id, "Steel"));

    var completed = MigrationService.SimulateDay(
        state,
        new MigrationSimulationRequest(70_000, "PackagedSurvivalMeal", 1, 99, 0));

    AssertEqual(2, completed.MigrationsCompleted);
    AssertEqual(MigrationGroupStatus.Arrived, state.GetMigrationGroup(result.MigrationGroup.Id)!.Status);
    AssertEqual(target.Id, state.GetOwner(first.Id));
    AssertEqual(target.Id, state.GetOwner(second.Id));
    AssertEqual(80, state.GetOwnedResourceQuantity(target.Id, "Steel"));
    AssertEqual(30, state.GetOwnedResourceQuantity(target.Id, "PackagedSurvivalMeal"));
    AssertEqual(0, state.GetOwnedResourceQuantity(result.MigrationGroup.Id, "Steel"));
    AssertEqual(0, state.Validate().Count());
}

static void TestRuinCanBeReclaimedWithoutDuplicatingResources()
{
    var state = new WorldState(4242);
    var settlement = state.CreateSettlement("old-town", "Old Town", "Settlers");
    state.AddResource(settlement.Id, "Steel", 100);
    state.AddResource(settlement.Id, "ComponentIndustrial", 5);
    var ruin = SettlementLifecycleService.DestroySettlement(
        state,
        settlement.Id,
        tick: 60_000,
        reason: "abandoned").Ruin;
    state = WorldStateCodec.Deserialize(WorldStateCodec.Serialize(state));
    ruin = state.GetRuin(ruin.Id)!;

    var reclaimed = SettlementLifecycleService.ReclaimRuin(
        state,
        ruin.Id,
        claimantFactionId: "Rebuilders",
        tick: 120_000);

    AssertEqual(RuinStatus.Reclaimed, state.GetRuin(ruin.Id)!.Status);
    AssertEqual(settlement.Id, reclaimed.Settlement.Id);
    AssertEqual("Rebuilders", reclaimed.Settlement.FactionId);
    AssertEqual(SettlementLifecycleStatus.Active, reclaimed.Settlement.Status);
    AssertEqual(100, state.GetOwnedResourceQuantity(settlement.Id, "Steel"));
    AssertEqual(0, state.GetOwnedResourceQuantity(ruin.Id, "Steel"));
    AssertEqual(5, state.GetOwnedResourceQuantity(settlement.Id, "ComponentIndustrial"));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.RuinReclaimed));
    AssertEqual(0, state.Validate().Count());
}

static void TestOldInactiveRuinsCanBePrunedAfterHistoryIsRecorded()
{
    var state = new WorldState(4242);
    var settlement = state.CreateSettlement("old-town", "Old Town", "Settlers");
    state.AddResource(settlement.Id, "Steel", 100);
    var ruin = SettlementLifecycleService.DestroySettlement(
        state,
        settlement.Id,
        tick: 60_000,
        reason: "abandoned").Ruin;
    SettlementLifecycleService.ReclaimRuin(state, ruin.Id, "Rebuilders", tick: 120_000);
    var eventsBeforePrune = state.Events.Count;

    var early = SettlementLifecycleService.PruneInactiveRuins(
        state,
        currentTick: 4 * 60_000,
        retentionDays: 5);
    var pruned = SettlementLifecycleService.PruneInactiveRuins(
        state,
        currentTick: 8 * 60_000,
        retentionDays: 5);

    AssertEqual(0, early.Pruned);
    AssertEqual(1, pruned.Pruned);
    AssertEqual(null, state.GetRuin(ruin.Id));
    AssertEqual(eventsBeforePrune + 1, state.Events.Count);
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.RuinPruned));
    AssertEqual(0, state.GetOwnedResourceQuantity(ruin.Id, "Steel"));
}

static void TestLifecycleDriverRelocatesStarvingSettlements()
{
    var state = new WorldState(4242);
    var source = state.CreateSettlement("hungry", "Hungry", "Settlers");
    var target = state.CreateSettlement("safe", "Safe", "Settlers");
    var first = state.CreateCitizen("Ada", 31, Sex.Female, "farmer", source.Id);
    var second = state.CreateCitizen("Bo", 34, Sex.Male, "builder", source.Id);
    state.CreateCitizen("Cal", 38, Sex.Male, "cook", target.Id);
    state.AddResource(source.Id, "Steel", 70);
    state.AddResource(target.Id, "PackagedSurvivalMeal", 120);

    var result = SettlementLifecycleDriver.SimulateDay(
        state,
        new SettlementLifecycleDriverRequest(
            Tick: 60_000,
            FoodResourceKey: "PackagedSurvivalMeal",
            FoodPerCitizen: 1,
            RelocationTravelTicks: 30_000));

    AssertEqual(1, result.RelocationsStarted);
    AssertEqual(SettlementLifecycleStatus.Abandoned, state.GetSettlement(source.Id)!.Status);
    AssertEqual(1, state.Ruins.Count(ruin => ruin.OriginalSettlementId == source.Id));
    var group = state.MigrationGroups.Single(group => group.SourceSettlementId == source.Id);
    AssertEqual(MigrationGroupStatus.Traveling, group.Status);
    AssertEqual(target.Id, group.TargetSettlementId);
    AssertEqual(group.Id, state.GetOwner(first.Id));
    AssertEqual(group.Id, state.GetOwner(second.Id));
    AssertEqual(70, state.GetOwnedResourceQuantity(group.Id, "Steel"));
    AssertEqual(0, state.GetOwnedResourceQuantity(source.Id, "Steel"));

    var completed = MigrationService.SimulateDay(
        state,
        new MigrationSimulationRequest(90_000, "PackagedSurvivalMeal", 1, 99, 0));

    AssertEqual(2, completed.MigrationsCompleted);
    AssertEqual(target.Id, state.GetOwner(first.Id));
    AssertEqual(target.Id, state.GetOwner(second.Id));
    AssertEqual(70, state.GetOwnedResourceQuantity(target.Id, "Steel"));
    AssertEqual(0, state.Validate().Count());
}

static void TestLifecycleDriverDestroysCollapsedFactionSettlements()
{
    var state = new WorldState(4242);
    var empty = state.CreateSettlement("empty", "Empty", "LostFaction");
    var alive = state.CreateSettlement("alive", "Alive", "LivingFaction");
    state.CreateCitizen("Ada", 31, Sex.Female, "farmer", alive.Id);
    state.AddResource(empty.Id, "Steel", 90);

    var result = SettlementLifecycleDriver.SimulateDay(
        state,
        new SettlementLifecycleDriverRequest(
            Tick: 60_000,
            FoodResourceKey: "PackagedSurvivalMeal",
            FoodPerCitizen: 1)
        {
            ResolveFactionCollapses = true
        });

    AssertEqual(1, result.CollapsedFactions);
    AssertEqual(1, result.DestroyedCollapsedSettlements);
    AssertEqual(SettlementLifecycleStatus.Destroyed, state.GetSettlement(empty.Id)!.Status);
    var ruin = state.Ruins.Single(ruin => ruin.OriginalSettlementId == empty.Id);
    AssertEqual(90, state.GetOwnedResourceQuantity(ruin.Id, "Steel"));
    AssertEqual(0, state.GetOwnedResourceQuantity(empty.Id, "Steel"));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.SettlementDestroyed));
    AssertEqual(0, state.Validate().Count());
}

static void TestLifecycleDriverPrunesInactiveRuins()
{
    var state = new WorldState(4242);
    var settlement = state.CreateSettlement("old-town", "Old Town", "Settlers");
    state.AddResource(settlement.Id, "Steel", 100);
    var ruin = SettlementLifecycleService.DestroySettlement(
        state,
        settlement.Id,
        tick: 60_000,
        reason: "destroyed").Ruin;
    SettlementLifecycleService.ReclaimRuin(state, ruin.Id, "Rebuilders", tick: 120_000);

    var result = SettlementLifecycleDriver.SimulateDay(
        state,
        new SettlementLifecycleDriverRequest(
            Tick: 8 * 60_000,
            FoodResourceKey: "PackagedSurvivalMeal",
            FoodPerCitizen: 1,
            RuinRetentionDays: 5));

    AssertEqual(1, result.RuinsPruned);
    AssertEqual(null, state.GetRuin(ruin.Id));
    AssertEqual(1, state.Events.Count(worldEvent => worldEvent.Kind == WorldEventKind.RuinPruned));
}

static void TestWorldWarDevelopActionInvestsInSettlement()
{
    var state = new WorldState(4242);
    var builders = state.CreateSettlement("builders", "Builders", "Builders");
    for (var i = 0; i < 6; i++)
    {
        state.CreateCitizen("Builder " + i, 30, Sex.Male, "builder", builders.Id);
    }

    state.AddResource(builders.Id, "PackagedSurvivalMeal", 80);
    state.AddResource(builders.Id, "Silver", 300);
    state.RecordSettlementCapability(new SettlementCapability(
        builders.Id,
        HousingCapacity: 5,
        FoodStorageCapacity: 5,
        MedicineStorageCapacity: 0,
        PowerCapacity: 0,
        LaboratoryCapacity: 0,
        AnimalCapacity: 0,
        CropCapacity: 0,
        ResearchCapacity: 0,
        MechanicalCapacity: 0,
        PollutionHandling: 0));
    state.AssignFactionBehavior("Builders", FactionBehavior.Cautious);

    var result = WorldWarService.SimulateDay(
        state,
        new WorldWarRequest(60_000, TravelDays: 1, RaidCombatants: 3)
        {
            DevelopmentSilverCost = 100,
            DevelopmentStep = 6,
            DevelopmentHousingHeadroom = 10,
            DevelopmentSpecialistGrowthStep = 1,
        });

    AssertEqual(1, result.DevelopmentsCompleted);
    AssertEqual(11, state.GetSettlementCapability(builders.Id)!.HousingCapacity);
    AssertEqual(200, state.GetOwnedResourceQuantity(builders.Id, "Silver"));
    AssertEqual(1, state.GetSpecialistPool(builders.Id)!.Engineers);
}

static void TestWorldWarScoutingRecordsIntel()
{
    var state = new WorldState(4242);
    var scouts = state.CreateSettlement("watch", "Watch", "Scouts");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen("Scout " + i, 30, Sex.Male, "scout", scouts.Id);
    }

    var village = state.CreateSettlement("village", "Village", "Settlers");
    for (var i = 0; i < 12; i++)
    {
        state.CreateCitizen("Settler " + i, 30, Sex.Female, "settler", village.Id);
    }

    state.AssignFactionBehavior("Scouts", FactionBehavior.Cautious);

    // Day 1: the scouting party is dispatched and travels — no intel yet, a scout mission is in flight.
    WorldWarService.SimulateDay(state, new WorldWarRequest(60_000, TravelDays: 1, RaidCombatants: 3));
    AssertEqual(0, state.IntelReports.Count(report => report.SourceKind == IntelSourceKind.Scout));
    AssertEqual(1, state.Missions.Count(mission => mission.Kind == WorldMissionKind.Scout));

    // Day 2: the party arrives and records intel about the target.
    WorldWarService.SimulateDay(state, new WorldWarRequest(120_000, TravelDays: 1, RaidCombatants: 3));

    var known = state.GetKnownSettlementInfo(village.Id)!;
    AssertEqual(IntelSourceKind.Scout, known.SourceKind);
    AssertEqual(KnowledgeConfidence.High, known.Confidence);
    AssertEqual(SettlementPopulationBand.Small, known.PopulationBand);
    AssertEqual(true, state.HasFactionSettlementIntel("Scouts", village.Id));
    AssertEqual(1, state.IntelReports.Count(report => report.SourceKind == IntelSourceKind.Scout));
}

static void TestWorldWarDiplomatChangesGoodwill()
{
    var state = new WorldState(4242);
    var envoys = state.CreateSettlement("envoys", "Envoys", "Envoys");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen("Envoy " + i, 30, Sex.Female, "diplomat", envoys.Id);
    }

    state.CreateSettlement("neighbor", "Neighbor", "Neighbors");
    state.AssignFactionBehavior("Envoys", FactionBehavior.Random);

    // Day 1: the diplomatic mission is dispatched and travels — goodwill unchanged in transit.
    WorldWarService.SimulateDay(state, new WorldWarRequest(4 * 60_000, TravelDays: 1, RaidCombatants: 3));
    AssertEqual(0, DiplomacyService.GetGoodwill(state, "Envoys", "Neighbors"));
    AssertEqual(1, state.Missions.Count(mission => mission.Kind == WorldMissionKind.Diplomat));

    // Day 2: the mission arrives and improves relations.
    WorldWarService.SimulateDay(state, new WorldWarRequest(5 * 60_000, TravelDays: 1, RaidCombatants: 3));
    AssertEqual(5, DiplomacyService.GetGoodwill(state, "Envoys", "Neighbors"));
}

static void TestWorldWarNonWarbandEffectsPersistThroughSaveLoad()
{
    var state = new WorldState(4242);
    var village = state.CreateSettlement("village", "Village", "Villagers");
    for (var i = 0; i < 4; i++)
    {
        state.CreateCitizen("Villager " + i, 30, Sex.Female, "settler", village.Id);
    }

    var market = state.CreateSettlement("market", "Market", "Traders");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen("Trader " + i, 30, Sex.Male, "merchant", market.Id);
    }

    var watch = state.CreateSettlement("watch", "Watch", "Scouts");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen("Scout " + i, 30, Sex.Male, "scout", watch.Id);
    }

    var envoys = state.CreateSettlement("envoys", "Envoys", "Envoys");
    for (var i = 0; i < 3; i++)
    {
        state.CreateCitizen("Envoy " + i, 30, Sex.Female, "diplomat", envoys.Id);
    }

    state.AddResource(market.Id, "Steel", 40);
    state.AssignFactionBehavior("Traders", FactionBehavior.Merchant);
    WorldWarService.SimulateDay(state, new WorldWarRequest(60_000, TravelDays: 1, RaidCombatants: 3));

    state.AssignFactionBehavior("Traders", FactionBehavior.Excluded);
    state.AssignFactionBehavior("Scouts", FactionBehavior.Cautious);
    WorldWarService.SimulateDay(state, new WorldWarRequest(2 * 60_000, TravelDays: 1, RaidCombatants: 3));

    state.AssignFactionBehavior("Scouts", FactionBehavior.Excluded);
    state.AssignFactionBehavior("Envoys", FactionBehavior.Random);
    WorldWarService.SimulateDay(state, new WorldWarRequest(4 * 60_000, TravelDays: 1, RaidCombatants: 3));

    // Let the in-flight scout and diplomat missions arrive before saving (they apply on arrival now).
    state.AssignFactionBehavior("Envoys", FactionBehavior.Excluded);
    WorldWarService.SimulateDay(state, new WorldWarRequest(6 * 60_000, TravelDays: 1, RaidCombatants: 3));

    var restored = WorldStateCodec.Deserialize(WorldStateCodec.Serialize(state));

    AssertEqual(10, restored.GetOwnedResourceQuantity(village.Id, "Steel"));
    AssertEqual(IntelSourceKind.Scout, restored.GetKnownSettlementInfo(village.Id)!.SourceKind);
    AssertEqual(true, restored.HasFactionSettlementIntel("Scouts", village.Id));
    AssertEqual(5, DiplomacyService.GetGoodwill(restored, "Envoys", "Villagers"));
}

static void TestWorldWarServiceSplitExecutors()
{
    var root = FindRepoRoot();
    var core = Path.Combine(root, "src", "LivingWorld.Core");
    var service = File.ReadAllText(Path.Combine(core, "WorldWarService.cs"));

    AssertFileExists(Path.Combine(core, "WorldWarActionDispatcher.cs"));
    AssertFileExists(Path.Combine(core, "WarbandActionExecutor.cs"));
    AssertFileExists(Path.Combine(core, "SettlementExpansionExecutor.cs"));
    AssertFileExists(Path.Combine(core, "CaravanActionExecutor.cs"));
    AssertFileExists(Path.Combine(core, "ScoutingActionExecutor.cs"));
    AssertFileExists(Path.Combine(core, "DiplomacyActionExecutor.cs"));
    AssertFileExists(Path.Combine(core, "SettlementDevelopmentActionExecutor.cs"));
    AssertFileExists(Path.Combine(core, "WorldWarTargetSelector.cs"));

    AssertContains("WorldWarActionDispatcher.Execute", service);
    AssertDoesNotContain("TryLaunchWarband", service);
    AssertDoesNotContain("TryFoundColony", service);
    AssertDoesNotContain("TryRunCaravan", service);
    AssertDoesNotContain("TryRunScoutingParty", service);
    AssertDoesNotContain("TryRunDiplomat", service);
    AssertDoesNotContain("TryDevelopSettlement", service);
}

static void TestRimWorldWorldWarIntegration()
{
    var component = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldWorldComponent.cs"));
    AssertContains("WorldWarService.SimulateDay", component);
    AssertContains("EnsureFactionBehaviors", component);
    // Mutual exclusion with Rim War.
    AssertContains("Torann.RimWar", component);
    AssertContains("settings.worldWarEnabled", component);

    var settings = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldSettings.cs"));
    AssertContains("public bool worldWarEnabled", settings);
    AssertContains("Scribe_Values.Look(ref worldWarEnabled", settings);
    AssertContains("public int worldWarTravelDays", settings);
    AssertContains("public int worldWarRaidCombatants", settings);
}

static void TestRimWorldWorldWarMainTab()
{
    var mainTab = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "MainTabWindow_LivingWorld.cs"));
    // A dedicated world-war section with capped, cached rows (no per-frame full-population scan).
    AssertContains("LW_WorldWarHeader", mainTab);
    AssertContains("MaxWarRows", mainTab);
    AssertContains("cachedWarHistoryRows", mainTab);
    AssertContains("cachedActiveWarbandRows", mainTab);
    AssertContains("cachedFactionStrengthRows", mainTab);
    // Warns the player when Rim War is driving the world instead of Living World.
    AssertContains("IsRimWarActive", mainTab);
    AssertContains("LW_WorldWarDisabledByRimWar", mainTab);
    // Strength is shown as a band unless debug exact values are enabled.
    AssertContains("LW_FactionStrengthBand", mainTab);

    var component = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldWorldComponent.cs"));
    AssertContains("public bool IsRimWarActive", component);

    var en = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ru = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));
    AssertContains("<LW_WorldWarHeader>", en);
    AssertContains("<LW_WorldWarHeader>", ru);
    AssertContains("<LW_WorldWarDisabledByRimWar>", en);
    AssertContains("<LW_WorldWarDisabledByRimWar>", ru);
}

static void TestRimWorldWorldWarNotifications()
{
    var component = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldWorldComponent.cs"));
    // A single rate-limited letter after the daily catch-up loop (not one per simulated day).
    AssertContains("MaybeSendWorldWarLetter", component);
    AssertContains("ReceiveLetter", component);
    AssertContains("worldWarLetterCooldownDays", component);
    // Persisted so a save/load never re-announces old captures.
    AssertContains("notifiedCaptureCount", component);
    AssertContains("Scribe_Values.Look(ref notifiedCaptureCount", component);
    // Respects the Rim War exclusion flag: no letters when the loop is off.
    AssertContains("IsRimWarActive", component);

    var settings = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldSettings.cs"));
    AssertContains("public int worldWarLetterCooldownDays", settings);
    AssertContains("Scribe_Values.Look(ref worldWarLetterCooldownDays", settings);

    var en = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ru = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));
    AssertContains("<LW_WorldWarLetterLabel>", en);
    AssertContains("<LW_WorldWarLetterLabel>", ru);
    AssertContains("<LW_WorldWarLetterText>", en);
    AssertContains("<LW_WorldWarLetterText>", ru);
}

// Task 6 RW-side: newly-declared NPC wars produce a batched, persisted, gated letter (distinct from
// the capture letter, which reports territory changes rather than declarations).
static void TestRimWorldConflictLetters()
{
    var component = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldWorldComponent.cs"));
    AssertContains("MaybeSendConflictLetters", component);
    AssertContains("WorldConflictStatus.Active", component);
    AssertContains("LW_ConflictLetterText", component);
    // Persisted per conflict id so a save/load never re-announces an old war.
    AssertContains("notifiedConflictIds", component);
    AssertContains("Scribe_Collections.Look(ref notifiedConflictIds", component);
    // Same gate as the capture letter: silent when the war is off, ceded to Rim War, or seeding.
    AssertContains("!settings.worldWarEnabled || RimWarIsActive || State.IsInitialWorldSeedingActive", component);

    var en = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ru = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));
    AssertContains("<LW_ConflictLetterLabel>", en);
    AssertContains("<LW_ConflictLetterLabel>", ru);
    AssertContains("<LW_ConflictLetterText>", en);
    AssertContains("<LW_ConflictLetterText>", ru);
}

static void TestRimWorldReleasesStaleReservations()
{
    var component = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldWorldComponent.cs"));
    // The daily tick returns reserved citizens/supplies from stale raid preparations and expired
    // materialization leases so nothing leaks (Task 3 lifecycle wiring + the flagged prep cleanup).
    AssertContains("RaidPreparationService.ReleaseExpiredPreparations(State", component);
    AssertContains("MaterializationLeaseService.ReleaseExpiredLeases(State", component);
}

// The daily tick drives the Core simulation drivers so facilities and ruins actually appear
// in-game: infrastructure builds/repairs facilities (gated with settlement development), and the
// lifecycle driver folds in faction collapse (ResolveFactionCollapses) plus ruins/relocation.
static void TestRimWorldDailyTickRunsSimulationDrivers()
{
    var component = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldWorldComponent.cs"));
    AssertContains("SettlementInfrastructureDriver.SimulateDay(", component);
    AssertContains("SettlementLifecycleDriver.SimulateDay(", component);
    AssertContains("ResolveFactionCollapses = true", component);
    // The lifecycle driver subsumes the collapse pass, so the bare collapse call must be gone to
    // avoid running faction collapse twice per day.
    AssertDoesNotContain("FactionLifecycleService.SimulateCollapses(", component);
}

static void TestRimWorldDailyTickRunsAnimalEcologyDriver()
{
    var component = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldWorldComponent.cs"));
    // Animal cohorts are not only a saved Core model: the RimWorld daily loop seeds biome-based
    // cohorts and runs feed pressure against the same food ledger used by settlements.
    AssertContains("AnimalEcologyDriver.SimulateDay(", component);
    AssertContains("new AnimalEcologyDriverRequest(", component);
    AssertContains("AnimalProductionService.SimulateDay(", component);
    AssertContains("AnimalBreedingDriver.SimulateDay(", component);
    AssertContains("FoodResourceKey", component);
}

static void TestRimWorldRaidRoutesThroughPreparation()
{
    var worker = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "IncidentWorker_LivingWorldFactionRaid.cs"));
    // The raid now asks Core for a prepared expedition (intent -> preparation) instead of reserving
    // citizens ad-hoc; intel scales the size but the storyteller's points set the floor.
    AssertContains("RaidIntentService.TryCreateBestIntent", worker);
    AssertContains("RaidPreparationService.PrepareRaid", worker);
    AssertContains("preparation.ArmyId", worker);
    AssertContains("LaunchRaidPreparation", worker);
    AssertContains("Math.Max(storytellerCombatants", worker);
    // No more ad-hoc reservation in the incident itself.
    AssertDoesNotContain("RaidPopulationAllocator.ReserveForRaid", worker);
}

static void TestRimWorldDailyTickReleasesExpiredRaidPreparations()
{
    var component = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldWorldComponent.cs"));
    AssertContains("RaidPreparationService.ReleaseExpiredPreparations", component);
    AssertContains("day * TicksPerDay", component);
}

static void TestRimWorldRaidWarningFromIntel()
{
    var component = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldWorldComponent.cs"));
    // A believable, deterministic warning when a hostile faction holds fresh player-targeted raid
    // intel; source-labeled, rate-limited, persisted per fact, and silent when ceded to Rim War.
    AssertContains("MaybeSendRaidWarnings", component);
    AssertContains("RaidIntelTargetKind.PlayerColony", component);
    AssertContains("RaidWarningSourceKey", component);
    AssertContains("LW_RaidWarningLetterText", component);
    AssertContains("notifiedRaidWarningFactIds", component);
    AssertContains("Scribe_Collections.Look(ref notifiedRaidWarningFactIds", component);
    AssertContains("IsRimWarActive", component);

    var en = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ru = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));
    AssertContains("<LW_RaidWarningLetterText>", en);
    AssertContains("<LW_RaidWarningLetterText>", ru);
    AssertContains("<LW_RaidWarningSource_Scout>", en);
    AssertContains("<LW_RaidWarningSource_Scout>", ru);
}

static void TestRimWorldRaidConsequenceLetter()
{
    var component = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldWorldComponent.cs"));
    // After a Living World raid resolves, one letter attributes it to the source settlement/faction
    // and reports the losses (the "raid used real people, and they are weaker now" payoff).
    AssertContains("MaybeSendRaidConsequenceLetters", component);
    AssertContains("RaidOutcomes", component);
    AssertContains("outcome.IsResolved", component);
    AssertContains("SourceSettlementId", component);
    AssertContains("LW_RaidConsequenceLetterText", component);
    // Persisted per-raid so save/load never re-announces a raid that already resolved.
    AssertContains("notifiedResolvedRaidArmyIds", component);
    AssertContains("Scribe_Collections.Look(ref notifiedResolvedRaidArmyIds", component);

    var en = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ru = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));
    AssertContains("<LW_RaidConsequenceLetterText>", en);
    AssertContains("<LW_RaidConsequenceLetterText>", ru);
    AssertContains("<LW_RaidConsequenceLetterLabel>", en);
    AssertContains("<LW_RaidConsequenceLetterLabel>", ru);
}

static void TestRimWorldWorldMapSpeedTestOverride()
{
    var root = FindRepoRoot();
    var patchPath = Path.Combine(root, "src", "LivingWorld.RimWorld", "LivingWorldWorldMapSpeedPatch.cs");
    var settingsPath = Path.Combine(root, "src", "LivingWorld.RimWorld", "LivingWorldSettings.cs");
    var drawerPath = Path.Combine(root, "src", "LivingWorld.RimWorld", "LivingWorldSettingsDrawer.cs");

    AssertFileExists(patchPath);

    var patch = File.ReadAllText(patchPath);
    var settings = File.ReadAllText(settingsPath);
    var drawer = File.ReadAllText(drawerPath);

    AssertContains("[HarmonyPatch(typeof(TickManager), \"get_TickRateMultiplier\")]", patch);
    AssertContains("WorldRendererUtility.WorldRendered", patch);
    AssertContains("CurTimeSpeed < TimeSpeed.Fast", patch);
    AssertContains("worldMapSpeedTestEnabled", patch);
    AssertContains("worldMapSpeedMultiplier", patch);
    // A real multiplier (base * N), capped — not Max(base, N), which no-ops at Superfast/Ultrafast
    // where vanilla's tick rate already exceeds N.
    AssertContains("__result * settings.worldMapSpeedMultiplier", patch);
    AssertContains("MaxBoostedTickRate", patch);

    AssertContains("public bool worldMapSpeedTestEnabled = false", settings);
    AssertContains("public int worldMapSpeedMultiplier = 5", settings);
    AssertContains("Scribe_Values.Look(ref worldMapSpeedTestEnabled", settings);
    AssertContains("Scribe_Values.Look(ref worldMapSpeedMultiplier", settings);

    AssertContains("LW_Settings_WorldMapSpeedTest", drawer);
    AssertContains("DrawWorldMapSpeedChoice", drawer);
    AssertContains("settings.worldMapSpeedMultiplier = 3", drawer);
    AssertContains("settings.worldMapSpeedMultiplier = 5", drawer);
    AssertContains("settings.worldMapSpeedMultiplier = 10", drawer);

    var en = File.ReadAllText(Path.Combine(root, "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ru = File.ReadAllText(Path.Combine(root, "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));
    AssertContains("<LW_Settings_WorldMapSpeedTest>", en);
    AssertContains("<LW_Settings_WorldMapSpeedTest>", ru);
    AssertContains("<LW_Settings_WorldMapSpeedTestTip>", en);
    AssertContains("<LW_Settings_WorldMapSpeedTestTip>", ru);
    AssertContains("<LW_Settings_WorldMapSpeedMultiplier>", en);
    AssertContains("<LW_Settings_WorldMapSpeedMultiplier>", ru);
}

static void TestLivingWorldSettingsAreGroupedByPlayerIntent()
{
    var drawer = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldSettingsDrawer.cs"));
    foreach (var section in new[]
    {
        "LW_SettingsSection_Population",
        "LW_SettingsSection_FactionActivity",
        "LW_SettingsSection_Development",
        "LW_SettingsSection_Baseline",
        "LW_SettingsSection_Performance",
        "LW_SettingsSection_Compatibility",
        "LW_SettingsSection_Debug",
    })
    {
        AssertContains(section, drawer);
    }

    // Ongoing behaviour is now exposed and grouped, not just the baseline sliders.
    AssertContains("settings.worldWarEnabled", drawer);
    AssertContains("settings.settlementDevelopmentEnabled", drawer);
    AssertContains("settings.targetWorldPopulationPerSettlement", drawer);

    // The grouped page scrolls (it does not fit a fixed settings window).
    var mod = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldMod.cs"));
    AssertContains("BeginScrollView", mod);
}

static void TestCompatibilitySettingsSurfaceCedenceState()
{
    var drawer = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldSettingsDrawer.cs"));
    AssertContains("DrawCompatibilityState", drawer);
    AssertContains("ModsConfig.IsActive(\"Torann.RimWar\")", drawer);
    AssertContains("ModsConfig.IsActive(\"Matathias.Empire\")", drawer);
    AssertContains("LW_WorldWarDisabledByRimWar", drawer);
    AssertContains("LW_Settings_CompatNoneActive", drawer);
}

static void TestLivingWorldSettingsHaveRussianAndEnglishKeys()
{
    var en = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ru = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));
    foreach (var key in new[]
    {
        "LW_SettingsSection_Population",
        "LW_SettingsSection_FactionActivity",
        "LW_SettingsSection_Performance",
        "LW_SettingsSection_Compatibility",
        "LW_Settings_TargetDensity",
        "LW_Settings_WorldWarEnabled",
        "LW_Settings_DevelopmentEnabled",
        "LW_Settings_CompatNoneActive",
    })
    {
        AssertContains($"<{key}>", en);
        AssertContains($"<{key}>", ru);
    }
}

static void TestRimWorldEmpireInterop()
{
    var component = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldWorldComponent.cs"));
    // Detect Empire (Matathias.Empire) like the Rim War flag, so the UI can tell the player who
    // manages their empire vs the NPC world Living World tracks.
    AssertContains("public bool IsEmpireActive", component);
    AssertContains("Matathias.Empire", component);

    var mainTab = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "MainTabWindow_LivingWorld.cs"));
    AssertContains("IsEmpireActive", mainTab);
    AssertContains("LW_EmpireActiveNote", mainTab);

    var en = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ru = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));
    AssertContains("<LW_EmpireActiveNote>", en);
    AssertContains("<LW_EmpireActiveNote>", ru);
}

static void TestRimWorldWatchersSection()
{
    var mainTab = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "MainTabWindow_LivingWorld.cs"));
    // A "watching your colony" section lists factions that hold player-targeted raid intel, shown as
    // an interest band + freshness (never exact), with the faction icon.
    AssertContains("LW_WatchersHeader", mainTab);
    AssertContains("cachedWatcherRows", mainTab);
    AssertContains("RaidIntelTargetKind.PlayerColony", mainTab);
    AssertContains("LW_WatcherLine", mainTab);
    AssertContains("IntelBand", mainTab);

    var en = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ru = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));
    AssertContains("<LW_WatchersHeader>", en);
    AssertContains("<LW_WatchersHeader>", ru);
    AssertContains("<LW_WatcherLine>", en);
    AssertContains("<LW_WatcherLine>", ru);
    AssertContains("<LW_IntelBand_Extreme>", en);
    AssertContains("<LW_IntelBand_Extreme>", ru);
}

// P1-R4: lock the whole EN/RU keyed set in parity so a key added to one language (or a key
// referenced by RimWorld code with no translation) fails the build instead of silently rendering
// the raw key to the player. Spot-check tests above cover specific keys; this covers the rest.
static void TestKeyedLanguageParity()
{
    var root = FindRepoRoot();
    var enXml = File.ReadAllText(Path.Combine(root, "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ruXml = File.ReadAllText(Path.Combine(root, "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));

    var en = ExtractKeyedElementNames(enXml);
    var ru = ExtractKeyedElementNames(ruXml);

    var missingInRu = en.Except(ru).OrderBy(k => k, StringComparer.Ordinal).ToList();
    var missingInEn = ru.Except(en).OrderBy(k => k, StringComparer.Ordinal).ToList();
    if (missingInRu.Count > 0 || missingInEn.Count > 0)
    {
        throw new InvalidOperationException(
            "EN/RU keyed translations are out of parity. "
            + $"Missing in Russian: [{string.Join(", ", missingInRu)}]. "
            + $"Missing in English: [{string.Join(", ", missingInEn)}].");
    }

    // Sanity floor: the P1 raid cause-chain keys the milestone slice depends on must exist so this
    // test fails if the whole warning/consequence/watchers block is ever dropped from both files.
    foreach (var key in new[]
    {
        "LW_RaidWarningLetterLabel", "LW_RaidWarningLetterText",
        "LW_RaidWarningSource_Scout", "LW_RaidWarningSource_Trade",
        "LW_RaidWarningSource_Rumor", "LW_RaidWarningSource_Prisoner",
        "LW_RaidWarningSource_Survivor", "LW_RaidWarningSource_Refugee",
        "LW_RaidWarningSource_DirectVisit", "LW_RaidWarningSource_Public",
        "LW_RaidConsequenceLetterLabel", "LW_RaidConsequenceLetterText",
        "LW_WatchersHeader", "LW_WatcherLine",
        "LW_IntelBand_Extreme", "LW_IntelBand_High", "LW_IntelBand_Moderate", "LW_IntelBand_Low",
    })
    {
        if (!en.Contains(key))
        {
            throw new InvalidOperationException($"Expected English keyed translation '<{key}>' to exist.");
        }
    }

    // Every LW_ key referenced by a whole string literal in the RimWorld source must have a
    // translation; otherwise RimWorld renders the raw key. Concatenation prefixes (a literal ending
    // in '_', joined with a computed suffix) are skipped — they are not complete keys.
    var sourceDir = Path.Combine(root, "src", "LivingWorld.RimWorld");
    foreach (var file in Directory.GetFiles(sourceDir, "*.cs"))
    {
        var text = File.ReadAllText(file);
        foreach (var key in ExtractSourceKeyLiterals(text))
        {
            if (!en.Contains(key))
            {
                throw new InvalidOperationException(
                    $"Source '{Path.GetFileName(file)}' references key '{key}' with no keyed translation.");
            }
        }
    }
}

static void TestRimWorldWorldEconomyMainTab()
{
    var mainTab = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "MainTabWindow_LivingWorld.cs"));
    // A World Economy section with capped, cached per-faction material rows shown as bands
    // (coarse, not omniscient exact totals unless debug is on).
    AssertContains("LW_WorldEconomyHeader", mainTab);
    AssertContains("cachedFactionEconomyRows", mainTab);
    AssertContains("LW_FactionEconomyLine", mainTab);
    AssertContains("WealthBand", mainTab);
    // E4b: the economy section reads the priced ledger wealth snapshot, not just a raw material sum.
    AssertContains("GetFactionWealth(factionId)?.TotalWealth", mainTab);

    var en = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ru = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));
    AssertContains("<LW_WorldEconomyHeader>", en);
    AssertContains("<LW_WorldEconomyHeader>", ru);
    AssertContains("<LW_FactionEconomyLine>", en);
    AssertContains("<LW_FactionEconomyLine>", ru);
}

// Task 6 RW-side: the main tab summarizes ongoing NPC-vs-NPC conflicts (WorldConflict) as banded
// rows — factions, status, duration, coarse intensity from cumulative war exhaustion, refugees.
static void TestRimWorldWorldConflictsSection()
{
    var mainTab = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "MainTabWindow_LivingWorld.cs"));
    AssertContains("LW_WorldConflictsHeader", mainTab);
    AssertContains("cachedConflictRows", mainTab);
    AssertContains("LW_WorldConflictLine", mainTab);
    AssertContains("state.Conflicts", mainTab);
    // Resolved conflicts drop off; intensity is a coarse band, not a raw casualty count.
    AssertContains("WorldConflictStatus.Resolved", mainTab);
    AssertContains("ConflictIntensityBand", mainTab);
    AssertContains("ConflictStatusLabel", mainTab);
    // Faction names are resolved to display labels (not raw defNames), matching the war letter.
    AssertContains("ResolveFactionName(conflict.FactionA)", mainTab);
    AssertContains("ResolveFactionName(conflict.FactionB)", mainTab);
    // Slice 2: alliance-offer buttons let the player join a war, forming an alliance via AllianceService.
    AssertContains("cachedAllianceOffers", mainTab);
    AssertContains("AllianceService.FormAlliance", mainTab);
    AssertContains("TryAddAllianceOffer", mainTab);

    var en = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ru = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));
    foreach (var key in new[]
    {
        "LW_WorldConflictsHeader", "LW_WorldConflictLine",
        "LW_ConflictStatus_Active", "LW_ConflictStatus_Truce",
        "LW_ConflictIntensity_Skirmish", "LW_ConflictIntensity_Devastating",
        "LW_ProposeAllianceButton", "LW_AllianceFormed", "LW_AllianceFailed",
    })
    {
        AssertContains($"<{key}>", en);
        AssertContains($"<{key}>", ru);
    }
}

static void TestRimWorldMainTabFactionIcons()
{
    var mainTab = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "MainTabWindow_LivingWorld.cs"));
    // Per-faction strength/economy rows draw the faction's native icon and colour (looked up by
    // the ledger FactionId, which is the RimWorld faction defName) instead of a plain label.
    AssertContains("DrawFactionRow", mainTab);
    AssertContains("FactionIcon", mainTab);
    AssertContains("GUI.DrawTexture", mainTab);
    AssertContains("faction.Color", mainTab);
    // Both per-faction lists route through the icon renderer, and the cached rows carry the
    // FactionId so the icon can be resolved at draw time.
    AssertContains("DrawFactionRow(new Rect(0f, y, viewRect.width, 24f), row.FactionId, row.Text, row.Fill)", mainTab);
    // Cached rows carry the FactionId (icon lookup) and a normalised Fill (comparative data bar).
    AssertContains("List<(string FactionId, string Text, float Fill)> cachedFactionStrengthRows", mainTab);
    AssertContains("List<(string FactionId, string Text, float Fill)> cachedFactionEconomyRows", mainTab);
    // Each per-faction row draws a translucent faction-coloured bar scaled by its share of the
    // strongest/richest faction.
    AssertContains("Mathf.Clamp01(fill)", mainTab);
    AssertContains("rect.width * clampedFill", mainTab);
    AssertContains("BaseContent.WhiteTex", mainTab);
}

static void TestFactionLifecycleSerializationRoundTrip()
{
    var state = new WorldState(4242);
    state.CreateSettlement("empty-camp", "Empty Camp", "Pirates");
    FactionLifecycleService.SimulateCollapses(state, new FactionLifecycleRequest(60_000));

    var restored = WorldStateCodec.Deserialize(WorldStateCodec.Serialize(state));

    var record = restored.GetFactionRecord("Pirates")!;
    AssertEqual(WorldFactionStatus.Collapsed, record.Status);
    AssertEqual(60_000, record.Tick);
    AssertEqual("population collapse", record.Reason);
}

static void TestDrifterSerializationRoundTrip()
{
    var state = new WorldState(4242);
    state.AddDrifterArrivalReservoir(10, "test reservoir");
    DrifterArrivalService.SimulateArrivals(
        state,
        new DrifterArrivalRequest(60_000, TargetWorldPopulation: 3, HardCeiling: 100, MaxArrivalsPerStep: 3));
    AssertEqual(3, state.Drifters.Count);

    var restored = WorldStateCodec.Deserialize(WorldStateCodec.Serialize(state));

    AssertEqual(3, restored.Drifters.Count);
    var original = state.Drifters.OrderBy(drifter => drifter.Id.Value).First();
    var roundTripped = restored.GetDrifter(original.Id)!;
    AssertEqual(original.Name, roundTripped.Name);
    AssertEqual(original.Age, roundTripped.Age);
    AssertEqual(original.Sex, roundTripped.Sex);
    AssertEqual(original.ArrivalTick, roundTripped.ArrivalTick);
    AssertEqual(original.CombatAptitude, roundTripped.CombatAptitude);
    AssertEqual(original.OrganizationAptitude, roundTripped.OrganizationAptitude);
}

static WorldState BuildFactionWithCombatants(int seed, string faction, int adults)
{
    var state = new WorldState(seed);
    var settlement = state.CreateSettlement("camp", "Camp", faction);
    for (var i = 0; i < adults; i++)
    {
        state.CreateCitizen($"Fighter {i + 1}", 25 + i, Sex.Male, "soldier", settlement.Id);
    }

    state.AddResource(settlement.Id, "PackagedSurvivalMeal", adults * 2);
    return state;
}

static void TestVanillaRaidInterceptedWithoutOpportunity()
{
    var state = BuildFactionWithCombatants(1, "Pirate", 4);

    // No trade intel / raid opportunity exists — the raid must still happen from real population.
    var result = VanillaRaidInterceptor.TryIntercept(
        state,
        new VanillaRaidInterceptionRequest("Pirate", 3, "vanilla raid"));

    AssertEqual(VanillaRaidInterceptionAction.Intercepted, result.Action);
    AssertEqual(false, result.ConsumedOpportunity);
    AssertEqual(3, result.ReservedCombatants);
    AssertEqual(3, state.Citizens.Count(citizen => state.GetOwner(citizen.Id) == result.Army!.Id));
}

static void TestVanillaRaidPassesThroughWithoutPopulation()
{
    // Empty ledger: do not cancel the vanilla raid, just leave it alone.
    var empty = new WorldState(1);
    var noData = VanillaRaidInterceptor.TryIntercept(
        empty,
        new VanillaRaidInterceptionRequest("Pirate", 3, "vanilla raid"));
    AssertEqual(VanillaRaidInterceptionAction.PassThrough, noData.Action);
    AssertEqual(null, noData.Army);

    // Faction is tracked elsewhere but has no settlement of its own: still pass-through, not cancel.
    var otherFaction = BuildFactionWithCombatants(2, "Tribe", 3);
    var mismatch = VanillaRaidInterceptor.TryIntercept(
        otherFaction,
        new VanillaRaidInterceptionRequest("Pirate", 3, "vanilla raid"));
    AssertEqual(VanillaRaidInterceptionAction.PassThrough, mismatch.Action);
    AssertEqual(0, otherFaction.Armies.Count);
}

static void TestVanillaRaidConsumesOpportunityWhenPresent()
{
    var state = BuildFactionWithCombatants(3, "Pirate", 4);
    RaidIntelService.RecordTradeIntel(
        state,
        new TradeIntelRequest("Pirate", 6000, 2, "gold and psychite trade"));

    var result = VanillaRaidInterceptor.TryIntercept(
        state,
        new VanillaRaidInterceptionRequest("Pirate", 2, "vanilla raid"));

    AssertEqual(VanillaRaidInterceptionAction.Intercepted, result.Action);
    AssertEqual(true, result.ConsumedOpportunity);
    AssertEqual(0, state.RaidOpportunities.Count(opportunity => opportunity.Status == RaidOpportunityStatus.Active));
}

static void TestVanillaRaidInterceptionFreesPriorOrphans()
{
    var state = BuildFactionWithCombatants(4, "Pirate", 4);

    // A previous raid reserved two combatants but was aborted before any pawn spawned.
    var orphanArmy = RaidPopulationAllocator.ReserveForRaid(
        state,
        new RaidPopulationAllocationRequest("Pirate", "aborted raid", 2)).Army!;

    var result = VanillaRaidInterceptor.TryIntercept(
        state,
        new VanillaRaidInterceptionRequest("Pirate", 2, "new vanilla raid"));

    AssertEqual(VanillaRaidInterceptionAction.Intercepted, result.Action);
    // The stranded reservists were handed back before the new raid drew its own.
    AssertEqual(0, state.Citizens.Count(citizen => state.GetOwner(citizen.Id) == orphanArmy.Id));
    AssertEqual(2, state.Citizens.Count(citizen => state.GetOwner(citizen.Id) == result.Army!.Id));
}

static void TestWorldStateSerializationRoundTrip()
{
    var state = new WorldState(98765);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "pirates");
    for (var i = 0; i < 5; i++)
    {
        state.CreateCitizen($"Raider {i + 1}", 20 + i, Sex.Male, "soldier", settlement.Id);
    }

    state.AddResource(settlement.Id, "PackagedSurvivalMeal", 40);
    state.AddResource(settlement.Id, "Steel", 100);

    var raid = RaidPlanner.PlanRaid(
        state,
        new RaidPlanRequest(settlement.Id, "test raid", 3, 12, 30));
    RaidPawnBindingService.BindRaidPawns(state, raid.Army!.Id, new[] { 101, 102, 103 });
    RaidPawnBindingService.MarkPawnReturned(state, 101, "roundtrip return");
    RaidPawnBindingService.MarkPawnReturned(state, 102, "roundtrip return");
    RaidPawnBindingService.MarkPawnDead(state, 103, "roundtrip casualty");
    RaidOutcomeService.TryRecordResolvedRaidOutcome(state, raid.Army.Id);
    RaidIntelService.RecordTradeIntel(
        state,
        new TradeIntelRequest("pirates", 1500, 1, "serialization trade intel"));

    var payload = WorldStateCodec.Serialize(state);
    var roundTripped = WorldStateCodec.Deserialize(payload);

    AssertEqual(98765, roundTripped.WorldSeed);
    AssertEqual(1, roundTripped.Settlements.Count);
    AssertEqual(5, roundTripped.Citizens.Count);
    AssertEqual(1, roundTripped.Armies.Count);
    AssertEqual(state.IntelReports.Count, roundTripped.IntelReports.Count);
    AssertEqual(state.RaidOpportunities.Count, roundTripped.RaidOpportunities.Count);
    AssertEqual(state.RaidPawnLinks.Count, roundTripped.RaidPawnLinks.Count);
    AssertEqual(state.RaidOutcomes.Count, roundTripped.RaidOutcomes.Count);
    AssertEqual(state.Events.Count, roundTripped.Events.Count);
    AssertEqual(4, roundTripped.GetSettlementPopulation(settlement.Id).Total);
    AssertEqual(1, roundTripped.Citizens.Count(citizen => roundTripped.GetOwner(citizen.Id) == raid.Army!.Id));
    AssertEqual(28, roundTripped.GetOwnedResourceQuantity(settlement.Id, "PackagedSurvivalMeal"));
    AssertEqual(70, roundTripped.GetOwnedResourceQuantity(settlement.Id, "Steel"));
    AssertEqual(12, roundTripped.GetOwnedResourceQuantity(raid.Army!.Id, "PackagedSurvivalMeal"));
    AssertEqual(30, roundTripped.GetOwnedResourceQuantity(raid.Army.Id, "Steel"));

    var nextCitizen = roundTripped.CreateCitizen("Late recruit", 33, Sex.Female, "soldier", settlement.Id);
    var nextArmy = roundTripped.CreateArmy("second raid", "pirates", settlement.Id);

    AssertEqual("Citizen:6", nextCitizen.Id.ToString());
    AssertEqual("Army:2", nextArmy.Id.ToString());
}

static void TestWorldStateSerializesCitizensCompactly()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "Pirate");
    for (var i = 0; i < 25; i++)
    {
        state.CreateCitizen($"Raider {i + 1}", 20 + i, i % 2 == 0 ? Sex.Male : Sex.Female, "soldier", settlement.Id);
    }

    var payload = WorldStateCodec.Serialize(state);
    var restored = WorldStateCodec.Deserialize(payload);

    AssertContains("<Citizens format=\"compact-v2\">", payload);
    AssertDoesNotContain("<Citizen ", payload);
    AssertEqual(25, restored.Citizens.Count);
    AssertEqual(state.GetSettlementPopulation(settlement.Id), restored.GetSettlementPopulation(settlement.Id));
}

static void TestWorldStateSerializesOwnershipAndEventsCompactly()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "Pirate");
    for (var i = 0; i < 10; i++)
    {
        state.CreateCitizen($"Raider {i + 1}", 20 + i, Sex.Male, "soldier", settlement.Id);
    }

    var payload = WorldStateCodec.Serialize(state);
    var restored = WorldStateCodec.Deserialize(payload);

    AssertContains("<Ownership format=\"compact-v2\">", payload);
    AssertContains("<Events format=\"compact-v2\">", payload);
    AssertDoesNotContain("<Owner ", payload);
    AssertDoesNotContain("<Event ", payload);
    AssertEqual(state.Citizens.Count, restored.Citizens.Count);
    AssertEqual(state.Events.Count, restored.Events.Count);
    AssertEqual(state.GetOwner(state.Citizens.First().Id), restored.GetOwner(state.Citizens.First().Id));
}

static void TestWorldStateLoadsLegacyCitizenElements()
{
    var payload =
        "<LivingWorldState version=\"1\" worldSeed=\"123\" currentTick=\"0\">" +
        "<Settlements><Settlement kind=\"Settlement\" id=\"1\" slug=\"legacy\" name=\"Legacy\" factionId=\"Pirate\" /></Settlements>" +
        "<Citizens><Citizen kind=\"Citizen\" id=\"1\" name=\"Legacy Raider\" age=\"31\" sex=\"Female\" profession=\"soldier\" settlementKind=\"Settlement\" settlementId=\"1\" status=\"Alive\" /></Citizens>" +
        "<Armies />" +
        "<Ownership><Owner assetKind=\"Citizen\" assetId=\"1\" ownerKind=\"Settlement\" ownerId=\"1\" /></Ownership>" +
        "<Resources />" +
        "<Events />" +
        "</LivingWorldState>";

    var restored = WorldStateCodec.Deserialize(payload);
    var citizen = restored.Citizens.Single();

    AssertEqual("Legacy Raider", citizen.Name);
    AssertEqual(Sex.Female, citizen.Sex);
    AssertEqual(new SettlementPopulation(1, 0, 1, 0), restored.GetSettlementPopulation(EntityId.Create(EntityKind.Settlement, 1)));
}

static void TestRimWorldSourceModMetadata()
{
    var aboutPath = Path.Combine(FindRepoRoot(), "mod", "About", "About.xml");

    AssertFileExists(aboutPath);

    var aboutXml = File.ReadAllText(aboutPath);

    AssertContains("<packageId>nakhmedov.livingworld</packageId>", aboutXml);
    AssertContains("<name>Living World</name>", aboutXml);
    AssertContains("<li>1.6</li>", aboutXml);
}

static void TestRimWorldLoadFolders()
{
    var loadFoldersPath = Path.Combine(FindRepoRoot(), "mod", "LoadFolders.xml");

    AssertFileExists(loadFoldersPath);

    var loadFoldersXml = File.ReadAllText(loadFoldersPath);

    AssertContains("<v1.6>", loadFoldersXml);
    AssertContains("<li>/</li>", loadFoldersXml);
    AssertContains("<li>1.6</li>", loadFoldersXml);
}

static void TestRimWorldLoaderProject()
{
    var projectPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorld.RimWorld.csproj");

    AssertFileExists(projectPath);

    var projectXml = File.ReadAllText(projectPath);

    AssertContains("<TargetFramework>net472</TargetFramework>", projectXml);
    AssertContains("<Reference Include=\"Assembly-CSharp\">", projectXml);
    AssertContains("<Reference Include=\"UnityEngine.CoreModule\">", projectXml);
    AssertContains("<Reference Include=\"UnityEngine.IMGUIModule\">", projectXml);
    AssertContains("<Reference Include=\"UnityEngine.TextRenderingModule\">", projectXml);
}

static void TestRimWorldModEntrypoint()
{
    var entrypointPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldMod.cs");

    AssertFileExists(entrypointPath);

    var source = File.ReadAllText(entrypointPath);

    AssertContains("sealed class LivingWorldMod : Mod", source);
    AssertContains("Living World", source);
    AssertContains("[LivingWorld] Loaded", source);
}

static void TestRimWorldInstallScript()
{
    var scriptPath = Path.Combine(FindRepoRoot(), "tools", "install-rimworld-mod.ps1");

    AssertFileExists(scriptPath);

    var source = File.ReadAllText(scriptPath);

    AssertContains("C:\\Games\\RimWorld\\Mods\\LivingWorld", source);
    AssertContains("dotnet build", source);
    AssertContains("mod", source);
    AssertContains("1.6\\Assemblies", source);
    AssertContains("LivingWorld.RimWorld.dll", source);
    AssertContains("LivingWorld.Core.dll", source);
}

static void TestCoreTargetsRimWorldRuntime()
{
    var projectPath = Path.Combine(FindRepoRoot(), "src", "LivingWorld.Core", "LivingWorld.Core.csproj");

    AssertFileExists(projectPath);

    var projectXml = File.ReadAllText(projectPath);

    AssertContains("<TargetFrameworks>net8.0;netstandard2.0</TargetFrameworks>", projectXml);
}

static void TestRimWorldLoaderReferencesCore()
{
    var projectPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorld.RimWorld.csproj");

    var projectXml = File.ReadAllText(projectPath);

    AssertContains("..\\LivingWorld.Core\\LivingWorld.Core.csproj", projectXml);
}

static void TestRimWorldWorldComponent()
{
    var componentPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldWorldComponent.cs");

    AssertFileExists(componentPath);

    var source = File.ReadAllText(componentPath);

    AssertContains("sealed class LivingWorldWorldComponent : WorldComponent", source);
    AssertContains("WorldState", source);
    AssertContains("BootstrapFromRimWorldSettlements", source);
    AssertContains("WorldComponentTick", source);
    AssertContains("SettlementDailySimulationService.SimulateDay", source);
    AssertContains("DemographyService.SimulateDay", source);
    AssertContains("DemographySimulationRequest", source);
    AssertContains("MigrationService.SimulateDay", source);
    AssertContains("MigrationSimulationRequest", source);
    AssertContains("DrifterArrivalService.SimulateArrivals", source);
    AssertContains("AddDrifterArrivalReservoir", source);
    AssertContains("DrifterFoundingService.SimulateFounding", source);
    AssertContains("DrifterAssimilationService.SimulateAssimilation", source);
    // Faction collapse now runs through the lifecycle driver (ResolveFactionCollapses), which also
    // turns collapsed settlements into ruins and relocates starving ones.
    AssertContains("SettlementLifecycleDriver.SimulateDay", source);
    AssertContains("public bool WantsDrifterArrival", source);
    AssertContains("PlayerKnowledgeService.RecordPublicSettlementInfo", source);
    AssertContains("lastSimulatedDay", source);
    AssertContains("GetSummary", source);
    AssertContains("IntelReports.Count", source);
    AssertContains("RaidOpportunities.Count", source);
    AssertContains("[LivingWorld] Ledger initialized", source);
}

static void TestRimWorldMainTab()
{
    var windowPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "MainTabWindow_LivingWorld.cs");

    AssertFileExists(windowPath);

    var source = File.ReadAllText(windowPath);

    AssertContains("sealed class MainTabWindow_LivingWorld : MainTabWindow", source);
    AssertContains("LivingWorldWorldComponent.Instance", source);
    AssertContains("LW_SettlementsHeader", source);
    AssertContains("LW_RaidHookStatus", source);
    AssertDoesNotContain("LW_PlanTestRaidButton", source);
    AssertDoesNotContain("PlanTestRaid", source);
    AssertContains("LW_ArmiesHeader", source);
    AssertContains("deadCitizens", source);
    AssertDoesNotContain("foodDays.Named", source);
    AssertDoesNotContain("migrationPressure.Named", source);
    AssertDoesNotContain("GetSettlementMigrationStatus(settlement.Id", source);
    AssertContains("GetKnownSettlementInfo", source);
    AssertContains("KnownSettlementInfo", source);
    AssertContains("LW_KnowledgeUnknown", source);
    AssertContains("ExactValuesVisible", source);
    AssertContains("LW_ProductionHiddenLine", source);
    AssertContains("LW_RaidOutcomesHeader", source);
    AssertContains("LW_RaidOutcomeLine", source);
    AssertContains("LW_FactionCollapsesHeader", source);
    AssertContains("LW_FactionCollapseLine", source);
    AssertContains("LW_DriftersHeader", source);
    AssertContains("LW_DrifterLine", source);
    AssertContains("LW_KnownIntelFreshnessHeader", source);
    AssertContains("LW_KnownIntelFreshnessLine", source);
    AssertContains("FactionRecords", source);
    AssertContains("Drifters", source);
    AssertContains("KnownSettlementInfos", source);
    AssertContains("LW_EventsHeader", source);
}

static void TestRimWorldMainTabLimitsRenderingWork()
{
    var windowPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "MainTabWindow_LivingWorld.cs");
    var englishPath = Path.Combine(FindRepoRoot(), "mod", "Languages", "English", "Keyed", "LivingWorld.xml");
    var russianPath = Path.Combine(FindRepoRoot(), "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml");

    var source = File.ReadAllText(windowPath);
    var englishXml = File.ReadAllText(englishPath);
    var russianXml = File.ReadAllText(russianPath);

    AssertContains("MaxSettlementRows", source);
    AssertContains("MaxArmyRows", source);
    AssertContains("MaxEventRows", source);
    AssertContains("MaxOutcomeRows", source);
    AssertContains("MaxFactionCollapseRows", source);
    AssertContains("MaxDrifterRows", source);
    AssertContains("MaxKnowledgeRows", source);
    AssertContains("cachedSettlementRows", source);
    AssertContains("cachedOutcomeRows", source);
    AssertContains("cachedFactionCollapseRows", source);
    AssertContains("cachedDrifterRows", source);
    AssertContains("cachedKnownIntelRows", source);
    AssertContains("RefreshCachedRows", source);
    AssertContains(".Take(MaxSettlementRows)", source);
    AssertContains(".Take(MaxArmyRows)", source);
    AssertContains(".Take(MaxEventRows)", source);
    AssertContains(".Take(MaxOutcomeRows)", source);
    AssertContains(".Take(MaxFactionCollapseRows)", source);
    AssertContains(".Take(MaxDrifterRows)", source);
    AssertContains(".Take(MaxKnowledgeRows)", source);
    AssertDoesNotContain("state.Settlements.Count * 72f", source);
    AssertContains("<LW_ListLimited>", englishXml);
    AssertContains("<LW_ListLimited>", russianXml);
    AssertContains("<LW_RaidOutcomesHeader>", englishXml);
    AssertContains("<LW_RaidOutcomesHeader>", russianXml);
    AssertContains("<LW_RaidOutcomeLine>", englishXml);
    AssertContains("<LW_RaidOutcomeLine>", russianXml);
    AssertContains("Recent raids", englishXml);
    AssertContains("Последние рейды", russianXml);
    AssertContains("<LW_FactionCollapsesHeader>", englishXml);
    AssertContains("<LW_FactionCollapsesHeader>", russianXml);
    AssertContains("<LW_FactionCollapseLine>", englishXml);
    AssertContains("<LW_FactionCollapseLine>", russianXml);
    AssertContains("<LW_DriftersHeader>", englishXml);
    AssertContains("<LW_DriftersHeader>", russianXml);
    AssertContains("<LW_DrifterLine>", englishXml);
    AssertContains("<LW_DrifterLine>", russianXml);
    AssertContains("<LW_KnownIntelFreshnessHeader>", englishXml);
    AssertContains("<LW_KnownIntelFreshnessHeader>", russianXml);
    AssertContains("<LW_KnownIntelFreshnessLine>", englishXml);
    AssertContains("<LW_KnownIntelFreshnessLine>", russianXml);
    AssertDoesNotContain("Food days {foodDays}", englishXml);
    AssertDoesNotContain("Дней еды {foodDays}", russianXml);
    AssertContains("<LW_FoodStatusOk>", englishXml);
    AssertContains("<LW_FoodStatusShortage>", russianXml);
    AssertDoesNotContain("Migration pressure {migrationPressure}", englishXml);
    AssertDoesNotContain("Давление миграции {migrationPressure}", russianXml);
    AssertContains("<LW_MigrationReasonStarvation>", englishXml);
    AssertContains("<LW_MigrationReasonStarvation>", russianXml);
    AssertContains("<LW_KnowledgeUnknown>", englishXml);
    AssertContains("<LW_KnowledgeUnknown>", russianXml);
    AssertContains("<LW_KnowledgeLine>", englishXml);
    AssertContains("<LW_KnowledgeLine>", russianXml);
}

static void TestRimWorldSettlementInspectPatch()
{
    var patchPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldSettlementInspectPatch.cs");
    var englishPath = Path.Combine(FindRepoRoot(), "mod", "Languages", "English", "Keyed", "LivingWorld.xml");
    var russianPath = Path.Combine(FindRepoRoot(), "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml");

    AssertFileExists(patchPath);

    var source = File.ReadAllText(patchPath);
    var englishXml = File.ReadAllText(englishPath);
    var russianXml = File.ReadAllText(russianPath);

    AssertContains("[HarmonyPatch(typeof(Settlement), \"GetInspectString\")]", source);
    AssertContains("LW_InspectPopulationLine", source);
    AssertContains("LW_InspectPopulationMissingLine", source);
    AssertContains("GetKnownSettlementInfo", source);
    AssertContains("LW_KnowledgeLine", source);
    AssertContains("CountVisibleSettlementPawns", source);
    AssertContains("LW_MapVisiblePawnsLine", source);
    AssertContains("LW_ProductionLine", source);
    AssertContains("LW_ProductionHiddenLine", source);
    AssertContains("RecordDirectVisitSettlementInfo", source);
    AssertContains("ExactValuesVisible", source);
    AssertContains("GetSettlementProductionStatus", source);
    AssertContains("AllPawnsSpawned", source);
    AssertContains("RaceProps?.Humanlike", source);
    AssertDoesNotContain("GetSettlementFoodStatus", source);
    AssertDoesNotContain("GetSettlementMigrationStatus", source);
    AssertDoesNotContain("GetSettlementPopulation", source);
    AssertContains("FindSettlementForWorldObject", source);
    AssertContains("candidate.Name", source);
    AssertContains("$\":{worldObject.Tile}:\"", source);
    AssertContains("$\"{line}\\n{__result}\"", source);
    AssertDoesNotContain("PackagedSurvivalMeal", source);
    AssertContains("<LW_InspectPopulationLine>", englishXml);
    AssertContains("<LW_InspectPopulationLine>", russianXml);
    AssertContains("<LW_ProductionLine>", englishXml);
    AssertContains("<LW_ProductionLine>", russianXml);
    AssertContains("<LW_ProductionHiddenLine>", englishXml);
    AssertContains("<LW_ProductionHiddenLine>", russianXml);
    AssertContains("<LW_MapVisiblePawnsLine>", englishXml);
    AssertContains("<LW_MapVisiblePawnsLine>", russianXml);
    AssertDoesNotContain("foodDays", englishXml);
    AssertDoesNotContain("foodStatus", russianXml);
    AssertDoesNotContain("migrationPressure", englishXml);
    AssertDoesNotContain("refugees", russianXml);
    AssertContains("confidence", englishXml);
    AssertContains("source", russianXml);
    AssertContains("<LW_InspectPopulationMissingLine>", englishXml);
    AssertContains("<LW_InspectPopulationMissingLine>", russianXml);

    // UI-5: a threat header appears when a world-war army is marching on the settlement (public
    // info, consistent with the on-map warband marker). It reads only army movements, never the
    // fog-gated ledger population/food, so it cannot leak unscouted state.
    AssertContains("BuildThreatLine", source);
    AssertContains("state.ArmyMovements", source);
    AssertContains("ArmyMovementStatus.Traveling", source);
    AssertContains("LW_InspectThreatLine", source);
    AssertContains("<LW_InspectThreatLine>", englishXml);
    AssertContains("<LW_InspectThreatLine>", russianXml);
}

// Task 4 RW-side: the inspect panel surfaces facilities + an active build/repair project, gated by
// the same fog-of-war rule as production (exact only when the player has directly-known intel,
// otherwise a coarse development band and a generic project note).
static void TestRimWorldSettlementFacilitiesInspection()
{
    var root = FindRepoRoot();
    var source = File.ReadAllText(Path.Combine(root, "src", "LivingWorld.RimWorld", "LivingWorldSettlementInspectPatch.cs"));

    // Reads the Core facility/project ledger.
    AssertContains("state.GetSettlementFacilities(settlementId)", source);
    AssertContains("SettlementProjectStatus.Active", source);
    // Gating: exact detail only behind ExactValuesVisible, coarse band otherwise.
    AssertContains("known.ExactValuesVisible", source);
    AssertContains("LW_InspectFacilitiesExactLine", source);
    AssertContains("LW_InspectFacilitiesBandLine", source);
    AssertContains("FacilityDevelopmentBand", source);
    // Hidden entirely for unknown settlements, like the production line.
    AssertContains("if (known == null)", source);
    // Project note distinguishes build vs repair when exact, generic when coarse.
    AssertContains("LW_InspectFacilityProjectBuildExact", source);
    AssertContains("LW_InspectFacilityProjectRepairExact", source);
    AssertContains("LW_InspectFacilityProjectCoarseLine", source);

    var en = File.ReadAllText(Path.Combine(root, "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ru = File.ReadAllText(Path.Combine(root, "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));
    foreach (var key in new[]
    {
        "LW_InspectFacilitiesExactLine", "LW_InspectFacilitiesBandLine", "LW_InspectFacilityItem",
        "LW_InspectFacilityProjectBuildExact", "LW_InspectFacilityProjectCoarseLine",
        "LW_FacilityDevBand_Basic", "LW_FacilityDevBand_Advanced",
        "LW_FacilityKind_Farm", "LW_FacilityKind_Workshop", "LW_FacilityKind_Storage",
    })
    {
        AssertContains($"<{key}>", en);
        AssertContains($"<{key}>", ru);
    }
}

// Task 7 RW visibility: the inspect panel surfaces the settlement's animal cohorts, fog-of-war gated
// (exact herd sizes only when directly known, else a coarse abundance band).
static void TestRimWorldSettlementAnimalsInspection()
{
    var root = FindRepoRoot();
    var source = File.ReadAllText(Path.Combine(root, "src", "LivingWorld.RimWorld", "LivingWorldSettlementInspectPatch.cs"));

    AssertContains("BuildAnimalsLine", source);
    AssertContains("state.AnimalCohorts", source);
    AssertContains("known.ExactValuesVisible", source);
    AssertContains("LW_InspectAnimalsExactLine", source);
    AssertContains("LW_InspectAnimalsBandLine", source);
    AssertContains("AnimalAbundanceBand", source);

    var en = File.ReadAllText(Path.Combine(root, "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ru = File.ReadAllText(Path.Combine(root, "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));
    foreach (var key in new[]
    {
        "LW_InspectAnimalsExactLine", "LW_InspectAnimalItem", "LW_InspectAnimalsBandLine",
        "LW_AnimalBand_Sparse", "LW_AnimalBand_Teeming",
    })
    {
        AssertContains($"<{key}>", en);
        AssertContains($"<{key}>", ru);
    }
}

static void TestRimWorldRaidIncidentPatch()
{
    var patchPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldRaidIncidentPatch.cs");

    AssertFileExists(patchPath);

    var source = File.ReadAllText(patchPath);

    AssertContains("[HarmonyPatch(typeof(IncidentWorker_RaidEnemy), \"TryResolveRaidFaction\")]", source);
    AssertDoesNotContain("\"TryGenerateRaidInfo\"", source);
    AssertContains("public static void Postfix(IncidentParms parms, ref bool __result)", source);
    AssertDoesNotContain("public static bool Prefix", source);
    AssertRimWorldMethodExists("RimWorld.IncidentWorker_RaidEnemy", "TryResolveRaidFaction");
    AssertContains("VanillaRaidInterceptor.TryIntercept", source);
    AssertContains("ref bool __result", source);
    // Living World must never cancel a vanilla raid: it only intercepts or steps aside.
    AssertDoesNotContain("__result = false", source);
    AssertContains("parms.faction", source);
    AssertContains("parms.points", source);
    AssertContains("PointsPerCombatant", source);
}

static void TestRimWorldRaidPawnGenerationPatch()
{
    var patchPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldRaidPawnGenerationPatch.cs");

    AssertFileExists(patchPath);

    var source = File.ReadAllText(patchPath);

    AssertContains("[HarmonyPatch(typeof(IncidentWorker_Raid), \"TryGenerateRaidInfo\")]", source);
    AssertRimWorldMethodExists("RimWorld.IncidentWorker_Raid", "TryGenerateRaidInfo");
    AssertContains("List<Pawn>", source);
    AssertContains("RaidPawnBindingService.BindRaidPawns", source);
    AssertContains("RaidReconciliationService.ReleaseUndeployedReserves", source);
    AssertContains("TryGetReservation", source);
}

static void TestRimWorldPawnKillPatch()
{
    var patchPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldPawnKillPatch.cs");

    AssertFileExists(patchPath);

    var source = File.ReadAllText(patchPath);

    AssertContains("[HarmonyPatch(typeof(Pawn), \"Kill\")]", source);
    AssertRimWorldMethodExists("Verse.Pawn", "Kill");
    AssertContains("TryGetLedgerId", source);
    AssertContains("LivingWorldPawnSyncService.Apply", source);
    AssertContains("PawnFateKind.Dead", source);
}

static void TestRimWorldPawnCapturePatch()
{
    var patchPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldPawnCapturePatch.cs");

    AssertFileExists(patchPath);

    var source = File.ReadAllText(patchPath);

    AssertContains("[HarmonyPatch(typeof(Pawn_GuestTracker), \"CapturedBy\")]", source);
    AssertRimWorldMethodExists("RimWorld.Pawn_GuestTracker", "CapturedBy");
    AssertContains("IsPrisoner", source);
    AssertContains("TryGetLedgerId", source);
    AssertContains("LivingWorldPawnSyncService.Apply", source);
    AssertContains("PawnFateKind.Prisoner", source);
}

static void TestRimWorldPawnExitPatch()
{
    var patchPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldPawnExitPatch.cs");

    AssertFileExists(patchPath);

    var source = File.ReadAllText(patchPath);

    AssertContains("[HarmonyPatch(typeof(Pawn), \"ExitMap\")]", source);
    AssertContains("[HarmonyPatch(typeof(Pawn), \"DeSpawn\")]", source);
    AssertRimWorldMethodExists("Verse.Pawn", "ExitMap");
    AssertRimWorldMethodExists("Verse.Pawn", "DeSpawn");
    AssertContains("Dead", source);
    AssertContains("IsPrisoner", source);
    AssertContains("TryGetLedgerId", source);
    AssertContains("LivingWorldPawnSyncService.Apply", source);
    AssertContains("PawnFateKind.Prisoner", source);
    AssertContains("PawnFateKind.Returned", source);
    AssertContains("PawnFateKind.Missing", source);
}

// Task 3 RW-side: neutral arrivals (visitors, trade caravans) are bound to ledger citizens through
// a SettlementVisit lease. Front half only — fate write-back reuses the shared sync service.
static void TestRimWorldSettlementVisitMaterialization()
{
    var root = FindRepoRoot();

    // The Harmony hook: IncidentWorker_NeutralGroup.SpawnPawns is the single generation point for
    // both visitor groups and trade caravans, and it exists in this RimWorld build.
    var patchPath = Path.Combine(root, "src", "LivingWorld.RimWorld", "LivingWorldNeutralGroupBindingPatch.cs");
    AssertFileExists(patchPath);
    var patch = File.ReadAllText(patchPath);
    AssertContains("[HarmonyPatch(typeof(IncidentWorker_NeutralGroup), \"SpawnPawns\")]", patch);
    AssertRimWorldMethodExists("RimWorld.IncidentWorker_NeutralGroup", "SpawnPawns");
    AssertContains("ref List<Pawn> __result", patch);
    AssertContains("LivingWorldVisitorBindingService.BindVisitorPawns", patch);

    // The binding service: leases citizens from the faction's settlement and stamps the identity comp,
    // mirroring the raid binding. Fail-safe on missing settlement/citizens.
    var servicePath = Path.Combine(root, "src", "LivingWorld.RimWorld", "LivingWorldVisitorBindingService.cs");
    AssertFileExists(servicePath);
    var service = File.ReadAllText(servicePath);
    AssertContains("MaterializationLeaseService.CreateLeases", service);
    AssertContains("MaterializationPurpose.SettlementVisit", service);
    AssertContains("MaterializationLeaseService.BindPawn", service);
    AssertContains("identity.SetLedgerId(lease.CitizenId)", service);
    AssertContains("IsInitialWorldSeedingActive", service);

    // The back half is already shared: a SettlementVisit lease resolves through the same pawn-fate
    // sync service the raid path uses, so the exit/kill/capture patches write back visit fates too.
    var syncService = File.ReadAllText(Path.Combine(root, "src", "LivingWorld.Core", "LivingWorldPawnSyncService.cs"));
    AssertContains("state.MaterializationLeases", syncService);
    AssertContains("MaterializationLeaseService.Resolve", syncService);
}

// Task 3: the full settlement-visit lease lifecycle resolves through the shared sync service, so a
// bound visit pawn's fate returns its citizen to the ledger exactly like a raider's.
static void TestSettlementVisitLeaseResolvesThroughPawnSync()
{
    var state = new WorldState(4242);
    var settlement = state.CreateSettlement("home", "Home", "Drifters");
    var citizen = state.CreateCitizen("Visitor", 30, Sex.Male, "farmer", settlement.Id);

    var leaseResult = MaterializationLeaseService.CreateLeases(
        state,
        new MaterializationLeaseRequest(
            settlement.Id,
            settlement.Id,
            MaterializationPurpose.SettlementVisit,
            "visit:Drifters",
            1,
            60_000));
    AssertEqual(MaterializationLeaseStatus.Success, leaseResult.Status);
    var lease = leaseResult.Leases.Single();
    AssertEqual(citizen.Id, lease.CitizenId);

    var bind = MaterializationLeaseService.BindPawn(state, lease.Id, 7777);
    AssertEqual(MaterializationLeaseBindStatus.Success, bind.Status);

    // The exit/kill/capture patches all funnel through Apply(ledgerId, fate); a returning visitor
    // resolves its lease as Returned.
    var sync = LivingWorldPawnSyncService.Apply(
        state,
        new PawnFateSyncRequest(citizen.Id, PawnFateKind.Returned, "visitor left the map"));
    AssertEqual(PawnFateSyncStatus.Success, sync.Status);

    var resolved = state.MaterializationLeases.Single(l => l.Id == lease.Id);
    AssertEqual(MaterializationLeaseLifecycle.Returned, resolved.Lifecycle);
}

// Player war participation Slice 1 (RW): defeating an NPC settlement on its map records the player as
// a third-party intervener through PlayerConflictInterventionService. The hook is a Prefix on the real
// RimWorld defeat method (verified via reflection); it dedups, and never blocks the vanilla destruction.
static void TestRimWorldPlayerAttackRegistersConflict()
{
    var root = FindRepoRoot();
    var patchPath = Path.Combine(root, "src", "LivingWorld.RimWorld", "LivingWorldSettlementDefeatPatch.cs");
    AssertFileExists(patchPath);
    var patch = File.ReadAllText(patchPath);

    AssertContains("[HarmonyPatch(typeof(SettlementDefeatUtility), \"CheckDefeated\")]", patch);
    AssertRimWorldMethodExists("RimWorld.Planet.SettlementDefeatUtility", "CheckDefeated");
    AssertContains("public static void Prefix(Settlement factionBase)", patch);
    // Only records on an actual defeat, resolved to the ledger settlement, and never returns false.
    AssertContains("SettlementDefeatUtility.IsDefeated", patch);
    AssertContains("PlayerConflictInterventionService.RecordSettlementAttack", patch);
    // Dedup guard so a repeated CheckDefeated call never records the same defeat twice.
    AssertContains("RecordedDefeats", patch);
    AssertContains("factionBase.ID", patch);
    // Slice 2: the same defeat credits any player-allied faction at war with the defeated one.
    AssertContains("AllianceService.CreditAlliesOnPlayerAttack", patch);
    AssertDoesNotContain("return false", patch);
}

static void TestRimWorldPawnIdentityService()
{
    var path = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldPawnIdentityService.cs");

    AssertFileExists(path);
    var source = File.ReadAllText(path);

    AssertContains("class LivingWorldPawnIdentityService", source);
    AssertContains("TryGetLedgerId", source);
    AssertContains("GetComp<CompLivingWorldIdentity>", source);
    AssertContains("RaidPawnLinks", source);
    AssertContains("thingIDNumber", source);
}

static void TestRimWorldPawnKillPatchUsesIdentity()
{
    var patchPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldPawnKillPatch.cs");

    var source = File.ReadAllText(patchPath);

    AssertContains("LivingWorldPawnIdentityService", source);
    AssertContains("TryGetLedgerId", source);
    AssertContains("CompLivingWorldIdentity", source);
}

static void TestRimWorldPawnCapturePatchUsesIdentity()
{
    var patchPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldPawnCapturePatch.cs");

    var source = File.ReadAllText(patchPath);

    AssertContains("LivingWorldPawnIdentityService", source);
    AssertContains("TryGetLedgerId", source);
    AssertContains("CompLivingWorldIdentity", source);
}

static void TestRimWorldPawnExitPatchUsesIdentity()
{
    var patchPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldPawnExitPatch.cs");

    var source = File.ReadAllText(patchPath);

    AssertContains("LivingWorldPawnIdentityService", source);
    AssertContains("TryGetLedgerId", source);
    AssertContains("CompLivingWorldIdentity", source);
}

static void TestCorePawnFateSyncService()
{
    var path = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.Core",
        "LivingWorldPawnSyncService.cs");

    AssertFileExists(path);
    var source = File.ReadAllText(path);

    AssertContains("enum PawnFateKind", source);
    AssertContains("record PawnFateSyncRequest", source);
    AssertContains("class LivingWorldPawnSyncService", source);
    AssertContains("Apply", source);
    AssertContains("RaidOutcomeService.TryRecordResolvedRaidOutcome", source);
    AssertContains("RaidPawnLinkStatus.Active", source);
    AssertContains("CitizenId == request.LedgerId", source);
}

static void TestRimWorldInboundPatchesUsePawnSyncService()
{
    var killPatch = File.ReadAllText(Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldPawnKillPatch.cs"));
    var capturePatch = File.ReadAllText(Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldPawnCapturePatch.cs"));
    var exitPatch = File.ReadAllText(Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldPawnExitPatch.cs"));

    AssertContains("LivingWorldPawnSyncService.Apply", killPatch);
    AssertContains("PawnFateKind.Dead", killPatch);
    AssertContains("LivingWorldPawnSyncService.Apply", capturePatch);
    AssertContains("PawnFateKind.Prisoner", capturePatch);
    AssertContains("LivingWorldPawnSyncService.Apply", exitPatch);
    AssertContains("PawnFateKind.Returned", exitPatch);
    AssertContains("PawnFateKind.Missing", exitPatch);
    AssertContains("PawnFateKind.Prisoner", exitPatch);
}

static void TestRimWorldTradeIntelPatch()
{
    var patchPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldTradeIntelPatch.cs");

    AssertFileExists(patchPath);

    var source = File.ReadAllText(patchPath);

    AssertContains("[HarmonyPatch(typeof(Dialog_Trade), \"Close\")]", source);
    AssertContains("TradeSession.trader", source);
    AssertContains("SettlementTradeLedgerService.RecordTrade", source);
    AssertContains("SettlementTradeLedgerRequest", source);
    AssertContains("SettlementTradeDirection", source);
    AssertContains("cachedTradeables", source);
}

static void TestRimWorldMainButtonDef()
{
    var defPath = Path.Combine(
        FindRepoRoot(),
        "mod",
        "Defs",
        "MainButtonDefs",
        "LivingWorld_MainButton.xml");

    AssertFileExists(defPath);

    var defXml = File.ReadAllText(defPath);

    AssertContains("<defName>LivingWorld_Main</defName>", defXml);
    AssertContains("<label>Living World</label>", defXml);
    AssertContains("<tabWindowClass>LivingWorld.RimWorld.MainTabWindow_LivingWorld</tabWindowClass>", defXml);
    // The button carries a globe icon; the referenced texture must ship with the mod.
    AssertContains("<iconPath>UI/LivingWorld_MainButton</iconPath>", defXml);
    AssertFileExists(Path.Combine(
        FindRepoRoot(), "mod", "Textures", "UI", "LivingWorld_MainButton.png"));
}

static void TestRimWorldHarmonyDependency()
{
    var aboutPath = Path.Combine(FindRepoRoot(), "mod", "About", "About.xml");
    var projectPath = Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorld.RimWorld.csproj");
    var modSource = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldMod.cs"));

    var aboutXml = File.ReadAllText(aboutPath);
    var projectXml = File.ReadAllText(projectPath);

    AssertContains("<li>brrainz.harmony</li>", aboutXml);
    AssertContains("<Reference Include=\"0Harmony\">", projectXml);
    AssertContains("new Harmony(\"nakhmedov.livingworld\")", modSource);
    AssertContains("PatchAll", modSource);
}

static void TestRimWorldWorldGenSettingsPatch()
{
    var patchPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldWorldGenSettingsPatch.cs");

    AssertFileExists(patchPath);

    var source = File.ReadAllText(patchPath);

    AssertContains("[HarmonyPatch(typeof(Page_CreateWorldParams), \"DoWindowContents\")]", source);
    // Clear, dedicated button label anchored to the top-right corner so it never overlaps the
    // vanilla planet sliders or bottom navigation.
    AssertContains("LW_WorldGenButton", source);
    AssertContains("rect.xMax - ButtonWidth", source);
    AssertContains("LivingWorldWorldGenSettingsWindow", source);
}

static void TestRimWorldWorldGenSettingsWindow()
{
    var settingsPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldSettings.cs");
    var windowPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldWorldGenSettingsWindow.cs");
    var drawerPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldSettingsDrawer.cs");

    AssertFileExists(settingsPath);
    AssertFileExists(windowPath);
    AssertFileExists(drawerPath);

    var settingsSource = File.ReadAllText(settingsPath);
    var windowSource = File.ReadAllText(windowPath) + File.ReadAllText(drawerPath);

    AssertContains("sealed class LivingWorldSettings : ModSettings", settingsSource);
    AssertContains("baselineHumanSettlementAdults", settingsSource);
    AssertContains("foodPerCitizen", settingsSource);
    AssertContains("steelPerCitizen", settingsSource);
    AssertContains("bootstrapLedgerDuringWorldGeneration", settingsSource);
    AssertContains("sealed class LivingWorldWorldGenSettingsWindow : Window", windowSource);
    AssertContains("doCloseButton = false", windowSource);
    AssertContains("FooterHeight", windowSource);
    AssertContains("\"CloseButton\".Translate()", windowSource);
    // World generation shows only the master toggle plus short guidance - the numeric knobs are
    // deferred to Options - Mod Settings so world creation stays simple and logical.
    AssertContains("DrawWorldGenEssentials", windowSource);
    AssertContains("LW_WorldGenBlurb", windowSource);
    AssertContains("LW_WorldGenAdvancedHint", windowSource);
    // The advanced tuning still lives in the full drawer used by the mod settings page.
    AssertContains("PreferredHeight", windowSource);
    AssertContains("LW_Settings_HumanAdults", windowSource);
    AssertContains("LW_Settings_FoodPerCitizen", windowSource);
    AssertContains("LW_Settings_SteelPerCitizen", windowSource);

    var en = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ru = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));
    AssertContains("<LW_WorldGenButton>", en);
    AssertContains("<LW_WorldGenButton>", ru);
    AssertContains("<LW_WorldGenBlurb>", en);
    AssertContains("<LW_WorldGenBlurb>", ru);
    AssertContains("<LW_WorldGenAdvancedHint>", en);
    AssertContains("<LW_WorldGenAdvancedHint>", ru);
}

static void TestRimWorldEconomyWindow()
{
    var root = FindRepoRoot();

    var windowPath = Path.Combine(root, "src", "LivingWorld.RimWorld", "LivingWorldEconomyWindow.cs");
    AssertFileExists(windowPath);
    var window = File.ReadAllText(windowPath);

    // A columnar table: one row per faction with settlements, population, top tier and wealth.
    AssertContains("class LivingWorldEconomyWindow : Window", window);
    AssertContains("GroupBy(settlement => settlement.FactionId", window);
    AssertContains("GetSettlementPopulation", window);
    AssertContains("SettlementDevelopmentService.GetTier", window);
    // Prefers the Core wealth snapshot, falls back to a live material sum so it is never empty.
    AssertContains("GetFactionWealth", window);
    AssertContains("debugExact ? data.Population.ToString() : PopulationBand(data.Population)", window);
    AssertContains("debugExact ? data.Wealth.ToString() : WealthBand(data.Wealth)", window);
    AssertContains("LW_EconomyCol_Faction", window);
    AssertContains("LW_EconomyCol_Population", window);
    AssertContains("LW_EconomyCol_Wealth", window);
    // It must show economic motion, not only identical starting stockpiles.
    AssertContains("LW_EconomyCol_Output", window);
    AssertContains("LW_EconomyCol_Change", window);
    AssertContains("DailyOutputValue", window);
    AssertContains("DailyWealthChange", window);
    // Reuses the native faction icon + comparative wealth bar treatment.
    AssertContains("FactionIcon", window);
    AssertContains("BaseContent.WhiteTex", window);

    // The main tab opens it via a button.
    var mainTab = File.ReadAllText(Path.Combine(root, "src", "LivingWorld.RimWorld", "MainTabWindow_LivingWorld.cs"));
    AssertContains("new LivingWorldEconomyWindow()", mainTab);
    AssertContains("LW_OpenEconomyWindow", mainTab);

    var en = File.ReadAllText(Path.Combine(root, "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ru = File.ReadAllText(Path.Combine(root, "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));
    foreach (var key in new[] { "LW_EconomyWindowTitle", "LW_EconomyCol_Settlements", "LW_EconomyCol_Tier", "LW_EconomyCol_Output", "LW_EconomyCol_Change", "LW_Tier_City", "LW_OpenEconomyWindow" })
    {
        AssertContains($"<{key}>", en);
        AssertContains($"<{key}>", ru);
    }
}

static void TestRimWorldSettlementObserverWindow()
{
    var root = FindRepoRoot();

    var windowPath = Path.Combine(root, "src", "LivingWorld.RimWorld", "LivingWorldSettlementObserverWindow.cs");
    AssertFileExists(windowPath);
    var window = File.ReadAllText(windowPath);

    AssertContains("class LivingWorldSettlementObserverWindow : Window", window);
    AssertContains("GetSettlementPopulation", window);
    AssertContains("GetSettlementProductionStatus", window);
    AssertContains("GetSettlementFacilities", window);
    AssertContains("GetAnimalCohorts", window);
    AssertContains("AnimalBreedingProjects", window);
    AssertContains("SettlementProjectStatus.Active", window);
    AssertContains("AnimalBreedingProjectStatus.Active", window);
    AssertContains("ResourcesForOwner", window);
    AssertContains("WorldEventKind.SettlementProjectStarted", window);
    AssertContains("WorldEventKind.SettlementFacilityBuilt", window);
    AssertContains("WorldEventKind.SettlementDeveloped", window);
    AssertContains("WorldEventKind.AnimalProductsHarvested", window);
    AssertContains("WorldEventKind.AnimalBreedingProjectStarted", window);
    AssertContains("LW_SettlementObserver_ProjectProgress", window);
    AssertContains("LW_SettlementObserver_AnimalLine", window);
    AssertContains("LW_SettlementObserver_BreedingProgress", window);
    AssertContains("LW_SettlementObserver_NoActiveProject", window);

    var mainTab = File.ReadAllText(Path.Combine(root, "src", "LivingWorld.RimWorld", "MainTabWindow_LivingWorld.cs"));
    AssertContains("new LivingWorldSettlementObserverWindow()", mainTab);
    AssertContains("LW_OpenSettlementObserverWindow", mainTab);

    var en = File.ReadAllText(Path.Combine(root, "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ru = File.ReadAllText(Path.Combine(root, "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));
    foreach (var key in new[]
    {
        "LW_OpenSettlementObserverWindow",
        "LW_SettlementObserverTitle",
        "LW_SettlementObserver_Settlements",
        "LW_SettlementObserver_Overview",
        "LW_SettlementObserver_Projects",
        "LW_SettlementObserver_Facilities",
        "LW_SettlementObserver_Animals",
        "LW_SettlementObserver_NoAnimals",
        "LW_SettlementObserver_AnimalLine",
        "LW_SettlementObserver_Breeding",
        "LW_SettlementObserver_NoBreedingProject",
        "LW_SettlementObserver_BreedingProgress",
        "LW_SettlementObserver_Resources",
        "LW_SettlementObserver_RecentEvents",
        "LW_SettlementObserver_NoActiveProject",
        "LW_SettlementObserver_ProjectProgress"
    })
    {
        AssertContains($"<{key}>", en);
        AssertContains($"<{key}>", ru);
    }
}

static void TestRimWorldWorldArmyMarker()
{
    var root = FindRepoRoot();

    // The def wires our display-only world object with a Rim-War-style icon and dynamic drawing.
    var defPath = Path.Combine(root, "mod", "Defs", "WorldObjectDefs", "LivingWorld_ArmyMarker.xml");
    AssertFileExists(defPath);
    var defXml = File.ReadAllText(defPath);
    AssertContains("<defName>LivingWorld_ArmyMarker</defName>", defXml);
    AssertContains("<worldObjectClass>LivingWorld.RimWorld.WorldObject_LivingWorldArmy</worldObjectClass>", defXml);
    AssertContains("<texture>World/LivingWorld_Warband</texture>", defXml);
    AssertContains("<useDynamicDrawer>true</useDynamicDrawer>", defXml);
    AssertContains("<expandingIcon>true</expandingIcon>", defXml);

    // The icon ships with the mod.
    AssertFileExists(Path.Combine(root, "mod", "Textures", "World", "LivingWorld_Warband.png"));

    // The world object is a pure visualizer: it interpolates between the origin and target tiles
    // from the ledger clock (no pathfinding), tints by faction, and round-trips through save data.
    var markerPath = Path.Combine(root, "src", "LivingWorld.RimWorld", "WorldObject_LivingWorldArmy.cs");
    AssertFileExists(markerPath);
    var marker = File.ReadAllText(markerPath);
    AssertContains("class WorldObject_LivingWorldArmy : WorldObject", marker);
    AssertContains("public override Vector3 DrawPos", marker);
    AssertContains("Find.WorldGrid", marker);
    AssertContains("GetTileCenter", marker);
    AssertContains("Vector3.Slerp", marker);
    AssertContains("MaterialPool.MatFrom", marker);
    AssertContains("WorldOverlayTransparentLit", marker);
    AssertContains("public override void ExposeData()", marker);
    AssertContains("LW_MissionMarkerInspect", marker);
    AssertContains("LW_MissionMarkerStrengthLine", marker);
    AssertContains("LW_MissionMarkerResourceLine", marker);
    AssertContains("LW_MissionMarkerReasonLine", marker);
    AssertContains("combatants.Named(\"combatants\")", marker);
    AssertContains("strength.Named(\"strength\")", marker);
    AssertContains("resourceSummary.Named(\"resources\")", marker);
    AssertContains("reason.Named(\"reason\")", marker);
    AssertContains("Scribe_Values.Look(ref combatants", marker);
    AssertContains("Scribe_Values.Look(ref strength", marker);
    AssertContains("Scribe_Values.Look(ref resourceSummary", marker);
    AssertContains("Scribe_Values.Look(ref reason", marker);
    // The marker's icon is per-instance (chosen by kind), not the fixed def texture.
    AssertContains("texPath: textureName", marker);

    // The world component reconciles markers from the ledger's active travels each day and on
    // load, and derives the tile from the settlement slug (Core has no tile geometry). Both
    // marching warbands and traveling caravans get a marker, each with its own icon.
    var component = File.ReadAllText(Path.Combine(root, "src", "LivingWorld.RimWorld", "LivingWorldWorldComponent.cs"));
    AssertContains("SyncArmyWorldObjects()", component);
    AssertContains("ParseSettlementTile", component);
    AssertContains("WorldObjectMaker.MakeWorldObject", component);
    AssertContains("ArmyMovementStatus.Traveling", component);
    AssertContains("CaravanStatus.Traveling", component);
    AssertContains("World/LivingWorld_Warband", component);
    AssertContains("World/LivingWorld_Trader", component);
    AssertContains("BuildWarbandMarkerDetails", component);
    AssertContains("BuildCaravanMarkerDetails", component);
    AssertContains("BuildMissionMarkerDetails", component);
    AssertContains("ResourceLedgerService.GetResources", component);
    AssertContains("existing.TryGetValue(key", component);
    // Scout and diplomat missions are rendered too, each with its own icon.
    AssertContains("State.Missions", component);
    AssertContains("WorldMissionStatus.Traveling", component);
    AssertContains("World/LivingWorld_Scout", component);
    AssertContains("World/LivingWorld_Diplomat", component);
    // Off when the war is disabled or Rim War is driving factions.
    AssertContains("settings.worldWarEnabled && !RimWarIsActive", component);

    // All action icons ship with the mod as full 256px source textures. RimWorld can
    // downscale them for map/UI use without us maintaining duplicate texture paths.
    foreach (var icon in new[]
    {
        "LivingWorld_Warband.png",
        "LivingWorld_Trader.png",
        "LivingWorld_Scout.png",
        "LivingWorld_Diplomat.png",
        "LivingWorld_Settler.png"
    })
    {
        AssertPngDimensions(Path.Combine(root, "mod", "Textures", "World", icon), 256, 256);
    }

    var en = File.ReadAllText(Path.Combine(root, "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ru = File.ReadAllText(Path.Combine(root, "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));
    AssertContains("<LW_MissionMarkerInspect>", en);
    AssertContains("<LW_MissionMarkerInspect>", ru);
    AssertContains("<LW_MissionMarkerStrengthLine>", en);
    AssertContains("<LW_MissionMarkerStrengthLine>", ru);
    AssertContains("<LW_MissionMarkerResourceLine>", en);
    AssertContains("<LW_MissionMarkerResourceLine>", ru);
    AssertContains("<LW_MissionMarkerReasonLine>", en);
    AssertContains("<LW_MissionMarkerReasonLine>", ru);
    AssertContains("<LW_MissionKind_Trader>", en);
    AssertContains("<LW_MissionKind_Trader>", ru);
}

// Task 5 RW-side: destroyed settlements show a static world-map ruin marker reconciled from the
// ledger's active ruins, reporting the former faction plus coarse salvage/danger bands.
static void TestRimWorldRuinWorldObjectMarker()
{
    var root = FindRepoRoot();

    var defPath = Path.Combine(root, "mod", "Defs", "WorldObjectDefs", "LivingWorld_RuinMarker.xml");
    AssertFileExists(defPath);
    var defXml = File.ReadAllText(defPath);
    AssertContains("<defName>LivingWorld_RuinMarker</defName>", defXml);
    AssertContains("<worldObjectClass>LivingWorld.RimWorld.WorldObject_LivingWorldRuin</worldObjectClass>", defXml);

    var markerPath = Path.Combine(root, "src", "LivingWorld.RimWorld", "WorldObject_LivingWorldRuin.cs");
    AssertFileExists(markerPath);
    var marker = File.ReadAllText(markerPath);
    AssertContains("class WorldObject_LivingWorldRuin : WorldObject", marker);
    AssertContains("public override string GetInspectString()", marker);
    AssertContains("LW_RuinMarkerInspect", marker);
    AssertContains("public override void ExposeData()", marker);
    // Static marker: no travel interpolation, unlike the army marker.
    AssertDoesNotContain("Vector3.Slerp", marker);

    // The component reconciles ruin markers from the ledger each day and on load: a marker per
    // active ruin, keyed and dropped when the ruin is reclaimed/pruned. Reuses the slug->tile parse.
    var component = File.ReadAllText(Path.Combine(root, "src", "LivingWorld.RimWorld", "LivingWorldWorldComponent.cs"));
    AssertContains("SyncRuinWorldObjects()", component);
    AssertContains("State.Ruins", component);
    AssertContains("RuinStatus.Active", component);
    AssertContains("LivingWorld_RuinMarker", component);
    AssertContains("ParseSettlementTile(ruin.Slug)", component);
    AssertContains("RuinSalvageBandLabel", component);
    AssertContains("RuinDangerBandLabel", component);

    var en = File.ReadAllText(Path.Combine(root, "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ru = File.ReadAllText(Path.Combine(root, "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));
    foreach (var key in new[]
    {
        "LW_RuinMarkerInspect",
        "LW_RuinSalvage_None", "LW_RuinSalvage_High",
        "LW_RuinDanger_Low", "LW_RuinDanger_High",
    })
    {
        AssertContains($"<{key}>", en);
        AssertContains($"<{key}>", ru);
    }
}

static void TestRimWorldDrifterFlowSettings()
{
    var path = Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldSettings.cs");
    AssertFileExists(path);
    var source = File.ReadAllText(path);

    AssertContains("public bool drifterFlowEnabled", source);
    AssertContains("public int targetWorldPopulationPerSettlement", source);
    AssertContains("public int drifterHardCeiling", source);
    AssertContains("public int maxDrifterArrivalsPerDay", source);
    AssertContains("public int maxDrifterAssimilationsPerDay", source);
    AssertContains("public int drifterMinFounders", source);
    AssertContains("public int drifterLeaderAptitudeThreshold", source);
    AssertContains("Scribe_Values.Look(ref drifterFlowEnabled", source);
    AssertContains("Scribe_Values.Look(ref targetWorldPopulationPerSettlement", source);
    AssertContains("Scribe_Values.Look(ref drifterHardCeiling", source);
    AssertContains("Scribe_Values.Look(ref maxDrifterArrivalsPerDay", source);
    AssertContains("Scribe_Values.Look(ref maxDrifterAssimilationsPerDay", source);
    AssertContains("Scribe_Values.Look(ref drifterMinFounders", source);
    AssertContains("Scribe_Values.Look(ref drifterLeaderAptitudeThreshold", source);
}

static void TestRimWorldDrifterFlowDrawer()
{
    var drawer = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldSettingsDrawer.cs"));
    AssertContains("drifterFlowEnabled", drawer);
    AssertContains("maxDrifterArrivalsPerDay", drawer);
    AssertContains("drifterHardCeiling", drawer);
    AssertContains("LW_SettingDrifterFlow", drawer);

    var en = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ru = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));
    AssertContains("<LW_SettingDrifterFlow>", en);
    AssertContains("<LW_SettingDrifterFlow>", ru);
    AssertContains("<LW_SettingDrifterArrivals>", en);
    AssertContains("<LW_SettingDrifterArrivals>", ru);
    AssertContains("<LW_SettingDrifterCeiling>", en);
    AssertContains("<LW_SettingDrifterCeiling>", ru);
}

static void TestWorldComponentUsesWorldGenSettings()
{
    var componentPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldWorldComponent.cs");

    var source = File.ReadAllText(componentPath);

    AssertContains("LivingWorldSettings.Instance", source);
    AssertContains("baselineHumanSettlementAdults", source);
    AssertContains("foodPerCitizen", source);
    AssertContains("steelPerCitizen", source);
    AssertContains("bootstrapLedgerDuringWorldGeneration", source);
    AssertContains("RimWorldSettlementProductionProfileFactory.Create", source);
    AssertContains("RecordSettlementProductionProfile", source);
    AssertContains("SettlementProductionService.SimulateDay", source);
    AssertContains("RepairMissingProductionProfilesFromRimWorldSettlements", source);
}

static void TestWorldComponentUsesRimWorldWorldSeed()
{
    var componentPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldWorldComponent.cs");

    var source = File.ReadAllText(componentPath);

    AssertDoesNotContain("const int WorldSeed", source);
    AssertContains("ResolveWorldSeed", source);
    AssertContains("world.info.seedString", source);
    AssertContains("StableSeedFromString", source);
    AssertContains("new WorldState(ResolveWorldSeed", source);
}

static void TestWorldComponentCatchesUpMissedSimulationDays()
{
    var componentPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldWorldComponent.cs");

    var source = File.ReadAllText(componentPath);

    AssertContains("MaxCatchUpSimulationDays", source);
    AssertContains("while (lastSimulatedDay < currentDay", source);
    AssertContains("simulatedDays < MaxCatchUpSimulationDays", source);
    AssertContains("SimulateWorldDay(lastSimulatedDay", source);
}

static void TestWorldComponentUsesInitialWorldSeedingBoundary()
{
    var componentPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldWorldComponent.cs");
    var statePath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.Core",
        "WorldState.cs");

    var componentSource = File.ReadAllText(componentPath);
    var stateSource = File.ReadAllText(statePath);

    AssertContains("RunInitialWorldSeeding", stateSource);
    AssertContains("IsInitialWorldSeedingActive", stateSource);
    AssertContains("RunInitialWorldSeeding", componentSource);
    AssertDoesNotContain("bootstrap citizen/resource import", componentSource);
}

static void TestWorldComponentSuppressesBootstrapEventSpam()
{
    var componentPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldWorldComponent.cs");

    var source = File.ReadAllText(componentPath);

    AssertContains("RunInitialWorldSeeding", source);
    AssertContains("initial world seeding", source);
}

static void TestRimWorldMainTabExplainsEmptyLedger()
{
    var componentPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldWorldComponent.cs");
    var mainTabPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "MainTabWindow_LivingWorld.cs");

    var componentSource = File.ReadAllText(componentPath);
    var mainTabSource = File.ReadAllText(mainTabPath);

    AssertContains("GetDiagnosticSummary", componentSource);
    AssertContains("RetryBootstrapFromRimWorldSettlements", componentSource);
    AssertContains("CreateDebugLedger", componentSource);
    AssertContains("LW_DiagnosticLine", componentSource);
    AssertContains("LW_RetryBootstrapButton", mainTabSource);
    AssertContains("LW_CreateDebugLedgerButton", mainTabSource);
    AssertContains("LW_EmptyLedgerHint", mainTabSource);
}

static void TestRimWorldWorldObjectScanner()
{
    var scannerPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "WorldObjectScanner.cs");

    AssertFileExists(scannerPath);

    var source = File.ReadAllText(scannerPath);

    AssertContains("sealed class WorldObjectScanner", source);
    AssertContains("Find.WorldObjects.AllWorldObjects", source);
    AssertContains("WorldObjectSettlementCandidate", source);
    AssertContains("StableKey", source);
    AssertContains("obj is Settlement", source);
    AssertContains("SafeRead(() => obj.Faction", source);
    // The player's own colony is the active map, not a ledger NPC settlement, so it must never be
    // imported — otherwise the world war could silently target/capture the player (see G1).
    AssertContains("faction.IsPlayer", source);
}

static void TestRimWorldWorldObjectScannerUsesImporterWhitelist()
{
    var scannerPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "WorldObjectScanner.cs");

    var source = File.ReadAllText(scannerPath);

    AssertContains("IWorldObjectImporter", source);
    AssertContains("VanillaSettlementImporter", source);
    AssertContains("CanImport", source);
    AssertContains("ImportCandidate", source);
    AssertContains("foreach (var importer in importers)", source);
    AssertContains("return importer.ImportCandidate(obj, ref scanErrorCount);", source);
    AssertContains("obj is Settlement", source);
    AssertDoesNotContain("return new WorldObjectSettlementCandidate(", source);
}

static void TestRimWorldWorldObjectScannerUsesExplicitSorting()
{
    var scannerPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "WorldObjectScanner.cs");

    var source = File.ReadAllText(scannerPath);

    AssertContains("candidates.Sort", source);
    AssertContains("string.CompareOrdinal", source);
    AssertDoesNotContain(".OrderBy(candidate => candidate.Tile)", source);
}

static void TestRimWorldBootstrapFailureDiagnostics()
{
    var scannerPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "WorldObjectScanner.cs");
    var componentPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldWorldComponent.cs");
    var mainTabPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "MainTabWindow_LivingWorld.cs");
    var englishPath = Path.Combine(FindRepoRoot(), "mod", "Languages", "English", "Keyed", "LivingWorld.xml");
    var russianPath = Path.Combine(FindRepoRoot(), "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml");

    var scannerSource = File.ReadAllText(scannerPath);
    var componentSource = File.ReadAllText(componentPath);
    var mainTabSource = File.ReadAllText(mainTabPath);
    var englishXml = File.ReadAllText(englishPath);
    var russianXml = File.ReadAllText(russianPath);

    AssertContains("ScanErrorCount", scannerSource);
    AssertContains("SafeRead", scannerSource);
    AssertContains("try", componentSource);
    AssertContains("LastBootstrapError", componentSource);
    AssertContains("bootstrap-error", componentSource);
    AssertContains("LW_BootstrapErrorHint", mainTabSource);
    AssertContains("<LW_BootstrapErrorHint>", englishXml);
    AssertContains("<LW_BootstrapErrorHint>", russianXml);
}

static void TestRimWorldDetailedBootstrapDiagnostics()
{
    var componentPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldWorldComponent.cs");
    var englishPath = Path.Combine(FindRepoRoot(), "mod", "Languages", "English", "Keyed", "LivingWorld.xml");
    var russianPath = Path.Combine(FindRepoRoot(), "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml");

    var componentSource = File.ReadAllText(componentPath);
    var englishXml = File.ReadAllText(englishPath);
    var russianXml = File.ReadAllText(russianPath);

    AssertContains("WorldObjectScanner", componentSource);
    AssertContains("TotalWorldObjects", componentSource);
    AssertContains("VanillaSettlementSourceCount", componentSource);
    AssertContains("FactionWorldObjectSourceCount", componentSource);
    AssertContains("ImportableWorldObjectSourceCount", componentSource);
    AssertContains("RejectedWorldObjectTypes", componentSource);
    AssertContains("<LW_DiagnosticLine>", englishXml);
    AssertContains("World objects", englishXml);
    AssertContains("<LW_DiagnosticLine>", russianXml);
    AssertContains("Объектов мира", russianXml);
}

static void TestRimWorldWorldComponentPersistsLedger()
{
    var componentPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldWorldComponent.cs");

    var source = File.ReadAllText(componentPath);

    AssertContains("livingWorld_serializedState", source);
    AssertContains("WorldStateCodec.Serialize", source);
    AssertContains("WorldStateCodec.Deserialize", source);
    AssertContains("Scribe.mode", source);
    AssertContains("LoadSaveMode.Saving", source);
    AssertContains("LoadSaveMode.LoadingVars", source);
}

static void TestRimWorldKeyedTranslations()
{
    var englishPath = Path.Combine(FindRepoRoot(), "mod", "Languages", "English", "Keyed", "LivingWorld.xml");
    var russianPath = Path.Combine(FindRepoRoot(), "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml");
    var russianMainButtonPath = Path.Combine(
        FindRepoRoot(),
        "mod",
        "Languages",
        "Russian",
        "DefInjected",
        "MainButtonDef",
        "LivingWorld_MainButton.xml");

    AssertFileExists(englishPath);
    AssertFileExists(russianPath);
    AssertFileExists(russianMainButtonPath);

    var englishXml = File.ReadAllText(englishPath);
    var russianXml = File.ReadAllText(russianPath);
    var russianMainButtonXml = File.ReadAllText(russianMainButtonPath);

    AssertContains("<LW_MainTitle>Living World</LW_MainTitle>", englishXml);
    AssertContains("<LW_MainTitle>Живой мир</LW_MainTitle>", russianXml);
    AssertContains("Raid opportunities", englishXml);
    AssertContains("Поводы для рейда", russianXml);
    AssertContains("External reserve", englishXml);
    AssertContains("Внешний резерв", russianXml);
    AssertContains("Dead {deadCitizens}", englishXml);
    AssertContains("Погибшие {deadCitizens}", russianXml);
    AssertContains("Returned {returnedCitizens}", englishXml);
    AssertContains("Вернулись {returnedCitizens}", russianXml);
    AssertContains("Captured {prisonerCitizens}", englishXml);
    AssertContains("Пленены {prisonerCitizens}", russianXml);
    AssertContains("<LW_ProductionLine>", englishXml);
    AssertContains("<LW_ProductionLine>", russianXml);
    AssertContains("<LW_ProductionHiddenLine>", englishXml);
    AssertContains("<LW_ProductionHiddenLine>", russianXml);
    AssertContains("давность {ageDays}", russianXml);
    AssertContains("production {production}", englishXml);
    AssertContains("<LW_RaidHookStatus>", englishXml);
    AssertContains("<LW_RaidHookStatus>", russianXml);
    AssertContains("<LW_DiagnosticLine>", englishXml);
    AssertContains("<LW_DiagnosticLine>", russianXml);
    AssertContains("<LW_CreateDebugLedgerButton>Create visible test ledger</LW_CreateDebugLedgerButton>", englishXml);
    AssertContains("<LW_CreateDebugLedgerButton>Создать видимый тестовый ledger</LW_CreateDebugLedgerButton>", russianXml);
    AssertContains("<LivingWorld_Main.label>Живой мир</LivingWorld_Main.label>", russianMainButtonXml);
}

static void TestRimWorldRussianOdysseyRulePackOverride()
{
    var rulePackPath = Path.Combine(
        FindRepoRoot(),
        "mod",
        "Languages",
        "Russian",
        "DefInjected",
        "RulePackDef",
        "Odyssey_Namers_Factions.xml");

    AssertFileExists(rulePackPath);

    var xml = File.ReadAllText(rulePackPath);

    AssertContains("<NamerFactionTradersGuild.rulePack.rulesStrings.0>r_name-&gt;[tradeAdj] [tradeNoun]</NamerFactionTradersGuild.rulePack.rulesStrings.0>", xml);
    AssertContains("tradeAdj-&gt;", xml);
    AssertContains("tradeNoun-&gt;", xml);
    AssertDoesNotContain("[tradeAdj_fem] [tradeNoun_fem]", xml);
}

static void TestRimWorldIdentityComp()
{
    var path = Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "CompLivingWorldIdentity.cs");
    AssertFileExists(path);
    var source = File.ReadAllText(path);

    AssertContains("class CompLivingWorldIdentity : ThingComp", source);
    AssertContains("class CompProperties_LivingWorldIdentity : CompProperties", source);
    AssertContains("public EntityId LedgerId", source);
    AssertContains("public override void PostExposeData()", source);
    AssertContains("Scribe_Values.Look", source);
    AssertContains("ThingDef declares", source);
    AssertDoesNotContain("will not be", source);
    AssertRimWorldMethodExists("Verse.ThingComp", "PostExposeData");
}

static void TestRimWorldIdentityCompThingDefPatch()
{
    var path = Path.Combine(
        FindRepoRoot(),
        "mod",
        "Patches",
        "LivingWorld_PawnIdentity.xml");
    AssertFileExists(path);
    var xml = File.ReadAllText(path);

    AssertContains("<Operation Class=\"PatchOperationAdd\">", xml);
    AssertContains("<xpath>/Defs/ThingDef[defName=\"Human\"]/comps</xpath>", xml);
    AssertContains("LivingWorld.RimWorld.CompProperties_LivingWorldIdentity", xml);
}

static void TestRimWorldUiUsesTranslations()
{
    var rimWorldSources = new[]
    {
        Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldMod.cs"),
        Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldSettingsDrawer.cs"),
        Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldWorldGenSettingsPatch.cs"),
        Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldWorldGenSettingsWindow.cs"),
        Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldWorldComponent.cs"),
        Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "MainTabWindow_LivingWorld.cs"),
        Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldSettlementInspectPatch.cs"),
    };

    var combinedSource = string.Join(Environment.NewLine, rimWorldSources.Select(File.ReadAllText));

    AssertContains("\"LW_MainTitle\".Translate()", combinedSource);
    AssertContains("\"LW_RaidHookStatus\".Translate()", combinedSource);
    AssertContains("\"LW_ProductionLine\".Translate(", combinedSource);
    AssertContains("\"LW_ProductionHiddenLine\".Translate()", combinedSource);
    AssertContains("\"LW_DiagnosticLine\".Translate(", combinedSource);
    AssertContains("\"LW_Settings_BootstrapLedger\".Translate()", combinedSource);
    AssertContains("\"LW_WorldGenTitle\".Translate()", combinedSource);
}

static void TestRimWorldDrifterArrivalIncidentDef()
{
    var path = Path.Combine(FindRepoRoot(), "mod", "Defs", "IncidentDefs", "LivingWorld_DrifterArrival.xml");
    AssertFileExists(path);
    var xml = File.ReadAllText(path);

    AssertContains("<defName>LivingWorld_DrifterArrival</defName>", xml);
    // Must be a real vanilla IncidentCategoryDef or RimWorld logs a red cross-reference error at
    // load (WandererJoin, our closest analog, uses Misc). "AllyArrival" does not exist in 1.6.
    AssertContains("<category>Misc</category>", xml);
    AssertDoesNotContain("AllyArrival", xml);
    AssertContains("<workerClass>LivingWorld.RimWorld.IncidentWorker_LivingWorldDrifterArrival</workerClass>", xml);
    AssertContains("<targetTags>", xml);
    AssertContains("<li>Map_PlayerHome</li>", xml);

    var en = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ru = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));
    AssertContains("<LW_DrifterArrivalLetterLabel>", en);
    AssertContains("<LW_DrifterArrivalLetterLabel>", ru);
}

static void TestRimWorldDrifterArrivalWorker()
{
    var path = Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "IncidentWorker_LivingWorldDrifterArrival.cs");
    AssertFileExists(path);
    var source = File.ReadAllText(path);

    AssertContains("class IncidentWorker_LivingWorldDrifterArrival : IncidentWorker", source);
    AssertContains("protected override bool CanFireNowSub(IncidentParms parms)", source);
    AssertContains("protected override bool TryExecuteWorker(IncidentParms parms)", source);
    AssertContains("WantsDrifterArrival", source);
    AssertContains("MaterializeDrifter", source);
    AssertContains("MaterializeNewArrival", source);
    AssertContains("CompLivingWorldIdentity", source);
    // fail-open: never throws out, no world mutation on a failed execute
    AssertContains("Instance", source);
    AssertRimWorldMethodExists("RimWorld.IncidentWorker", "TryExecuteWorker");
    AssertRimWorldMethodExists("RimWorld.IncidentWorker", "CanFireNowSub");
}

static void TestDrifterArrivalGateRequiresPooledDrifter()
{
    var path = Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldWorldComponent.cs");
    AssertFileExists(path);
    var source = File.ReadAllText(path);

    AssertContains("State.Drifters.Count > 0", source);
    AssertDoesNotContain("cachedWorldPopulation < cachedTargetPopulation", source);
}

static void TestRimWorldDrifterArrivalWorkerValidatesSpawnCell()
{
    var path = Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "IncidentWorker_LivingWorldDrifterArrival.cs");
    AssertFileExists(path);
    var source = File.ReadAllText(path);

    AssertContains("TryFindRandomEdgeCellWith", source);
    AssertContains("Standable", source);
    AssertContains("return false;", source);
}

static void TestRimWorldIncidentDefRussianLocalization()
{
    var path = Path.Combine(
        FindRepoRoot(),
        "mod",
        "Languages",
        "Russian",
        "DefInjected",
        "IncidentDef",
        "LivingWorld_Incidents.xml");
    AssertFileExists(path);
    var xml = File.ReadAllText(path);

    AssertContains("<LivingWorld_DrifterArrival.label>", xml);
    AssertContains("<LivingWorld_DrifterArrival.letterLabel>", xml);
    AssertContains("<LivingWorld_DrifterArrival.letterText>", xml);
}

static void TestRimWorldFactionRaidIncidentDef()
{
    var path = Path.Combine(FindRepoRoot(), "mod", "Defs", "IncidentDefs", "LivingWorld_FactionRaid.xml");
    AssertFileExists(path);
    var xml = File.ReadAllText(path);

    AssertContains("<defName>LivingWorld_FactionRaid</defName>", xml);
    AssertContains("<category>ThreatBig</category>", xml);
    AssertContains("<workerClass>LivingWorld.RimWorld.IncidentWorker_LivingWorldFactionRaid</workerClass>", xml);
    AssertContains("<li>Map_PlayerHome</li>", xml);
}

static void TestRimWorldFactionRaidWorker()
{
    var path = Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "IncidentWorker_LivingWorldFactionRaid.cs");
    AssertFileExists(path);
    var source = File.ReadAllText(path);

    AssertContains("class IncidentWorker_LivingWorldFactionRaid : IncidentWorker_RaidEnemy", source);
    // Routes through Core's intel-driven prepared expedition (see TestRimWorldRaidRoutesThroughPreparation).
    AssertContains("RaidPreparationService.PrepareRaid", source);
    AssertContains("LivingWorldRaidBindingRuntime.TryAddReservation", source);
    AssertContains("base.TryExecuteWorker(parms)", source);
    AssertContains("RaidReconciliationService.ReleaseUndeployedReserves", source);
    AssertContains("EstimateRequestedCombatants", source);
    AssertContains("humanlikeFaction", source);
    AssertRimWorldMethodExists("RimWorld.IncidentWorker_RaidEnemy", "TryExecuteWorker");
}

static void TestRimWorldFactionRaidHonorsRimWarGuard()
{
    var path = Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "IncidentWorker_LivingWorldFactionRaid.cs");
    AssertFileExists(path);
    var source = File.ReadAllText(path);

    AssertContains("component.IsRimWarActive", source);
}

static void TestRimWorldRaidPawnGenerationAttachesIdentity()
{
    var path = Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldRaidPawnGenerationPatch.cs");
    AssertFileExists(path);
    var source = File.ReadAllText(path);

    AssertContains("CompLivingWorldIdentity", source);
    AssertContains("SetLedgerId", source);
    AssertContains("GetRaidPawnLink", source);
}

static void TestRimWorldFactionRaidLocalization()
{
    var englishPath = Path.Combine(FindRepoRoot(), "mod", "Languages", "English", "Keyed", "LivingWorld.xml");
    var russianPath = Path.Combine(FindRepoRoot(), "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml");
    var incidentRussianPath = Path.Combine(
        FindRepoRoot(),
        "mod",
        "Languages",
        "Russian",
        "DefInjected",
        "IncidentDef",
        "LivingWorld_Incidents.xml");
    var englishXml = File.ReadAllText(englishPath);
    var russianXml = File.ReadAllText(russianPath);
    var incidentRussianXml = File.ReadAllText(incidentRussianPath);

    AssertContains("<LW_FactionRaidLetterLabel>", englishXml);
    AssertContains("<LW_FactionRaidLetterLabel>", russianXml);
    AssertContains("<LW_FactionRaidLetterText>", englishXml);
    AssertContains("<LW_FactionRaidLetterText>", russianXml);
    AssertContains("<LivingWorld_FactionRaid.label>", incidentRussianXml);
}

static void TestRaidPrimaryPathAndFallbackContract()
{
    var docsPath = Path.Combine(FindRepoRoot(), "docs", "simulation.md");
    var patchPath = Path.Combine(
        FindRepoRoot(),
        "src",
        "LivingWorld.RimWorld",
        "LivingWorldRaidIncidentPatch.cs");

    var docs = File.ReadAllText(docsPath);
    var raidPatchSource = File.ReadAllText(patchPath);

    AssertContains("custom raid incident owns primary Living World raid path", docs);
    AssertContains("legacy vanilla raid patches are fallback", docs);
    AssertContains("LivingWorld_FactionRaid", raidPatchSource);
    AssertContains("fallback", raidPatchSource);
}

static void AssertAggregateMatchesFullScan(WorldState state, EntityId settlementId)
{
    var aggregate = state.GetSettlementDerivedAggregate(settlementId);
    var fullScan = FullScanPopulation(state, settlementId);

    AssertEqual(fullScan, aggregate.Population);
    AssertEqual(fullScan.Adults, aggregate.Power.Combatants);
    AssertEqual(SettlementPowerService.CombatPowerOf(fullScan.Adults), aggregate.Power.CombatPower);
    AssertEqual(fullScan, state.GetSettlementPopulation(settlementId));
    AssertEqual(aggregate.Power, SettlementPowerService.GetSettlementPower(state, settlementId));
}

static void AssertFactionAggregateMatchesFullScan(WorldState state, string factionId)
{
    var aggregate = state.GetFactionDerivedAggregate(factionId);
    var settlements = state.Settlements
        .Where(settlement => string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal))
        .Select(settlement => settlement.Id)
        .ToList();
    var fullScan = SumPopulations(settlements.Select(settlementId => FullScanPopulation(state, settlementId)));
    var combatants = settlements.Sum(settlementId => FullScanPopulation(state, settlementId).Adults);
    var combatPower = settlements.Sum(settlementId =>
        SettlementPowerService.CombatPowerOf(FullScanPopulation(state, settlementId).Adults));

    AssertEqual(fullScan, aggregate.Population);
    AssertEqual(combatants, aggregate.Power.Combatants);
    AssertEqual(combatPower, aggregate.Power.CombatPower);
}

static SettlementPopulation FullScanPopulation(WorldState state, EntityId settlementId)
{
    var citizens = state.Citizens
        .Where(citizen =>
            citizen.SettlementId == settlementId
            && citizen.Status == CitizenStatus.Alive
            && state.GetOwner(citizen.Id) == settlementId)
        .ToList();

    return new SettlementPopulation(
        citizens.Count,
        citizens.Count(citizen => citizen.IsChild),
        citizens.Count(citizen => citizen.IsAdult),
        citizens.Count(citizen => citizen.IsElderly));
}

static SettlementPopulation SumPopulations(IEnumerable<SettlementPopulation> populations)
{
    var total = 0;
    var children = 0;
    var adults = 0;
    var elderly = 0;
    foreach (var population in populations)
    {
        total += population.Total;
        children += population.Children;
        adults += population.Adults;
        elderly += population.Elderly;
    }

    return new SettlementPopulation(total, children, adults, elderly);
}

static string FindRepoRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);

    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "LivingWorld.sln")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Could not find LivingWorld.sln from test output directory.");
}

static void AssertFileExists(string path)
{
    if (!File.Exists(path))
    {
        throw new InvalidOperationException($"Expected file '{path}' to exist.");
    }
}

static void AssertPngDimensions(string path, int expectedWidth, int expectedHeight)
{
    AssertFileExists(path);
    var bytes = File.ReadAllBytes(path);
    if (bytes.Length < 24
        || bytes[0] != 0x89
        || bytes[1] != 0x50
        || bytes[2] != 0x4E
        || bytes[3] != 0x47)
    {
        throw new InvalidOperationException($"Expected file '{path}' to be a PNG image.");
    }

    var width = ReadBigEndianInt32(bytes, 16);
    var height = ReadBigEndianInt32(bytes, 20);
    if (width != expectedWidth || height != expectedHeight)
    {
        throw new InvalidOperationException(
            $"Expected PNG '{path}' to be {expectedWidth}x{expectedHeight}, got {width}x{height}.");
    }
}

static int ReadBigEndianInt32(byte[] bytes, int offset)
{
    return (bytes[offset] << 24)
        | (bytes[offset + 1] << 16)
        | (bytes[offset + 2] << 8)
        | bytes[offset + 3];
}

static void AssertContains(string expected, string actual)
{
    if (!actual.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected content to contain '{expected}'.");
    }
}

// Collect the opening-tag names of every <LW_...> element in a Keyed XML file. Closing tags
// ("</LW_...") never match because the search prefix is "<LW_". No regex: implicit usings do not
// import System.Text.RegularExpressions.
static HashSet<string> ExtractKeyedElementNames(string xml)
{
    var names = new HashSet<string>(StringComparer.Ordinal);
    var i = 0;
    while (true)
    {
        var open = xml.IndexOf("<LW_", i, StringComparison.Ordinal);
        if (open < 0)
        {
            break;
        }

        var close = xml.IndexOf('>', open);
        if (close < 0)
        {
            break;
        }

        var tag = xml.Substring(open + 1, close - open - 1);
        i = close + 1;
        if (tag.IndexOf(' ') < 0 && tag.IndexOf('/') < 0)
        {
            names.Add(tag);
        }
    }

    return names;
}

// Collect the LW_ keys that appear as whole "LW_..." string literals in a C# source file. A literal
// that ends in '_' is a concatenation prefix (joined with a computed suffix at runtime), not a
// complete key, and is skipped.
static HashSet<string> ExtractSourceKeyLiterals(string source)
{
    var keys = new HashSet<string>(StringComparer.Ordinal);
    var i = 0;
    while (true)
    {
        var quote = source.IndexOf("\"LW_", i, StringComparison.Ordinal);
        if (quote < 0)
        {
            break;
        }

        var start = quote + 1;
        var j = start;
        while (j < source.Length && (char.IsLetterOrDigit(source[j]) || source[j] == '_'))
        {
            j++;
        }

        if (j < source.Length && source[j] == '"' && source[j - 1] != '_')
        {
            keys.Add(source.Substring(start, j - start));
        }

        i = j + 1;
    }

    return keys;
}

static void AssertDoesNotContain(string unexpected, string actual)
{
    if (actual.Contains(unexpected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected content not to contain '{unexpected}'.");
    }
}

static void AssertThrows<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(
            $"Expected exception {typeof(TException).Name}, got {ex.GetType().Name}.",
            ex);
    }

    throw new InvalidOperationException($"Expected exception {typeof(TException).Name}.");
}

static void AssertRimWorldMethodExists(string typeName, string methodName)
{
    var managedPath = Path.Combine("C:\\Games\\RimWorld", "RimWorldWin64_Data", "Managed");
    var assemblyPath = Path.Combine(managedPath, "Assembly-CSharp.dll");

    AssertFileExists(assemblyPath);

    AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
    {
        var dependencyName = new System.Reflection.AssemblyName(args.Name).Name;
        if (string.IsNullOrWhiteSpace(dependencyName))
        {
            return null;
        }

        var dependencyPath = Path.Combine(managedPath, dependencyName + ".dll");
        return File.Exists(dependencyPath)
            ? System.Reflection.Assembly.LoadFrom(dependencyPath)
            : null;
    };

    var assembly = System.Reflection.Assembly.LoadFrom(assemblyPath);
    var type = assembly.GetType(typeName)
        ?? throw new InvalidOperationException($"Expected RimWorld type '{typeName}' to exist.");
    var flags = System.Reflection.BindingFlags.Public
        | System.Reflection.BindingFlags.NonPublic
        | System.Reflection.BindingFlags.Instance
        | System.Reflection.BindingFlags.Static
        | System.Reflection.BindingFlags.DeclaredOnly;

    if (!type.GetMethods(flags).Any(method => method.Name == methodName))
    {
        throw new InvalidOperationException($"Expected RimWorld type '{typeName}' to declare method '{methodName}'.");
    }
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}
