# Rim War Reverse Engineering Analysis

Date: 2026-07-07

Scope:

- Installed mod: `C:\Games\RimWorld\Mods\Rim War`
- Source reference: `tools/reference-src/RimWar---Threaded`
- Local version metadata: Rim War `0.9.9.8`, package id `Torann.RimWar`, RimWorld `1.6`

This document is a research input for Living World. Rim War is not a dependency target for Living World Core.

## 1. Project Overview

Rim War adds a strategic layer where factions generate visible world-map activity: warbands, scouts, traders, settlers, diplomats, launched warbands, battle sites, faction power, aggression heat, capitals, and faction victory pressure.

Main subsystems:

- `RimWar.Planet`: world objects, settlement comps, movement, combat resolution, world components.
- `RimWar.Harmony`: vanilla incident, map generation, goodwill, settlement, world generation, and comms patches.
- `RimWar.Options`: mod settings and world generation settings UI.
- `RimWar.Utility`: incident workers, faction dialog replacement, arrival estimation, alerts, debug tools.
- `RimWar.RocketTools`: optional task scheduler/threading helper.
- XML defs under `v1.6/Defs`: `RW_WorldObjects`, `RimWarDef`, damages, letters, main button.

Main namespaces:

- `RimWar`
- `RimWar.Planet`
- `RimWar.Harmony`
- `RimWar.Options`
- `RimWar.Utility`
- `RimWar.RocketTools`

Dependencies:

- RimWorld / Verse
- Harmony
- UnityEngine
- Historical HugsLib dependency existed in older versions, but local `v1.6` metadata only requires Harmony.

## 2. Source Structure

Important source areas:

```text
Source/RimWar/
|-- Base.cs
|-- RimWarData.cs
|-- RimWarDef.cs
|-- RimWarDefData.cs
|-- RimWarFactionUtility.cs
|-- RimWarMatPool.cs
|
|-- Harmony/
|   |-- HarmonyPatches.cs
|   `-- HarmonyPatchesExtras.cs
|
|-- Options/
|   |-- RimWarSettingsWindow.cs
|   `-- Settings.cs
|
|-- Planet/
|   |-- WorldComponent_PowerTracker.cs
|   |-- WorldComponent_IncidentTracker.cs
|   |-- WorldComponentMod.cs
|   |-- RimWarSettlementComp.cs
|   |-- RimWarCaravanComp.cs
|   |-- WarObject.cs
|   |-- WarObject_PathFollower.cs
|   |-- WarObject_Tweener.cs
|   |-- Warband.cs
|   |-- LaunchedWarband.cs
|   |-- Scout.cs
|   |-- Trader.cs
|   |-- Settler.cs
|   |-- Diplomat.cs
|   |-- BattleSite.cs
|   |-- RimWarSite.cs
|   |-- IncidentUtility.cs
|   |-- WorldUtility.cs
|   `-- SettlementUtility.cs
|
|-- RocketTools/
`-- Utility/
```

Module relationship:

```text
WorldComponent_PowerTracker
  owns RimWarData per faction
  schedules global actions
  creates WarObject subclasses

RimWarSettlementComp
  attaches to vanilla Settlement
  stores RimWarPoints, heat, scan caches, attackers
  exposes settlement gizmos

WarObject subclasses
  represent mobile strategic units
  own path follower/tweener
  resolve arrival/scan/combat

HarmonyPatches
  connect vanilla incidents, map generation, goodwill, comms and settlement UI
```

## 3. World State Ownership

Rim War's source of truth is not individual people. It is:

- `WorldComponent_PowerTracker.rimwarData`: faction-level strategic state.
- `RimWarData`: faction behavior, active settlements, faction relations, power state.
- `RimWarSettlementComp`: per-settlement `RimWarPoints`, point damage, heat, event ticks, scan caches, attackers.
- `WarObject`: mobile world objects with `RimWarPoints`, `PointDamage`, parent settlement, destination, path follower, optional referenced pawns.

How objects relate:

- Settlements are vanilla `RimWorld.Planet.Settlement` objects with `RimWarSettlementComp`.
- Factions are vanilla `Faction` plus `RimWarData`.
- Armies are `WarObject` subclasses, not persistent citizen lists.
- Pawns are materialized at encounter/map boundaries and may be referenced by war objects, but are not the core population.
- Caravans can be vanilla `Caravan` plus `RimWarCaravanComp`, or Rim War `Trader` world objects.
- Economy is represented mostly as `RimWarPoints`, goodwill, and generated trade/war objects, not a real stockpile ledger.

For Living World this means Rim War is a strategic-action reference, not a population/economy owner.

## 4. Update Loop

`WorldComponent_PowerTracker.WorldComponentTick` is the global scheduler.

Observed cadence:

- On tick >= 10: initializes Rim War data.
- Every 60 ticks: adjusts caravan targets.
- Every `heatFrequency`: updates player aggression.
- Every `rwdUpdateFrequency`: checks new factions and updates faction state, optionally via `RocketTasker`.
- Every 60,000 ticks: global Rim War data action.
- At `nextEvaluationTick`: chooses a faction/settlement and creates one weighted settlement action.
- Every tick while threading enabled: `tasker.Tick()`.

Settlement component loop:

- `RimWarSettlementComp.CompTick`
- Every 2,500 ticks for non-player factions:
  - If no map is loaded and attackers exist, resolve abstract settlement combat.
  - If a map is loaded, update active unit combat status from spawned pawns.

World object loop:

- `WarObject.TickInterval`
- Periodically:
  - heals/decays point damage;
  - scans nearby world objects/caravans/sites;
  - validates parent/destination;
  - moves using `WarObject_PathFollower`;
  - runs arrival action when destination reached.

Queues/schedulers:

- `RocketTasker<ContextStorage>` is used for optional off-main-thread calculation and main-thread callbacks.
- `nextEventTick`, `nextCombatTick`, `nextSettlementScan`, `NextMoveTick`, `NextSearchTick` are local schedulers.

## 5. Harmony Analysis

Rim War has a high-conflict patch surface because it deliberately replaces random vanilla world events with visible strategic actions.

Key patches:

| Patch class / method | Vanilla method | Type | Purpose | Conflict risk | Living World relevance |
|---|---|---:|---|---:|---|
| `Prevent_IsDefeated_Patch` | `SettlementDefeatUtility.IsDefeated` | Prefix | Prevent friendly reinforced settlement defeat while humanlike defenders remain | High | Avoid direct replacement; use outcome tracking instead |
| `Prevent_AffectRelationsOnAttacked_Patch` | `SettlementUtility.AffectRelationsOnAttacked` | Prefix | Suppress goodwill loss for reinforcement cases | Medium | Similar need, but use scoped flags |
| manual patch | `FactionGiftUtility.GiveGift*` | Prefix | Convert gifts into RimWar points | Medium | Living World should convert gifts into owned resources/reputation |
| manual patch | transport pod/shuttle arrival actions | Postfix | Add reinforce options and spawn attackers | High | Useful concept; avoid broad arrival patches unless scoped |
| manual patch | `CaravanExitMapUtility.ExitMapAndCreateCaravan` | Prefix | Resolve post-battle reinforcement results | High | Living World needs dematerialization hooks |
| manual patch | `CaravanEnterMapUtility.Enter` / attack map entry | Postfix | Apply prior point damage and generate attackers | High | Similar boundary hook needed |
| manual patch | `GenStep_Settlement.ScatterAt` | Prefix | Capture settlement points for map generation | High | Avoid mutating vanilla map generation broadly |
| manual patch | `BaseGen.SymbolStack.Push` | Prefix | Override `pawnGroupMakerParams.points` from RimWar points | High | Living World should materialize specific citizens, not points |
| manual patch | `WorldPathPool.GetEmptyWorldPath` | Prefix | Prevent path pool leak with many war objects | Medium | Good lesson for many world objects |
| `FactionRelationCheck_Patch` | `Faction.RelationWith` | Prefix | Force missing faction relation creation | High | Avoid replacing core faction relation method |
| `RemoveFaction_Patch` | `FactionManager.Remove` | Postfix | Remove RimWar faction data | Medium | Need cleanup hooks |
| `IncidentQueueAdd_Replacement_Prefix` | `IncidentQueue.Add` | Prefix | Replace queued trader caravan with visible Rim War trader | High | Living World may need a safer incident-planning API |
| `IncidentWorker_Prefix` | `IncidentWorker.TryExecute` | Prefix | Ensure missing def is raid def | High | Avoid unless fixing vanilla crash |
| `Settlement_InspectString_WithPoints_Postfix` | `Settlement.GetInspectString` | Postfix | Add RimWar points, behavior, heat | Medium | Living World already patches this; ordering matters |
| `CanFireNow_*_RemovalPatch_Prefix` | `IncidentWorker_*CanFireNowSub` | Prefix | Suppress random ambush/caravan/pawn arrival incidents | High | Living World should replace only targeted incidents |
| `Patch_Page_CreateWorldParams_DoWindowContents` | `Page_CreateWorldParams.DoWindowContents` | Postfix | Add world-gen settings button | Low/Medium | Living World uses same UI area; coordinate layout |
| `CommsConsole_RimWarOptions_Patch` | `FactionDialogMaker.FactionDialogFor` | Postfix | Remove/replace trade/military aid options | Medium | Trade intel can hook here but should not remove vanilla lightly |
| `SettlementProximity_NoVassalDegradation_Patch` | `SettlementProximityGoodwillUtility.AppendProximityGoodwillOffsets` | Postfix | Exclude vassals from proximity degradation | Medium | Diplomacy module may need similar but API-based |

