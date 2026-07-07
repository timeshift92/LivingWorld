# Living World - Public API Design

## Цель API

Living World должен дать другим модам стабильный способ взаимодействовать с глобальной симуляцией без прямых Harmony-патчей во внутренние классы.

API должен быть:

- read-friendly;
- deterministic where possible;
- event-driven;
- permission-aware;
- stable across versions.

## Основной контракт

Концептуальный интерфейс:

```text
ILivingWorldApi
  GetCitizen(id)
  TryGetCitizenByPawn(pawn)
  GetSettlement(id)
  GetPopulation(query)
  GetAnimalPopulation(query)
  GetOwner(assetId)
  GetOwnedAssets(ownerId)
  TransferOwnership(request)
  WithdrawAssets(request)
  ReturnAssets(request)
  CreateMigration(request)
  KillCitizen(request)
  AllocateRaiders(request)
  AllocateVisitors(request)
  Subscribe(eventType, handler)
```

Это не implementation code, а публичный контракт, который надо стабилизировать до того, как другие моды начнут интеграцию.

## Текущий Core baseline

В `LivingWorld.Core` уже закреплен минимальный read API:

```text
ILivingWorldApi
  GetCitizen(id)
  GetSettlement(id)
  GetSettlementPopulation(settlementId)
  GetOwner(assetId)
  GetOwnedResourceQuantity(ownerId, resourceKey)
```

Этот baseline намеренно малый: он позволяет адаптерам читать population/ownership без доступа к внутренним коллекциям `WorldState`. Write-команды пока остаются на уровне `WorldState`; перед публикацией для сторонних модов их надо обернуть в command API с reason codes и versioned events.

## Query API

Примеры queries:

- get citizen by ID;
- get citizen linked to active pawn;
- get settlement population;
- get combat-capable population by faction;
- get animal population by species/region;
- get settlement food/medicine status;
- get recent world events.

## Command API

Commands должны быть контролируемыми и валидируемыми:

- kill citizen;
- move citizen;
- transfer ownership;
- withdraw owned assets;
- return owned assets;
- destroy owned assets;
- start migration;
- create refugee group;
- allocate visitors;
- allocate raid group;
- report external pawn creation.

Каждая command должна:

- проверять invariants;
- писать event;
- обновлять derived caches;
- возвращать результат с reason code.

## Event API

Другие моды должны иметь возможность подписаться на события:

- `CitizenBorn`;
- `CitizenDied`;
- `CitizenStatusChanged`;
- `SettlementChanged`;
- `SettlementDestroyed`;
- `RaidStarted`;
- `RaidReturned`;
- `WarStarted`;
- `PeaceSigned`;
- `AnimalMigration`;
- `CropFailure`.
- `OwnershipAssigned`;
- `OwnershipTransferred`;
- `OwnershipDestroyed`;
- `OwnershipCaptured`;
- `OwnershipLooted`.

## Reason codes

Каждая операция должна возвращать объяснимый результат:

- Success;
- NotFound;
- InvalidState;
- InsufficientPopulation;
- ProtectedActivePawn;
- CompatibilityFallbackUsed;
- StrictModeBlocked;
- WouldBreakInvariant.
- InsufficientOwnedAssets;
- OwnerMismatch;
- AssetAlreadyMaterialized.

Обоснование: без reason codes интеграции будут ломаться молча.

## API stability rules

- breaking changes only on major version;
- old methods deprecated before removal;
- event payloads versioned;
- mods can query API version;
- debug UI shows registered API consumers.

## Почему API важнее чужих Harmony-патчей

Если Living World станет крупным модом, сторонние авторы захотят:

- брать реальных жителей для своих событий;
- создавать миграции;
- реагировать на войны;
- читать поселения;
- учитывать популяции животных.

Если API не будет, они начнут патчить внутренние классы. Это приведет к хрупкости, конфликтам и невозможности менять архитектуру.

## Ownership API Direction

World Ownership должен стать частью публичного контракта.

Пример будущего flow:

```text
RimWar Adapter asks Living World:
  AllocateArmy(sourceSettlement, target, requestedPower)

Living World withdraws:
  citizens
  animals
  food
  medicine
  equipment

Living World returns:
  WorldArmyId
  materialization policy
  event ids
```

Это сохраняет правильное направление зависимости: adapter consumes Living World API, not Living World consumes RimWar internals.
