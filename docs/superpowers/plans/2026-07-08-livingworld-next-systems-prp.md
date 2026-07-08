# LivingWorld Next Systems PRP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the existing Living World ledger foundation into a visible gameplay loop where information, economy, faction decisions, travel, materialization and consequences all come from the same world state.

**Architecture:** `LivingWorld.Core` remains the source of truth and owns simulation rules; `LivingWorld.RimWorld` only translates RimWorld events into Core commands, materializes selected ledger events, draws UI and fails open. Work must stay clean-room: reference mods inform mechanics only, never code, names, formulas, XML or assets.

**Tech Stack:** C#/.NET, RimWorld 1.6, Verse/RimWorld APIs, Harmony only for thin adapters, XML Defs, keyed EN/RU localization, deterministic Core services, custom test runner in `src/LivingWorld.Tests/Program.cs`.

---

## Current Baseline

As of `origin/main` at `7b9fe01`, Living World already has:

- ledger-backed citizens, settlements, resources, ownership, events and save/load;
- compact save blocks for citizens, ownership and event history;
- derived population/combat aggregates;
- finite drifter reservoir, assimilation, founding and materialization incident;
- settlement production profiles, wealth snapshots and virtual trade;
- persistent caravans with cargo conservation and pruning;
- world-war actions: warband, develop, caravan, scout, diplomat and expansion;
- world missions for scout/diplomat travel and world-map markers for visible travel;
- player faction protection and Rim War mutual exclusion guards;
- identity comp on materialized human pawns and inbound fate sync for death/capture/exit;
- player-facing bands for population, strength and wealth unless debug is enabled.

This means the next work should not be another isolated mechanic. The next milestone must connect the mechanics into a cause-and-effect loop:

```text
intel source -> faction decision -> preparation -> travel/materialization -> player-visible event -> ledger consequence -> future decisions
```

## Non-Goals For This PRP

- Do not create a mandatory custom storyteller. Use custom incidents and optional storyteller components later.
- Do not add Rim War, Empire or Economics & Demography adapters before Living World's own core loop is coherent.
- Do not materialize all settlement residents as pawns.
- Do not implement full Biotech human gene editing before animal/crop/ecology cohorts exist.
- Do not expose exact hidden ledger values to the player without intel or debug logging.

## Global Invariants

Every task below must preserve these rules:

- No people, animals, goods or silver are created without a ledger source.
- Every movement that carries citizens, animals or cargo has a terminal cleanup path.
- Player faction settlements are never silently attacked, captured, collapsed or inspected as omniscient NPC ledger objects.
- Rim War active means Living World does not double-drive world war or custom faction raids.
- RimWorld layer fails open: errors should preserve vanilla gameplay rather than corrupt the ledger.
- Save/load remains backward-compatible when new fields are added.
- UI uses bands and source labels unless the value is directly known or debug is enabled.
- Core tests must fail without the behavior they claim to protect.

## File Ownership

### Codex Lane

- `src/LivingWorld.Core/*`
- `src/LivingWorld.Tests/Program.cs`
- `docs/design/*`
- `docs/superpowers/plans/*`
- `docs/roadmap.md`
- `docs/performance.md`
- `docs/save_format.md`

### Claude Lane

- `src/LivingWorld.RimWorld/*`
- `mod/Defs/*`
- `mod/Patches/*`
- `mod/Languages/*`
- `docs/simulation.md`
- `docs/compatibility.md`

### Shared Files

Rebase from `origin/main` immediately before touching:

- `src/LivingWorld.Tests/Program.cs`
- `src/LivingWorld.Core/WorldState.cs`
- `src/LivingWorld.Core/WorldStateCodec.cs`
- `src/LivingWorld.RimWorld/LivingWorldWorldComponent.cs`
- `mod/Languages/English/Keyed/LivingWorld.xml`
- `mod/Languages/Russian/Keyed/LivingWorld.xml`

---

## Analysis By Missing System

## 1. World State To Visible Game Event

### Current State

