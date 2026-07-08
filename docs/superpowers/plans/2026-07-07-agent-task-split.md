# LivingWorld Agent Task Split Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement assigned tasks. This file is the coordination layer; detailed feature plans live in the linked design and implementation documents.

**Goal:** Keep Claude/Codex work parallel without duplicate ownership, lost fixes, or conflicting edits.

**Architecture:** `main` is the shared integration branch. `WorldState` remains the source of truth; each agent works in an isolated branch/worktree, rebases or fast-forwards from `origin/main`, and pushes only verified commits. Compatibility adapters are secondary until LivingWorld's own core systems are stable.

**Tech Stack:** C#/.NET, RimWorld 1.6, Harmony, Verse/RimWorld APIs, XML Defs, RimWorld keyed localization, custom test runner in `src/LivingWorld.Tests/Program.cs`.

---

## Existing Plans

Use these documents as the current source of planning truth:

- `docs/roadmap.md` - macro project stages from core ledger to API/performance hardening.
- `docs/research/livingworld-foundation.md` - final source-of-truth architecture and integration rules.
- `docs/design/rimwar-absorption.md` - R1-R7 world-war absorption plan.
- `docs/design/rimwar-absorption-edge-cases.md` - invariants and edge-case matrix for R1-R7.
- `docs/design/population-flow.md` - drifter, settlement growth, migration, and materialization direction.
- `docs/design/gap-analysis.md` - known gaps, compatibility pressure, and performance concerns.
- `docs/superpowers/plans/2026-07-07-livingworld-core-loop.md` - implemented core playable loop plan.
- `docs/superpowers/plans/2026-07-07-drifter-flow-materialization.md` - drifter flow/materialization plan.

## Current Integrated Baseline

As of `origin/main` commit `021795d`:

- R1-R7 world-war loop is in `main`.
- `WorldBattleService` preserves survivor ownership after NPC battles.
- Battle power applies `FactionBehavior.CombatMultiplier`.
- The world-war loop is wired behind the Rim War safety flag.
- World-war history events and warband cooldown are integrated.
- Expansionist `Settler` action founds colonies by moving real adults from the source settlement.
- Verification while adding this coordination plan: `dotnet run --project src\LivingWorld.Tests\LivingWorld.Tests.csproj` passed 157 tests, and `dotnet build LivingWorld.sln` passed with 0 warnings and 0 errors.

## Coordination Rules

1. Do not merge an older branch over newer `main`. Port only the needed commits or recreate the change on top of `origin/main`.
2. Keep Compatibility adapters second. Finish LivingWorld's own core, UI, save/load, and gameplay consequences first.
3. Every behavior change starts with a failing test in `src/LivingWorld.Tests/Program.cs`.
4. Every pushed change must run:

```powershell
dotnet run --project src\LivingWorld.Tests\LivingWorld.Tests.csproj
dotnet build LivingWorld.sln
git diff --check HEAD~1..HEAD
```

5. Commit author and committer must be `Nurbek Akhmedov <timeshift92@outlook.com>`.
6. No co-author trailers.
7. If two agents need the same file, the agent who owns the core behavior lands first; the UI/adapter agent rebases and consumes that API.

## File Ownership

### Codex Lane: Core And Simulation

Primary files:

- `src/LivingWorld.Core/*`
- `src/LivingWorld.Tests/Program.cs`
- `docs/design/*`
- `docs/roadmap.md`
- `docs/research/livingworld-foundation.md`

Responsibilities:

- ledger invariants;
- population/resource ownership;
- world-war action execution;
- economy and production formulas;
- migration/refugees;
- save/load and back-compat;
- performance and scale tests;
- public API shape.

### Claude Lane: RimWorld Integration And Visibility

Primary files:

- `src/LivingWorld.RimWorld/*`
- `mod/Defs/*`
- `mod/Patches/*`
- `mod/Languages/*`
- `docs/simulation.md`
- `docs/compatibility.md`

Responsibilities:

- RimWorld incidents and world-component wiring;
- main-tab and inspect-string visibility;
- player-facing letters and notifications;
- Russian and English localization;
- install script/live mod verification;
- fail-open Harmony behavior;
- Rim War safety flag and user-facing settings.

### Shared Files

These files are shared and require extra care:

- `src/LivingWorld.Tests/Program.cs`
- `src/LivingWorld.Core/WorldState.cs`
- `src/LivingWorld.Core/WorldStateCodec.cs`
- `src/LivingWorld.RimWorld/LivingWorldWorldComponent.cs`
- `mod/Languages/English/Keyed/LivingWorld.xml`
- `mod/Languages/Russian/Keyed/LivingWorld.xml`

Rule: rebase from `origin/main` immediately before editing or pushing shared files.

## Next Work Queue

### Codex Task C1: Finish Non-Warband Faction Actions

Goal: `FactionActionPlanner` outcomes must affect the ledger, not only return intent.

Scope:

- `Settler` is already started in `main`; harden it with edge-case tests for insufficient adults, ownership, save/load, and no duplicate colony slugs.
- `Caravan` transfers real goods through the existing trade/resource ledger.
- `ScoutingParty` creates real intel with source/confidence.
- `Diplomat` calls the existing diplomacy ledger.

Acceptance:

- no action creates citizens/resources from nothing;
- no action can use citizens already reserved in armies/raids;
- save/load roundtrip preserves new action state;
- tests cover no-target, low-power, one-settlement, allied-target, and exhausted-population cases.

Owner: Codex. Status: DONE (`009bfc4`) — Caravan/Scout/Diplomat execute against the ledger. Reviewed by Claude 2026-07-08: **APPROVED** — conservation OK (Caravan transfers/clamps, Scout/Diplomat create nothing), sensible targeting (trade non-hostile, diplomacy skips irreconcilable), no citizen double-drive, no player targeting (post-K4); tests present (caravan goods, scouting, diplomat, save/load). Detail in [`world-war-open-gaps.md`](../../design/world-war-open-gaps.md).

### Claude Task K1: Show World War Consequences In Game

Goal: player can understand what the world-war loop is doing without opening debug logs.

Scope:

- main-tab section for active world-war status;
- recent battles/captures with capped rows;
- faction strength/readiness bands, not exact omniscient values unless debug is enabled;
- warning when Rim War is active and LivingWorld world-war loop is disabled;
- EN/RU localization.

Acceptance:

- no uncapped per-frame LINQ over full population in `OnGUI`;
- UI distinguishes known intel from actual ledger/debug exact values;
- source-shape tests cover capped rendering and localization keys.

Owner: Claude.

Status: DONE (landed on `main`, commit `3d82a16`). Main-tab "World War" section: Rim War-active warning, active warbands on the march, per-faction strength as coarse bands (exact only under debug), recent war history. All rows capped and cached in `RefreshCachedRows` (count-gated, no per-frame full-population scan). EN/RU. Structural test `TestRimWorldWorldWarMainTab`.

### Codex Task C2: Production Model Depth

Goal: production must depend on landscape, terrain, technology, labor, and stock inputs, not only population.

Scope:

- extend production profiles with terrain/biome/tech multipliers already documented in `livingworld-foundation.md`;
- connect owned tools/animals/resources as optional modifiers;
- produce food/steel/medicine/components through owned resource stacks;
- expose only bands through intel unless exact values are known.

Acceptance:

- tests show same population produces different outputs in different terrain/tech contexts;
- starvation/migration pressure uses produced food;
- no production update scans active-map pawns.

Owner: Codex.

### Claude Task K2: Live Playtest And RimWorld Fail-Open Hardening

Goal: keep the mod usable in real RimWorld while core systems expand.

Scope:

- verify mod load with current active mod stack;
- inspect RimWorld logs for Harmony/RimWorld exceptions;
- keep custom incidents fail-open;
- validate world-gen settings and Russian UI in-game;
- report screenshots/logs back into docs when a behavior is confusing.

