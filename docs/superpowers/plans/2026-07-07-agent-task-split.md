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

Owner: Codex.

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
