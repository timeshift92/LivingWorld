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

### Deterministic seed

Living World uses the RimWorld world seed (`World.info.seedString`) as the
source for deterministic simulation. The seed is converted with a stable hash
and stored in `WorldState`.

Reason: two different RimWorld worlds must not share the same deterministic
birth/production/migration pattern merely because the mod used a constant seed.

### Daily catch-up

The RimWorld world component runs daily services by advancing
`lastSimulatedDay` one day at a time:

```text
while lastSimulatedDay < currentDay and catch-up cap not reached
  lastSimulatedDay++
  run production
  run food/birth simulation
  run demography
  run migration pressure
  run faction lifecycle collapse checks
```

The cap protects the game from a large load-time catch-up spike. Remaining days
continue on later ticks instead of being silently skipped.

### Initial seeding vs runtime generation

Initial bootstrap is explicitly marked as `RunInitialWorldSeeding(...)`.
During that phase Living World may create baseline citizens/resources from
RimWorld world objects to seed an existing save/world.

After bootstrap, runtime systems should transform existing ledger entities:

- births create citizens through demographic rules;
- ageing and natural mortality are handled by `DemographyService`, so population can shrink without combat;
- raids move citizens settlement -> army -> returned/dead/prisoner/missing;
- trade moves resources into/out of settlement ledgers;
- migration creates `WorldMigrationGroup` records rather than instant fake population.
- faction collapse is recorded by `FactionLifecycleService` when a tracked faction has no living citizens left.

This distinction preserves the "nothing appears from nowhere" design rule while
still allowing first-run world seeding.

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
1. Produce food and goods from settlement production profile
2. Consume food and medicine
3. Update health and diseases
4. Run births/deaths/aging
5. Update work allocation
6. Update military readiness
7. Update migration pressure
8. Emit crisis or recovery events
```

### Settlement production profile (current implementation)

Каждое импортированное RimWorld-поселение получает `SettlementProductionProfile`.
Профиль строится из:

- biome / tile plant density;
- hilliness;
- average temperature;
- estimated growing days;
- faction technology estimate.

Daily tick запускает `SettlementProductionService` до потребления еды. Сервис
добавляет реальные owned resources в ledger поселения:

- `PackagedSurvivalMeal`;
- `Steel`;
- `MedicineIndustrial`;
- `ComponentIndustrial`.

Это не просто UI-цифры: произведенные ресурсы становятся частью `WorldState`,
сохраняются в сейв и могут быть потрачены на питание, рейды, миграцию и будущие
караваны. Производство намеренно зависит от местности и технологии, а не только
от числа жителей.

### Settlement knowledge visibility (current implementation)

Player-facing settlement UI не должен показывать глобальный ledger напрямую.
Сведения проходят через `KnownSettlementInfo`:

- `Public` и `Trade` дают оценки, но не точные значения;
- `DirectVisit` дает `Confirmed` и разрешает точные значения;
- у записи есть `Tick`, поэтому UI показывает давность и признак устаревания;
- более слабый источник не затирает уже подтвержденное прямым посещением знание.

Сейчас в знание входят:

- population band;
- food status;
- migration status;
- production band.

Точное производство (`workers`, `food/day`, `steel/day`, `medicine/day`,
`components/day`, biome, hilliness, tech) показывается только если
`ExactValuesVisible = true`. Иначе UI показывает, что точные сведения неизвестны
и нужны свежие разведданные или посещение карты поселения.

### Trade ledger (current implementation)

Торговля больше не является только источником `IntelReport`. Закрытие RimWorld
trade dialog теперь проходит через `SettlementTradeLedgerService`:

- выбирается первое известное Living World поселение той же фракции;
- товары, проданные игроком торговцу, добавляются в owned resources поселения;
- товары, купленные игроком у торговца, списываются из owned resources поселения;
- если у поселения недостаточно товара, списывается только доступное количество;
- если фракция есть в игре, но ее поселения нет в ledger, ресурсы не создаются
  из воздуха, но trade intel всё равно записывается.

Trade intel остается отдельным следом: дорогие или чувствительные товары
(`Gold`, psychoid/psychite drugs, luciferium и т.п.) по-прежнему могут создать
`RaidOpportunity`. Отличие в том, что теперь торговый след одновременно меняет
экономику поселения и объясняет будущий интерес фракции к игроку.

### Demography lifecycle (current implementation)

Daily simulation now has an explicit demographic lifecycle:

- `DemographyService` ages `Alive` citizens on a configured age interval
  (`AgeIntervalDays`, currently one RimWorld year);
- citizens crossing the natural death age can die from old age;
- deaths write `CitizenDied` and remove that citizen from settlement population
  through status filtering;
- ageing writes `CitizenAged` for surviving citizens.

This is deterministic and capped per day. The goal is to prevent population from
only growing through births while avoiding a large one-tick death spike in old
saves.

### Migration groups (current implementation)

Migration is no longer a direct settlement-to-settlement teleport when a safe
target exists:

```text
starving settlement
  -> citizen becomes Refugee
  -> WorldMigrationGroup is created if a stable target exists
  -> citizen becomes Migrating and is owned by the group
  -> on/after ArrivalTick, citizen joins target settlement
