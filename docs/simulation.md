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

### Перехват ванильного рейда (реализация)

Решение «взять ли ванильный рейд на реальное население» принимает чистая
Core-функция `VanillaRaidInterceptor.TryIntercept(state, request)`. Ключевое
правило: **Living World никогда не отменяет рейд.** Возможны только два исхода:

| Исход | Когда | Что делает патч |
|---|---|---|
| `Intercepted` | фракция имеет доступное боеспособное население | резервирует реальных граждан в `WorldArmy`, каппит `parms.points` под их число; рейд становится «настоящим» |
| `PassThrough` | нет данных Living World по фракции / нет доступных комбатантов | не трогает инцидент — ваниль генерит рейд как обычно (`__result` остаётся `true`) |

Раньше патч ставил `__result = false`, если у фракции не было «раскрытой
возможности» (`RaidOpportunity`), а та рождается **только** из торгового интела
с ценностью ≥ 500. Итог: фракция, с которой игрок не торговал, вообще не могла
напасть — угроза рейдов фактически отключалась. Теперь `RaidOpportunity` —
**не гейт**, а «наводка»: она потребляется как флавор, когда рейд действительно
случается, но не решает, быть ли рейду. Размер рейда берётся из `parms.points`
шторителлера.

### Возврат незадействованных резервистов (реализация)

Рейд резервирует комбатантов (переводя граждан в армию) **до** генерации пешек,
а точное число пешек зависит от стоимости pawnkind'ов, а не строго от очков.
Поэтому число пешек и число зарезервированных граждан могут не совпасть:

- **пешек меньше**, чем зарезервировано → лишние граждане никогда не выходят в
  бой. `RaidReconciliationService.ReleaseUndeployedReserves(army)` (вызывается
  сразу после привязки в `TryGenerateRaidInfo`) возвращает их в поселение как
  живых — иначе они бы навсегда зависли в армии и молча истощали поселение;
- **пешек больше**, чем зарезервировано → лишние пешки не привязываются к
  гражданам и считаются нетрекаемым «подкреплением» (их гибель не влияет на
  ledger) — это неизбежное следствие конечного населения;
- **рейд отменён** после резерва, но до генерации → осиротевший резерв
  подчищается: `TryIntercept` в начале каждого перехвата зовёт
  `ReleaseAllUndeployedReserves`, возвращая домой живых граждан армий без единой
  `RaidPawnLink`.

«Задействованным» считается только гражданин, привязанный к `RaidPawnLink`
(в любом статусе). Всё остальное, чем ещё владеет армия, — это резервист,
который в бой не пошёл, и он возвращается домой.

### Два входа в рейд

- `RaidPopulationAllocator.ReserveForRaid` — **авто-триггер** от ванильного
  инцидента через `VanillaRaidInterceptor`. Каппит по доступным взрослым и
  **учитывает голод** (голодающее поселение не отправляет рейд).
- `RaidPlanner.PlanRaid` — **явный параметризованный** запрос (число комбатантов,
  еда, сталь). Требует все запрошенные комбатанты (строгий all-or-nothing) и
  голод **не** проверяет: это осознанно, ответственность за припасы и уместность
  на вызывающей стороне. Различие намеренное, не баг.

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