Cross-mod overlap table:

| Vanilla Method | Rim War | Empire | Economics | Living World recommendation |
|---|---|---|---|---|
| `Settlement.GetInspectString` | Adds points/heat | May add settlement UI through custom object/gizmos | no direct inspect patch observed | Append concise Living World intel, cache, avoid exact hidden values |
| `Page_CreateWorldParams.DoWindowContents` | Adds Rim War settings button | no core patch observed | no patch observed | Keep button layout compact and independent |
| `IncidentWorker.TryExecute` | Fixes/redirects incident execution | no direct core patch | Tracks raid costs | Living World should reserve assets before vanilla generation |
| `IncidentWorker_Raid/TryExecuteWorker` | suppresses random events via CanFireNow hooks | friendly raid faction logic | blocks/costs raids | Living World should patch raid generation narrowly |
| `PawnGenerator.GeneratePawn` | indirect through incidents | custom pawn generation settings | gender/raid cost tracking | Living World should link materialized pawn to citizen |
| `Pawn.Kill/DeSpawn/ExitMap` | used indirectly for battles | battle cleanup/capture handling | pop loss/gear return | Living World must own lifecycle binding and be idempotent |
| `MapGenerator.GenerateMap` | settlement points affect generated pawns | settlement maps for Empire | spawn civilians/loot, protection | Living World should avoid heavy map-gen ownership unless materializing settlement |
| `WorldObject.Destroy/SetFaction` | cleanup ideas | protects `WorldSettlementFC`, caches | settlement destruction population effects | Living World should record settlement-destroyed event and ownership transfer |
| `Faction.TryAffectGoodwillWith` | reduces/overrides goodwill in places | many faction relation patches | no major goodwill patch | Living World diplomacy should avoid replacing core relations globally |

## 6. World Generation

Rim War does not create a full population. It creates strategic power:

- Existing settlements receive `RimWarSettlementComp`.
- Factions receive `RimWarData`.
- Capitals may be marked.
- Mobile objects are created over time from settlement actions.
- Warband/trader/scout size is point-based.
- Pawns are generated when an incident/map encounter happens.
- Supplies/economy are mostly abstracted into points and generated pawn/equipment, so there is generation "из воздуха".

Animals are not globally generated by Rim War.

