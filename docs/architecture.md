# Living World - Architecture

## Архитектурная позиция

Living World должен быть ledger-first системой.

Это значит:

- глобальный мир хранится как компактные записи данных;
- RimWorld `Pawn` создается только на границе с активным gameplay;
- все изменения проходят через сервисы и события;
- агрегаты и кеши являются производными, а не источником истины.
- ownership ledger является базовым слоем: у граждан, животных, ресурсов, караванов и армий должен быть владелец.

См. также:

- [World Ownership](world_ownership.md)
- [Upstream Mod Research Notes](research/upstream_mods.md)

## Почему не Pawn-first

Pawn-first подход выглядит простым, потому что он использует vanilla RimWorld модель напрямую. Но для Living World это главная архитектурная ошибка.

Причины:

- `Pawn` несет много данных: health, needs, relations, apparel, inventory, jobs, thoughts, hediffs;
- тысячи pawn'ов увеличат размер сохранений;
- часть систем может случайно начать tick/update inactive pawn'ов;
- совместимость станет хрупкой;
- симуляция 100,000+ животных как `Pawn` практически невозможна.

Вывод: `Pawn` - это active representation, а не storage model.

## Слои системы

### 1. RimWorld Integration Layer

Отвечает за связь с игрой:

- Harmony patches;
- bridge к `PawnGenerator`, incidents, raids, traders, visitors, wildlife;
- materialization/dematerialization pawn'ов;
- интеграции с другими модами;
- UI и debug windows.

### 2. Core Simulation Layer

Отвечает за неизменные правила мира:

- `WorldState`;
- `EntityId`;
- deterministic random;
- scheduler;
- event bus;
- service registry;
- validation.

### 3. Domain Modules

Модули предметной области:

- `LivingWorld.World`;
- `LivingWorld.Population`;
- `LivingWorld.Demography`;
- `LivingWorld.Ecology`;
- `LivingWorld.Economy`;
- `LivingWorld.Military`;
- `LivingWorld.Diplomacy`;
- `LivingWorld.Events`;
- `LivingWorld.Persistence`;
- `LivingWorld.UI`;
- `LivingWorld.Debug`.

### 4. Public API Layer

Стабильная поверхность для других модов:

- `ILivingWorldApi`;
- query services;
- command services;
- event subscriptions;
- compatibility adapters.

Ключевая цель API: другие моды должны использовать Living World как источник истины, а не патчить внутренние классы или владеть собственной параллельной population/economy model.

## Предлагаемая структура проекта

```text
LivingWorld/
|-- docs/
|   |-- vision.md
|   |-- architecture.md
|   |-- simulation.md
|   |-- performance.md
|   |-- save_format.md
|   |-- roadmap.md
|   |-- compatibility.md
|   `-- api.md
|
|-- src/
|   |-- LivingWorld.Core/
|   |-- LivingWorld.World/
|   |-- LivingWorld.Population/
|   |-- LivingWorld.Demography/
|   |-- LivingWorld.Ecology/
|   |-- LivingWorld.Economy/
|   |-- LivingWorld.Military/
|   |-- LivingWorld.Diplomacy/
|   |-- LivingWorld.Events/
|   |-- LivingWorld.Persistence/
|   |-- LivingWorld.UI/
|   |-- LivingWorld.Debug/
|   `-- LivingWorld.Tests/
|
|-- assets/
|-- examples/
`-- tools/
```

Обоснование: такая структура отделяет документацию, доменную логику, тесты, инструменты и будущие ассеты. Это важно для open-source проекта, где разные участники смогут работать над отдельными зонами.

## Основные классы и роли

### Runtime

- `LivingWorldMod`
  - настройки мода;
  - проверка зависимостей;
  - регистрация API.

- `LivingWorldGameComponent`
  - игровой lifecycle;
  - запуск scheduler;
  - обработка tick-boundary событий.

- `LivingWorldWorldComponent`
  - хранение `WorldState`;
  - save/load;
  - migration entrypoint.

### World model

- `WorldState`
  - корневой объект состояния мира.

- `WorldCitizen`
  - легкая запись человека.

- `WorldAnimal`
  - легкая запись животного.

- `WorldSettlement`
  - запись поселения.

- `WorldFactionState`
  - расширенное состояние фракции.

- `WorldHousehold`
  - семья/домохозяйство.

- `WorldArmy`
  - армия из реальных людей.

- `OwnershipRecord`
  - связь asset -> owner.

- `WorldOwnerId`
  - стабильная ссылка на владельца: world, faction, settlement, household, army, caravan, individual, wilderness region.

### Services

- `BirthService`;
- `MigrationService`;
- `RaidPlanner`;
- `SettlementSimulator`;
- `AnimalSimulator`;
- `WarSimulator`;
- `EconomySimulator`;
- `PawnMaterializationService`;
- `PawnDematerializationService`;
- `WorldHistoryService`;
- `WorldIntegrityService`.
- `OwnershipService`.

## Текстовая UML-схема

```text
LivingWorldWorldComponent
  owns WorldState

WorldState
  owns CitizenRegistry
  owns AnimalRegistry
  owns SettlementRegistry
  owns FactionStateRegistry
  owns EventStore
  owns SimulationCaches

WorldCitizen
  references WorldSettlement
  references WorldHousehold
  references WorldFactionState
  may link to active Pawn through PawnLink

WorldAnimal
  references SpeciesDef
  references BiomeRegion
  may link to active Pawn through PawnLink

Services
  read/write WorldState
  emit WorldEvents
  never store RimWorld Pawn as source of truth

OwnershipService
  transfers citizens, animals, resources and groups between owners
  emits ownership events
  prevents raids/caravans from using assets that owner does not have
```

## Главные архитектурные решения

### Decision 1: Ledger-first

Обоснование: только ledger-first модель выдерживает десятки тысяч людей и животных.

### Decision 2: Event Sourcing

Обоснование: история мира является важной gameplay-функцией, а не просто debug log.

### Decision 3: Сервисы вместо логики в объектах

Обоснование: тестируемость, расширяемость и контроль side effects.

### Decision 4: API как отдельный контракт

Обоснование: популярный мод должен быть интегрируемым без чужих Harmony-патчей.

### Decision 5: Детерминизм

Обоснование: без воспроизводимости невозможно стабильно развивать такую сложную симуляцию.

### Decision 6: Living World owns the world

Rim War, Economics & Demography и Animal Control можно изучать как источники идей и compatibility maps, но Living World не должен строиться поверх их внутренних моделей.

Обоснование:

- Rim War хорош как reference для armies, scouts, traders, settlers, faction movement and territory pressure, но его source of truth - world objects и points, а не индивидуальные граждане и ресурсы.
- Economics & Demography полезен как reference для population/economy patches, daily updates, virtual stockpiles, trade sync and raid costs, но его source of truth - faction-level aggregates, а не individual citizen ownership.
- Animal Control управляет уже существующими животными на активной карте, а Living World должен владеть глобальным происхождением и жизненным циклом животных.

Правильное направление: adapters use Living World API, not Living World depends on adapter internals.