Core already records many facts: migrations, wars, caravan movement, scouting, diplomacy, settlement development and drifters. RimWorld currently visualizes some of them: main tab rows, inspect lines, world-map markers and custom incidents for drifters/faction raids.

### Gap

The rule for what becomes visible is not centralized. Some events are ledger-only, some become letters, some become markers, and some become incidents. Without a materialization policy, future systems will add one-off UI and incident decisions.

### Required Model

Introduce a Core-level materialization intent concept that is still RimWorld-agnostic:

```text
WorldEvent -> EventVisibilityPolicy -> MaterializationIntent
```

Examples:

- `RaidPrepared` -> incident candidate;
- `ScoutMissionLaunched` -> world-map marker and later intel;
- `SettlementDestroyed` -> history event and occasional letter;
- `RefugeesCreated` -> possible refugee incident if near player;
- `AnimalMigrationStarted` -> map event only if region is relevant.

Core should decide what is important and why. RimWorld should decide how to display or spawn it.

### Acceptance

- A single service returns pending materialization intents for a tick range.
- Intents are deterministic and save/load safe.
- Letters are rate-limited and aggregate catch-up days.
- RimWorld materializes only intents it understands and ignores the rest fail-open.
- Tests prove a captured settlement produces history but not an automatic active-map event against the player.

### Owner

Codex designs Core intents. Claude consumes them in RimWorld UI/incidents.

## 2. Cause-Based Raid Chain

### Current State

Raid population is ledger-backed, raid pawns can bind to citizens, casualties return to Core, and trade intel can create raid opportunities. Review hardening restored fallback raids so lack of trade intel does not disable all threats.

### Gap

The raid chain is still too short. It does not yet feel like:

```text
someone learned something -> faction judged it worth raiding -> settlement gathered people/supplies -> party traveled -> raid happened -> survivors returned
```

### Required Model

Split raid cause into four records:

```text
RaidIntelFact
  source: trader | scout | prisoner | ally | public | direct visit
  target: player colony or visible settlement
  goods: bands of observed wealth, drugs, gold, weapons, food
  confidence
  expiresTick

RaidIntent
  factionId
  targetKind
  reason: revenge | plunder | ideology | rescue | suppression | opportunity
  desiredCombatants
  desiredSupplies
  createdTick
  latestLaunchTick

RaidPreparation
  sourceSettlementId
  reservedCitizens
  reservedResources
  readinessBand
  failureReason if preparation cannot complete

RaidExpedition
  armyId
  movementId
  targetMap
  launchedTick
```

This does not require a custom storyteller. A custom Living World raid incident can ask Core for a ready expedition. Vanilla storyteller still controls pressure; Living World controls source and consequences.

### Acceptance

- Selling high-value drugs/gold creates an intel fact, not an immediate raid.
- A hostile faction can create a raid intent from intel only if it has eligible adults and supplies.
- Preparation reserves real citizens and cargo from a source settlement.
- If the incident never fires, stale preparation releases citizens and resources.
- If Rim War is active, Living World does not launch the custom raid.
- Player can see a vague warning only when there is a believable source, such as scout sighting, rumor, ally warning or detected movement.

### Owner

Codex builds Core raid cause/preparation. Claude wires incidents, letters and RU/EN visibility.

## 3. Economy Depth Beyond Population

### Current State

Production already uses profile data and wealth snapshots. Virtual trade conserves resources and silver. Development can improve tiers and specialists.

### Gap

The economy can still feel abstract because production does not yet depend enough on infrastructure, stock inputs, roads/logistics, security, climate shocks and specialists.

### Required Model

Production should be calculated from:

```text
terrain + biome + rainfall + temperature
+ technology tier
+ labor pool
+ specialist pool
+ facilities
+ tools/animals/input stock
+ security/disruption
+ trade route access
+ seasonal or disaster modifiers
```

Add `SettlementFacility`, `SettlementProject` and `ProductionInputNeed` before adding more resources. This prevents formulas from becoming hardcoded population multipliers.

### Acceptance