```

If no target exists, the citizen stays a self-owned `Refugee` and remains visible
to migration pressure queries. This keeps the "nothing from nowhere" rule:
people move through ledger ownership instead of being silently copied into
another settlement.

Destroyed settlements remain destroyed unless rebuilt, occupied or resettled.

### Faction lifecycle (current implementation)

`FactionLifecycleService` is the ledger-level answer to population collapse.
It does not destroy or create RimWorld `Faction` objects. It records that, from
Living World's point of view, a tracked faction has collapsed because no living
citizens remain.

Counting is deliberately broader than settlement population:

- `Alive`, `Prisoner`, `Refugee` and `Migrating` citizens still keep their
  original faction alive;
- `Dead` and `Missing` citizens do not;
- empty settlements alone are not enough to preserve a faction.

The service is idempotent. Once `WorldFactionRecord.Status == Collapsed`, later
daily passes do not emit duplicate collapse events. This keeps history readable
and prevents repeated effects.

## Drifter lifecycle

Дрифтеры — это категория пешек, которые прибывают в мир как одиночки через инцидент, стабилизируют население поселений и получают роль через присоединение к поселению. Это минимальный срез системы прихода граждан, которая позже расширится на беженцев, фракционных подкреплений и другие источники.

### Daily drifter flow

Daily tick в `LivingWorldWorldComponent.SimulateWorldDay` запускает последовательность:

```text
Spawn/record captured drifters (RevenueService)
  -> DrifterArrivalService.SimulateArrivals  (планирует прибытие в текущий день)
  -> DrifterFoundingService.SimulateFounding (переводит прибывших в статус основателей)
  -> DrifterAssimilationService.SimulateAssimilation (ассимилирует основателей в поселения)
  -> (в конце дня)
  -> FactionLifecycleService.SimulateCollapses (проверка вымирания фракций)