Acceptance:

- no startup red errors from LivingWorld;
- `tools/install-rimworld-mod.ps1` produces current DLLs;
- user-facing settings explain whether world-war is enabled or disabled by Rim War.

Owner: Claude.

Status: BLOCKED on a live RimWorld run (cannot launch the game in the current agent environment). Static parts verified: `dotnet build LivingWorld.sln` 0/0; world-war enable/disable + Rim War exclusion are surfaced in-game (K1 warning + K3 letters). Remaining acceptance (no startup red errors, install-script DLL freshness, in-game RU UI) needs a human playtest.

### Codex Task C3: Save/Performance Hardening

Goal: avoid save bloat and UI/tick stalls before population scales.

Scope:

- stress tests for 20k citizens;
- cached settlement/faction aggregates;
- back-compat XML load for missing world-war sections;
- compact serialization design for future cohort/chunk save format.

Acceptance:

- tests prove old saves without new fields load with defaults;
- benchmark or test guard catches accidental hot-path full-population scans;
- docs update `docs/performance.md` and `docs/save_format.md`.

Owner: Codex.

### Claude Task K3: Materialization UX

Goal: decide which ledger events become visible RimWorld objects/incidents and when.

Scope:

- player-directed attacks remain active-map incidents;
- NPC-vs-NPC battles remain ledger-first until UI is stable;
- optional world objects are behind a feature flag;
- letters/notifications are rate-limited and localized.

Acceptance:

- no silent attack on player settlement;
- no flood of letters during catch-up;
- no world-object materialization when Rim War safety flag disables LivingWorld world-war.

Owner: Claude.

Status: DONE (landed on `main`, commit `bfe844d`). Rate-limited world-war letters: one summary letter AFTER the daily catch-up loop (never one per simulated day, so no flood), only when the loop is active (silent when world war is disabled, ceded to Rim War, or during initial seeding). Captures accumulate across `worldWarLetterCooldownDays` so nothing is lost; `lastWorldWarLetterTick`/`notifiedCaptureCount` persist so save/load never re-announces. EN/RU. Structural test `TestRimWorldWorldWarNotifications`. Deferred sub-scope: optional world-object materialization behind a flag (still ledger-first UI).

## Post-R7 Gap Review

A step-back review found invariants that were documented but never enforced. Full detail
and fix plan: [`docs/design/world-war-open-gaps.md`](../../design/world-war-open-gaps.md).

### Claude Task K4: Protect the player faction from the world war (G1, primary)

Scope: `VanillaSettlementImporter` must not import `Faction.OfPlayer` settlements into the
ledger, so NPC world war can never silently target/capture the player's base or collapse
the player faction. Add a test that the scanner drops player settlements.

Acceptance: no player-faction settlement enters the ledger from the scanner; existing
scanner tests still pass. Owner: Claude. Status: DONE — `VanillaSettlementImporter`
returns null for `faction.IsPlayer` settlements; `TestRimWorldWorldObjectScanner` asserts
the guard. Build 0/0. Codex C4 (Core defense-in-depth) is also done now.

### Codex Task C4: Player-faction exclusion in Core (G1, defense in depth)

Scope: the ledger knows its player faction id; `FactionActionPlanner.FindEnemyTarget` and
`FactionLifecycleService` never target or collapse the player faction, even if a player
settlement reaches the ledger by another path. Acceptance: tests prove the world war
never targets/collapses the player faction. Owner: Codex. Status: DONE — Core now persists
`PlayerFactionId`, excludes it from warband target selection and lifecycle collapse, and
blocks `WorldBattleService.TryResolve` with `BlockedPlayerSettlement` if a player-owned
settlement still reaches the battle path. Tests cover planner, lifecycle, battle blocking
and save/load.

### Codex Task C5: Prune resolved army movements (G2)

