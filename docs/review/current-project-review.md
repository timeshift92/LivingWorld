# Living World — Current Project Review

Status: architectural review after the latest world-war, scanner, catch-up, policy and documentation updates.

Date: 2026-07-08

Repository: `timeshift92/LivingWorld`

Update after C4/C5: Core player-faction defense-in-depth and resolved army
movement pruning have been implemented. The historical review below remains useful
for rationale, but the next open hardening work is now service split, cached
aggregates and large-save serialization.

---

## Executive summary

Living World has moved beyond a concept document. The project now has a real foundation:

- `WorldState` as the central ledger;
- persistent citizens, settlements, armies, resources and ownership;
- event history;
- save/load serialization;
- raid lifecycle tracking;
- population flow;
- production;
- demography;
- migration;
- drifter flow;
- world war simulation;
- diplomacy effects;
- scanner/importer architecture;
- RimWar mutual exclusion;
- player settlement protection at scanner level;
- project policy for original implementation and legal safety.

The direction is correct. The current biggest risk is no longer whether the idea is viable. The risk is that the core services will grow too large and become hard to reason about if the next phase keeps adding features instead of stabilizing architecture boundaries.

The next stage should focus on hardening, splitting responsibilities and preventing long-save bloat before adding another large gameplay layer.

---

## Current verdict

```text
Idea: strong
Foundation: good
MVP shape: visible
Architecture direction: correct
Main risk: service/god-class growth
Next priority: hardening + split orchestration + performance aggregates
```

Living World is now in a healthy early-core phase. It is already doing more than a raid experiment, but it must avoid becoming a monolithic simulation blob.

---

## What became better

### 1. Daily catch-up simulation is now safer

Earlier, if several in-game days passed between ticks or after loading, the simulation risked processing only one day. The current implementation uses a catch-up loop with a cap:

```csharp
while (lastSimulatedDay < currentDay && simulatedDays < MaxCatchUpSimulationDays)
{
    lastSimulatedDay++;
    SimulateWorldDay(lastSimulatedDay);
    simulatedDays++;
}
```

This is the right shape. It prevents skipped simulation days while also preventing the game from freezing after a long catch-up window.

Remaining note: the cap must be tuned carefully. If the cap is too low, long catch-up may take many ticks. That is acceptable as long as the player is not flooded with letters/events.

### 2. The simulation loop is now closer to a real world model

`SimulateWorldDay` now coordinates multiple systems:

- settlement production;
- food consumption and births;
- demography;
- migration;
- drifter arrivals/founding/assimilation;
- world war;
- faction collapse.

This is a major improvement over a pure raid-lifecycle MVP.

The order is also mostly correct:

```text
Production
Daily consumption / births
Demography
Migration
Drifters
World war
Faction lifecycle / collapse
```

This means faction collapse sees the result of the day's economic, demographic and military changes.

### 3. RimWar mutual exclusion is implemented

World war only runs when Living World is allowed to own the faction/world-war simulation. If RimWar is active, Living World stands down from its own world-war loop.

That is important because RimWar and Living World both aim to drive faction activity. Running both at the same time would double-drive the world and produce incoherent results.

This is a good compatibility decision.

### 4. Player settlements are no longer imported as NPC ledger settlements

The scanner now excludes `faction.IsPlayer` settlements in `VanillaSettlementImporter`.

This prevents the most dangerous scenario:

```text
NPC faction selects player settlement as normal ledger target
↓
WorldBattleService resolves it silently
↓
player settlement flips faction or loses ledger citizens
↓
player faction may collapse in ledger
```

The scanner-level fix is correct because the player's active colony is not just another NPC settlement. It should be represented through active-map integration, not through silent world-war resolution.

### 5. Scanner architecture is now extensible

The old scanner logic risked importing any `WorldObject` with a faction and tile. The new design introduces:

```csharp
public interface IWorldObjectImporter
{
    bool CanImport(WorldObject obj);
    WorldObjectSettlementCandidate? ImportCandidate(WorldObject obj, ref int scanErrorCount);
}
```

This is a good architectural improvement.

It enables future explicit importers:

```text
VanillaSettlementImporter
EmpireColonyImporter
RimCitiesImporter
QuestSiteImporter
CustomSettlementImporter
```

By default, only vanilla settlements are imported. This is safer and more predictable.

### 6. Non-warband world actions now exist

`WorldWarService` now supports more than warbands:

- warband launch;
- colony founding;
- caravan transfer;
- scouting party intel;
- diplomat goodwill.

This moves Living World toward a broader faction activity model instead of a pure war simulator.

The conservation model is mostly good:

- caravan uses `TransferResource`, so resources move instead of being created;
- scouting writes intel, not pawns/resources;
- diplomacy changes goodwill, not physical population/resources;
- settler expansion moves existing adults into a new colony.

### 7. World-war notifications are rate-limited

The player receives occasional world-war letters instead of one letter per simulated day. This is important because catch-up simulation could otherwise spam the player.

The design is right:

```text
simulate all catch-up days
↓
send at most one aggregated letter
```

### 8. Documentation now honestly records open gaps

`docs/design/world-war-open-gaps.md` records what is done and what is not done:

- G1 done: scanner guard + Core defense-in-depth;
- G2 movement pruning done;
- G3 war-loop scale not done;
- C4 Core player-faction defense-in-depth done;
- C5 movement pruning done.

This is good process hygiene. It avoids the common problem where acceptance criteria are written once and then forgotten.

### 9. Legal/project policy is now clearer

The project now clearly states that existing mods are research/inspiration only. Living World does not copy code, XML, formulas, assets or text.

This is important because the project studies RimWar, Economics & Demography and Empire. The current documentation position is safer:

```text
Study ideas, learn lessons, write our own code.
```

---

## Current strengths

## 1. Ledger-first direction is real, not just documented

The project already has a ledger model where settlements, citizens, armies, ownership, resources, events and raid outcomes are stored in core state.

That is the correct foundation for the core promise:

```text
Nothing appears from nowhere.
```

The project should keep pushing this model: all gameplay systems should be transformations of ledger state.

## 2. Ownership model is a major architectural asset

Ownership is one of the most important parts of Living World.

The current model already supports:

```text
citizen -> settlement
citizen -> army
resource -> settlement
resource -> army/caravan later
asset transfer
resource transfer
```

This makes it possible to implement:

- real raiders;
- real caravans;
- real settlement stockpiles;
- real prisoners;
- real missing/dead outcomes;
- future loot and trade.

This should remain a core invariant.

## 3. Save/load discipline is already present

The project serializes complex state and has already shown backward compatibility discipline with optional fields like `missing`.

That is very important for RimWorld mods because saves live for a long time.

Keep this principle:

```text
Every schema change needs migration/backward compatibility behavior.
```

## 4. Tests appear to be growing with features

Recent commits mention 150+ tests and specific tests for world war, non-warband actions, player-settlement scanner guard, rate-limited letters, save/load and conservation.

This is essential. Living World is a simulation project. Bugs are often state-machine bugs, not visual bugs. Tests are not optional.

## 5. Compatibility awareness is good

The RimWar exclusion is a good example of correct compatibility strategy.

Living World should continue this approach:

- detect conflicting world-driver mods;
- avoid double-driving;
- prefer adapters over hard dependencies;
- do not silently overwrite another mod's world logic.

---

## Current weaknesses and risks

## 1. Core services are starting to become too broad

`WorldWarService` now does orchestration and also contains action execution details:

- launch warband;
- found colony;
- run caravan;
- run scouting party;
- run diplomat;
- select trade target;
- select scouting target;
- select diplomacy target;
- check in-flight armies;
- check cooldown.

This is still acceptable today, but it is becoming a service blob.

If this continues, future changes will become risky because every action will depend on the same orchestrator.

### Recommended split

```text
WorldWarService                  // orchestration only
WorldWarActionDispatcher          // maps plan -> executor
WarbandActionExecutor             // reserves citizens, dispatches army
SettlementExpansionExecutor       // founds colony from real adults
CaravanActionExecutor             // creates or transfers via caravan
ScoutingActionExecutor            // writes intel / later creates scout party
DiplomacyActionExecutor           // adjusts goodwill / later creates diplomatic mission
WorldWarTargetSelector            // target selection policy
WorldWarCooldownService           // cooldown rules
```

