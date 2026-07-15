# Eight Point Live World Completion Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the current Living World prototype from visible ledger activity into a more believable world where movement, intel, economy, materialization, ecology, technology and UI all explain themselves.

**Architecture:** Keep Core ledger-first and deterministic. Add only testable vertical slices; every physical thing that appears must have a ledger source or a documented abstraction boundary. RimWorld UI remains a visibility layer over Core state, not a second simulation.

**Tech Stack:** C#/.NET, `LivingWorld.Core`, `LivingWorld.RimWorld`, structural and behavior tests in `LivingWorld.Tests`, RimWorld 1.6 Harmony integration.

---

## Current Baseline

`origin/main` already includes several fixes that directly address the user's screenshots:

- hostile armies can intercept each other in transit;
- warmongers scout before attacking unknown enemies;
- targets are spread instead of dogpiling the lowest settlement id;
- caravans are persistent ledger entities with cargo;
- settlement observer exists and shows projects/resources/animals/events;
- attacked settlement maps materialize defenders, resources and a bounded animal sample.

This plan therefore focuses on the remaining gaps, not reimplementing shipped work.

## Task 1: Intel-Gated Faction Decisions

- [x] Ensure warband planning cannot skip the intel layer except as a documented fallback.
- [x] Make scouts, traders and diplomats visible as the preferred source of actionable knowledge.
- [x] Add tests proving a faction with no knowledge scouts first and only attacks after knowledge exists.

Status: already present on current `origin/main` through `FactionActionPlanner` and tests such as
`warmonger scouts before attacking unknown enemy`.

## Task 2: Movement Encounters

- [x] Extend transit encounters beyond warband-vs-warband where feasible.
- [x] Make hostile armies able to threaten caravans and missions through deterministic ledger events.
- [x] Add conservation tests for cargo/personnel loss or return.

Status: added `TransitEncounterService`; hostile opposite-route armies can destroy caravans
and disrupt scout/diplomat missions before arrival.

## Task 3: Economy Movement And Daily Delta

- [x] Add a Core daily activity summary service that groups recent events into production, consumption, construction, military, trade, ecology and technology deltas.
- [x] Surface this in docs and, where safe, RimWorld observer UI.
- [x] Ensure it uses bands unless exact knowledge is justified.

Status: `WorldActivitySummaryService` groups recent activity by domain and the observer/economy UI
surfaces trends subject to the player's knowledge. Population and economy values come from current
ledger aggregates or direct-visit snapshots, not the bootstrap baseline.

## Task 4: Settlement Materialization Roadmap

- [x] Keep current attacked-settlement materialization real and conservative.
- [x] Add explicit docs and tests for what is real now: defenders, resources, animals.
- [x] Generate districts, housing, storage, work, defense, power and facility structures from the ledger.
- [x] Reconcile loot pickup, animal fate, resident fate, floors and facility damage back to the ledger.

Status: the visit site is a persistent map projection. A second visit reuses the same settlement
identity and observes prior casualties, removed warehouse stock and facility damage. Materialized
gear is excluded from daily warehouse checkpoints so equipment cannot be duplicated or stripped by
the resource synchronizer.

## Task 5: Animal Fate Sync Next Slice

- [x] Add Core contract for map-spawned animal fate reconciliation.
- [x] Keep current spawned animals deducted upfront until a stable RimWorld identity link exists.
- [x] Add tests around return/death/tamed outcomes.

Status: cohort-stack fate sync is implemented for the attacked-settlement animal sample. Failed spawns return to cohorts, live departures return, dead animals remain lost, and player-taken/tamed animals are treated as lost to the source settlement rather than silently returned. Full named-animal identity remains a later quality layer.

## Task 6: Technology, Crop Strains And Diffusion

- [x] Add first Core crop-strain and technology-diffusion model if not present.
- [x] Tie it to settlement production profiles without exact UI leaks.
- [x] Add save/load and deterministic simulation tests.

## Task 7: Fog-Of-War Audit

- [x] Review main tab, economy table, settlement inspect and observer for exact values.
- [x] Keep exact values only for direct visit/debug; otherwise show bands and source labels.
- [x] Add structural tests for suspicious exact-value leaks.

Status: exact observer details remain debug-only globally. A new direct settlement observer action records `DirectVisit` intel and opens exact data for that one settlement only, preserving the "learn through visit/scout/trader" rule.

## Task 8: World-Map UX And Review Closure

- [x] Keep marker filters/clustering bounded and preserve physical contact independently of visual filters.
- [x] Update the task board with exact status.
- [x] Run full automated verification (`521` tests, Release adapter build, localization parity).
- [ ] Install the final merged build and run the live smoke gate.

---

## Execution Policy

- Work only in isolated branches/worktrees from `origin/main`.
- Commit author and committer: `Nurbek Akhmedov <timeshift92@outlook.com>`.
- No co-author trailers.
- Required verification before push: `dotnet run LivingWorld.Tests`, `dotnet build LivingWorld.sln`, `git diff --check`, install script when RimWorld-facing files change.