Scope: `WorldState._armyMovements` currently keeps every Disbanded/Arrived/Recalled
movement forever (save bloat + growth). Prune resolved movements past a retention window,
back-compat load. Folds into C3. Owner: Codex. Status: DONE — `ArmyMovementPruneService`
removes old resolved movements, movement `StatusTick` is persisted with optional
back-compat load, and `WorldWarService` runs pruning after the daily world-war phase.
Tests prove traveling/recent movements remain and `WorldEvent` history survives pruning.

## Economy / Empire / Optimization Backlog (2026-07-08 gap analysis)

A gap analysis of the separate reference mods found large areas we adopted only thinly.
Sources: **Economics-and-Demography** (economy), **Empire** (settlement/faction depth +
compatibility), **optimization mods** (RocketMan/RuntimeGC/Missile Girl) plus E&D's own
perf. What we have vs missed is summarized per task. Do not copy E&D's faction-aggregate
economy — Living World keeps settlement/citizen ownership; adopt the *depth*, not the
coarse model.

### Economy (mostly not adopted; we have biome+tech output + resource ledger + simple transfer)

- **E1 (Codex): Ledger money/wealth.** Silver as an owned resource + cached faction/
  settlement wealth aggregates + earn/consume through normal ledger resource APIs.
  Status: DONE — `ResourcePriceBook` + `SettlementWealthService` calculate settlement and
  faction wealth snapshots from owned resources and cache them in `WorldState`.
- **E2 (Codex): Production depth (extends C2).** Terrain factors (hilliness/rainfall/temp/
  animal density) + production archetypes (Miner/Farmer/Medical/Warrior/...) + labor force,
  economy-of-scale, complexity penalty. Status: DONE — `SettlementProductionProfile` now
  carries archetype/labor/scale/complexity modifiers with optional save-load fallbacks, and
  `SettlementProductionService` uses effective outputs instead of flat per-adult output.
- **E3 (Codex): Faction-to-faction virtual trade + prices.** Exports/barter/silver transfer
  between factions, dynamic prices/inflation from stock/assets/tech demand. Status: DONE —
  `VirtualTradeService` quotes deterministic stock/wealth-sensitive prices and executes
  conservation-safe goods/silver transfers between ledger settlements.
- **E4 (Claude): Materialize trade from ledger + economy UI.** Draw arriving vanilla trader
  stock from the nearest ledger settlement's owned resources; show wealth/price bands in the
  main tab; EN/RU.
  - **E4a (economy UI): DONE.** Main-tab "World economy" section shows per-faction material
    wealth as bands (poor/modest/wealthy, exact only under debug), capped and cached in
    `RefreshCachedRows` (no per-frame full-scan); EN/RU. Test `TestRimWorldWorldEconomyMainTab`.
    Uses a resource-stock proxy until E1 wealth lands. Build 0/0.
  - **E4b (trader materialization/economy consumption): DONE by Claude for the current UI
    consumption slice.** The economy UI now reads priced wealth snapshots end-to-end. Full
    trader-caravan materialization remains a later gameplay layer.

### Empire compatibility (no adapter today)

- **EMP1 (Claude): Detect Empire + interop.** Detect `Empire` (packageId) like the Rim War
  flag and avoid double-driving the player-empire's world settlements (K4 player-exclusion
  already keeps player settlements out of the ledger); surface the state to the player.
  Status: DONE — `LivingWorldWorldComponent.IsEmpireActive` (`Matathias.Empire`) + a main-tab
  note (`LW_EmpireActiveNote`, EN/RU); no guard needed since K4 already keeps player-empire
  settlements out of the ledger. Test `TestRimWorldEmpireInterop`. Build 0/0.
- **EMP2 (Codex, backlog): Settlement depth.** prosperity/loyalty/unrest/buildings/worker
  allocation on ledger settlements (inspiration from Empire, not a port). Secondary per the
  plan. Status: BACKLOG.

### Optimizations (scale blockers, gap-analysis §C — the biggest miss)

