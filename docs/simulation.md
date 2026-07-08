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

### Attacked settlement map materialization (current implementation)

When the player opens an NPC settlement map, Living World now performs the
first real-map settlement materialization slice after vanilla map generation:

1. resolve the vanilla `Settlement` to the ledger settlement by faction and
   imported world tile;
2. select living adult defenders from that settlement, excluding citizens that
   already have an active materialization lease;
3. create `SettlementDefense` leases and stamp matching vanilla-generated
   defender pawns with `CompLivingWorldIdentity`;
4. move a bounded resource payload (`Steel`, meals, medicine, components,
   silver) from the settlement ledger to the materialization lease owner;
5. spawn that payload as real map things through RimWorld's normal `ThingDef`
   and `GenSpawn` APIs;
6. if binding fails, abort the preparation and return the payload to the
   settlement ledger.

This is deliberately not a full settlement generator yet. It does not build a
custom town layout, materialize facilities as buildings, spawn animal cohorts,
or reconcile every individual looted stack back from the map. The important
contract is already real: defenders are concrete ledger citizens, and spawned
loot is removed from the settlement ledger before the player can take it. If
the player later defeats the settlement, the existing defeat bridge destroys
the matched ledger settlement and only the remaining ledger-owned resources
move into the ruin.

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

**Primary path:** custom raid incident owns primary Living World raid path.
`LivingWorld_FactionRaid` выбирает hostile humanlike фракцию с доступным
ledger-населением, резервирует реальных граждан через
`RaidPopulationAllocator`, отдаёт активный бой vanilla raid flow и затем
закрывает незадействованных резервистов через `RaidReconciliationService`.

**Fallback path:** legacy vanilla raid patches are fallback. Harmony-перехват
`IncidentWorker_RaidEnemy.TryResolveRaidFaction` нужен только для совместимости
с ванильным storyteller и чужими инцидентами, которые всё ещё вызывают обычный
enemy raid. Если `LivingWorld_FactionRaid` уже зарезервировал армию на этих
`IncidentParms`, fallback-патч видит reservation и ничего повторно не
резервирует.

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

- `AnimalCohortCreated`;
- `AnimalCohortGrew`;
- `AnimalCohortDeclined`;
- `AnimalCohortMigrated`;
- `AnimalProductsHarvested`;
- `AnimalHunted`;
- `AnimalBreedingProjectStarted`;
- `AnimalBreedingProjectCompleted`;
- `AnimalCohortIncubated`.

Daily tick сначала обновляет ecology: стада растут, голодают или мигрируют
через `AnimalEcologyDriver`. Затем `AnimalProductionService` даёт
ресурсный эффект:

- домашние стада дают небольшой ежедневный food output без уменьшения
  поголовья;
- охота даёт больше еды, но уменьшает реальный wild cohort.

`AnimalBreedingDriver` завершает готовые selection/incubation projects и
детерминированно стартует новые проекты только там, где есть `AnimalCapacity`,
handlers/lab specialists и реальные feed/medicine/components. Проекты не
создают pawns: они улучшают ledger cohort или добавляют новый ledger cohort.

Wildlife spawn на карте должен выбирать существующих животных из regional pool.
Полная materialization животных на временной карте является отдельным слоем:
карта берёт животных из ledger cohort, а результат охоты/смерти/ухода
возвращается обратно в ledger.

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

Production depth adds deterministic modifiers:

- `ProductionArchetype` (`Balanced`, `Farmer`, `Miner`, `Medical`, `Warrior`);
- labor efficiency;
- economy-of-scale;
- complexity penalty.

The service multiplies these factors into effective daily output and still writes
only owned ledger resources. Virtual trade uses `VirtualTradeService`: it quotes a
deterministic price from stock pressure and silver/wealth pressure, then transfers
real goods and silver between settlement owners. No trade path creates resources.

Settlement development now has a deeper Core layer:

- births are blocked when housing is full;
- `SettlementDevelopmentService.GetTier` derives camp/village/town/city from
  population and housing;
- `WarAction.Develop` lets a faction deliberately invest in an underbuilt base;
- development can consume owned silver and grow specialist pools.

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

