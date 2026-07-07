# Living World - Performance Strategy

## Главная производительная граница

Living World не должен хранить весь мир как RimWorld `Pawn`.

Источник истины:

- `WorldCitizen`;
- `WorldAnimal`;
- `WorldSettlement`;
- `WorldArmy`;
- `WorldEvent`.

`Pawn` - временное активное представление.

## Целевые нагрузки

Минимальные цели:

- 20,000 humans;
- 100,000 animals;
- десятки поселений;
- long-running saves.

Стратегическая цель:

- 100,000 humans;
- 100,000+ animals;
- множество поколений;
- длинная история мира.

## Data layout

Рекомендации:

- stable numeric IDs вместо ссылок на тяжелые объекты;
- enum/int refs для defs;
- interned names/strings;
- sparse optional data;
- отдельные индексы для быстрых queries;
- derived caches rebuildable;
- no inactive `Pawn` references.

## Simulation cadence

Не весь мир обновляется каждый tick.

Suggested cadence:

- active pawn critical events: immediate;
- active pawn sync: batched;
- regional updates: hourly/daily;
- settlement economy: daily;
- demography: daily plus yearly milestones;
- ecology: daily/seasonal;
- diplomacy/war: daily/quadrum;
- history compaction: quadrum/yearly.

## Caches

Нужны производные индексы:

- citizens by settlement;
- citizens by faction;
- combat-capable citizens;
- workers by profession;
- animals by species/region;
- settlements by status;
- armies by faction;
- active pawn links.

Правило: кеш можно удалить и пересобрать из source-of-truth records/events.

## Work slicing

Большие обновления должны резаться на части:

- N settlements per tick;
- N regions per tick;
- N history events compacted per interval;
- no giant frame spikes.

## Memory tiers

### Hot

- active pawns;
- active map links;
- near-region entities;
- pending incidents.

### Warm

- settlement caches;
- military pools;
- recent history;
- visible faction data.

### Cold

- distant citizens;
- distant animals;
- old history;
- inactive settlements.

## Profiling metrics

Debug UI должен показывать:

- module update time;
- records processed;
- materialized pawn count;
- fake-spawn interceptions;
- cache rebuild time;
- save size by chunk;
- allocation failures;
- active/regional/global workload.

## Обоснование

Производительность Living World зависит не от одной оптимизации, а от архитектуры:

- меньше heavy objects;
- меньше per-tick work;
- больше batch updates;
- больше deterministic caches;
- четкое разделение active/regional/global simulation.

Если эти правила не заложить сразу, позже проект придется переписывать.