The orchestrator should become boring:

```csharp
var plans = planner.PlanDay(state, request);
foreach (var plan in plans)
{
    dispatcher.Execute(state, plan, request);
}
```

## 2. `WorldState` is still at risk of becoming a god class

`WorldState` owns a lot of different concerns:

- entity registry;
- settlements;
- citizens;
- armies;
- ownership;
- resources;
- raid links;
- outcomes;
- intel;
- events;
- migration state;
- faction data;
- movement data;
- validation.

For now, this is survivable because the project is still young. But the next major feature will likely make it too large.

### Recommended direction

Keep `WorldState` as the atomic state container and mutation gateway, but move domain logic into services.

Possible structure:

```text
LivingWorld.Core/State
  WorldState.cs
  WorldStateSnapshot.cs
  WorldStateCodec.cs

LivingWorld.Core/Population
  PopulationService.cs
  DemographyService.cs
  PopulationQueries.cs

LivingWorld.Core/Economy
  ResourceLedgerService.cs
  SettlementProductionService.cs
  TradeLedgerService.cs

LivingWorld.Core/Military
  RaidLifecycleService.cs
  ArmyMovementService.cs
  WorldBattleService.cs
  WarbandActionExecutor.cs

LivingWorld.Core/Diplomacy
  DiplomacyService.cs
  DiplomaticMissionExecutor.cs

LivingWorld.Core/WorldWar
  WorldWarService.cs
  FactionActionPlanner.cs
  WorldWarActionDispatcher.cs
```

## 3. G1 is solved in scanner and Core

The player settlement scanner guard was the first layer. C4 adds Core defense-in-depth.

Current status:

```text
Scanner excludes player settlements: DONE
Core never targets player faction: DONE
FactionLifecycle never collapses player faction: DONE
WorldBattleService blocks silent battle resolution: DONE
```

If another importer accidentally imports a player-owned world object, the Core layer should still refuse to target or collapse it.

### Implemented fix: C4

Add a player-faction concept to the ledger or request context:

```csharp
public string? PlayerFactionId { get; }
```

Then enforce:

```text
FactionActionPlanner: never choose PlayerFactionId as enemy target
WorldWarTargetSelector: never target PlayerFactionId
FactionLifecycleService: never collapse PlayerFactionId
WorldBattleService: never silently resolve battle against PlayerFactionId settlement
```

Tests now fail if the player faction can be targeted, collapsed or silently resolved
through a ledger battle.

## 4. Army movement pruning is implemented

The docs identified this as G2. If every launched army movement stayed forever,
long games would suffer:

- bigger saves;
- more memory;
- slower cooldown checks;
- slower UI summaries;
- slower history queries.

### Implemented fix: C5

Introduce retention policy:

```text
Keep:
- Traveling movements always
- Arrived/Resolved/Recalled movements for last N days

Prune:
- old resolved movement records
```

Historical information should live in `WorldEvent`, not forever in movement records.

Recommended API:

```csharp
public sealed record ArmyMovementPruneRequest(
    int CurrentTick,
    int RetentionDays);

public static class ArmyMovementPruneService
{
    public static ArmyMovementPruneResult Prune(WorldState state, ArmyMovementPruneRequest request);
}
```

Run it after world-war simulation or during save cleanup.

## 5. War-loop performance still depends on scans

The docs identify G3: war-loop scale is not solved yet.

Anything that repeatedly does LINQ over all citizens per faction per day will eventually become expensive.

This is not urgent at 200–1000 citizens. It matters at the real target scale:

```text
10,000–50,000 people
hundreds of settlements
multi-year saves
```

### Recommended fix: aggregates

Add cached aggregates updated by mutations:

```text
SettlementPopulationAggregate
FactionPopulationAggregate
SettlementMilitaryAggregate
FactionMilitaryAggregate
SettlementResourceAggregate
FactionResourceAggregate
```

Instead of:

```csharp
state.Citizens.Count(c => c.FactionId == faction && c.Status == Alive)
```

