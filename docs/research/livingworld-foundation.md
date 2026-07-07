# Living World Foundation Architecture

Date: 2026-07-07

Basis:

- [Rim War analysis](rimwar-analysis.md)
- [Economics & Demography analysis](economics-analysis.md)
- [Empire Refactored analysis](empire-analysis.md)
- [Upstream mod research notes](upstream_mods.md)
- [World Ownership](../world_ownership.md)

This document is the foundation for further Living World development.

## Final Source Of Truth

Living World Core is the source of truth.

The root object is `WorldState`, persisted by Living World and exposed through `ILivingWorldApi`.

Source-of-truth entities:

- `WorldCitizen`: individual lightweight humanlike person.
- `WorldAnimal`: individual or cohort-based animal record, depending on species scale.
- `WorldSettlement`: settlement identity, tile, faction, visibility, capacity, production profile.
- `WorldFactionState`: faction-level strategy, diplomacy, war fatigue, technology, known intel.
- `WorldHousehold`: family/household ownership and relationships.
- `WorldArmy`: mobile military group with real members and supplies.
- `WorldCaravan`: mobile trade/migration group with real members, animals and cargo.
- `OwnedResourceStack`: resource quantity owned by settlement, faction, household, caravan, army or citizen.
- `WorldEvent`: append-only history record.
- `PawnLink`: temporary link between a `WorldCitizen`/`WorldAnimal` and materialized RimWorld `Pawn`.

Derived state:

- population totals;
- settlement strength;
- faction power;
- economy summary;
- visible UI intel;
- trade value;
- raid points;
- diplomacy pressure.

These are caches or projections, not truth.

## Original Systems

Living World must implement these as original systems:

- identity-first population;
- event-sourced history;
- world ownership ledger;
- deterministic scheduler;
- settlement production;
- per-settlement resource storage;
- migration/refugee flow;
- raid/army allocation from real citizens and resources;
- pawn materialization/dematerialization;
- player knowledge/intel visibility;
- ecology and animal population;
- public API and compatibility adapters.

## Ideas Borrowed

From Rim War:

- visible strategic world objects;
- warbands/scouts/traders/settlers/diplomats as world-map actors;
- settlement scan ranges;
- player heat/aggression;
- battle sites;
- strategic action cadence;
- incoming threat notifications;
- pathing and path-pool pressure lessons.

Do not borrow:

- `RimWarPoints` as source of truth;
- broad incident suppression;
- global faction relation replacement;
- direct dependency on RimWar internals.

From Economics & Demography:

- virtual stockpile concept;
- daily/monthly/yearly update cadence;
- tech/biome/terrain production formulas;
- inflation/market pressure ideas;
- starvation/collapse/expansion/rebirth ideas;
- raid cost/debt accounting;
- trade session boundaries;
- pawn death/faction-change/exit hooks.

Do not borrow:

- faction aggregate population as truth;
- global market value patch as initial design;
- broad map generation spawning;
- reflection-based RimWar compatibility.

From Empire Refactored:

- settlement resource profiles;
- terrain, biome, mutator, landmark and building modifiers;
- policy/edict modifiers;
- settlement event manager;
- cached stat/profit calculations;
- military cooldowns and settlement battle consequences;
- explicit compatibility patch folders/adapters.

Do not borrow:

- abstract workers as citizens;
- player-empire-only model as global model;
- custom `Settlement` subclass as universal representation;
- wide faction goodwill replacement.

## Required Harmony Patches

Living World should use narrow, scoped patches. Every patch must have:

- owner service;
- idempotency guard;
- compatibility notes;
- tests or source static assertions;
- logging only under debug settings where possible.

Initial required patches:

| Vanilla Method | Living World Use | Risk | Rule |
|---|---|---:|---|
| `Page_CreateWorldParams.DoWindowContents` | World generation settings | Low/Medium | keep compact, do not assume button order |
| `Settlement.GetInspectString` | Player-visible intel and loaded-map pawn count | Medium | no exact hidden data unless known |
| `IncidentWorker_RaidEnemy.TryGenerateRaidInfo` or nearest stable raid generation point | Reserve real citizens/resources and cap raid strength | High | narrow to enemy raids, guard same parms |
| `PawnGenerator.GeneratePawn` | attach generated pawn to reserved citizen when active reservation exists | High | only during Living World materialization scope |
| `Pawn.Kill` | record citizen/animal death | High | idempotent citizen status transition |
| `Pawn.SetFaction` | capture/recruit/defection ownership transfer | High | only linked pawns |
| `Pawn.ExitMap` / `Pawn.DeSpawn` | return, prisoner, escaped, dead, missing outcomes | High | distinguish downed, prisoner, despawn, caravan |
| `TradeSession.SetupWith` | start trade intel/resource transaction context | Medium | observe first, mutate later |
| `TradeDeal.TryExecute` / `Dialog_Trade.Close` | record trade outcome and intel leaks | Medium | no price patch initially |
| `IncidentWorker_TraderCaravanArrival.TryExecuteWorker` | materialize trader caravan from real stock | Medium/High | phase 2, after ownership stable |
| `WorldObject.Destroy` or settlement-specific destruction hook | record settlement destruction and ownership consequences | High | avoid blocking vanilla unless scoped |
| `WorldObjectsHolder.Add/Remove` | maintain world object cache/bootstrap signals | Medium | postfix only |

