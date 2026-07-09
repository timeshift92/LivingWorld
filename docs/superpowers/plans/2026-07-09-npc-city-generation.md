# NPC City Generation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make attacked NPC settlement maps look and behave like ledger-backed towns: districts, roads, faction/biome/tech style, defensive perimeter, power network and activity props.

**Architecture:** Extend the pure Core settlement map layout so every visual element is deterministic and derived from ledger state, then keep the RimWorld layer as a defensive materializer that converts that plan into vanilla things/terrain. Damage/loot reconciliation remains on the existing trackers.

**Tech Stack:** C# net8 Core, C# net472 RimWorld shell, existing structural test harness in `LivingWorld.Tests`, Harmony map generation postfix.

---

## Task 1: Core city layout plan

**Files:**
- Modify: `src/LivingWorld.Core/SettlementMapLayoutService.cs`
- Modify: `src/LivingWorld.Tests/Program.cs`

- [x] Add behavior tests proving the layout contains deterministic districts, roads, style, power-network cells and civilian activity features.
- [x] Extend the layout records with district/path/style records without adding save-format fields.
- [x] Build districts from facilities plus population/capabilities/profile.
- [x] Build deterministic road cells connecting the district anchors.
- [x] Build power-network features from power capability, power facilities and tech level.
- [x] Build activity features from population, biome and tech level.

## Task 2: RimWorld materialization of city plan

**Files:**
- Modify: `src/LivingWorld.RimWorld/LivingWorldSettlementMapMaterializationService.cs`
- Modify: `src/LivingWorld.Tests/Program.cs`

- [x] Add structural tests for district terrain, road terrain, power conduits/generators/lights, guard posts and civilian activity props.
- [x] Materialize district floor bands and roads before spawning structures.
- [x] Spawn power-network things using safe `DefDatabase.GetNamedSilentFail`.
- [x] Spawn activity props and guard posts through the existing city feature path.
- [x] Track facility-bound spawned things with `LivingWorldSettlementMapFacilityTracker`.

## Task 3: Docs and verification

**Files:**
- Modify: `docs/superpowers/plans/2026-07-09-settlement-real-map-materialization.md`
- Modify: `docs/simulation.md`

- [x] Update docs so the remaining non-goal is only peaceful NPC schedules/observer mode, not city layout quality.
- [x] Run `dotnet run --project src/LivingWorld.Tests/LivingWorld.Tests.csproj`.
- [x] Run `dotnet build LivingWorld.sln`.
- [x] Run `git diff --check`.
- [x] Install to `C:\Games\RimWorld\Mods\LivingWorld`.
