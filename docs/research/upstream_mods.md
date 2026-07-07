# Upstream Mod Research Notes

Date: 2026-07-07

Purpose: use existing mods as research material for ideas, patch maps, compatibility risks, and feature inventory. These repositories are not dependencies of Living World.

Detailed reverse engineering reports:

- [Rim War analysis](rimwar-analysis.md)
- [Economics & Demography analysis](economics-analysis.md)
- [Empire Refactored analysis](empire-analysis.md)
- [Living World Foundation Architecture](livingworld-foundation.md)

Reference clones:

- Rim War Threaded: `tools/reference-src/RimWar---Threaded`, commit `ec8ce84`, upstream `https://github.com/TorannD/RimWar---Threaded`
- Economics & Demography: `tools/reference-src/Economics-and-Demography`, commit `deb17eb`, upstream `https://github.com/helldanpwnz/Economics-and-Demography`

The reference clones are ignored by `.gitignore` through `tools/reference-src/`. They should remain research inputs, not copied source.

## Architectural Decision

Living World should not be built on top of Rim War, Economics & Demography, or Animal Control.

Correct dependency direction:

```text
Living World Core
  -> intercepts vanilla
  -> owns world simulation
  -> exports public API
  -> optional adapters let other mods consume Living World state
```

Incorrect dependency direction:

```text
Living World
  -> depends on Rim War internals
  -> depends on E&D population ownership
  -> inherits external patch timing and save model
```

Reason: Living World's core promise is persistent identity and ownership. That cannot be delegated to a mod whose data model is faction-level points, aggregate population, or active-map animal control.

## Rim War Threaded

### Use as source of ideas

Rim War is valuable for design ideas:

- mobile world objects;
- warbands;
- scouts;
- traders;
- settlers;
- flying/launched warbands;
- battle sites;
- faction power;
- capitals;
- settlement scan ranges;
- player heat/aggression;
- faction action cadence;
- map-visible strategic movement;
- world-map warnings for incoming threats;
- history auto recorders for world activity.

Useful source areas:

- `v1.6/Defs/WorldObjectDefs/RW_WorldObjects.xml`
- `Source/RimWar/RimWarData.cs`
- `Source/RimWar/Planet/RimWarSettlementComp.cs`
- `Source/RimWar/Planet/Warband.cs`
- `Source/RimWar/Planet/Trader.cs`
- `Source/RimWar/Planet/Scout.cs`
- `Source/RimWar/Planet/Settler.cs`
- `Source/RimWar/Planet/WorldComponent_PowerTracker.cs`
- `Source/RimWar/Planet/IncidentUtility.cs`
- `Source/RimWar/Harmony/HarmonyPatches.cs`

### Do not use as foundation

Reasons:

- It is centered on RimWar points and world objects, not individual persistent people/resources.
- It actively patches world map incidents, caravans, settlement attacks, incident queues, and faction behavior.
- Its strategic model is built around generated world-object actions, not ownership-ledger withdrawals.
- Integration through internals would force Living World to follow RimWar timing, patch order, and save assumptions.
- E&D's compatibility layer has to use reflection against RimWar internals, including private fields, which is a warning sign for direct dependency.

### Ideas to port into Living World's own model

Do not port code. Port concepts:

- `WorldArmy` as a real owned bundle of citizens, animals, supplies, and equipment.
- `WorldTradeCaravan` as a real owned bundle of traders, pack animals, goods, and silver.
- `WorldScoutParty` as a light strategic object with detection and report behavior.
- `WorldSettlerParty` as migration/founding action.
- `WorldBattleSite` as a history-backed world object created by army collisions.
- `FactionStrategyState` with aggression, expansion pressure, trade pressure, and war fatigue.
- `FactionPower` derived from owned citizens, settlements, armies, resources, and technology, not from points alone.

## Economics & Demography

### Study deeply

E&D is the most useful reference mod for Living World research.

It already explores:

- population counts;
- adults/children/elders;
- male/female ratios;
- birth rates by technology level;
- daily background demographic updates;
- starvation and population loss;
- migration/kidnapping/luring between factions;
- settlement expansion and collapse;
- virtual stockpiles;
- faction production;
- trade synchronization;
- raid cost/debt;
- market inflation and homeostasis;
- settlement destruction consequences;
- RimWar compatibility.