- **C6 (Codex): WorldWarService split.** Status: DONE — world-war action execution is now
  separated into `WorldWarActionDispatcher`, focused action executors, and
  `WorldWarTargetSelector`; `WorldWarService` is back to phase orchestration. Test
  `TestWorldWarServiceSplitExecutors` guards the boundary.

- **O1 (Codex): Cached derived aggregates.** Status: DONE — `WorldState` now keeps a
  non-serialized lazy dirty-cache for settlement/faction resident population and combat power.
  `GetSettlementPopulation`, `SettlementPowerService`, and faction action power read through
  the cache; it is invalidated by citizen lifecycle, ownership transfer, migration, raid
  resolution, expansion, settlement capture, and simulation citizen replacement. Tests compare
  the cache with a full citizens/ownership scan after death, migration, raid reserve, return,
  missing and prisoner transitions.
- **O2 (Codex): Compact serialization / cohorts.** Status: DONE for the current high-volume
  save blocker — `WorldStateCodec` now writes `Citizens`, `Ownership`, and `Events` as
  deterministic `format="compact-v2"` row blocks instead of one XML element per record, while
  still reading legacy `<Citizen>`, `<Owner>`, and `<Event>` payloads. Full binary/chunked
  storage remains optional future work, but the 20k-100k citizen XML element explosion is
  removed.
- **P1 (Codex): Persistent WorldCaravan.** Status: DONE — caravan actions now create a
  `WorldCaravan` entity, load resources into caravan-owned inventory, resolve arrival through
  movement timing, unload into the target settlement, and preserve/destroy cargo correctly
  through save/load. This closes the instant-transfer abstraction for goods; future work is
  caravan members, animals, route risk, ambushes and loot.
- **P2 (Codex): Finite drifter arrival reservoir.** Status: DONE — drifter arrivals now spend
  saved `WorldState.DrifterArrivalReservoir` instead of drawing from an unlimited tap. RimWorld
  bootstrap seeds a finite outside-world reserve from settlement count and target population;
  future systems should replenish it explicitly from refugees, liberated prisoners, evacuation
  and diplomacy.
- **O3 (Codex, later): Time-dilation / adaptive tick.** Tick cohorts/regions at variable
  frequency by relevance (quiet regions rarely, active wars often), per RocketMan/Missile
  Girl. Today: every settlement/faction ticks daily uniformly. Status: BACKLOG.

Order: O1 + O2 gate scale and should precede large-world testing; E1→E3 give the economy
meaning; E4/EMP1 are the Claude-lane visibility/compat pieces that consume the above.

## Settlement Development ("do bases grow?")

A review of "how do AI bases develop" found the world grows in population (births,
assimilation, arrivals) and factions expand (Settler founds new settlements), but existing
settlements never *developed* — the `SettlementCapability` model existed but was static, never
grown by a tick service, and births ignored housing.

- **SD1 (Claude — normally Core/Codex, taken by Claude because Codex is overloaded): DONE.**
  `SettlementDevelopmentService.SimulateDay`: a fed (non-starving) settlement that outgrows its
  housing grows housing/food-storage capacity a step/day toward `population + headroom`, up to a
  cap; records a `SettlementDeveloped` event. Deterministic, pure Core (capacity is
  infrastructure, not people — conservation-safe). Wired into the daily tick behind
  `settlementDevelopmentEnabled` (+ `settlementDevelopmentStep`, `settlementHousingHeadroom`).
  Tests `TestSettlementDevelopmentGrowsHousing` + `TestRimWorldSettlementDevelopmentWiring`.
- **SD2 (Codex): Deeper development.** Settlement tiers/levels (camp/village/town/city),
  population-flow §8, a `Develop` `WarAction` so factions deliberately invest in a base,
  specialist-pool growth, and tying births to housing capacity. Status: DONE — births are
  blocked when housing is full, `SettlementDevelopmentService` exposes tier calculation and
  specialist growth, and `WorldWarService` executes `Develop` plans against the target settlement.

## Adopted Mod UI / Icons (2026-07-08 — "what UI/icons/functionality can we adopt")

