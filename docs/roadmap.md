# Living World - Roadmap

## Принцип roadmap

Проект нельзя делать как один огромный "total simulation" milestone. Каждый этап должен давать рабочий результат и проверяемую ценность.

Главный вертикальный срез: real raid allocation from real population.

Если рейд можно собрать из реальных жителей поселения, провести бой, вернуть выживших, записать погибших и уменьшить силу фракции, значит основа проекта работает.

## Этап 1: Core world ledger

Цель:

- создать `LivingWorld.Core`;
- создать глобальную базу населения и поселений;
- ввести IDs, registries, services, events, save/load.

Deliverables:

- `WorldState`;
- `WorldCitizen`;
- `WorldSettlement`;
- `EntityId`;
- `EventStore`;
- базовый save/load;
- integrity validator.

Обоснование: без этого нельзя безопасно строить рейды, семьи, экономику или экологию.

## Этап 2: Real raid replacement

Цель:

- заменить генерацию рейдов на использование реальных жителей мира.

Deliverables:

- `RaidPlanner`;
- `OwnershipService`;
- `WorldOwnerId`;
- `OwnershipRecord`;
- ownership transfer events;
- combat-capable citizen queries;
- resource/supply withdrawal;
- pawn materialization;
- casualty sync;
- `RaidStarted`, `CitizenDied`, `RaidReturned` events.

Обоснование: это первый gameplay-visible proof, что "ничто не появляется из воздуха". Рейд должен забирать у поселения не только людей, но и реальные ресурсы: животных, еду, медицину, оружие, броню и припасы.

Перед реализацией `RaidPlanner` нужно добавить World Ownership, иначе рейд останется только population withdrawal и не сможет дать полные последствия для экономики.

Текущий статус:

- базовый ownership slice в `LivingWorld.Core` реализован;
- граждане получают владельца-поселение;
- `WorldArmy` создан как стратегический владелец ресурсов;
- ресурсные stack'и можно добавлять поселению и передавать армии;
- публичный read API умеет читать owner/resource quantity.
- RimWorld shell установлен как реальный мод: `LivingWorldWorldComponent` создаёт ledger при загрузке мира, импортирует поселения RimWorld, создаёт синтетическое население/ресурсы и показывает их во вкладке `Living World`.

Следующий рабочий шаг: `RaidPlanner` должен забирать у поселения реальных взрослых/боеспособных граждан и ресурсный набор в `WorldArmy`, а затем материализовать их на карте только при входе в активный gameplay.

## Этап 3: Demography and families

Цель:

- добавить семьи, возраст, рождение, смерть, поколения.

Deliverables:

- `BirthService`;
- `FamilyService`;
- aging updates;
- household model;
- family consequences after death.

Обоснование: без демографии популяция только уменьшается и мир не является living world.

## Этап 4: Economy and migration

Цель:

- поселения производят и потребляют ресурсы;
- кризисы создают миграцию, голод и беженцев.

Deliverables:

- `SettlementSimulator`;
- `EconomySimulator`;
- food/medicine/housing;
- `MigrationService`;
- `CropFailure`, `Starvation`, `RefugeesCreated` events.

Обоснование: экономика превращает численность населения в причинную систему.

## Этап 5: Ecology and animals

Цель:

- глобальная симуляция животных;
- predator-prey balance;
- wildlife на карте из реальных популяций.

Deliverables:

- `AnimalSimulator`;
- species/region pools;
- migration;
- hunting/death sync;
- animal spawn replacement.

Обоснование: животные должны подчиняться тому же принципу no fake spawning.

## Этап 6: Diplomacy, wars and world history

Цель:

- фракции воюют, заключают мир, ослабевают, распадаются и создают историю.

Deliverables:

- `WarSimulator`;
- `DiplomacyService`;
- armies;
- refugees;
- faction split;
- settlement destruction/rebuild/occupation;
- world history UI.

Обоснование: только на этом этапе появляется полноценная total world simulation.

## Этап 7: Public API and compatibility hardening

Цель:

- открыть стабильное API для других модов;
- добавить adapters для ключевых модов.

Deliverables:

- `ILivingWorldApi`;
- compatibility adapters;
- docs for modders;
- debug compatibility reports.

Обоснование: без API другие моды будут ломать внутренности через Harmony.

## Этап 8: Performance hardening

Цель:

- доказать, что архитектура выдерживает целевые масштабы.

Deliverables:

- stress tests;
- save-size tests;
- profiler;
- cache rebuild tools;
- benchmarks for 20k/50k/100k citizens.

Обоснование: performance нельзя оставить на конец, но отдельный hardening этап нужен перед public release.