- Same population produces different outputs on fertile plain, desert, mountain and polluted tile.
- A mine/farm/lab facility changes output capacity.
- Missing inputs reduce output instead of creating products anyway.
- War damage or unsafe roads reduce production/trade capacity.
- Exact output remains hidden behind intel bands.

### Owner

Codex owns Core economy model. Claude owns player-facing bands and settlement/economy windows.

## 4. Settlement Growth, Construction, Ruins And Relocation

### Current State

Settlements have population, capabilities, tier, development and expansion. Expansion can create new colonies by moving real adults. Housing limits births.

### Gap

Existing settlements still need a stronger physical lifecycle: construct, damage, repair, abandon, relocate, leave ruins, recover.

### Required Model

Add settlement projects:

```text
BuildHousing
BuildStorage
BuildFarm
BuildMine
BuildClinic
BuildWall
RepairDamage
RelocateSettlement
ReclaimRuin
```

Relocation should be a movement, not a teleport:

```text
source settlement -> migration caravan with people/goods -> target tile -> new settlement or failed migration
```

Destroyed settlements should become `WorldRuin` records with claims, salvage bands, danger bands and possible refugee links.

### Acceptance

- A damaged settlement has lower production/security until repaired.
- A starving or unsafe settlement can decide to relocate.
- Relocation moves real citizens/resources into a caravan-like entity.
- Ruins preserve salvage/claim history without keeping full active settlement state forever.
- Player inspect shows public ruin/settlement state without exact hidden values.

### Owner

Codex owns lifecycle records and services. Claude owns world-map markers, inspect strings and letters.

## 5. Information Visibility And Fog Of War

### Current State

Known settlement info exists. Trade and scouting can create intel. UI has moved toward bands and debug-only exact values.

### Gap

Information is not yet a full gameplay system. The player should learn through believable channels, and factions should also act on what they know about the player.

### Required Model

Make intel bidirectional:

```text
PlayerKnowledge
  what player knows about factions/settlements/resources/missions

FactionKnowledge
  what an NPC faction knows about the player colony and other factions
```

Sources:

- trader observation;
- scout mission;
- prisoner interrogation;
- ally warning;
- public city policy;
- direct visit;
- battle survivors;
- caravan ambush;
- rumor with low confidence.

Each fact needs confidence, age and visibility band.

### Acceptance

- The player does not see exact population, stockpile, army or wealth without direct reliable intel.
- NPC faction does not raid for gold/drugs unless it has an intel source or generic hostility fallback.
- Old intel becomes stale and UI labels it.
- Public settlements can expose more information than secretive factions.
- Debug mode can still show exact values for development.

### Owner

Codex owns intel records and confidence rules. Claude owns UI labels, tooltips and localization.

## 6. NPC Wars And Political Consequences

### Current State

World-war actions exist and can capture settlements, move caravans, scout, improve goodwill, develop and found colonies. Player faction protection is in place.

### Gap

Wars still need campaign-level state: war goals, claims, truces, escalation, exhaustion, refugees, annexation and revenge. Otherwise battles are isolated actions.

### Required Model

Add `WorldConflict`:

```text
WorldConflict
  attackerFactionId
  defenderFactionId
  goal: raid | conquest | retaliation | tradeRoute | liberation | containment
  startTick
  intensity
  exhaustionAttacker
  exhaustionDefender
  claimedSettlements
  lastMajorEventTick
  status: active | truce | ended
```

World-war actions should attach to a conflict when possible. Battles should increase exhaustion, produce refugees and shift diplomacy.

### Acceptance

- Repeated losses reduce a faction's willingness and ability to attack.
- Capturing a settlement creates a claim/history event, not just a faction id flip.
- Refugees from war enter the migration/drifter systems through finite sources.
- Truces pause attacks but do not erase hostility.
- UI can show "war is ongoing" as a banded summary.

### Owner

Codex owns Core conflict state. Claude owns letters/world tab summaries.

## 7. Materialization Of People, Settlements And Missions

### Current State

