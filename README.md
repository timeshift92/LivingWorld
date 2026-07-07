# Living World

**Living World** is an open-source RimWorld mod project focused on persistent world simulation.

> Nothing appears from nowhere.

The goal is to replace RimWorld's "magic spawn" world model with a persistent simulation where every person, animal, settlement, army, caravan and trader belongs to an existing world state.

Living World is not planned as a small raid overhaul. It is intended to become a standalone simulation core for the planet layer of RimWorld.

---

## Core idea

In vanilla RimWorld, many events are generated on demand:

- raids generate attackers;
- traders generate goods;
- caravans appear as incidents;
- world pawns often exist only when the game needs them;
- settlements are mostly abstract points on the map.

Living World aims to change this philosophy.

Instead of generating entities from nowhere, the simulation should select existing entities from the world ledger.

Example:

```text
Pirate settlement #14
Population: 736
Available fighters: 124
Raid selected: 58 fighters
Killed in battle: 7
Returned home: 51
New available fighters: 117
Population: 729
```

A caravan should not be a temporary event. It should be a real group of people, animals and goods sent from a real settlement. If the caravan is destroyed, those people, animals and goods are lost permanently.

---

## Design principles

### 1. Persistent world

The world has a persistent state that survives between events and saves.

Every important world entity must have an identity:

- humans;
- animals;
- settlements;
- factions;
- armies;
- caravans;
- traders;
- resources;
- historical events.

### 2. Ledger-first simulation

The source of truth is the ledger, not generated incidents.

Events are projections of the ledger:

```text
WorldState -> Simulation System -> World Event -> Materialized RimWorld objects
```

### 3. No magic spawning

Living World should avoid creating new pawns, animals, goods or armies without accounting for where they came from.

Vanilla generation can still be used as a materialization mechanism, but the result must be tied back to world entities.

### 4. Materialization / dematerialization

Most of the world should not run full pawn AI all the time.

Living World separates:

- **abstract persistent entities** stored in `WorldState`;
- **materialized RimWorld pawns/things/world objects** created only when needed.

When a raid, caravan, battle or settlement map becomes active, abstract entities are materialized. When they leave the active map, the result is written back into the ledger.

### 5. Deterministic simulation

The simulation should be reproducible where possible.

Systems should use stable IDs, controlled random seeds, scheduled updates and event logs.

### 6. Layered simulation

Living World should support multiple simulation levels:

| Level | Scope | Simulation detail |
|---|---|---|
| Active | Player map and currently loaded maps | Full RimWorld logic |
| Regional | Nearby settlements, caravans and conflicts | Simplified periodic logic |
| Global | Rest of the planet | Aggregate simulation only |

This is required for performance. The project should support thousands or tens of thousands of world entities without running full AI for all of them.

---

## Planned systems

### LivingWorld.Core

The core module owns the world state and the simulation scheduler.

Responsibilities:

- stable IDs;
- world entity registry;
- save/load;
- event log;
- deterministic random streams;
- simulation ticks;
- materialization contracts;
- compatibility API.

### Population & Demography

Tracks human population across factions and settlements.

Planned data:

- person ID;
- age;
- sex;
- family links;
- faction;
- settlement;
- profession;
- health state;
- combat eligibility;
- migration state.

Planned mechanics:

- births;
- deaths;
- aging;
- migration;
- family impact;
- disease impact;
- settlement abandonment;
- faction decline and recovery.

### Economy

Tracks production, consumption, storage and trade.

Planned mechanics:

- settlement production;
- food consumption;
- goods storage;
- trade surpluses;
- finite silver mode;
- item category valuation;
- settlement loot based on actual storage;
- caravan goods tied to origin settlement.

### Military

Tracks armies, raids, warbands, scouts and losses.

Planned mechanics:

- persistent armies;
- real recruitment from settlement populations;
- raid size limited by available fighters;
- battlefield losses written back into the ledger;
- army movement on the world map;
- settlement defense strength;
- reinforcement logic;
- long-term war exhaustion.

### Diplomacy

Tracks faction relationships and long-term strategy.

Planned mechanics:

- wars;
- peace;
- alliances;
- tribute;
- vassalage;
- faction collapse;
- civil wars;
- strategic decisions based on population, economy and military capacity.

### Ecology

Tracks animal populations and ecosystem pressure.

Planned mechanics:

- regional animal populations;
- births and deaths;
- predator/prey pressure;
- migration;
- overhunting consequences;
- biome-based carrying capacity.

### World History

Records important world events.

Examples:

- wars;
- epidemics;
- famines;
- settlement founding;
- settlement destruction;
- population crashes;
- major battles;
- faction collapse.

---

## Research sources and inspiration

Living World is a standalone project. It is **not** intended to be built on top of existing mods.

The following mods are used as research sources for architecture, design lessons and feature comparison only.

No code, art assets, XML definitions, text, compiled DLLs or proprietary resources should be copied into Living World unless the license explicitly allows it and the project deliberately accepts the license consequences.

### RimWar Threaded

Repository:

https://github.com/TorannD/RimWar---Threaded

License:

- MIT License.
- This is permissive, but Living World still should not copy code unless there is a clear reason.
- If any code is ever reused, the MIT copyright notice and license text must be preserved.

Useful ideas:

- planet-layer activity;
- `WorldComponent`-based simulation;
- moving world objects such as warbands, scouts, diplomats and traders;
- settlement power/points as a cheap abstraction;
- periodic world updates;
- faction actions selected from weighted choices;
- settlement reinforcement logic;
- interaction between caravans and war objects;
- world object pathing and detection concepts.

What not to copy directly:

- point-based settlement strength as the only truth;
- generated pawns as final source of truth;
- tight Harmony coupling to many vanilla methods;
- world simulation that is not ledger-first;
- architecture where war objects own too much mutable gameplay state.