Core now has a defense-in-depth player faction guard. If
`WorldState.PlayerFactionId` is set, the world-war planner never selects that
faction as an autonomous target, `FactionLifecycleService` never collapses it,
and `WorldBattleService.TryResolve` returns `BlockedPlayerSettlement` rather than
silently resolving a ledger battle. A blocked player-settlement battle stands
the ledger army down and returns reserved attackers to their source settlement,
so no arrived army or citizen remains in limbo while the RimWorld layer decides
how to materialize a real player-visible incident. Non-combat world-war actions
(caravan, scouting, diplomacy) also skip the player faction; they must not
quietly trade with, spy on, or alter goodwill for player-owned settlements from
the abstract NPC simulation.

When the player defeats an NPC settlement on an actual RimWorld map, the RimWorld
layer now bridges that visible event back into the ledger through
`PlayerSettlementDefeatService.RecordDefeat`:

1. resolve the vanilla `Settlement` to the ledger settlement by faction and tile;
2. call `SettlementLifecycleService.DestroySettlement`;
3. move owned resources into a `WorldRuin`;
4. turn living citizens into refugees;
5. pressure the defeated faction's active wars through
   `PlayerConflictInterventionService`;
6. create the world-map ruin site immediately through `EnsureRuinSites`.

If the vanilla settlement cannot be matched to the ledger, Living World preserves
the older fallback and still records player war pressure for the defeated faction,
but it does not invent a ruin or resources. This keeps compatibility fail-open:
player action still affects diplomacy/war, while physical destruction and
lootable ruin sites only run when the ledger has a real settlement to own the
consequences.

Counting is deliberately broader than settlement population:

- `Alive`, `Prisoner`, `Refugee` and `Migrating` citizens still keep their
  original faction alive;
- `Dead` and `Missing` citizens do not;
- empty settlements alone are not enough to preserve a faction.

Resolved army movements are not permanent history. `ArmyMovementPruneService`
keeps traveling and recent resolved movements for UI/cooldown context, then
removes old resolved movement records. Durable history remains in `WorldEvent`.

Terminal caravans follow the same active-state rule. `WorldCaravan` remains in
the ledger while it is traveling and for a short post-resolution window, then
`CaravanPruneService` removes old `Arrived`/`Destroyed` rows. This does not
delete resources: arrival already moved cargo to the target settlement, and
destruction already removed caravan-owned cargo. Durable caravan history remains
in `WorldEvent`.

The service is idempotent. Once `WorldFactionRecord.Status == Collapsed`, later
daily passes do not emit duplicate collapse events. This keeps history readable
and prevents repeated effects.

### Player-facing information visibility

Living World's ledger may know exact population, wealth, losses and resources,
but the player UI should not expose those values by default. Settlement inspect
strings use `KnownSettlementInfo` from direct visits, scouts and traders. Global
world-war/economy views show coarse strength, population and wealth bands unless
debug logging is enabled. Exact values remain available for development and
diagnostics, not as normal omniscient gameplay information.

## Drifter lifecycle

Дрифтеры — это категория пешек, которые прибывают в мир как одиночки через инцидент, стабилизируют население поселений и получают роль через присоединение к поселению. Это минимальный срез системы прихода граждан, которая позже расширится на беженцев, фракционных подкреплений и другие источники.

### Daily drifter flow

Daily tick в `LivingWorldWorldComponent.SimulateWorldDay` запускает последовательность:

```text
Spawn/record captured drifters (RevenueService)
  -> DrifterArrivalService.SimulateArrivals  (планирует прибытие в текущий день)
  -> DrifterFoundingService.SimulateFounding (годная группа с лидером основывает новое поселение/банду)
  -> DrifterAssimilationService.SimulateAssimilation (остальные вливаются в наименее населённые поселения)
  -> (в конце дня)
  -> FactionLifecycleService.SimulateCollapses (проверка вымирания фракций)
```

Все три сервиса дрифтеров (Arrival, Founding, Assimilation) работают **только** если:

```text
settings.drifterFlowEnabled && !State.IsInitialWorldSeedingActive
```

Вымирание фракций проверяется в конце дневного тика без гейтов.

**Обоснование последовательности:**

- **Arrival** — гомеостатический расход внешнего резерва: добавляет в пул новые `Drifter`-записи, только чтобы закрыть дефицит до целевой популяции, не выше жёсткого потолка и не больше сохранённого `State.DrifterArrivalReservoir`; метрированно (1–2 в день).
- **Founding** идёт **раньше** ассимиляции: если в пуле набралась группа (≥ `drifterMinFounders`) и лучший по `LeadershipAptitude` лидер проходит порог, группа основывает новое поселение или банду; дрифтеры-основатели удаляются из пула и становятся гражданами (`FoundSettlement`).
- **Assimilation** — оставшиеся дрифтеры (старейшие по `ArrivalTick`) вливаются в наименее населённые поселения как граждане (`AssimilateDrifter`), тоже метрированно.