Human pawns are lightweight records until spawned. Raid/drifter materialization exists. Identity comp binds materialized pawns back to ledger. World missions have markers.

### Gap

Visiting an NPC settlement, attacking it, trading with it or seeing a caravan still needs a consistent rule for which abstract citizens become pawns and how they return to abstraction.

### Required Model

Define a materialization contract:

```text
MaterializationRequest
  sourceEntityId
  purpose: raid | trader | visitor | settlementVisit | prisoner | refugee | battleSite
  requestedRoles
  maxPawns
  targetMap

MaterializationLease
  entityIds
  pawnIds
  expiresWhen
  returnPolicy
```

Only leased citizens can become pawns. When the lease ends, Core resolves their fates: returned, dead, prisoner, missing, recruited, enslaved, abandoned.

### Acceptance

- A settlement with 57 citizens does not spawn all 57 by default.
- A settlement visit materializes a representative subset based on role and map purpose.
- Materialized citizens keep `CompLivingWorldIdentity`.
- Returning pawns update ledger status and ownership.
- Leases expire or reconcile on map removal/save/load.

### Owner

Codex owns lease/fate model. Claude owns RimWorld pawn generation and map event integration.

## 8. Ecology, Animals And Food Web

### Current State

Animal/ecology work is mostly design. BioTech analysis already says animals must be cohorts, not off-map pawns.

### Gap

Food production, trade, war logistics and settlement survival need animal populations and ecological constraints. Without them, animals and crops become virtual numbers.

### Required Model

Start with compact cohorts:

```text
AnimalCohort
  species
  owner: settlement | wildRegion | caravan
  count bands
  age bands
  health band
  fertility
  adaptation traits
  disease pressure

WildRegion
  biome
  carryingCapacity
  animalCohorts
  foragePressure
  huntingPressure
```

Settlement livestock, pack animals and wild animal populations should share cohort mechanics but have different owners.

### Acceptance

- Hunting reduces a real wild cohort.
- Pack animals for caravans are drawn from owned animal cohorts.
- Famine reduces livestock before creating free food.
- Animal migration moves cohort counts between regions.
- No off-map animal is saved as a full pawn.

### Owner

Codex owns Core cohorts/ecology. Claude owns materialized animals/trader/raid visuals later.

## 9. Technology, Breeding, Selection And Incubators

### Current State

The design baseline exists in `docs/design/biotech-breeding-and-selection.md`. It correctly frames breeding/incubation as projects consuming inputs over time.

### Gap

Technology and selection are not connected to the economy, specialists, facilities, intel and ethics yet.

### Required Model

Use projects, not free spawning:

```text
TechCapability
  factionId
  domain: agriculture | medicine | weapons | genetics | logistics | construction
  tier
  secrecy
  decayRisk

BioProject
  ownerSettlementId
  kind: animalSelection | cropSelection | incubation | geneResearch
  inputs
  requiredFacility
  requiredSpecialists
  progress
  risk
  outputCohortOrStrain
```

Technology spreads through trade, espionage, conquest, prisoners and diplomacy. Advanced work should be gated by DLC and ideology where applicable.

### Acceptance

- Incubation consumes nutrition, medicine, power/facility capacity and time.
- Selection improves traits but increases disease/inbreeding risk if diversity is low.
- Tech can be stolen or learned through a believable source.
- Without Biotech, human gene systems are disabled but animal/crop selection still works.
- Exact genetic values remain hidden behind intel bands.

### Owner

Codex owns Core technology/project model. Claude owns DLC gates, UI and localization.

## 10. Balance, Settings And Player Control

### Current State

World-gen and mod settings exist. There are toggles for bootstrap, debug, drifter flow and world war safety.

### Gap

As systems grow, the player needs control over intensity without turning the simulation into a spreadsheet.

### Required Model

Group settings by player intent:

```text
World Population
  target density
  hard ceiling
  migration pressure

Faction Activity
  war frequency
  raid preparation strictness
  caravan activity
  diplomacy activity

Information
  fog strictness
  rumor frequency
  debug exact values

Performance
  simulation cadence
  UI row caps
  save retention days

Compatibility
  Rim War cede mode
  Empire cede mode
  vanilla raid fallback mode
```

Defaults should be conservative and compatible. Debug controls should not be mixed with normal player options.

### Acceptance

- Settings have EN/RU text and clear consequences.
- No setting silently creates infinite population or resources.
- Compatibility mode visibly says what Living World has ceded to another mod.
- Performance settings do not break conservation or save/load.
- World generation settings cover initial shape; mod settings cover ongoing behavior.

### Owner

Claude owns RimWorld settings UI and localization. Codex owns Core request parameters and defaults.

---

## Recommended Execution Order

The order below is chosen to make every slice playable and testable without requiring a custom storyteller.

### Phase 1: Intel And Raid Causes

Build the cause chain before more event types. This creates the "why did this happen?" layer.

Primary tasks:

- define `RaidIntelFact`, `FactionKnowledge` and stale/confidence rules;
- convert trade/scout facts into raid intent;
- add raid preparation with reservation and stale release;
- route custom faction raid incident through prepared expeditions.

Expected commits:

```text
feat(core): add faction knowledge and raid intel facts
feat(core): prepare raids from real settlements and intel
feat(rimworld): route custom faction raids through prepared expeditions
```

Verification:

```powershell
dotnet run --project src\LivingWorld.Tests\LivingWorld.Tests.csproj
dotnet build LivingWorld.sln
git diff --check HEAD~1..HEAD
```

### Phase 2: Materialization Lease Contract

Do this before settlement visits or deeper trader materialization. It prevents "57 citizens on ledger, 8 pawns on map" from feeling fake.

Primary tasks:

- create Core lease records;
- lease citizens by role and purpose;
- reconcile death/capture/return/missing/recruit outcomes;
- persist leases with backward-compatible save defaults;
- make RimWorld pawn generation consume leases.

Expected commits:

```text
feat(core): add materialization leases
feat(rimworld): materialize leased citizens with Living World identity
```

### Phase 3: Economy Facilities And Settlement Projects

Add physical capacity before adding more products.

Primary tasks:

- add settlement facilities and projects;
- connect facilities to production profile capacity;
- add damage/repair modifiers;
- keep output hidden behind intel bands.

Expected commits:

```text
feat(core): add settlement facilities and production inputs
feat(core): add settlement construction and repair projects
feat(rimworld): expose facility and production bands
```

### Phase 4: Settlement Lifecycle And Ruins

Once projects and leases exist, settlements can safely decline, relocate and leave ruins.

Primary tasks:

- add `WorldRuin`;
- add abandon/relocate/reclaim paths;
- make destroyed settlements produce refugees and claims;
- materialize ruins only as UI/inspect/world-object data, not full maps by default.

Expected commits:

```text
feat(core): add settlement ruins and relocation
feat(rimworld): show ruins and relocation markers
```

### Phase 5: Conflicts As Campaigns

World war becomes long-form politics instead of isolated battles.

Primary tasks:

- add `WorldConflict`;
- attach warband/capture/refugee events to conflict records;
- add exhaustion/truce/claim logic;
- show conflict summaries as bands.

Expected commits:

```text
feat(core): track faction conflicts and exhaustion
feat(rimworld): show conflict summaries
```

### Phase 6: Ecology And Animal Cohorts

Add animal/livestock ecology after facilities and logistics exist.

Primary tasks:

- add `AnimalCohort` and `WildRegion`;
- make hunting/livestock/caravan animals consume cohort counts;
- add migration and disease pressure;
- keep active-map animal materialization leased and capped.

Expected commits:

```text
feat(core): add animal cohorts and wild regions
feat(core): simulate animal migration and livestock pressure
```

### Phase 7: Technology, Selection And Incubation

Build on facilities, specialists, ecology and intel.

Primary tasks:

- add tech capabilities by domain;
- add animal/crop selection projects;
- add incubation projects with resources, risk and time;
- gate human/xenotype systems behind Biotech and ideology compatibility.