Use:

```csharp
state.Aggregates.GetFactionPower(factionId)
```

Important: aggregates must be validated against full scans in tests/debug mode.

## 6. Caravan is still not a persistent entity

Current caravan action is conservation-safe because it transfers resources from source to target. But it is not yet a real persistent caravan.

Current model:

```text
source settlement resource -> target settlement resource
```

Future Living World model should be:

```text
source settlement resource -> WorldCaravan inventory
source settlement citizens/animals -> WorldCaravan members
WorldCaravan travels
arrival: WorldCaravan inventory -> target settlement
if destroyed: members/goods lost or looted
```

This is important because the project philosophy says caravans should exist, travel and be vulnerable.

### Recommended future entity

```csharp
public sealed record WorldCaravan(
    EntityId Id,
    string FactionId,
    EntityId SourceSettlementId,
    EntityId TargetSettlementId,
    CaravanStatus Status,
    int DepartTick,
    int ArrivalTick);
```

It should own:

- citizens;
- animals later;
- resources;
- route/movement data.

## 7. Scouting and diplomacy currently do not reserve people

This is acceptable as an early abstraction, because they do not create people/resources. But long term, it should become more physical.

Future model:

```text
Scout party = 1–3 citizens temporarily leave settlement
Diplomatic mission = diplomat + optional guards
Mission can arrive, fail, return, disappear, be attacked
```

For now, keep them abstract. But document them as abstract world-level effects, not full persistent missions.

## 8. Drifters are philosophically sensitive

Drifter flow can easily violate "nothing appears from nowhere" if not handled carefully.

Current design likely uses target population and hard ceiling. That is useful for world recovery, but it must be framed as one of these:

1. initial world seeding;
2. off-map unknown population entering known world;
3. migration from untracked global population pool;
4. storyteller emergency fallback.

Best option:

```text
GlobalPopulationReservoir
```

Drifters should come from a finite abstract pool, not from infinite creation.

Recommended future model:

```csharp
WorldPopulationReservoir
  UnknownHumans
  UnknownAnimals
  OffMapMigrants
```

Then drifters are not created from nothing; they are materialized from the reservoir.

---

## Architectural recommendations

## 1. Freeze feature expansion briefly

Before adding ecology, diseases, families or deeper economy, finish the remaining stabilization tasks.

Recommended order:

```text
1. WorldWarService split
2. Population/faction aggregates
3. WorldCaravan entity
4. Save format migration/chunking review
```

## 2. Keep RimWorld layer thin

RimWorld integration should only:

```text
read RimWorld state/event
translate to LivingWorld command
call Core service
materialize/dematerialize
show UI/letters
save/load WorldState
```

It must not own simulation rules.

Good:

```csharp
WorldWarService.SimulateDay(State, request)
```

Bad:

```csharp
LivingWorldWorldComponent manually decides battle casualties or migration rules
```

## 3. Add invariants as tests, not just docs

The project already documents gaps well. The next step is enforcing each important acceptance rule with tests.

Rule:

```text
Every invariant in docs/design must have a test that fails without it.
```

Examples:

```text
Player faction is never silently targeted.
Player faction never collapses through world-war ledger simulation.
Movement pruning never removes active traveling movements.
Raid outcome totals always match Sent.
No resource transfer creates net resources.
No colony founding creates new citizens.
```

## 4. Separate history from active simulation state

`WorldEvent` is history. `ArmyMovement` is active simulation state.

Do not keep old active-state objects forever just because the UI needs history. Convert resolved actions into history events, then prune active objects after retention.

This principle will also apply later to:

- caravans;
- scout missions;
- diplomatic missions;
- disease outbreaks;
- animal migrations;
- wars.

## 5. Introduce explicit simulation phases

Right now `SimulateWorldDay` order is readable, but as systems grow it should become formal.

Possible enum:

```csharp
public enum WorldSimulationPhase
{
    Production,
    Consumption,
    Demography,
    Migration,
    Drifters,
    MilitaryMovement,
    Battles,
    FactionPlanning,
    Diplomacy,
    Collapse,
    Cleanup
}
```

