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
    ("simulates daily settlement food and births", TestSettlementDailySimulationConsumesFoodAndBirths),
    ("records food shortage and blocks births during starvation", TestSettlementDailySimulationRecordsFoodShortage),
    ("ages citizens and records natural deaths", TestDemographyServiceAgesAndKillsElders),
    ("builds settlement production profile from terrain and technology", TestSettlementProductionProfileUsesTerrainAndTechnology),
    ("produces owned resources every day from settlement profile", TestSettlementProductionAddsOwnedResources),
    ("serializes settlement production profiles", TestSettlementProductionProfileSerialization),
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
    ("serializes and restores Living World state", TestWorldStateSerializationRoundTrip),
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
    ("patches vanilla enemy raids into Living World population", TestRimWorldRaidIncidentPatch),
    ("patches generated raid pawns into Living World citizens", TestRimWorldRaidPawnGenerationPatch),
    ("patches pawn death into Living World casualties", TestRimWorldPawnKillPatch),
    ("patches pawn capture into Living World prisoners", TestRimWorldPawnCapturePatch),
    ("patches pawn exit into Living World raid returns", TestRimWorldPawnExitPatch),
    ("records trade intel from RimWorld trade dialog", TestRimWorldTradeIntelPatch),
    ("defines Living World main button def", TestRimWorldMainButtonDef),
    ("declares Harmony dependency and reference", TestRimWorldHarmonyDependency),
    ("patches world generation settings page", TestRimWorldWorldGenSettingsPatch),
    ("defines world generation settings window", TestRimWorldWorldGenSettingsWindow),
    ("defines drifter-flow settings persisted in ExposeData", TestRimWorldDrifterFlowSettings),
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
    ("keeps Odyssey Russian faction namer grammar valid", TestRimWorldRussianOdysseyRulePackOverride),
    ("uses translations in RimWorld UI", TestRimWorldUiUsesTranslations),
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

static void TestDrifterArrivalFillsTowardTarget()
{
    var state = new WorldState(4242);
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
    var result = DrifterArrivalService.SimulateArrivals(
        state,
        new DrifterArrivalRequest(60_000, TargetWorldPopulation: 50, HardCeiling: 100, MaxArrivalsPerStep: 0));

    AssertEqual(0, result.Arrived);
    AssertEqual(0, state.Drifters.Count);
}

static void TestDrifterArrivalRecordsUnaffiliated()
{
    var state = new WorldState(4242);
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
    AssertContains("DrifterFoundingService.SimulateFounding", source);
    AssertContains("DrifterAssimilationService.SimulateAssimilation", source);
    AssertContains("FactionLifecycleService.SimulateCollapses", source);
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
    AssertContains("cachedSettlementRows", source);
    AssertContains("cachedOutcomeRows", source);
    AssertContains("RefreshCachedRows", source);
    AssertContains(".Take(MaxSettlementRows)", source);
    AssertContains(".Take(MaxArmyRows)", source);
    AssertContains(".Take(MaxEventRows)", source);
    AssertContains(".Take(MaxOutcomeRows)", source);
    AssertDoesNotContain("state.Settlements.Count * 72f", source);
    AssertContains("<LW_ListLimited>", englishXml);
    AssertContains("<LW_ListLimited>", russianXml);
    AssertContains("<LW_RaidOutcomesHeader>", englishXml);
    AssertContains("<LW_RaidOutcomesHeader>", russianXml);
    AssertContains("<LW_RaidOutcomeLine>", englishXml);
    AssertContains("<LW_RaidOutcomeLine>", russianXml);
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
    AssertContains("thingIDNumber", source);
    AssertContains("RaidPawnBindingService.MarkPawnDead", source);
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
    AssertContains("RaidPawnBindingService.MarkPawnPrisoner", source);
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
    AssertContains("MarkPawnPrisoner", source);
    AssertContains("thingIDNumber", source);
    AssertContains("RaidPawnBindingService.MarkPawnReturned", source);
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
    AssertContains("LW_MainTitle", source);
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
    AssertContains("Widgets.BeginScrollView", windowSource);
    AssertContains("Widgets.EndScrollView", windowSource);
    AssertContains("\"CloseButton\".Translate()", windowSource);
    AssertContains("PreferredHeight", windowSource);
    AssertContains("LW_Settings_HumanAdults", windowSource);
    AssertContains("LW_Settings_FoodPerCitizen", windowSource);
    AssertContains("LW_Settings_SteelPerCitizen", windowSource);
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
    AssertContains("Scribe_Values.Look(ref drifterHardCeiling", source);
    AssertContains("Scribe_Values.Look(ref drifterLeaderAptitudeThreshold", source);
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

static void AssertContains(string expected, string actual)
{
    if (!actual.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected content to contain '{expected}'.");
    }
}

static void AssertDoesNotContain(string unexpected, string actual)
{
    if (actual.Contains(unexpected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected content not to contain '{unexpected}'.");
    }
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