Expected commits:

```text
feat(core): add technology capabilities and selection projects
feat(core): add incubation projects for animal cohorts
feat(rimworld): add biotech-aware gates and visibility
```

### Phase 8: Optional Storyteller Component

Only after the cause chains are stable.

Primary tasks:

- add optional `StorytellerComp` or incident weight integration;
- make it read Core readiness/intents through cheap aggregates;
- keep vanilla storyteller compatibility;
- expose opt-in settings.

Expected commits:

```text
feat(rimworld): add optional Living World storyteller component
```

---

## Detailed Task Seeds

These are not all to be executed in one branch. Each task should become its own implementation branch.

### Task 1: Raid Intel Facts

**Files:**

- Create: `src/LivingWorld.Core/RaidIntelFact.cs`
- Create: `src/LivingWorld.Core/FactionKnowledgeService.cs`
- Modify: `src/LivingWorld.Core/WorldState.cs`
- Modify: `src/LivingWorld.Core/WorldStateCodec.cs`
- Test: `src/LivingWorld.Tests/Program.cs`

- [ ] **Step 1: Write failing tests**

Add tests named:

```csharp
TestFactionKnowledgeRecordsTradeIntelAboutPlayer()
TestRaidIntelExpiresAndStopsCreatingNewIntent()
TestRaidIntelDoesNotRevealExactPlayerWealth()
```

Expected red failures: missing types/methods.

- [ ] **Step 2: Implement minimal records and state storage**

Use deterministic IDs and optional codec sections. Store facts by faction id and expiry tick.

- [ ] **Step 3: Convert existing trade intel path**

Trade should create a fact with bands and confidence, not direct exact omniscience.

- [ ] **Step 4: Verify**

Run:

```powershell
dotnet run --project src\LivingWorld.Tests\LivingWorld.Tests.csproj
dotnet build LivingWorld.sln
```

### Task 2: Raid Preparation And Release

**Files:**

- Create: `src/LivingWorld.Core/RaidIntent.cs`
- Create: `src/LivingWorld.Core/RaidPreparationService.cs`
- Modify: `src/LivingWorld.Core/RaidPopulationAllocator.cs`
- Modify: `src/LivingWorld.Core/RaidOpportunityService.cs`
- Test: `src/LivingWorld.Tests/Program.cs`

- [ ] **Step 1: Write failing tests**

Add tests named:

```csharp
TestRaidIntentRequiresIntelOrGenericHostilityFallback()
TestRaidPreparationReservesRealCitizensAndSupplies()
TestStaleRaidPreparationReturnsReservedCitizensAndResources()
TestPreparedRaidLaunchConsumesPreparationExactlyOnce()
```

- [ ] **Step 2: Implement `RaidIntent` and preparation records**

Store intended source settlement, reserved citizens, reserved resources, readiness and expiry.

- [ ] **Step 3: Add stale release path**

Release must use existing ownership transfer rules. No citizen stays owned by a dead preparation/army after expiry.

- [ ] **Step 4: Verify**

Run standard tests/build/diff-check.

### Task 3: Materialization Leases

**Files:**

- Create: `src/LivingWorld.Core/MaterializationLease.cs`
- Create: `src/LivingWorld.Core/MaterializationLeaseService.cs`
- Modify: `src/LivingWorld.Core/WorldState.cs`
- Modify: `src/LivingWorld.Core/WorldStateCodec.cs`
- Modify: `src/LivingWorld.RimWorld/LivingWorldPawnSyncService.cs`
- Test: `src/LivingWorld.Tests/Program.cs`

- [ ] **Step 1: Write failing tests**

Add tests named:

```csharp
TestSettlementVisitLeasesRepresentativeCitizensOnly()
TestLeaseReconcilesDeadCapturedReturnedAndMissingCitizens()
TestLeaseSurvivesSaveLoad()
TestExpiredLeaseReturnsUnresolvedCitizensSafely()
```