Useful source areas:

- `Source/ED_WorldPopulationManager.cs`
- `Source/ED_WorldPopulationManager.Demography.cs`
- `Source/ED_WorldPopulationManager.Economy.cs`
- `Source/ED_WorldPopulationManager.Production.cs`
- `Source/ED_WorldPopulationManager.Trade.cs`
- `Source/ED_VirtualStockpile.cs`
- `Source/ED_Patch_PawnGenerator.cs`
- `Source/ED_Patch_PawnKill.cs`
- `Source/ED_Patch_PawnSetFaction.cs`
- `Source/ED_Patch_RaidSafety.cs`
- `Source/ED_Patch_RaidEconomy.cs`
- `Source/ED_Patch_TradeSession_Setup.cs`
- `Source/ED_Patch_TraderArrival_SetupStock_Spawn.cs`
- `Source/ED_Patch_Settlement_Destroy.cs`
- `Source/ModsPatch/ED_Patch_RimWar_Compatibility.cs`

### What E&D does well

- Uses a `WorldComponent` as a global simulation manager.
- Saves faction state through Scribe.
- Runs heavy updates on a background cadence instead of every tick.
- Uses virtual stockpiles instead of spawning all goods physically.
- Tracks trade session transitions so goods do not simply disappear.
- Limits raids based on available population.
- Accounts for pawn death and faction changes.
- Connects settlement destruction to economic loss.
- Provides settings for update intervals and economic multipliers.

### Limits for Living World

E&D is aggregate-first. It tracks population at faction/cohort level:

- adults;
- children;
- elders;
- female count;
- virtual goods;
- raid debt.

Living World requires identity-first records:

- individual citizen IDs;
- family links;
- household ownership;
- settlement ownership;
- materialized pawn link;
- historical participation.

Therefore E&D should not own population when Living World is active. The correct compatibility model is:

- Living World owns population, identity, settlement ownership, animals, and long-term history.
- E&D adapter may read Living World aggregates through `ILivingWorldApi`.
- If E&D is active, overlapping population-loss/growth systems should be disabled or redirected through Living World.
- E&D's patch map should inform where Living World must intercept vanilla.

### Specific lessons for Living World

1. Patch map:
   - `PawnGenerator.GeneratePawn`
   - `Pawn.Kill`
   - `Pawn.SetFaction`
   - `IncidentWorker_Raid.TryExecuteWorker`
   - `IncidentWorker.TryExecute`
   - `TradeSession.SetupWith`
   - `IncidentWorker_TraderCaravanArrival.TryExecuteWorker`
   - `WorldObject.Destroy`
   - `MapGenerator.GenerateMap`

2. Virtual stockpiles:
   - Living World needs owned resource ledgers before it materializes goods.
   - Trade caravans should temporarily withdraw goods from owner storage.
   - Destroyed or robbed caravans should not refund those goods.

3. Raid costs:
   - Raids should withdraw people, animals, food, medicine, equipment, and supplies.
   - If a faction cannot pay the ownership cost, the raid should scale down, go into debt, or be blocked.

4. Compatibility:
   - Reflection against another mod's private fields is a last resort.
   - Living World should expose stable API so other mods do not need this pattern.

## Animal Control

Animal Control is not a foundation for Living World.

Reason:

- It manages already existing animals on active maps.
- Living World needs to define why animals exist, where they come from, who owns them, when they migrate, how they reproduce, and how they die globally.

Potential compatibility:

- Once animals are materialized on active maps, Animal Control can still affect player-facing handling.
- Living World remains the owner of inactive animal population and animal ownership.

## Final Direction

Living World should become the lower-level world substrate:

```text
Living World Core
  owns citizens
  owns animals
  owns settlements
  owns resources
  owns armies
  owns history

RimWar Adapter
  reads Living World armies/factions
  optionally mirrors strategic movement

Economics & Demography Adapter
  reads Living World population/resources
  optionally contributes economic UI/logic

Animal Control Compatibility
  affects only materialized active-map animals
```

Long-term target: other modders should be able to say, "If you need a real living world, depend on Living World Core."
