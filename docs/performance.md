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

### Реализованные Core-индексы

`WorldState` лениво пересобирает из ledger и не сохраняет в XML:

- citizens by settlement;
- citizens by current owner;
- citizen counts by settlement/status;
- living population by faction for lifecycle checks;
- total alive citizen count;
- resource keys by owner.

Индексы инвалидируются всеми изменениями citizen/ownership ledger. Это убирает
дневные `F x N citizens` проверки коллапса, повторные полные citizen scans в
population flow/migration и `S x R resources` при пересчете богатства. Первый
query после изменения делает один `O(N)` rebuild; последующие queries до новой
мутации работают по индексу. `SettlementWealthService.RefreshAll` группирует
богатство фракций за один проход по поселениям.

## Bounded event journal

Подробная runtime-история ограничена детерминированной политикой:

- максимум `8,192` подробных `WorldEvent` в hot journal;
- compaction идет пакетами по `1,024`, поэтому append не делает постоянный
  `RemoveAt(0)`;
- старые события переходят в `WorldEventArchiveCheckpoint`;
- checkpoint хранит lifetime counts по `WorldEventKind`, first/last event id и
  tick, SHA-256 chain digest и bounded recent aggregates;
- recent aggregates ограничены `8,192` rows; global rows сохраняются в первую
  очередь, settlement-detail удаляется раньше global history.

Размер runtime/save поэтому зависит от заданных лимитов, а не от общей длины
кампании. `WorldActivitySummaryService` читает подробный journal и checkpoint:
полный global lookback использует точные lifetime totals, recent lookback -
bounded tick/settlement aggregates.

Старый подробный текст после checkpoint намеренно не хранится. Audit value
сохраняется через totals, диапазон ID/tick и digest; точное расследование по
summary/subject гарантируется для hot journal. Это operational checkpoint, а не
бесконечный replay log.

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

