# Living World - Simulation Model

## Уровни симуляции

Living World обязан поддерживать несколько уровней детализации.

### Active

Это текущая карта игрока и другие загруженные карты.

Характеристики:

- используется полноценный vanilla RimWorld AI;
- существуют настоящие `Pawn`;
- смерть, плен, ранения, инвентарь и faction changes синхронизируются обратно в world ledger.

Обоснование: на активной карте игрок ожидает vanilla качество симуляции. Нельзя заменять RimWorld AI статистикой там, где игрок видит детали.

### Regional

Это близкие регионы, соседние поселения, активные армии, торговые пути, миграционные маршруты.

Характеристики:

- simplified simulation;
- периодические обновления;
- группы и поселения симулируются без полного pawn AI.

Обоснование: игрок может скоро столкнуться с этими событиями, поэтому регион должен быть детальнее глобального мира.

### Global

Это все остальное.

Характеристики:

- статистическая симуляция;
- daily/quadrum/seasonal updates;
- никаких активных pawn'ов;
- работа через агрегаты, кеши и события.

Обоснование: только так можно поддержать 20,000-100,000 людей и 100,000+ животных без major FPS loss.

## World update pipeline

```text
1. Capture active map changes
2. Sync materialized pawns back to world records
3. Run scheduled domain services
4. Resolve incident demands through existing entities
5. Emit world events
6. Update derived caches
7. Run integrity checks
```

## Pawn lifecycle

### Birth

1. `BirthService` создает `WorldCitizen`.
2. Назначаются родители, семья, поселение, фракция.
3. Записывается событие `CitizenBorn`.
4. `Pawn` создается только если рождение произошло на активной карте.

### Materialization

Когда человеку нужно появиться в игре:

1. система выбирает существующий `WorldCitizen`;
2. `PawnMaterializationService` создает `Pawn`;
3. `Pawn` получает ссылку на `EntityId`;
4. vanilla systems работают с pawn'ом как обычно.

Причины materialization:

- raid;
- trader group;
- visitors;
- refugee event;
- prisoner transfer;
- caravan;
- quest;
- active settlement map.

### Dematerialization

Когда pawn уходит из активного контекста:

1. `PawnDematerializationService` читает его состояние;
2. обновляет `WorldCitizen`;
3. записывает события;
4. освобождает active pawn link.

### Death

Смерть всегда постоянна.

Если гражданин умер:

- обновляется `WorldCitizen.Status`;
- пишется `CitizenDied`;
- семья получает последствия;
- поселение теряет человека;
- военная/экономическая сила пересчитывается.

## Raid lifecycle

```text
Storyteller requests raid
  -> RaidPlanner identifies faction and source settlement
  -> Military service queries real combat-capable citizens
  -> Selected citizens are materialized as pawns
  -> Vanilla combat happens
  -> Survivors, deaths, prisoners and missing citizens are synced
  -> Settlement and faction state changes permanently
  -> RaidReturned / CitizenDied events are written
```

Если запрошено 45 рейдеров, а доступно только 28:

- рейд уменьшается;
- рейд откладывается;
- подключается соседнее поселение;
- событие заменяется на слабую атаку/засаду/требование дани;
- но fake soldiers не создаются.

### Привязка pawn'ов и разрешение исхода (реализация)

Когда ванильный рейд выходит на карту игрока, каждый заспавненный `Pawn`
привязывается к зарезервированному `WorldCitizen` через `RaidPawnLink`
(ключ — `Pawn.thingIDNumber`). Владение гражданином на время рейда переходит
от поселения к армии (`WorldArmy`).

`RaidPawnLink.Status` — это конечный автомат:

```text
Active ──kill patch────────────────► Dead
       ──capture patch───────────────► Prisoner
       ──exit/despawn (сам ушёл)─────► Returned   (владение возвращается поселению, гражданин снова Alive)
       ──exit/despawn (сбит, потерян)─► Missing    (гражданин Missing, домой не возвращается, не воскрешается)
```