Re-scanned the reference mods (Rim War `Textures/World/*`, E&D `ED_MainTabWindow_Population` +
`iconPath` main button, Empire's 255 UI PNGs) against our text-only tab. Icons are plentiful; our
UI was a wall of plain labels. All items below are Claude's lane (RimWorld/UI). Landed directly
per the user's "just do it" — Codex overloaded.

- **UI-1 (Claude): DONE.** Native faction icon + colour in front of every per-faction war-strength
  and economy row (`DrawFactionRow`, lookup by `FactionId` = `def.defName`), fall-back to plain
  label when the faction left the world. No new assets. Test `TestRimWorldMainTabFactionIcons`.
- **UI-3 (Claude): DONE.** Comparative data bars: each per-faction row draws a translucent
  faction-coloured bar scaled by its share of the strongest/richest faction, so the two lists read
  as bar charts. Cached rows carry a normalised `Fill`. Folded into `TestRimWorldMainTabFactionIcons`.
- **UI-4 (Claude): DONE.** Globe glyph at `Textures/UI/LivingWorld_MainButton.png` wired via
  `MainButtonDef.iconPath`, so the bottom-bar button shows an icon (and reads when minimised).
  Test extends `TestRimWorldMainButtonDef` (asserts iconPath + texture file exists).
- **World-gen simplification (Claude): DONE.** `Page_CreateWorldParams` button moved to the
  top-right (computed, no magic coords, clear `LW_WorldGenButton` label); the popup now shows only
  the master on/off toggle + a short blurb (`DrawWorldGenEssentials`) — density is vanilla's
  population slider, so we stopped duplicating it. All numeric knobs stay in Options → Mod Settings
  via the full `LivingWorldSettingsDrawer.Draw`. Tests updated (patch + window).
- **UI-2 (Claude): DONE.** World-map army markers. `WorldObject_LivingWorldArmy` (display-only)
  interpolates along the sphere from the origin settlement tile to the target tile using the
  ledger travel clock (`DrawPos` + `Find.WorldGrid.GetTileCenter` + `Vector3.Slerp`; tile parsed
  from the settlement `Slug`, since Core has no tile geometry), tinted by faction colour, with a
  clean-room crossed-swords icon (`Textures/World/LivingWorld_Warband.png`) in the Rim War style.
  It never pathfinds — took only Rim War's `Material`/tile-interp idioms, NOT its `WarObject`
  pathfollower (our sim stays in the ledger). The world component reconciles markers from active
  `WorldArmyMovement`s each simulated day and on load (`SyncArmyWorldObjects`), and clears them
  when the war is off or Rim War is active. Def + icon + inspect string (EN/RU) + test
  `TestRimWorldWorldArmyMarker`. **Needs an in-game smoke test** — world rendering is untestable
  headless; structural tests + net472 build (validates every RimWorld API/override) are green.
- **UI-2 per-action icons — Stage A (Claude): DONE (commit `56fe4c9`).** The marker was generalized
  so each ledger travel shows its own icon: marching **warbands** (crossed swords, `WorldArmyMovement`)
  and hauling **caravans** (wagon, Core's conservation-safe `WorldCaravan` — Codex already built the
  caravan travel/goods-reservation, so this was pure visualization). The marker now carries a
  per-instance texture + kind noun + string key so the two id spaces dedupe cleanly; the reconciler
  spawns/removes markers for both. Clean-room icon set (scout/settler/trader/diplomat) shipped in
  `Textures/World/`. Inspect string generalized to `LW_MissionMarkerInspect` with per-kind nouns
  (EN/RU). Test extended.
- **UI-2 per-action icons — Stage B (Claude, backlog): Scout / Diplomat / Settler travel.** Those
  actions still resolve instantly (no world object). Making them travel-then-apply like Rim War needs
  a Core mission model: Scout/Diplomat carry nothing (conservation-trivial), Settler moves adults (needs
  in-transit reservation like the caravan does for goods). Icons already in the tree. Status: BACKLOG.

### In-game playtest fixes (Claude)

Found while the user playtested the mod in a full modlist (Rim War + Empire + E&D + AutoTranslation):

- **Invalid incident category — DONE (commit `db3ba5a`).** `LivingWorld_DrifterArrival` referenced a
  non-existent `IncidentCategoryDef` `AllyArrival`, logging a red "category is undefined" at load.
  Switched to `Misc` (vanilla `WandererJoin`'s category). Note: the structural test had been asserting
  the broken value — structural tests can't see cross-references to real RimWorld defs, so the test now
  pins `Misc` and forbids `AllyArrival`.
- **Missing mod preview — DONE (commit `7443ee9`).** `Preview.png`/`icon.png` lived only in the repo-root
  `About/` (README/GitHub), not in `mod/About/` (what the installer deploys), so RimWorld showed no mod
  preview. Copied both into `mod/About/`.
- **Note:** the other red errors in that playtest (Rim War `WarObject.GetInspectString` FloodFill crash,
  Rim War × E&D gizmo NRE, broken RU grammar rules) are third-party, not Living World. Our Rim War
  mutual-exclusion worked: with Rim War active we added no markers and stood our world war down.
- **UI-5 (Claude): DONE.** Threat header on the settlement inspect: when a world-war army is
  marching on a settlement, its inspect leads with "arrives in ~N days" (`BuildThreatLine`, reads
  only `ArmyMovements` — never the fog-gated population/food — so it matches the public UI-2 marker
  and leaks no unscouted state). EN/RU + folded into `TestRimWorldSettlementInspectPatch`. (Famine/
  growth intentionally stay inside the fog-gated knowledge line, not surfaced as glyphs.)
- **F-1 (Claude): DONE.** `LivingWorldEconomyWindow` — a columnar Population/Economy table, one
  aligned row per faction (settlements, population, top `SettlementTier`, wealth) with native
  faction icons + comparative wealth bars, opened from a button on the Living World tab. Built on
  Codex's economy core (commit `1661df3`): `SettlementDevelopmentService.GetTier`,
  `FactionWealthSnapshot`/`WorldState.GetFactionWealth`. Wealth prefers the Core snapshot and falls
  back to a live material-stock sum so the table is populated today. Test `TestRimWorldEconomyWindow`.

### Wealth snapshots now driven (Codex TODO — DONE by Claude)

Codex's economy core (`SettlementWealthService`, `FactionWealthSnapshot`, `VirtualTradeService`)
was present and codec-persisted but never refreshed, so `GetFactionWealth`/`GetSettlementWealth`
stayed empty. **DONE (Claude, commit `01a1d3d`):** added `SettlementWealthService.DefaultPriceBook`
(canonical silver key + per-resource unit values for the ledger's tracked resources: Steel 2,
PackagedSurvivalMeal 14, MedicineIndustrial 18, ComponentIndustrial 24) and `RefreshAll`, called at
the end of each simulated world day so wealth tracks end-of-day stock. The economy UI (main-tab
bands, F-1 table) now reads real value. Deterministic + conservation-safe; test
`TestSettlementWealthRefreshAllUsesDefaultPrices`.

### Still open in Codex's Core lane (Claude can take if Codex stays overloaded)

- **E4b (Claude): DONE (commit `701e814`).** The main-tab World Economy section now reads the
  priced faction wealth snapshot (`GetFactionWealth().TotalWealth`) instead of summing raw
  quantities (raw sum kept only as a pre-first-tick fallback); `WealthBand` recalibrated for the
  priced scale. F-1 already consumed the snapshot, so the two economy views are consistent. Test
  updated. (Trade-price surfacing via `VirtualTradeService` in the UI remains an optional extra.)
- **O1 (DONE by Codex, `11a3e27+`):** cache per-faction/per-settlement resident population and
  combat power instead of re-LINQ-ing citizens each call; this closes G3's first war-loop scale
  issue. Wealth snapshots already have their own daily cache from Claude's `01a1d3d`.
- **O2 (DONE by Codex):** high-volume save blocks use compact-v2 rows for citizens,
  ownership and event history with legacy XML fallback. Further binary/chunk storage can wait.
- **EMP2 (BACKLOG):** real Empire adapter. **O3 (BACKLOG):** tiered/heat-map ticking cadence.

## Review results — Codex optimization/caravan/drifter batch (Claude, 2026-07-08)

Claude reviewed Codex's recent Core batch against the Review Contract (build 0/0, 199 tests green on
the integrated `main`). Verdicts:

- **`e354c32` compact high-volume save blocks (O2): APPROVED.** Loader branches on `format="compact-v2"`
  and falls back to legacy per-record XML, so old saves still load; free-text fields (name/profession/
  event summary) are base64 so `|`/`\n` delimiters never collide; field counts (Citizen 8, Ownership 4,
  Event 6) round-trip; caches are not persisted. Back-compat solid.
- **`443ca1d` cache derived population aggregates (O1): APPROVED.** Every population/ownership/settlement
  mutation path invalidates the cache (13 direct + all indirect through Ownership/Demography/Migration
  verified); conservation partition and residency filter match the old full scan exactly; deterministic;
  load rebuilds lazily with no stale persistence. Minor: capture/expansion transitions lack direct
  invalidation tests (code is correct).
- **`11a3e27` split world-war action executors: APPROVED.** Faithful mechanical extraction — guards,
  clamps, ordering, counting, and the battle phase are behaviour-identical to the monolith; one action
  per faction per day and warband adult-reservation preserved. Minor: the new split test is structural-only.
- **`ec17eef` persist world caravans: APPROVED with follow-ups.** Goods conserved across create/travel/
  arrive/destroy; additive optional codec re-owns cargo correctly; deterministic. **Follow-up (Important,
  Codex): terminal-state caravans are never pruned** — `MarkCaravanArrived`/`DestroyCaravan` only flip
  status, so `_caravans` + `_owners` + the save grow unbounded (no caravan equivalent of
  `ArmyMovementPruneService`; ties to O2/C5 save-size goals). Minor: `CaravansCompleted` double-counts
  (launch + arrival); zero-cargo caravan can launch; add load-then-arrive + post-load `Validate()` tests.
- **`309b0b6` limit drifter arrivals by reservoir: CHANGES-NEEDED (Important).** New worlds seed the
  reservoir correctly and are fine. But an **already-bootstrapped save from before this field loads with
  `bootstrapped=true` and reservoir `0`; bootstrap early-returns, nothing else seeds it, and no
  replenishment path exists — so drifter arrivals stop permanently and silently** for in-progress games
  (the exact "silently 0 forever" the contract warns against). Confirmed by Claude. Fix: a one-time
  legacy migration on load (if `bootstrapped && reservoir == 0 && Settlements.Count > 0`, seed
  `Settlements.Count * targetWorldPopulationPerSettlement` once) — doable in the RimWorld component
  (Claude's lane) without touching Core. Also add a legacy-load (no attribute) test.

**Net:** 3 clean APPROVED; 2 Important follow-ups — (a) caravan pruning (Codex/Core), (b) drifter
reservoir legacy migration (Claude/RimWorld). Everything is already merged and passing tests; these are
correctness/scale follow-ups, not merge blockers.

## Review Contract

For every completed task, the other agent reviews from these angles before merge:

- gameplay consequence;
- ownership/lifecycle leaks;
- save/load and back-compat;
- performance/hot path;
- compatibility/fail-open;
- UI visibility and Russian localization;
- tests that would have failed before the change.

## Immediate Recommended Order

1. Codex lands C1 or C2 from a clean branch on top of `origin/main`.
2. Claude lands K1 in parallel, only consuming existing public/core data.
3. Rebase both after each push; do not merge stale R3-R7 branches.
4. After C1/K1, run a live RimWorld check and update `docs/simulation.md` with actual player-visible behavior.