```

Все три сервиса дрифтеров (Arrival, Founding, Assimilation) работают **только** если:

```text
settings.drifterFlowEnabled && !State.IsInitialWorldSeedingActive
```

Вымирание фракций проверяется в конце дневного тика без гейтов.

**Обоснование последовательности:**

- **Arrival** создаёт новые `WorldDrifter`-записи (либо из пула захватанных, либо как новые прибытия).
- **Founding** берёт дрифтеров с `Status == Arrived` и переводит их в `Status == Founding`, если поселение спокойно.
- **Assimilation** берёт дрифтеров с `Status == Founding` и выбирает целевое поселение, затем вызывает `DrifterAssimilationService.CompleteAssimilation`, который создаёт `WorldCitizen`, присвязывает дрифтера и ставит гейт обратно.

Таким образом, за один день дрифтер может пройти только один переход; полный цикл Arrived→Founding→Assimilated обычно занимает 3-5 дней в зависимости от миграционного давления и емкости поселений.

### LivingWorld_DrifterArrival incident

Инцидент `LivingWorld_DrifterArrival` — это точка входа для спавна дрифтер-пешек, который был запрошен дневным флоу `DrifterArrivalService`.

**Параметры:**

- **Category:** `AllyArrival` (не враг, не торговец, союзный одиночка).
- **Worker:** `IncidentWorker_LivingWorldDrifterArrival`.
- **Gate (CanFireNowSub):** `LivingWorldWorldComponent.WantsDrifterArrival` — истина если:
  - есть дрифтер в пуле захватанных, **ИЛИ**
  - текущее население мира ниже целевого (target population из `WorldState.PopulationDensityTarget`).
- **Fail-open:** если worker падает, инцидент отмечается как успешный, чтобы storyteller не зависел.

**Выполнение (TryExecuteWorker):**

1. Выберите случайное поселение (или используйте предзаданное, если это вызов из плана).
2. **Спавньте пешку** через vanilla Pawn generator с дефолтными параметрами.
3. **Создайте запись ledger:**
   - Если есть дрифтер в пуле: `DrifterMaterializationService.MaterializeDrifter(pawn)` — переведите пул-дрифтера в статус Arrived.
   - Если нет: `DrifterMaterializationService.MaterializeNewArrival(pawn)` — создайте новый `WorldDrifter` с `Status == Arrived`. Это всегда записывает пешку как новый прибытие (история).
4. **Привяжите пешку к ledger:** прикрепите `CompLivingWorldIdentity` с `EntityId` дрифтера.
5. Успешно вернитесь.

### CompLivingWorldIdentity

`CompLivingWorldIdentity` — это стабильная привязка пешки к ledger-записи. Компонент хранит `EntityId` (тип `long`) и гарантирует, что при `Save` / `Load` / `Garbage Collect` связь пешка↔ledger не разрывается.

**Свойства:**

- Прицеплена к пешке сразу при спавне через инцидент.
- Переживает сохранение мира (сохраняется как часть `Pawn`'s list of comps).
- Используется в дальнейшем для синхронизации (dematerialization, raids, etc.).
- Это **первый** минимальный срез архитектуры идентичности (будет расширен: raid-линки, frequency comp, migration links).

### Явные хвосты вне scope

Следующие возможности **нарочно оставлены вне реализации**:

1. **Frequency comp (уровень 2)** — отслеживание `StorytellerComp` на пешке, чтобы избежать скопления дрифтер-инцидентов в одно время. Сейчас шторителлер может планировать много инцидентов подряд.

2. **Полная миграция идентичности** — raid-линки (`RaidPawnLink`), которые уже существуют для рейдеров, не используются для дрифтеров. Дрифтер получает только `CompLivingWorldIdentity`; полная цепочка синхронизации (dematerialization, outcomes, prisoner conversion) добавится отдельно.

3. **Собственный raid-инцидент** — дрифтеры могут стать раидерами из поселения, в которое они ассимилировались, но не создают отдельный инцидент. Это вулканизируется через расширение `RaidPlanner` на дрифтер-пулы.

4. **Освобождение вакуума при коллапсе (G2)** — когда поселение коллапсирует, его дрифтеры-ассимилированцы не освобождаются автоматически обратно в глобальный пул. Это требует явной политики переобладания и выходит на G2 milestone.

5. **Дополнительные каналы прихода** — беженцы из голодающих поселений, подкрепления из фракционных источников, рожденцы из партнерских поселений. Сейчас только дрифтеры (из пула и новые прибытия) и ассимиляция.

**Ссылка на архитектуру:** см. `docs/design/storyteller-normalization.md` для полной карты частотного компенсирования и идентификационного хребта.

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