У `Drifter` нет поля статуса: «переход» — это удаление из пула с созданием `WorldCitizen`. Метрированные темпы оставляют небольшой постоянный пул-буфер между днями — из него берёт инцидент прибытия.

`DrifterArrivalReservoir` — конечный внешний запас людей за пределами активной
ledger-карты. Он сохраняется в `WorldState`, уменьшается при каждом arrival и
пополняется явными будущими источниками: беженцы, освобождённые пленники,
эвакуация разрушенных поселений, торговые/дипломатические события. При
bootstrap RimWorld layer создаёт стартовый резерв как
`settlements * targetWorldPopulationPerSettlement`, но daily flow не может
создавать людей сверх этого запаса.

Legacy saves from before this field are repaired once in the RimWorld component:
if the save is already bootstrapped, has settlements, and has not run the bridge,
the component seeds the reservoir and stores a migration flag. That is a save
compatibility bridge, not a replenishment rule; once the reservoir is consumed,
future refills must come from explicit gameplay sources.

Вымирание фракций (`FactionLifecycleService.SimulateCollapses`) вызывалось в дневном тике **уже до этого среза**; данный срез добавил именно drifter-конвейер (Arrival/Founding/Assimilation), а не привязку вымирания.

### LivingWorld_DrifterArrival incident

Инцидент `LivingWorld_DrifterArrival` — это точка входа для спавна дрифтер-пешек, который был запрошен дневным флоу `DrifterArrivalService`.

**Параметры:**

- **Category:** `Misc`.
  Vanilla RimWorld 1.6 does not define `AllyArrival`; `Misc` is the closest
  safe vanilla bucket for a gated arrival-style custom incident. The worker gate
  remains authoritative: the incident only fires when Living World already has a
  ledger drifter ready to materialize.
- **Worker:** `IncidentWorker_LivingWorldDrifterArrival`.
- **Gate (CanFireNowSub):** `LivingWorldWorldComponent.WantsDrifterArrival` — истина если:
  - есть дрифтер в пуле захватанных (`State.Drifters.Count > 0`).
  Дефицит населения мира сам по себе больше не открывает инцидент: дневной
  `DrifterArrivalService` сначала должен создать ledger-дрифтера, и только
  потом storyteller может материализовать его как pawn. Это убирает
  повторяющиеся «бесплатные» прибытия без записи в пуле.
- **Fail-open:** нет компонента/карты или исключение → `TryExecuteWorker` возвращает `false` (инцидент не срабатывает, ванильное поведение), без частичных мутаций мира.

**Выполнение (TryExecuteWorker):**

1. Карта — цель инцидента или `Find.AnyPlayerHomeMap`.
2. Выбрать пол **один раз** (из пула — `pooled.Sex`, иначе `tick % 2`) и передать его и в `fixedGender` генератора, и в ledger — чтобы пол пешки и запись совпадали.
3. **Сначала заспавнить пешку** (`GenSpawn.Spawn`) — ledger трогаем только после успешного спавна, иначе сбой спавна «потерял» бы дрифтера.
4. **Материализовать ledger (одноразово):**
   - Есть дрифтер в пуле: `WorldState.MaterializeDrifter(id, pawn.thingIDNumber, tick)` — удаляет дрифтера из пула и пишет событие `DrifterMaterialized`.
   - Пул пуст: `WorldState.MaterializeNewArrival(...)` — «всегда записывать»: создаёт запись-происхождение и тут же материализует (чистое изменение пула — ноль).
5. Повесить `CompLivingWorldIdentity` на пешку и `SetLedgerId(EntityId)`; отправить письмо (`SendStandardLetter`); вернуть `true`.

### CompLivingWorldIdentity

`CompLivingWorldIdentity` — это стабильная привязка пешки к ledger-записи. Компонент хранит `EntityId` (пара `EntityKind` + `long`) и гарантирует, что при `Save` / `Load` / `Garbage Collect` связь пешка↔ledger не разрывается.

**Свойства:**

