# Living World — Абсорбция Rim War в ledger (анализ + план)

Статус: **анализ и стратегическое направление к согласованию.** Отвечает на задачу
«Rim War себе перебрать»: реверс-инжиниринг модели Rim War, отображение на наш
ledger и план, как Living World **замещает** Rim War своей мировой симуляцией
(без двойного вождения фракций и без зависимости от чужого мода).

Связано: [gap-analysis.md §D](gap-analysis.md) (Rim War активен, «двойное вождение»,
приоритет №1 совместимости), [population-flow.md](population-flow.md),
[roadmap.md](../roadmap.md) (Этап 6 «total world simulation»),
[storyteller-normalization.md](storyteller-normalization.md) (свой инцидент/сторителлер).

## 1. Что такое Rim War (по исходникам `TorannD/RimWar---Threaded` + Defs v1.6)

Rim War (`Torann.RimWar`, зависит HugsLib+Harmony) — это **мировая военная
симуляция фракций** поверх ванильной статики. Модель (подтверждена исходниками —
`WorldComponent_PowerTracker` = мозг, `RimWarData`, `WorldUtility`,
`RimWarSettlementComp`; это **«Threaded»-форк**, гоняющий симуляцию вне main-потока
ради перфа — наш аналог не потоки, а дневной тик + кэш-агрегаты, §C):

### 1.1 Сущности (`RimWar.Planet.*`)
- **`RimWarSettlementComp`** — вешается на каждое поселение мира и хранит его
  **«очки» (points)** = военно-экономическая мощь. Растёт со временем
  (`settlementGrowth` + `bonusGrowth` по поведению), стартовые — `settlementGenPoints`.
- **`WarObject` / `Warband_Caravan`** — движущийся по карте мира **варбанд** (несёт
  очки/пешек), с движением (`movementBonus`, `nextTileCost`/`DefaultPathCost`).
- **`CapitolBuilding`** — столица фракции (маркер).
- **`RimWarSite` / `BattleSite`** — интерактивные сайты, места сражений
  (form-caravan + enter-cooldown компоненты).
- **`RimWar.History`** — журнал мировых событий (`RW_HistoryEventDefs`:
  `RW_DiplomacyAction`, `RW_ReinforcedSettlement`, `RW_UnitRequest`,
  `RW_RandomizeRelations`).

### 1.2 Поведение фракций (`RimWarDef`)
Каждой людской фракции назначается **архетип поведения**, задающий её мировую AI:

| Behavior | Смысл |
|---|---|
| Expansionist | активно основывает поселения |
| Cautious | обороняется, растёт медленно/безопасно |
| Merchant | шлёт торговые караваны, богатеет |
| Aggressive | чаще атакует |
| Warmonger | максимум атак/экспансии (пираты, rough-племена) |
| Random / Undefined | случайно |
| Player / Vassal / Excluded | игрок / под игроком / вне Rim War |

Плюс множители: `growthBonus`, `combatBonus`, `movementBonus`, флаги
`createsSettlements`, `hatesPlayer`, `movesAtNight`.

### 1.3 Петля (`WorldComponent_PowerTracker.WorldComponentTick` → `UpdateFactions`)
1. Поселения **копят `RimWarPoints`** (в `UpdateFactions`: heal/grow ≈
   `Rand.Range(.005,.01) × RimWarPoints` за апдейт × `bonusGrowth`; частота
   `rwdUpdateFrequency` ≈ 2500 тиков).
2. Поселение **тратит очки на действие `RimWarAction`** — набор ровно из шести:
   `Diplomat`, `Caravan` (торговый караван), `ScoutingParty`, `Warband` (варбанд
   идёт по карте), `LaunchedWarband` (дроп-поды), `Settler` (экспансия — новое
   поселение). Выбор **вероятностный, взвешенный по behavior** (`ActionTypesCount`
   режет набор: нет settler если `!createsSettlements`, нет launched если
   `!CanLaunch`, −1 для Warmonger). **Дальность цели — по behavior**
   (`GetEngagementRange`: Warmonger 4, Aggressive 3, Expansionist/Random 2,
   Cautious/Merchant 1).
3. Варбанды **движутся** (`WarObject`/`WarObject_PathFollower`) к цели и
   **разрешают бой по очкам** (`combatBonus`); победа/поражение меняют очки,
   могут уничтожить/захватить поселение.