- [ ] **Step 2: Add lease records**

Leases must track purpose, entity IDs, issue tick, expiry tick and return policy.

- [ ] **Step 3: Make inbound sync lease-aware**

Pawn fate sync should prefer identity comp -> lease -> citizen status resolution.

- [ ] **Step 4: Verify**

Run standard tests/build/diff-check.

### Task 4: Facilities And Settlement Projects

**Files:**

- Create: `src/LivingWorld.Core/SettlementFacility.cs`
- Create: `src/LivingWorld.Core/SettlementProject.cs`
- Create: `src/LivingWorld.Core/SettlementProjectService.cs`
- Modify: `src/LivingWorld.Core/SettlementProductionService.cs`
- Modify: `src/LivingWorld.Core/WorldStateCodec.cs`
- Test: `src/LivingWorld.Tests/Program.cs`

- [ ] **Step 1: Write failing tests**

Add tests named:

```csharp
TestFacilityChangesProductionCapacity()
TestMissingProductionInputsReduceOutput()
TestSettlementDamageReducesProductionUntilRepaired()
TestSettlementProjectsConsumeOwnedResourcesOverTime()
```

- [ ] **Step 2: Add facilities and projects**

Facilities are capacity. Projects consume resources and time. Neither creates citizens.

- [ ] **Step 3: Connect to production**

Production profile reads facilities/input availability through cached settlement data.

- [ ] **Step 4: Verify**

Run standard tests/build/diff-check.

### Task 5: Ruins And Relocation

**Files:**

- Create: `src/LivingWorld.Core/WorldRuin.cs`
- Create: `src/LivingWorld.Core/SettlementLifecycleService.cs`
- Modify: `src/LivingWorld.Core/MigrationService.cs`
- Modify: `src/LivingWorld.Core/WorldBattleService.cs`
- Test: `src/LivingWorld.Tests/Program.cs`

- [ ] **Step 1: Write failing tests**

Add tests named:

```csharp
TestDestroyedSettlementLeavesRuinAndRefugees()
TestRelocationMovesCitizensAndResourcesThroughMigrationGroup()
TestRuinCanBeReclaimedWithoutDuplicatingResources()
TestOldInactiveRuinsCanBePrunedAfterHistoryIsRecorded()
```

- [ ] **Step 2: Implement ruin records**

Ruin stores claims, salvage bands, danger band and history references.

- [ ] **Step 3: Implement relocation**

Relocation uses migration/caravan ownership transfers and can fail without data loss.

- [ ] **Step 4: Verify**

Run standard tests/build/diff-check.

### Task 6: Conflict Campaigns

**Files:**

- Create: `src/LivingWorld.Core/WorldConflict.cs`
- Create: `src/LivingWorld.Core/ConflictService.cs`
- Modify: `src/LivingWorld.Core/WorldBattleService.cs`
- Modify: `src/LivingWorld.Core/WorldWarService.cs`
- Test: `src/LivingWorld.Tests/Program.cs`

- [ ] **Step 1: Write failing tests**

Add tests named:

```csharp
TestRepeatedLossesIncreaseFactionWarExhaustion()
TestConflictClaimTracksCapturedSettlement()
TestTrucePreventsNewWarbandsUntilExpired()
TestWarRefugeesEnterFinitePopulationFlow()
```

- [ ] **Step 2: Add conflict state**

Conflict records active goals, claims, exhaustion and truce state.

- [ ] **Step 3: Connect battle outcomes**

Battle/capture/refugee events update conflict state and diplomacy.

- [ ] **Step 4: Verify**

Run standard tests/build/diff-check.

### Task 7: Animal Cohorts And Ecology

**Files:**

- Create: `src/LivingWorld.Core/WorldAnimalCohort.cs`
- Create: `src/LivingWorld.Core/WildRegion.cs`
- Create: `src/LivingWorld.Core/EcologyService.cs`
- Modify: `src/LivingWorld.Core/WorldStateCodec.cs`
- Test: `src/LivingWorld.Tests/Program.cs`

