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

- **E1 (Codex): Ledger money/wealth.** Silver as an owned resource + a cached faction/
  settlement wealth aggregate + earn/consume. Today: no money at all. Status: NOT STARTED.
- **E2 (Codex): Production depth (extends C2).** Terrain factors (hilliness/rainfall/temp/
  animal density) + production archetypes (Miner/Farmer/Medical/Warrior/…) + labor force,
  economy-of-scale, complexity penalty. Today: flat output = f(biome, tech). Status: NOT STARTED.
- **E3 (Codex): Faction-to-faction virtual trade + prices.** Exports/barter/silver transfer
  between factions, dynamic prices/inflation from stock/assets/tech demand. Today: only a
  direct resource transfer caravan. Status: NOT STARTED.
- **E4 (Claude): Materialize trade from ledger + economy UI.** Draw arriving vanilla trader
  stock from the nearest ledger settlement's owned resources; show wealth/price bands in the
  main tab; EN/RU.
  - **E4a (economy UI): DONE.** Main-tab "World economy" section shows per-faction material
    wealth as bands (poor/modest/wealthy, exact only under debug), capped and cached in
    `RefreshCachedRows` (no per-frame full-scan); EN/RU. Test `TestRimWorldWorldEconomyMainTab`.
    Uses a resource-stock proxy until E1 wealth lands. Build 0/0.
  - **E4b (trader materialization): DEFERRED.** Harmony hook on `IncidentWorker_TraderCaravan
    Arrival` to draw trader stock from ledger owned resources; pairs better after Codex E1–E3
    (money/prices) give trade real value. Status: NOT STARTED.

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

- **O1 (Codex): Cached derived aggregates.** Cache settlement power / faction strength /
  population totals, invalidate on change, so the daily war/economy loops stop recomputing
  LINQ over citizens per faction. Also fixes war-loop scale (G3). Status: NOT STARTED.
- **O2 (Codex): Compact serialization / cohorts.** Replace the per-entity XML codec (40
  per-element writers, one per citizen) with a compact/cohort format for the global layer,
  back-compat load. This is the 20k–100k save/load blocker. Status: NOT STARTED.
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
- **SD2 (Codex, backlog): Deeper development.** Settlement tiers/levels (village→town→city,
  population-flow §8), a `Develop`/`Build` `WarAction` so factions deliberately invest in a base,
  specialist-pool growth, and tying births to housing capacity. Ties to EMP2/E2. Status: BACKLOG.

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