4. **Отношения** фракций дрейфуют (`Diplomat`, `RW_DiplomacyAction`,
   `RW_RandomizeRelations`).
5. Всё пишется в **History**; рейды на игрока материализуются как варбанды/поды
   (`IncidentWorker_WarObjectRaid`, `TransportPodsArrivalAction_JoinBattle/ReinforceSettlement`).

**Вывод:** Rim War — это ровно та «живая мировая симуляция», к которой идёт Living
World (roadmap Этап 6), но: (а) хранит мощь как абстрактные «очки», а не реальное
население; (б) материализует через свои world-объекты и Harmony-патчи; (в) ведёт
фракции параллельно нам → **двойное вождение** (§D).

## 2. Отображение на наш ledger (что уже есть)

Ключевое преимущество: **у нас мощь = реальное население** («ничто из воздуха»),
а не абстрактные очки. Большая часть каркаса уже есть.

| Rim War | Living World (есть) | Зазор |
|---|---|---|
| `RimWarSettlementComp.points` | `WorldSettlement` + население (`WorldCitizen`), экономика/производство (Codex) | нужен производный **индекс мощи** (население×оснащённость), кэш |
| `WarObject`/варбанд | `WorldArmy` (+ raid reservation/binding) | нужно **движение армий по карте мира** + прибытие |
| Behavior-архетипы | — (нет) | добавить `FactionBehavior` в ledger-фракцию + множители |
| Петля «очки→действие» | частично: рейды (`RaidPlanner`), founding (drifter), миграция, экономика | нужен **планировщик действий фракции** по поведению |
| Дипломатия/отношения | — (пиггибэк на ваниль) | своя `DiplomacyService` (Этап 6) |
| Экспансия/капитолии | drifter-founding + `FoundSettlement`; вымирание (`FactionLifecycleService`) | привязать к поведению; капитолий = флаг поселения |
| History | `WorldEvent` (append-only) + World History | расширить типы событий |
| Материализация рейда игроку | свой `LivingWorld_FactionRaid` инцидент (в работе, core-loop) | переиспользуем |

Итог: **~60% каркаса Living World уже покрывает Rim War**; не хватает трёх
крупных кусков — (1) индекс мощи поселения, (2) движение армий по карте + бой
NPC-vs-NPC, (3) планировщик действий по behavior-архетипам + дипломатия.

## 3. Стратегическая развилка

- **(A) Адаптер/сосуществование.** Детектим Rim War, уступаем ему вождение
  фракций, сами лишь бэкаем население. Дёшево, но **сохраняет двойную систему и
  зависимость**, и Rim War всё равно двигает варбанды абстрактными очками. Не
  решает §D по существу.
- **(B) Абсорбция/замещение (рекомендуется, соответствует «перебрать себе»).**
  Living World реализует петлю Rim War в ledger'е на **реальном населении**, и
  Rim War **выключается** из модлиста. Одна авторитетная система, лор-честная
  (мощь = люди), тестируемая в Core. Больше работы, но это и есть Этап 6.

При (B) Rim War не патчим и не адаптируем — мы его **заменяем**. На время
перехода: Living World и Rim War **взаимоисключающи** (если Rim War включён —
Living World-петля мировой войны отключается фичефлагом, чтобы не было двойного
вождения; когда Living World-петля готова — игрок убирает Rim War).

## 4. План абсорбции (фазовый, ledger-first, по TDD)

Всё — чистая логика в `LivingWorld.Core`, материализация консервативная (§11
population-flow). Каждая фаза самодостаточна и тестируема.

### Фаза R1 — Индекс мощи поселения (Core)
`SettlementPowerService`: производная «мощь» = f(живое население, оснащённость/
ресурсы, тип поселения). Кэш-агрегат (пересчёт в дневном тике, не в горячем пути).
Заменяет абстрактные `points` Rim War реальными данными. **Не** новое хранимое
поле — вычисляемое из существующего ledger'а.

### Фаза R2 — Behavior-архетипы фракций (Core)
`FactionBehavior` enum (Expansionist/Cautious/Merchant/Aggressive/Warmonger/…) +
множители (growth/combat/movement) на `WorldFactionRecord` (или отдельный
`FactionProfile`). Назначение — из def (как RimWarDef) с fail-open дефолтом.
`FactionBehaviorService` — выбор действия по архетипу и мощи.