- [ ] **Step 1: Write failing tests**

Add tests named:

```csharp
TestAnimalCohortsSaveLoadWithoutPawnStorage()
TestHuntingReducesWildAnimalCohort()
TestCaravanPackAnimalsMoveFromOwnedCohort()
TestAnimalMigrationConservesCohortCounts()
```

- [ ] **Step 2: Add cohort records**

Save compact counts and bands, not individual animal pawns.

- [ ] **Step 3: Add daily/monthly ecology tick**

Use time-dilated cadence and cached region data, not per-tick animal scans.

- [ ] **Step 4: Verify**

Run standard tests/build/diff-check.

### Task 8: Technology And Bio Projects

**Files:**

- Create: `src/LivingWorld.Core/TechCapability.cs`
- Create: `src/LivingWorld.Core/BioProject.cs`
- Create: `src/LivingWorld.Core/TechnologyService.cs`
- Create: `src/LivingWorld.Core/BioProjectService.cs`
- Test: `src/LivingWorld.Tests/Program.cs`

- [ ] **Step 1: Write failing tests**

Add tests named:

```csharp
TestIncubationConsumesResourcesAndTimeBeforeOutput()
TestSelectionImprovesTraitBandWithDiversityRisk()
TestTechnologyCanSpreadThroughTradeIntel()
TestBiotechHumanSystemsAreDisabledWithoutDlcGate()
```

- [ ] **Step 2: Add capability and project records**

Capabilities affect what projects a settlement can start. Projects consume inputs and progress deterministically.

- [ ] **Step 3: Connect to facilities and cohorts**

Bio projects require facilities and output animal cohorts/crop strains first. Human/xenotype adapters remain gated.

- [ ] **Step 4: Verify**

Run standard tests/build/diff-check.

### Task 9: Settings Reorganization

**Files:**

- Modify: `src/LivingWorld.RimWorld/LivingWorldSettings.cs`
- Modify: `src/LivingWorld.RimWorld/LivingWorldSettingsDrawer.cs`
- Modify: `src/LivingWorld.RimWorld/LivingWorldWorldGenSettingsWindow.cs`
- Modify: `mod/Languages/English/Keyed/LivingWorld.xml`
- Modify: `mod/Languages/Russian/Keyed/LivingWorld.xml`
- Test: `src/LivingWorld.Tests/Program.cs`

- [ ] **Step 1: Write failing structural tests**

Add tests named:

```csharp
TestLivingWorldSettingsAreGroupedByPlayerIntent()
TestLivingWorldSettingsHaveRussianAndEnglishKeys()
TestCompatibilitySettingsSurfaceCedenceState()
```

- [ ] **Step 2: Group settings**

Create UI sections for population, faction activity, information, performance and compatibility.

- [ ] **Step 3: Keep debug separate**

Exact values and developer diagnostics must stay visibly debug-only.

- [ ] **Step 4: Verify**

Run standard tests/build/diff-check.

---

## Cross-Agent Review Checklist

Before any task is merged, the other agent must review:

- gameplay consequence;
- ownership and lifecycle leaks;
- save/load and backward compatibility;
- performance and hot paths;
- compatibility and fail-open behavior;
- UI visibility and Russian localization;
- tests that fail without the change;
- player faction protection;
- Rim War / Empire double-drive risk;
- clean-room risk against reference mods.

## Final Acceptance For The Milestone

The milestone is complete when the following player story is true:

```text
The player trades gold/drugs to a visitor.
A hostile faction learns about it with limited confidence.
The faction prepares a raid from a real settlement using real citizens and supplies.
The raid travels or becomes visible through a believable warning.
The active-map raid uses leased/materialized citizens with stable identity.
Deaths, capture, escape and return update the ledger.
The source settlement is weaker afterward.
The player's UI explains the consequence with bands and source labels, not omniscient exact values.
```

This story intentionally crosses intel, economy, materialization, raids, UI and persistence. If it works, Living World stops being a collection of systems and becomes a coherent world simulation.