Then the daily loop becomes easier to audit.

---

## Recommended next tasks

## Task 1 — C4: Core player-faction defense-in-depth (done)

### Goal

Even if a player settlement somehow enters the ledger, Core must not silently target, capture or collapse it.

### Required changes

- Add player faction identity to `WorldState` or simulation request.
- Exclude player faction in target selection.
- Exclude player faction in collapse logic.
- Prevent silent `WorldBattleService.Resolve` against player-owned settlement.

### Acceptance tests

- NPC war planner never selects player faction settlement.
- World battle against player settlement returns blocked/manual-materialization-required.
- Faction lifecycle never collapses player faction.
- Scanner still excludes player settlement.

## Task 2 — C5: Army movement pruning (done)

### Goal

Prevent unbounded movement/save growth.

### Required changes

- Add movement retention setting.
- Prune old resolved movements.
- Keep traveling movements.
- Keep recent resolved movements for UI/cooldown.
- Ensure save/load remains compatible.

### Acceptance tests

- traveling movement is never pruned;
- old arrived/disbanded movement is pruned;
- recent movement remains;
- history event remains after movement is pruned.

## Task 3 — Split world-war executors

### Goal

Prevent `WorldWarService` from becoming a god service.

### Required changes

Create:

```text
WarbandActionExecutor
SettlementExpansionExecutor
CaravanActionExecutor
ScoutingActionExecutor
DiplomacyActionExecutor
WorldWarActionDispatcher
WorldWarTargetSelector
```

`WorldWarService` should orchestrate only.

### Acceptance tests

Existing world-war tests should pass unchanged.

## Task 4 — Add settlement/faction aggregates

### Goal

Prepare for large populations.

### Required changes

- Settlement population aggregate.
- Faction population aggregate.
- Settlement combat-capable aggregate.
- Faction military power aggregate.
- Resource aggregate later.

### Acceptance tests

- aggregate matches full scan after creation;
- aggregate matches after death;
- aggregate matches after migration;
- aggregate matches after army reservation;
- aggregate matches after return/missing/prisoner.

## Task 5 — Design persistent WorldCaravan

### Goal

Turn caravan from instant transfer into real entity.

### Required changes

- Add `WorldCaravan` entity.
- Add caravan inventory ownership.
- Add caravan movement.
- Add arrival resolution.
- Later: caravan attack/loss/loot.

### Acceptance tests

- resources move settlement -> caravan -> target;
- no resources created;
- caravan can be destroyed and goods are lost/looted;
- save/load preserves caravan.

---

## Current risk matrix

| Risk | Severity | Status | Recommendation |
|---|---:|---|---|
| Core can still target/collapse player if imported by non-vanilla path | Medium | Closed by C4 | Keep tests around planner/lifecycle/battle guard |
| `_armyMovements` grows forever | Medium | Closed by C5 | Monitor retention value during long-play saves |
| WorldWarService grows too broad | Medium | Emerging | Split executors |
| War-loop scans citizens daily | Medium later | Open | Add aggregates |
| Caravan is instant transfer | Low now / High later | Accepted abstraction | Design WorldCaravan |
| Drifters may feel like magic spawn | Medium | Needs framing | Add finite reservoir |
| Save format may grow too large | Medium later | Not urgent | Chunk/compress/migrate later |
| UI remains debug-heavy | Low | Acceptable for now | Split player UI/dev UI later |

---

## Final recommendation

Do not add ecology, disease, full economy chains or family trees yet.

The next milestone should be called something like:

```text
Milestone 1.1 — Core hardening and world-war stabilization
```

It should complete:

```text
C4 — Player faction defense-in-depth (done)
C5 — Army movement pruning (done)
C6 — WorldWarService split
C7 — Population/faction aggregates foundation
```

After that, the project will be ready for the next real feature layer:

```text
Persistent caravans
```

Persistent caravans are the best next gameplay system because they connect all core ideas:

- people;
- resources;
- ownership;
- movement;
- risk;
- trade;
- world events;
- materialization later.

---

## One-line verdict

Living World is now on the right path: the foundation is strong, but the next work must be hardening and modularization, not feature sprawl.
