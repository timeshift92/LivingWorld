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
- caravans;
- migration groups;
- settlement production profiles;
- settlement capabilities;
- specialist pools;
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
CaravanChunks
EconomyChunks
EcologyChunks
DiplomacyChunks
EventChunks
PawnLinkChunks
MigrationGroupChunks
ProductionProfileChunks
SettlementCapabilityChunks
SpecialistPoolChunks
CacheChunks
```

## Current XML payload

Текущая RimWorld-реализация сериализует `WorldState` в XML внутри
`LivingWorldWorldComponent`.

High-volume ledger containers use compact v2 text rows instead of one XML
element per record:

```xml
<Citizens format="compact-v2">...</Citizens>
<Ownership format="compact-v2">...</Ownership>
<Events format="compact-v2">...</Events>
```

The compact rows are deterministic and are rebuilt into normal ledger records
on load. String fields are UTF-8/base64 encoded inside the row so names,
professions and event summaries can contain punctuation without changing the
delimiter contract. The loader remains backward-compatible with legacy
per-record XML:

```xml
<Citizens>
  <Citizen ... />
</Citizens>
<Ownership>
  <Owner ... />
</Ownership>
<Events>
  <Event ... />
</Events>
```

Derived aggregate caches are not saved. They are rebuilt lazily from citizens
and ownership after load.

`drifterArrivalReservoir` is saved as a root attribute because it is global
world state, not a row collection:

```xml
<LivingWorldState ... drifterArrivalReservoir="320" />
```

It is the finite outside-world population reserve consumed by
`DrifterArrivalService`. Legacy saves without the attribute load with `0`, so
arrivals will not create people until a system explicitly replenishes the
reservoir.

`Caravans` are saved as their own optional container. Cargo remains in the
resource ledger and is owned by `EntityKind.Caravan`, so the save format keeps
the entity separate from the inventory:

```xml
<Caravans>
  <Caravan kind="Caravan" id="1" name="Traders caravan" factionId="Traders"
           sourceSettlementKind="Settlement" sourceSettlementId="1"
           targetSettlementKind="Settlement" targetSettlementId="2"
           departTick="60000" arrivalTick="120000" status="Traveling" />
</Caravans>
```

The `Caravans` container is optional for backward compatibility with saves from
before persistent caravans.

Текущие обязательные инварианты после загрузки:

- `worldSeed` приходит из seed RimWorld world и round-trip'ится через XML;
- `drifterArrivalReservoir` round-trip'ится как root attribute; старые сейвы без него получают `0`;
- `MigrationGroups` читается как optional container для обратной совместимости со старыми сейвами;
- `FactionRecords` читается как optional container для обратной совместимости со старыми сейвами;
- `SettlementCapabilities` читается как optional container для обратной совместимости со старыми сейвами;
- `SpecialistPools` читается как optional container для обратной совместимости со старыми сейвами;
- `Caravans` читается как optional container для обратной совместимости со старыми сейвами;
- `playerFactionId` читается как optional root attribute: старые сейвы без него
  считаются не имеющими Core-защиты игрока, пока RimWorld layer не передаст id;
- каждый `Alive` citizen должен иметь owner;
- ownership asset и owner должны ссылаться на существующие entities;
- settlement population считается только из `Alive` citizens, которыми владеет settlement;
- resource quantity не может быть отрицательным;
- caravan source and target settlement ids must reference existing settlements;
- stable settlement slug не должен дублироваться;
- raid outcome должен балансироваться: `Sent == Active + Dead + Returned + Prisoner + Missing`.

Ресурсы в текущем XML остаются совместимыми с прежним форматом через строковый
`resourceKey`, но новый код должен использовать `WorldResourceKey` как typed
обертку с `DefName`, category, perishability, market value and mass. При записи
в v1 XML сохраняется `DefName`.

Новый контейнер:

```xml
<ProductionProfiles>
  <ProductionProfile
    settlementKind="Settlement"
    settlementId="1"
    biome="TemperateForest"
    hilliness="SmallHills"
    techLevel="Industrial"
    growingDays="55"
    rainfall="850"
    averageTemperature="21"
    foodPerAdult="4"
    steelPerAdult="2"
    medicinePerAdult="1"
    componentPerAdult="1"
    archetype="Balanced"
    laborEfficiencyPercent="100"
    economyScalePercent="100"
    complexityPenaltyPercent="100" />
</ProductionProfiles>
```

Чтение контейнера optional: старые сейвы без `ProductionProfiles` загружаются
с пустым списком профилей, после чего профиль может быть восстановлен новым
bootstrap/repair-проходом. Production-depth атрибуты optional: старые профили
без `archetype`, `laborEfficiencyPercent`, `economyScalePercent` и
`complexityPenaltyPercent` читаются как balanced 100/100/100.

Settlement infrastructure and specialists are stored as compact aggregates:

```xml
<SettlementCapabilities>
  <SettlementCapability
    settlementKind="Settlement"
    settlementId="1"
    housingCapacity="80"
    foodStorageCapacity="1200"
    medicineStorageCapacity="90"
    powerCapacity="2000"
    laboratoryCapacity="4"
    animalCapacity="60"
    cropCapacity="40"
    researchCapacity="3"
    mechanicalCapacity="2"
    pollutionHandling="1" />
</SettlementCapabilities>

<SpecialistPools>
  <SpecialistPool
    settlementKind="Settlement"
    settlementId="1"
    farmers="10"
    handlers="6"
    doctors="3"
    researchers="4"
    engineers="5"
    geneticists="1"
    mechanitors="1"
    soldiers="12"
    diplomats="2" />
</SpecialistPools>
```

Both containers are optional on load. Missing values mean the settlement has no
recorded advanced capacity yet; they do not create hidden buildings, pawns or
workers. Negative input values are normalized to zero by the ledger.

Economy wealth snapshots are cache chunks. They speed UI and downstream trade
selection, but can be rebuilt from owned resources and price books:

```xml
<SettlementWealth>
  <Wealth
    settlementKind="Settlement"
    settlementId="1"
    factionId="Outlander"
    silver="120"
    materialWealth="32"
    totalWealth="152" />
</SettlementWealth>

<FactionWealth>
  <Wealth
    factionId="Outlander"
    silver="150"
    materialWealth="42"
    totalWealth="192" />
</FactionWealth>
```

Both containers are optional on load.

Ledger-level faction lifecycle is stored separately from RimWorld `Faction`
objects:

```xml
<FactionRecords>
  <FactionRecord
    factionId="Pirates"
    status="Collapsed"
    tick="60000"
    reason="population collapse" />
</FactionRecords>
```

`FactionRecords` is optional on load. Missing records mean no faction lifecycle
state has been recorded yet; they do not imply that every faction is healthy.

Army movements also persist `statusTick` so old resolved movements can be pruned
without deleting recent UI/cooldown context:

```xml
<ArmyMovements>
  <Movement
    armyKind="Army"
    armyId="12"
    targetKind="Settlement"
    targetId="4"
    departTick="60000"
    arrivalTick="180000"
    status="Disbanded"
    statusTick="180000" />
</ArmyMovements>
```

`statusTick` is optional on load. Older saves fall back to `departTick`, then the
next pruning pass can remove stale resolved records while preserving `WorldEvent`
history.

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