Avoid initially:

- `Faction.TryAffectGoodwillWith` global prefix;
- `Faction.RelationWith` replacement;
- `SettlementDefeatUtility.CheckDefeated` replacement;
- `MapGenerator.GenerateMap` broad civilian/loot spawning;
- `StatWorker_MarketValue.GetValueUnfinalized` dynamic price patch;
- transpilng settlement generation unless no stable boundary exists.

## Integration Points

Public API:

```csharp
ILivingWorldApi
  GetCitizen(EntityId id)
  GetSettlement(EntityId id)
  GetSettlementByWorldObject(WorldObject worldObject)
  GetPopulation(EntityId settlementId)
  GetKnownSettlementInfo(EntityId settlementId)
  GetOwnedResources(WorldOwnerId owner)
  CreateMigration(...)
  ReserveRaid(...)
  RecordTrade(...)
  KillCitizen(...)
  Subscribe(...)
```

Adapters:

- RimWar adapter:
  - reads Living World faction/settlement/army power;
  - may mirror `WorldArmy` as RimWar-like visible world object;
  - must not own Living World population.
- E&D adapter:
  - reads Living World population and stockpile aggregates;
  - disables or redirects overlapping E&D population growth/loss where possible;
  - can contribute economic formulas/UI only through API.
- Empire adapter:
  - maps `WorldSettlementFC` resources/workers to Living World settlement profile;
  - lets Empire settlement battles consume/affect Living World population/resources when enabled;
  - treats Empire as a client of Living World ownership, not the owner.

## Conflict Avoidance

Rules:

1. Living World owns hidden state; UI shows estimates unless intel says exact.
2. Never spawn all world people as `Pawn`.
3. Use reservation scopes when vanilla generation is about to happen.
4. Link only pawns created under a known scope.
5. All pawn lifecycle patches must be idempotent.
6. Do not replace global faction diplomacy in the first versions.
7. Do not patch dynamic market value until economy is mature.
8. Prefer postfix observation before prefix suppression.
9. Use mod detection and feature flags for RimWar/E&D/Empire adapters.
10. Save data must survive missing optional mods.

Compatibility surface by system:

| System | Primary owner | External mods may do | Living World stance |
|---|---|---|---|
| Population identity | Living World | read aggregates through API | no external owner |
| Settlement resources | Living World | contribute modifiers/read stock | no parallel hidden stock owner |
| Visible armies | Living World | display/mirror/target | no hidden point-only armies |
| Trade intel | Living World | add sources/effects | record source and confidence |
| Diplomacy | Living World eventually | vanilla goodwill still active early | observe first, replace later |
| Active-map pawns | RimWorld + Living World links | mods may alter pawns | lifecycle patches must handle changes |

## First Modules To Build

Priority order:

1. `LivingWorld.Core`
   - `WorldState`, IDs, event store, ownership ledger, deterministic random.
2. `LivingWorld.Persistence`
   - save/load with versioning and migrations.
3. `LivingWorld.World`
   - settlement/faction import from RimWorld world objects.
4. `LivingWorld.Population`
   - citizens, households, cohorts, citizen queries.
5. `LivingWorld.Military`
   - raid reservation, army ownership, pawn binding, outcome service.
6. `LivingWorld.Intel`
   - player knowledge, source confidence, hidden/exact values.
7. `LivingWorld.Economy`
   - per-settlement stockpiles, production profiles, trade transfers.
8. `LivingWorld.Demography`
   - births, aging, deaths, families, migration.
9. `LivingWorld.Ecology`
   - animal population, migration, reproduction, carrying capacity.
10. `LivingWorld.Diplomacy`
   - wars, alliances, vassals, treaties, reputation.
11. `LivingWorld.Adapters`
   - RimWar, E&D, Empire adapters after core ownership is stable.

## Production Model Direction

Production must not depend only on population count.

Inputs:

- settlement population by profession/age/health;
- terrain and biome;
- hilliness, rainfall, temperature, growing season;
- tile mutators and landmarks;
- faction technology;
- buildings/infrastructure;
- owned tools/animals;
- security and war pressure;
- trade access and road distance;
- policy/culture modifiers;
- current stockpile inputs.

Outputs:

- owned resource stacks;
- waste/loss/spoilage;
- trade surplus;
- migration pressure;
- famine/stability pressure.

## Visibility And Intel Direction

Information must be discovered, not automatically omniscient.

Intel sources:

- public settlement disclosure;
- direct visit/scout;
- trader rumor;
- prisoner interrogation;
- refugee reports;
- battle survivors;
- comms diplomacy;
- sensors/tech later.

Intel stores:

- source;
- confidence;
- tick;
- population band, not exact count by default;
- food status band;
- migration/refugee pressure;
- military readiness band;
- trade goods hints;
- whether exact values are visible.

Player UI should always distinguish:

- known/rumored ledger information;
- actual loaded-map visible pawns;
- debug-only exact values.

## Development Principle

Every feature must produce a real gameplay consequence:

- Raid kills reduce real citizens.
- Destroyed caravans remove real people/resources/animals.
- Trade can leak intel.
- Food shortages block births, create deaths/refugees/migration.
- Production depends on terrain, tech, labor and resources.
- Settlement destruction transfers, destroys or displaces ownership.

If a feature only changes a debug number and cannot affect future vanilla gameplay, it should stay out of the playable path.