- Зарегистрирована на vanilla `ThingDef Human` через `mod/Patches/LivingWorld_PawnIdentity.xml`, поэтому новые human pawns получают объявленный `ThingComp`; материализация всё равно defensively создаёт comp, если модовая/нестандартная пешка пришла без него.
- Заполняется ledger id сразу при спавне через инцидент.
- Переживает сохранение мира (сохраняется как часть `Pawn`'s list of comps).
- Используется в дальнейшем для синхронизации (dematerialization, raids, etc.).
- Это **первый** минимальный срез архитектуры идентичности (будет расширен: raid-линки, frequency comp, migration links).

`LivingWorldPawnIdentityService` является resolver-слоем для активных
pawn-событий. Патчи смерти, плена и выхода с карты сначала читают
`CompLivingWorldIdentity`; `Pawn.thingIDNumber` остаётся только fallback для
старых связей и не является долгосрочным source of truth.

`LivingWorldPawnSyncService` — единая Core-точка применения судьбы materialized
pawn. RimWorld-патчи передают в неё `(EntityId, PawnFateKind, reason)`, а сервис
находит активный `RaidPawnLink`, вызывает нужный переход `WorldState`
(`Dead/Prisoner/Returned/Missing`) и сразу пробует закрыть `WorldRaidOutcome`.
Это оставляет RimWorld-слой тонким: он только ловит факт и резолвит identity.

### Явные хвосты вне scope

Следующие возможности **нарочно оставлены вне реализации**:

1. **Frequency comp (уровень 2)** — отслеживание `StorytellerComp` на пешке, чтобы избежать скопления дрифтер-инцидентов в одно время. Сейчас шторителлер может планировать много инцидентов подряд.

2. **Полная миграция идентичности** — raid-линки (`RaidPawnLink`), которые уже существуют для рейдеров, не используются для дрифтеров. Дрифтер получает только `CompLivingWorldIdentity`; полная цепочка синхронизации (dematerialization, outcomes, prisoner conversion) добавится отдельно.

3. **Собственный raid-инцидент** — дрифтеры могут стать раидерами из поселения, в которое они ассимилировались, но не создают отдельный инцидент. Это делается отдельно, через свой raid-инцидент (см. `storyteller-normalization.md`).

4. **Освобождение вакуума при коллапсе (G2)** — когда поселение коллапсирует, его дрифтеры-ассимилированцы не освобождаются автоматически обратно в глобальный пул. Это требует явной политики переобладания и выходит на G2 milestone.

5. **Дополнительные каналы прихода** — беженцы из голодающих поселений, подкрепления из фракционных источников, рожденцы из партнерских поселений. Сейчас только дрифтеры (из пула и новые прибытия) и ассимиляция.

**Ссылка на архитектуру:** см. `docs/design/storyteller-normalization.md` для полной карты частотного компенсирования и идентификационного хребта.

## World-map mission markers

RimWorld-layer mission markers are display-only world objects. They make ledger
travel visible on the globe, but they do not pathfind, advance missions, own
resources or decide outcomes.

Current source of truth:

- `WorldArmyMovement` for marching warbands;
- `WorldCaravan` for goods caravans.

`LivingWorldWorldComponent.SyncArmyWorldObjects` reconciles
`WorldObject_LivingWorldArmy` markers from those records on load and after daily
simulation. Each marker stores:

- a stable key (`army:{id}` or `caravan:{id}`) so different Core id spaces do
  not collide;
- origin and target world tiles parsed from imported settlement slugs;
- depart and arrival ticks for visual progress;
- faction label, target label, translated mission kind and a per-kind texture.

The marker position is derived from the ledger clock with `Vector3.Slerp`
between origin and target tiles. If a marker is removed or a save reloads, the
next reconciliation can recreate it from the ledger.

Current visible mission kinds:

- **warband** — `World/LivingWorld_Warband`, backed by `WorldArmyMovement`;
- **caravan** — `World/LivingWorld_Trader`, backed by `WorldCaravan`.

Scout, settler and diplomat icons already ship with the mod, but those actions
still resolve instantly in Core. They stay as backlog for now because they need
real persistent mission records first:

- scout/diplomat missions can be lightweight travel records;
- settler missions must reserve real adults while in transit.

Markers are disabled when `worldWarEnabled` is off or Rim War is active. This
prevents double world-map driving: Rim War owns its own visible war objects,
while Living World keeps its ledger as the source of truth.

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

