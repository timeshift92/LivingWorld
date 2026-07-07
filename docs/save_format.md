# Living World - Save Format

## Цель формата сохранения

Сохранение должно хранить:

- текущее состояние мира;
- историю изменений;
- связи между людьми, семьями, поселениями, фракциями и животными;
- версию схемы;
- данные для миграций.

Формат должен выдерживать длинные игры и крупные популяции.

## Источник истины

Источник истины:

- compact state records;
- append-only event log;
- deterministic IDs.

Не источник истины:

- RimWorld `Pawn` для inactive entities;
- derived caches;
- UI state;
- temporary incident state.

## Snapshot + Event Sourcing

Рекомендуемая модель:

```text
Save = Snapshot + EventStore + RebuildableCaches
```

Snapshot хранит актуальное состояние:

- citizens;
- animals;
- settlements;
- factions;
- armies;
- economy;
- ecology;
- diplomacy.

EventStore хранит историю:

- births;
- deaths;
- marriages;
- raids;
- wars;
- crop failures;
- migrations;
- settlement destruction.

Caches ускоряют игру, но не являются обязательными для восстановления.

## Chunk layout

```text
LivingWorldSaveHeader
CitizenChunks
AnimalChunks
SettlementChunks
FactionChunks
ArmyChunks
EconomyChunks
EcologyChunks
DiplomacyChunks
EventChunks
PawnLinkChunks
CacheChunks
```

## Header

Header должен содержать:

- save schema version;
- mod version;
- RimWorld version;
- world seed;
- enabled modules;
- strict/balanced compatibility mode;
- population counts;
- migration status.

## Event records

Каждое событие должно иметь:

- event ID;
- tick;
- event type;
- involved entity IDs;
- settlement/faction IDs;
- metrics;
- short summary key;
- optional payload.

Пример:

```text
Event: RaidReturned
Tick: 1842000
Faction: PirateUnion
SourceSettlement: NorthCamp
Participants: 45
Dead: 20
Captured: 3
Returned: 22
```

## Миграции

Правила:

- каждая версия схемы имеет явную migration step;
- migrations выполняются последовательно;
- migration пишет диагностический event;
- нельзя молча удалять неизвестные сущности;
- derived caches можно выбросить и пересобрать.

## Rollback и восстановление

Полный rollback мира в gameplay может быть дорогим, но event sourcing дает:

- debug replay;
- integrity investigation;
- восстановление derived caches;
- объяснение причин мира;
- возможность future tooling для rollback.

## Обоснование

Без событий сохранение покажет только "что сейчас". Living World должен знать "почему так стало". Поэтому event sourcing является частью gameplay design, а не только технической реализацией.