How Living World should improve on it:

- replace `points-first` with `ledger-first`;
- store real population and resources behind military strength;
- separate simulation data from RimWorld materialized objects;
- make Harmony patches thin adapters rather than core logic;
- make military losses affect actual population and economy.

### Economics & Demography

Repository:

https://github.com/helldanpwnz/Economics-and-Demography

License status:

- No `LICENSE` file was found in the repository during research.
- No explicit license statement was found in the README during research.
- Treat the project as **source-available but not reusable** unless the author adds a license or gives permission.
- Do not copy code, XML, text, balancing formulas or assets from this project.

Useful ideas:

- population counters per settlement/faction;
- adults/children/elders as demographic groups;
- births and deaths affected by tech level and resources;
- migration/desertion based on living conditions;
- faction expansion when population/resources are sufficient;
- settlement abandonment when population collapses;
- raids limited by actual population and demographics;
- daily production and consumption;
- settlement storage;
- persistent goods;
- faction-to-faction trade;
- finite money / gold standard option;
- global inflation/homeostasis model;
- low-TPS background simulation.

What not to copy directly:

- aggregate counters as the only population model;
- formulas without independent redesign;
- any source code or data tables while license is unclear.

How Living World should improve on it:

- use aggregate counters only for global/regional simulation layers;
- keep a path toward real individual people in the ledger;
- allow lazy generation/materialization of detailed people from demographic cohorts;
- connect economy directly to raids, migration, diplomacy and ecology;
- make all economic changes traceable through event sourcing.

### Empire Refactored

Steam Workshop:

https://steamcommunity.com/workshop/filedetails/?id=3701480464

GitHub:

https://github.com/matathias/Empire-1_6-Continued

License:

- GNU General Public License v3.0.
- GPL-3.0 is copyleft.
- Do not copy GPL code into Living World unless Living World intentionally adopts GPL-compatible licensing for the affected work.
- It is safe to study ideas and public behavior, but implementation should be original.

Useful ideas:

- player-controlled colony/vassal layer;
- taxes paid in silver or goods;
- colony events with player choices;
- edicts/policies split into social, tax and military categories;
- squads as configurable military units;
- manual defense battles;
- in-game codex/help system;
- XML-driven extensibility;
- settlement/resource types defined by XML;
- submod-friendly architecture designed to avoid Harmony patching.

What not to copy directly:

- GPL code unless the project accepts GPL implications;
- icons, banners, UI graphics, faction flags or other assets;
- exact XML schema or implementation classes;
- player-empire gameplay as the core model of Living World.

How Living World should improve on it:

- support player colonies as one possible projection of the global world state;
- make taxes and goods come from real settlement storage;
- make squads consume real population, equipment and wages;
- provide a clean public API for other mods;
- prefer data-driven definitions and adapters over large Harmony patch sets.

---

## Licensing policy for Living World

Living World should be developed as an original implementation.

Rules:

1. Ideas, mechanics and architectural lessons can be studied.
2. Code must not be copied unless the license allows it and the project explicitly accepts the consequences.
3. Art assets must not be copied from other mods.
4. XML definitions must not be copied from other mods unless explicitly permitted.
5. Public documentation should credit research sources.
6. If GPL code is used, the affected project/license strategy must be reviewed before merging.
7. If a repository has no license, treat it as copyrighted and not reusable.

Recommended license for Living World:

- **MIT** if the goal is maximum reuse and permissive open source.
- **GPL-3.0** only if the project intentionally wants strong copyleft.
- **MPL-2.0** if the project wants a middle ground: file-level copyleft while allowing broader integration.

Current recommendation: **MIT for code**, with a separate clear rule that generated art/assets are project-owned and should not be reused without permission unless later relicensed.

---

## Architecture direction

Living World should not be a compatibility patch over RimWar, Economics & Demography or Empire Refactored.

It should become a standalone core with adapters.

Proposed structure:

```text
LivingWorld.Core
  WorldState
  EntityRegistry
  EventLog
  SimulationClock
  DeterministicRandom
  SaveLoad

LivingWorld.Population
  People
  Cohorts
  Families
  BirthDeathMigration

LivingWorld.Economy
  Resources
  Production
  Consumption
  Storage
  Trade
  Inflation

LivingWorld.Military
  Armies
  Raids
  Scouts
  Reinforcements
  Losses

LivingWorld.Diplomacy
  Relations
  Wars
  Peace
  Vassals
  Treaties

LivingWorld.Ecology
  AnimalPopulations
  Migration
  PredatorPrey
  CarryingCapacity

LivingWorld.RimWorldAdapter
  HarmonyPatches
  Materialization
  Dematerialization
  VanillaIncidentBridge
  UI
```

---

## Milestone 1: MVP proposal

The first milestone should not attempt to simulate everything.

Recommended MVP:

1. `WorldState` saved in `WorldComponent`.
2. Stable IDs for settlements and factions.
3. Settlement population counters.
4. Simple daily demographic update.
5. Raid budget limited by faction/settlement population.
6. Losses from raids written back into population.
7. Debug UI showing settlement population and military pool.
8. Event log for births, deaths, raids and losses.

MVP rule:

```text
No raid should be allowed to happen without being accounted for in world state.
```

---

## Project status

Early research and architecture phase.

Current focus:

- reverse engineering major RimWorld world-simulation mods;
- defining a safe original architecture;
- preparing `LivingWorld.Core` MVP;
- establishing branding and documentation.

---

## Disclaimer

Living World is an independent RimWorld mod project.

RimWorld is developed by Ludeon Studios. Existing mods mentioned in this README belong to their respective authors. They are referenced only for research, comparison and credit.