## 7. Population Model

Rim War has no individual demographic model:

- no persistent citizen identities;
- no families;
- no children/elders as world population;
- no professions;
- no settlement-level household ownership;
- no true migration.

Population pressure is represented by `RimWarPoints`, settlement count, war objects, and faction behavior. This is useful for high-level strategy but insufficient for Living World.

## 8. Economy Model

Economy is point-based:

- `RimWarSettlementComp.RimWarPoints` drives raid/trader/scout/diplomat strength.
- Gifts can increase points.
- War objects spend/withdraw points from settlements.
- Player heat controls aggression against the player.
- Trader world objects are strategic, but goods are generated at encounter boundaries.

No real warehouses or production chains exist. Rim War can inspire strategic costs, but Living World needs real owned resource ledgers.

## 9. Military Model

Military is Rim War's strongest area:

- `WarObject` is base class for mobile strategic units.
- `Warband`, `Scout`, `Trader`, `Settler`, `Diplomat`, `LaunchedWarband` specialize behavior.
- Units move on the world map using path follower/tweener.
- Units scan for nearby caravans, war objects, or sites.
- Settlement attacks can resolve abstractly through points or materialize attackers on loaded maps.
- Casualties are stored as point damage, not individual deaths.
- Permanent armies exist as world objects, but their internal people are not persistent citizens.

Living World should use the visible strategic object idea, but back each army by real `WorldCitizen`, animals, food, medicine, equipment, and ownership transfers.

## 10. Ecology

No global ecology model was found. Rim War does not simulate animal populations, migration, grazing, hunting pressure, or species constraints.

## 11. Persistence

Rim War saves:

- `WorldComponent_PowerTracker`: initialized flags, counters, victory state, `rimwarData`, caravan targets.
- `RimWarData`: faction strategic state.
- `RimWarSettlementComp`: points, heat, event ticks, attackers, scan data.
- `WarObject`: ids, points, damage, parent/destination, path follower, referenced pawns.
- Settings via `Scribe`.

Problems for Living World:

- point damage is lossy compared to individual casualty history;
- referenced pawn lists are not enough for inactive world population;
- some state is recalculated from world objects and may drift if other mods destroy or mutate them.

## 12. Performance

Good ideas:

- strategic units are lightweight world objects, not always active maps;
- local `nextTick` schedulers avoid doing all work every tick;
- scan caches and `maxObjectsPerScan` reduce spikes;
- optional tasker shows an attempt to separate expensive selection from main-thread creation;
- path pool patch addresses many-world-object pressure.

Bottlenecks/risks:

- many world-object scans use broad world-object lists;
- off-thread code must not touch RimWorld APIs unsafely;
- Harmony patches modify high-traffic paths;
- settlement scan locks/mutexes add complexity;
- generated map encounters still create many Pawns at once.

## 13. Weaknesses

- Source of truth is `RimWarPoints`, not people/resources.
- Heavy Harmony footprint across incidents, goodwill, world pathing, map generation.
- Some patches replace vanilla behavior broadly.
- Threading design risks touching game state off main thread.
- Combat casualties are abstract point damage.
- Economy and production are not real.
- Integration through internals is fragile; E&D and Empire both need compatibility patches for Rim War.

## 14. Lessons Learned

Use:

- visible world-map armies/traders/scouts;
- strategic movement and arrival actions;
- settlement heat/aggression pressure;
- per-settlement action cadence;
- battle sites and map-visible conflicts;
- compact world objects instead of active maps.

Do not use:

- points as source of truth;
- broad incident suppression as a default design;
- global replacement of faction relations;
- map generation point overrides as population model;
- direct dependency on Rim War internals.

Rewrite for Living World:

- `WarObject` -> `WorldArmy` / `WorldCaravan` backed by owned citizens/resources.
- `RimWarPoints` -> derived military/economic power from ledger.
- abstract combat damage -> citizen casualty/event resolution.
- trader creation -> owned goods withdrawn from settlement stockpile.
- scouting -> intel reports that control player-visible information.