Решение о том, чем является выход pawn'а с карты, принимает **чистая**
(без RimWorld-типов, потому юнит-тестируемая) функция
`RaidPawnExitPolicy.Resolve(isDead, isPrisoner, isDowned)`:

| Состояние pawn'а при выходе | Действие | Обоснование |
|---|---|---|
| `isDead` | `Ignore` | смерть уже разрешена патчем `Pawn.Kill` → `MarkPawnDead`; не разрешаем дважды |
| `isPrisoner` | `Capture` | пленён игроком; приоритетнее «сбит» |
| `isDowned` (не мёртв, не пленён) | `Miss` | сбит и покидает карту без известной судьбы → потерян |
| иначе | `Return` | ушёл с карты своим ходом → благополучно вернулся домой |

Приоритет важен: пешку, которую унесли в плен сбитой, `CapturedBy` помечает
пленной **до** деспавна, поэтому политика даёт `Capture`, а не `Miss`.

**Почему `Missing`, а не «вернулся».** Раньше любой не-мёртвый и не-пленный
pawn при деспавне засчитывался как `Returned`: сбитый враг, брошенный на
карте, «воскресал», возвращался в поселение как `Alive` и завышал счётчик
возвратов. Теперь такой рейдер получает статус `Missing`: гражданин исключён
из населения поселения, не воскресает и не числится ни погибшим, ни
вернувшимся — его судьба неизвестна.

**Разрешение рейда.** После каждого перехода вызывается
`RaidOutcomeService.TryRecordResolvedRaidOutcome`. Пока хотя бы одна связь
армии в статусе `Active`, исход не фиксируется. Когда активных не осталось,
единожды записывается `WorldRaidOutcome` со счётчиками `Dead / Returned /
Prisoner / Missing` (сумма = `Sent`) и событием `RaidResolved`. Именно
`Missing`-переход гарантирует, что рейд с сбитым-и-потерянным рейдером всё
равно закроется, а не зависнет в `Active` навсегда.

Владение: `Dead`, `Prisoner` и `Missing` граждане остаются во владении армии
(из населения поселения их исключает фильтр владения); домой возвращается
только `Returned`. `WorldRaidOutcome.Missing` сериализуется в сейв; старые
сейвы без атрибута читаются с `missing = 0` (обратная совместимость).

## Animal lifecycle

Животные тоже являются частью global world.

События:

- `AnimalBorn`;
- `AnimalDied`;
- `AnimalMigration`;
- `AnimalTamed`;
- `AnimalHunted`;
- `AnimalStarved`.

Wildlife spawn на карте должен выбирать существующих животных из regional pool.

Если волков стало больше:

1. уменьшается популяция оленей;
2. при нехватке добычи начинается starvation;
3. популяция волков падает;
4. олени восстанавливаются.

Обоснование: экология должна стабилизироваться сама, а не полагаться на магическое появление животных.

## Settlement lifecycle

Каждое поселение имеет:

- population;
- children/adults/elderly;
- soldiers/workers;
- food;
- medicine;
- livestock;
- production;
- housing;
- economy;
- technology;
- military power.

Daily settlement update:

```text
1. Produce food and goods
2. Consume food and medicine
3. Update health and diseases
4. Run births/deaths/aging
5. Update work allocation
6. Update military readiness
7. Update migration pressure
8. Emit crisis or recovery events
```

Destroyed settlements remain destroyed unless rebuilt, occupied or resettled.

## Real consequence chain

Пример цепочки:

```text
Player kills 300 raiders
  -> faction loses 300 real citizens
  -> source settlements lose workers and soldiers
  -> food production drops
  -> families lose adults
  -> birth rate drops
  -> refugees may appear
  -> rival faction sees weakness
  -> war or tribute demand starts
```

Это должно быть не scripted event, а естественный результат работы Population, Economy, Military и Diplomacy modules.

