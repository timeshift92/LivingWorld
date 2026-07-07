# Empire Refactored Reverse Engineering Analysis

Date: 2026-07-07

Scope:

- Installed mod: `C:\Games\RimWorld\Mods\Empire`
- Package id: `Matathias.Empire`
- Local version metadata: `1.3.68`, RimWorld `1.6`
- Source analyzed: `C:\Games\RimWorld\Mods\Empire\1.6\Source`

Empire Refactored is a compatibility target, not a base for Living World Core.

## 1. Project Overview

Empire lets the player create a self-governing faction with world settlements, taxes, production, policies, edicts, events, roads, caravans, and deployable military. It is player-empire-focused rather than full world simulation for all factions.

Main subsystems:

- `FactionFC`: global `WorldComponent` for the player's faction/empire.
- `WorldSettlementFC`: custom settlement world object derived from `Settlement`.
- Settlement comps: military, buildings, resources, trade actions.
- Military: squads, units, mercenaries, fire support, battle simulation, deployment jobs.
- Economy/resources: resource defs, resource pools, production, tithe/tax generation.
- Policies/edicts/events: faction-level behavior, settlement events, bills.
- UI windows: faction overview, settlement management, military design, dialogs.
- Compatibility patch assemblies/folders: RimWar, World Domination, Faction Territories, Vehicle Framework, HAR, CE, KCSG, PRD.

Main namespaces:

- `FactionColonies`
- `FactionColonies.util`
- compatibility namespaces per patch folder.

Dependencies:

- RimWorld / Verse
- Harmony
- UnityEngine
- Optional integrations with RimWar, Faction Territories, World Domination, Vehicle Framework, HAR, CE, KCSG, PRD.

## 2. Source Structure

Important areas:

```text
1.6/Source/
|-- Core/FactionColonies/
|   |-- FactionFC.cs
|   |-- FactionColonies.cs
|   |-- FactionCache.cs
|   |-- HarmonyPatches/
|   |-- Worldobjects/
|   |-- Comps/
|   |-- Military/
|   |-- Settlements/
|   |-- Defs/
|   |-- Buildings/
|   |-- Lords/
|   |-- Windows/
|   |-- Jobs/
|   `-- Util/
|
|-- Patch-RW/
|-- Patch-WD/
|-- Patch-WDExp/
|-- Patch-FTV/
|-- Patch-VF/
|-- Patch-HAR/
|-- Patch-CE/
|-- Patch-KCSG/
|-- Patch-PRD/
`-- Patch-VF/
```

Module relationship:

```text
FactionFC
  owns player faction identity, settlements, policy, events, resources, roads, military customization

WorldSettlementFC
  extends Settlement
  owns settlement stats, workers, prosperity, loyalty, unrest, buildings, resources, prisoners

WorldObjectComp_SettlementMilitary
  attached to WorldSettlementFC
  owns active attack/defense state, pawns, forces, military jobs

ResourceFC / ResourcePool / BuildingFC
  drive taxes and production

Harmony patches
  connect vanilla faction, pawn, world object, trade, research and compatibility behavior
```

## 3. World State Ownership

Empire's source of truth is player-faction state:

- `FactionFC.settlements`: list of `WorldSettlementFC` references.
- `FactionFC`: faction name/title/icon/colors, capital, tax map, policies, edicts, event manager, bills, resources, roads, military customization, military targets, caravan settings, levels and ids.
- `WorldSettlementFC`: settlement identity, level, workers, social stats, resources, prisoners, buildings, tax accumulation, biome modifiers, permanent/stat modifiers.
- `WorldObjectComp_SettlementMilitary`: military state for a settlement.

Relations:

- Settlements are custom `WorldSettlementFC : Settlement`, not vanilla settlements.
- Factions are centered on the player's Empire faction through `FactionCache`.
- Pawns are materialized for military events, defenders, attackers, caravans, prisoners, mercenary squads; many are referenced while active or saved through squad/prisoner systems.
- Armies are not global AI armies; they are settlement military jobs, squads, forces, and battle events.
- Economy is settlement/faction resource and tax based.
- Caravans are generated from Empire resources and trader kind configuration.

Living World implication:

- Empire has a rich settlement management model, but its ownership is player-empire-specific. Living World should interoperate through an adapter and not inherit `FactionFC` as global world state.

## 4. Update Loop

`FactionFC.WorldComponentTick` is the central scheduler.

Observed responsibilities:

- First tick initialization and recovery.
- Fire support tick.
- Road builder tick.
- Stat tick.
- Tax tick.
- Military tick.
- Threat adaptation tick.
- Policy behavior ticks.
- Event processing and orphan recovery.

Important local loops:

- `WorldObjectComp_SettlementMilitary.CompTick`
  - If settlement is under attack:
    - cleans stale pawn refs every 250 ticks when map loaded;
    - registers untracked player pawns;
    - detects stuck battles and queues `EndAttack`;
    - clears orphaned attack flags every 2,500 ticks if no matching event exists.
- `WorldObjectComp_SettlementBuildings.CompTick`
  - ticks building comps.
- `SettlementBuildingComp.Tick`
  - building-specific component logic.
- `FCPolicyBehavior.Tick`
  - policy behavior runtime effects.
- `LordJob_DefendColony` / `LordJob_DeployMilitary`
  - active map combat lords tick while maps are loaded.
- Road builder:
  - `FCRoadBuilder.FirstTick`
  - `FCRoadBuilder.RoadTick`

Schedulers/queues:

- `taxTimeDue`
- `militaryTimeDue`
- `eventManager`
- `Bills` / `OldBills`
- `settlementCaravansList`
- `militaryTargets`
- `cooldownMilitary` events
- `FCRoadQueue`

## 5. Harmony Analysis

Empire has many compatibility and gameplay patches. The core risk is high around faction relations, pawn lifecycle, world-object mutation, and settlement defeat.

Core patches:

| Patch file/class | Vanilla method | Type | Purpose | Conflict risk | Living World relevance |
|---|---|---:|---|---:|---|
| `CommsConsolePatches` | `FactionDialogMaker.RequestMilitaryAidOption` | Postfix | Add/alter military aid option | Medium | LW trade/intel may share comms surface |
| `FactionDefDescriptionPatch` | `FactionDef.Description` getter | Postfix | Add Empire description | Low | UI only |
| `ApparelColorPatch` | `PawnApparelGenerator.PostProcessApparel` | Postfix | Apply faction apparel colors | Low/Medium | Materialized pawn styling can coexist |
| `DebugOptionsPatches` | `WorldPawns.PassToWorld` | Prefix | Debug/pass-to-world handling | Medium | overlaps pawn lifecycle |
| `CachePatches` | `Game.Dispose`, `Game.ClearCaches` | Postfix | Clear mod caches | Low | Good pattern |
| `PawnGroupPatch` | `PawnGroupMakerUtility.GetOptions` | Postfix | Modify pawn group options | Medium | may affect generated groups |
| `GizmosPatches` | `Pawn.GetGizmos` | Postfix | Add pawn commands | Low/Medium | UI overlap only |
| `JobPatches` | `JobDriver_Goto.TryExitMap` | Prefix | Control exit behavior | High | overlaps dematerialization |
| `TraderPatches` | `TradeDeal.DoesTraderHaveEnoughSilver` | Postfix | Trade behavior | Medium | trade ownership overlap |
| `TraderPatches` | `Tradeable.GetPriceFor` | Postfix | Price behavior | Medium | economy overlap |
| `TraderPatches` | `TradeDeal.TryExecute` | Postfix | Record/handle trade result | Medium | useful trade boundary |
| `WorldPathPoolPatches` | `WorldPathPool.GetEmptyWorldPath`, `WorldPath.ReleaseToPool` | Prefix | Debug path pool handling | Medium | many world objects lesson |
| `PawnGenerationPatches` | `PawnGenerator.GeneratePawn` | Prefix | Force/adjust Empire pawn generation | High | LW materialization overlap |
| `PawnPatches` | `Pawn.Kill` | Prefix | Handle Empire battle deaths | High | LW death binding overlap |
| `PawnPatches` | `DeathActionWorker_Simple.PawnDied` | Prefix | Prevent/alter corpse death action | High | death behavior risk |
| `PawnPatches` | `Pawn.DeSpawn` | Prefix | Track military pawn despawn | High | LW raid outcome overlap |
| `ResearchPatches` | `ResearchManager.FinishProject` | Postfix | Unlock/update Empire tech | Low | tech dependency possible |
| `TransportPodArrivalActionPatch` | `TransportersArrivalAction_LandInSpecificCell.Arrived` | Prefix/Postfix | Track shuttle/pod arrivals | Medium/High | overlaps reinforcement/materialization |
| `WorldObjectPatches` | `WorldObjectsHolder.Add/Remove` | Postfix | Maintain caches/world object awareness | Medium | LW also tracks world objects |
| `WorldObjectPatches` | `WorldObject.SetFaction` | Postfix | Update Empire state on faction change | High | LW ownership/faction transfer overlap |
| `WorldObjectPatches` | `SettlementDefeatUtility.CheckDefeated` | Prefix | Intercept settlement defeat | High | LW settlement destruction overlap |
| `FactionPatches` | `IncidentWorker_RaidFriendly.TryResolveRaidFaction` | Postfix | Make friendly raid choose Empire | Medium | incident overlap |
| `FactionPatches` | `SettlementProximityGoodwillUtility.AppendProximityGoodwillOffsets` | Postfix | Goodwill/proximity handling | Medium | diplomacy overlap |
| `FactionPatches` | `Faction.CheckReachNaturalGoodwill` | Prefix | Control goodwill drift | High | diplomacy overlap |
| `FactionPatches` | `Faction.TryAffectGoodwillWith` | Prefix/Postfix | Control goodwill changes | High | diplomacy overlap |
| `FactionPatches` | `Faction.Notify_*` | Prefix | Suppress or redirect member/trade/damage notifications | High | faction relation and death/capture overlap |
| `FactionPatches` | `QuestNode_GetFaction.IsGoodFaction` | Prefix | Quest faction selection | Medium | low initial priority |
| `FactionPatches` | `GenHostility.HostileTo` | Postfix | Hostility logic for Empire | High | combat/faction overlap |
| `FactionPatches` | `Faction.SetRelationDirect` | Postfix | Relation sync | High | diplomacy overlap |

