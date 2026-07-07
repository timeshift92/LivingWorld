# Living World - Compatibility Strategy

## Общий подход

Living World должен быть модульным и совместимым там, где это возможно.

Правило:

> Core simulation не должна зависеть от конкретного стороннего мода.

Интеграции должны жить в adapters.

Дополнительное правило после исследования Rim War и Economics & Demography:

> Living World должен быть источником истины, а другие моды должны интегрироваться через API или adapters.

Это значит, что Rim War, E&D и Animal Control рассматриваются как research references and optional compatibility targets, а не как foundation.

## Adapter model

Каждая интеграция должна реализовывать понятный контракт:

- detect if mod is loaded;
- register hooks;
- contribute defs/policies;
- sync materialized pawn data;
- contribute settlement/economy/military stats;
- report conflicts.

World-object import is also adapter-based. The bootstrap scanner must not treat
"has faction + has tile" as a settlement. Default behavior imports only vanilla
`Settlement` through `VanillaSettlementImporter`; future support for Empire
colonies, RimCities, quest camps or other modded settlement-like objects must be
added as explicit `IWorldObjectImporter` implementations.

Reason: temporary sites, quest objects, caravans and modded map markers can have
factions and tiles, but they are not automatically population owners. Importing
them as settlements would create fake citizens/resources and corrupt the ledger.

## Harmony strategy

Harmony-патчи нужны, но они должны быть последней точкой входа, а не главным API.

Категории патчей:

- pawn generation interception;
- raid/group generation interception;
- wildlife spawn interception;
- trader/visitor generation interception;
- quest pawn requests;
- pawn death/despawn/faction-change sync;
- settlement/faction state changes.

Fallback modes:

- Strict: fake generation блокируется или событие преобразуется.
- Balanced: неподдержанный pawn импортируется в Living World с audit event.
- Compatibility: vanilla/mod generation разрешается, но помечается как external source.

Обоснование: абсолютный no-fake-spawning идеален, но сторонние моды могут создавать pawn'ов неизвестными путями. Нужно не ломать игру, а фиксировать нарушение контракта.

## Моды из целевого списка

### Economics & Demography

Риск:

- дублирование демографии и экономики.

Стратегия:

- один мод должен быть владельцем population truth;
- adapter может читать/передавать settlement economy;
- конфликтующие функции должны отключаться настройками.

Research conclusion:

- study E&D deeply for patch points and solved compatibility problems.
- useful ideas: daily background world component, virtual stockpiles, faction production, trade synchronization, raid safety, raid debt, pawn kill/faction-change accounting, settlement destruction cost.
- do not let E&D own population when Living World is active. Living World must own individual citizen IDs and ownership records; E&D can consume aggregates via API.

### Rim War

Риск:

- Rim War может владеть world-map movement/armies, а Living World - реальными людьми.

Стратегия:

- preferred long-term mode: Living World owns armies, citizens, animals, supplies and history; RimWar Adapter may mirror or consume strategic movement through Living World API.
- short-term compatibility mode: if Rim War is active, do not build Living World core on its internals; read only stable public/game-facing state where possible.
- если версии несовместимы, adapter не активируется.

Research conclusion:

- use Rim War as source of ideas: warbands, scouts, traders, settlers, battle sites, faction power, player heat, incoming threat warnings.
- do not depend on Rim War as architecture: its model is point/world-object based and heavily patches world-map behavior.

### Hospitality

Стратегия:

- гости выбираются из реальных поселений;
- смерти/плен гостей возвращают последствия домой.

### Vanilla Expanded

Стратегия:

- def-driven support;
- новые faction/animal/pawn kinds должны становиться policies, а не hardcoded logic.

### Biotech / Children, School and Learning

Стратегия:

- active children use active mod systems;
- inactive children progress through Living World demography;
- education data syncs when materialized/dematerialized.

### Vehicle Framework

Стратегия:

- vehicles are assets of armies/caravans;
- crews are real citizens.

### Empire Refactored

Стратегия:

- treat as a major compatibility target for player-owned subject settlements, taxes, loyalty, unrest and vassal-style gameplay.
- Living World should own citizens, animals, resources and settlement ownership; Empire adapter can translate subject settlements and tax flows into Living World ownership transfers.
- Empire military requests should eventually withdraw real people/resources through `WorldArmy`, not create disconnected forces.

Research conclusion:

- local package ID is `Matathias.Empire`, version `1.3.68`, RimWorld 1.6 compatible.
- it depends on Harmony, loads after Rim War and several world/faction mods, and ships RimWar compatibility assemblies.
- it should be studied later as an idea source for subject settlements, taxes, settlement production, unrest, loyalty and player-controlled faction growth.

### Performance Fish / RocketMan

Стратегия:

- avoid fragile tick assumptions;
- expose lower-frequency simulation settings;
- keep background simulation out of pawn AI.

### Animal Controls

Стратегия:

- active map animal restrictions should apply only after materialization.
- global ecology remains authoritative for inactive animals.

Research conclusion:

- Animal Control is not foundational because it manages already materialized animals.
- Living World operates one layer below: animal existence, ownership, migration, reproduction, starvation and death.

## Current local install notes

Текущая локальная установка:

- RimWorld 1.6.4850;
- installed DLC: Core, Royalty, Ideology, Biotech, Anomaly, Odyssey;
- local mods detected in `C:\Games\RimWorld\Mods`: Harmony 2.4.2.0, Rim War 0.9.9.8 with 1.6 support, RimHUD, RimHUD Ru, Animal Control, Animal Controls, Economics & Demography.

Current local readiness:

- no duplicate `packageId` was found in the active Mods folder during the last check;
- no required dependency was missing for the active 1.6 mod set during the last check;
- the old RimWar v1.2 duplicate is no longer present in active Mods.

Обоснование: compatibility docs should distinguish architecture from actual local readiness. A mod can be conceptually supported but still not safe to enable if dependencies/version do not match.

## Source Research References

Reference clones are kept out of the Living World source tree by `.gitignore`:

- `tools/reference-src/RimWar---Threaded`
- `tools/reference-src/Economics-and-Demography`

Detailed notes: [Upstream Mod Research Notes](research/upstream_mods.md).

Current enabled stack from the user's RimWorld mod manager screenshot is documented here:

- [Enabled Mod Stack](research/enabled_mod_stack.md)