### Фаза R3 — Движение армий по карте мира (Core)
`WorldArmy` получает позицию/цель/ETA и `ArmyMovementService.SimulateDay`
(перемещение по тайлам, `pathCost`-абстракция без RimWorld-reachability в Core).
Прибытие армии к цели → бой. Переиспользует reservation/binding из raid-работы.

### Фаза R4 — Бой NPC-vs-NPC + последствия (Core)
`WorldBattleService`: разрешение боя двух сил по мощи (`combatBonus`), потери в
**реальном населении**, победа/поражение → захват/разрушение поселения, обновление
`FactionLifecycleService` (вымирание при 0). Всё — перетоки, без создания из
воздуха. Пишет World History.

### Фаза R5 — Планировщик действий фракции (Core)
`FactionActionPlanner.SimulateDay`: воспроизводит выбор `RimWarAction` — по
behavior'у (вероятностные веса) и накопленной мощи тратит «бюджет» на один из
шести аналогов: `Warband`/`LaunchedWarband` (атака — R3/R4), `Settler` (экспансия
— переиспользует drifter-founding), `Caravan` (торговля — экономика Codex),
`ScoutingParty` (разведка/интел — уже есть `RaidIntelService`), `Diplomat` (R6).
Дальность цели по behavior (аналог `GetEngagementRange`). Гомеостаз + потолки
(никакого бесконечного спавна).

### Фаза R6 — Дипломатия/отношения (Core)
`DiplomacyService` (Этап 6): ledger-отношения фракций (враг/нейтрал/союзник),
дрейф, влияние войн; материализация в ванильный goodwill консервативно/за флагом.

### Фаза R7 — Материализация и UI
- Рейды игроку — через уже делаемый `LivingWorld_FactionRaid` (core-loop).
- Мировые варбанды/сайты — опционально как world-объекты (за флагом, аккуратно с
  производительностью; можно оставить абстрактными до Этапа 6-UI).
- Main-tab / World History UI: тренды мощи фракций, войны, экспансия (легибельность,
  §E).
- **Фичефлаг «Living World world-war loop»**, взаимоисключающий с активным Rim War.

## 5. Совместимость и риски

- **Двойное вождение (§D):** решается фичефлагом — Living World-петля и Rim War не
  работают одновременно. Детект Rim War по `ModLister`/packageId `Torann.RimWar`.
- **Производительность (§C):** петля на частоте дневного тика + кэш-агрегаты мощи,
  никаких per-citizen проходов в горячих путях; масштаб требует
  **компактной сериализации/когорт** (отдельный трек §C) до 20k–100k.
- **Материализация фракций в RimWorld `Faction`:** остаётся консервативной/за
  флагом (§11) — новые ledger-фракции сперва как источники рейдов.
- **Сейв-совместимость:** новые ledger-поля (behavior, позиции армий) — back-compat
  сериализация, как делали для raid/missing.
- **Пересечение с текущим core-loop:** R-фазы трогают `WorldState`/армии/фракции —
  **координировать** с параллельной core-loop работой (raids/identity), чтобы не
  конфликтовать; R-работа идёт в своём worktree/ветке и мержится после core-loop.

## 6. Порядок и связь с roadmap

R1→R2 (дёшево, разблокируют остальное) → R3→R4 (мировая война) → R5 (мозг) →
R6 (дипломатия) → R7 (материализация/UI/флаг). Это фактически **исполнение Этапа 6
roadmap**, где Rim War служил референсом механик, а не зависимостью.

## 7. Открытые вопросы (к согласованию)

- **Развилка §3: подтвердить (B) абсорбцию** (а не адаптер).
- Держать ли мировые варбанды **абстрактными** в ledger (дёшево) или
  материализовать world-объектами (как Rim War) сразу? (Рекомендую абстрактные до
  R7.)
- Назначение behavior-архетипов: копировать раскладку Rim War (`RimWarDef`) или
  задать свою? (Можно взять их как стартовые дефолты.)
- Момент выключения Rim War: сразу (флаг взаимоисключения) или после готовности
  R1–R5?
- Приоритет относительно других хвостов (нормализация core-loop идёт параллельно;
  §C компактная сериализация — предпосылка масштаба).