Compatibility patches:

| Patch folder | Target | Purpose | Living World note |
|---|---|---|---|
| `Patch-RW` | RimWar `WorldUtility`, `RimWarSettlementComp`, `IncidentUtility` | Treat Empire settlements as valid RimWar settlements, override points/gizmos/combat | Shows why direct RimWar internals are fragile |
| `Patch-WD` / `Patch-WDExp` | World Domination APIs | Exclude/protect Empire faction and settlement, sync diplomacy/world power | LW should expose API instead |
| `Patch-FTV` | Faction Territories | Add Empire territories and intercept destruction letters | territory integration idea |
| `Patch-VF` | Vehicle Framework | Override caravan defense | deployment compatibility |
| `Patch-KCSG` | KCSG settlement gen placement | transpiler | high fragility |
| `Patch-PRD` | Pawn race determination | avoid incompatible races | pawn generation compatibility |

Cross-mod overlap table:

| Vanilla Method | Rim War | Empire | Economics | Living World recommendation |
|---|---|---|---|---|
| `PawnGenerator.GeneratePawn` | incident-side | Empire pawn generation | gender/raid cost | LW owns pawn materialization service |
| `Pawn.Kill` | abstract combat points | military battle cleanup | population loss | LW must be idempotent and recognize Empire pawns |
| `Pawn.DeSpawn/ExitMap` | post-battle/caravan | military return/capture | gear reabsorb | LW outcome service must ignore unrelated Empire military where needed |
| `WorldObject.SetFaction` | relation/validity | cache/sync | pawn set faction only | LW records ownership/faction transfer events |
| `SettlementDefeatUtility.CheckDefeated/IsDefeated` | prevent defeat for reinforced settlements | intercept Empire settlement defeat | settlement destruction effects | LW should record after vanilla decision or provide scoped prevent flag |
| `Faction.TryAffectGoodwillWith` | custom diplomacy | extensive Empire diplomacy | not main | LW diplomacy should avoid replacing global goodwill initially |
| `TradeDeal.TryExecute` | indirect | trade hooks | trade close/setup | LW should observe trade result and create intel/resource events |
| `WorldObjectsHolder.Add/Remove` | world object list usage | cache sync | no direct | LW bootstrap/cache can use low-risk postfixes |

## 6. World Generation

Empire creates player-owned settlements through its own UI/events:

- `WorldSettlementFC.PostMake` initializes trader tracker, name, icon, and settlement def.
- `PostPostMake(tile)` binds tile, biome/resource data, building slots, resources, modifiers, settlement type, mutators, landmarks.
- Settlement founding and expansion are event-driven by `FactionFC` and `FCEventManager`.

Generated entities:

- settlements as `WorldSettlementFC`;
- resources from `ResourceFC`;
- taxes/tithes as real `Thing`;
- military pawns during deployment/defense;
- caravans/traders based on enabled caravan types.

Generation "из воздуха":

- Taxes and tithes are produced from settlement stats/resources, not from a global owned inventory.
- Workers are numeric stats, not named citizens.
- Military forces are squad/unit definitions and generated pawns, not citizens pulled from a world ledger.

## 7. Population Model

Empire does not simulate real population for every settlement.

It has:

- `workers`, `workersMax`, `workersUltraMax`;
- prisoners;
- mercenary squads/units;
- settlement military level;
- social stats: happiness, loyalty, unrest, prosperity;
- policy effects and buildings.

It does not have:

- individual resident identities;
- families;
- children/aging;
- per-person profession history;
- migration between settlements as citizens.

Workers are economic capacity, not persistent people.

## 8. Economy Model

Empire's economy is settlement management:

- `ResourceFC` per settlement resource.
- `ResourcePool` for pooled resources.
- Buildings modify production/stats.
- Biome, tile mutators, landmarks, settlement type and policies apply stat modifiers.
- `WorldSettlementFC.AccumulateDailyProduction` samples production/upkeep.
- `WorldSettlementFC.CreateTax` produces silver and tithe things.
- `FactionFC` aggregates income/upkeep/profit and handles tax tick.
- `TraderKindDef` list is rebuilt from resource types and tech.

Good ideas for Living World:

- terrain/biome/mutator/landmark modifiers;
- building-driven production;
- daily accumulation and averaged tax/income values;
- resource/tithe selection;
- policy modifiers;
- cached stat/profit calculations.

Limit:

- resources are production abstractions, not necessarily stored inputs/outputs in an ownership ledger.

## 9. Military Model

Empire's military model is player-commanded:

- `MilitaryCustomizationUtil`: squads, units, customization.
- `WorldObjectComp_SettlementMilitary`: current attack state, attackers, defenders, drafted NPCs, forces, job, target, cooldown.
- `MilitaryForce`: abstract offensive/defensive power.
- `MilitaryJobDef`: deployment job behavior.
- `SimulateBattleFc`: auto-battle simulation.
- Active battle maps can spawn attackers/defenders and resolve with deaths/cooldowns.
- Failed defenses reduce prosperity/happiness/loyalty, destroy buildings, or de-level settlement.

Permanent armies:

- Not global AI armies like Rim War.
- Settlement military persists as settlement/squad state, with active pawns only during operations.

Living World should reuse the idea of consequences, cooldowns, and settlement damage, but replace abstract workers/squad pawns with owned citizens and equipment where integration is enabled.

## 10. Ecology

Empire has animal-related filters/resources and production categories, but no global ecology:

- `AnimalFilter` exists for settlement/military/resource configuration.
- Resource production may include animal products.
- No wildlife population, migration, reproduction, or regional carrying capacity was found.

## 11. Persistence

Empire saves extensive state through `Scribe`:

- `FactionFC`: identity, capital, tax map, settlements, policies, events, event manager, caravans, military targets, resources, xenotype/animal filters, ids, bills, roads, threat adaptation, levels, edicts, trade amount.
- `WorldSettlementFC`: settlement level, name, social stats, resources, prisoners, tax accumulation, modifiers, buildings, upgrade state, tithe.
- `WorldObjectComp_SettlementMilitary`: attackers, defenders, drafted NPCs, forces, battle flags, job, enemy, squad, timers, initial defender count.
- Military and resource classes implement their own `ExposeData`.

Good:

- robust migration and null-scrubbing code;
- caches are invalidated/rebuilt after load;
- events moved into a manager abstraction.

Risk:

- heavy save surface;
- many references to pawns/world objects can break under other mods;
- custom settlement class intentionally overrides `Destroy`, which can confuse mods expecting vanilla `Settlement`.

## 12. Performance

Good:

- many values are lazy-cached and dirtied explicitly;
- daily accumulation avoids recomputing tax continuously;
- settlement comps isolate expensive behavior;
- stuck-battle cleanup prevents indefinite ticking;
- cache clearing on game dispose/clear caches.

Bottlenecks:

- large UI/state surface;
- many Harmony patches on high-frequency or sensitive methods;
- pawn reference cleanup every active battle;
- resource/stat recomputation can cascade if caches are dirtied too often;
- custom world settlement compatibility checks add overhead.

## 13. Weaknesses

- Player-empire scope, not full world simulation.
- Workers are abstract capacity, not people.
- Heavy Harmony surface around faction relations and pawn lifecycle.
- Custom `WorldSettlementFC : Settlement` and overridden `Destroy` can conflict with mods.
- Taxes/resources are production abstractions, not ownership-ledger withdrawals.
- Integration with RimWar requires many direct patches into RimWar internals.

## 14. Lessons Learned

Use:

- settlement-level resources and terrain/building modifiers;
- policy/edict modifiers;
- event manager for scheduled settlement events;
- cached stats/profit;
- battle consequences for prosperity, buildings, level, happiness, loyalty;
- clear compatibility adapters per external mod.

Do not use:

- player-empire state as global world source;
- abstract workers as Living World citizens;
- global faction relation patches as first design;
- direct RimWar internal patches as integration model;
- custom `Settlement` subclass as the only representation for all settlements.

Rewrite for Living World:

- Empire resources -> `SettlementProductionProfile` and owned resource stacks.
- workers -> citizens with professions and work capacity.
- military squads -> `WorldArmy` with citizen membership.
- taxes/tithes -> resource transfers/events.
- settlement events -> Living World event-sourced scheduled actions.
