# Settlement Real Map Materialization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make attacked NPC settlement maps show ledger-backed facilities, room shells, real stockpiles, bounded city infrastructure and reconcile spawned map consequences back into the ledger.

**Architecture:** Core owns the deterministic settlement map payload: existing facilities become bounded map features, room shells, storage cells and city features with stable def names and layout slots. RimWorld consumes that payload on `MapGenerator.GenerateMap`, spawns real walls/doors/floors/things opportunistically without failing map generation, places spawned resource stacks into storage cells, tracks spawned settlement animals by cohort until they die or safely leave the map, and reconciles map-end resource/facility consequences back into the ledger.

**Tech Stack:** C#/.NET, `LivingWorld.Core`, RimWorld/Verse Harmony patches, existing `LivingWorld.Tests` structural and behavior tests.

---

## Scope

- [x] Add Core layout records and service for settlement facility map materialization.
- [x] Spawn facility-backed real buildings/things on attacked NPC settlement maps.
- [x] Track materialized animal pawns and return only live departures to their cohorts.
- [x] Document the contract and remaining non-goals.
- [x] Extend Core layout with facility rooms and deterministic stockpile cells.
- [x] Spawn real wall/door/floor room shells for attacked settlement facilities.
- [x] Place ledger-backed resource payload stacks inside layout stockpile cells before falling back to generic spawn cells.
- [x] Extend Core layout with bounded city features: beds, defenses, power props, work props and storage markers.
- [x] Track spawned walls, doors and facility-bound props and translate destroyed/missing things into `SettlementFacility.ConditionPercent` damage on map deinit.
- [x] Track spawned resource stacks and return only unlooted stacks to the settlement or active ruin ledger on map deinit.
- [x] Track materialized room floor terrain cells and translate replaced/stripped floors into `SettlementFacility.ConditionPercent` damage on map deinit.
- [x] Extend Core layout with deterministic facility districts, housing, commons, security district, roads, biome/tech/archetype style and power-network/activity/guard features.
- [x] Materialize district terrain, roads, power conduits/generators/lights, guard posts and activity props on attacked NPC settlement maps.

## Non-Goals

- Peaceful NPC observer/visit maps with complete pawn job schedules and daily routines. Attacked settlement maps now have deterministic districts, roads, style and a bounded power network, but defenders still behave through RimWorld's combat map AI.
- Full per-animal identity records. This slice tracks spawned animals by cohort stack, because animal cohorts are intentionally lightweight ledger records.

## Verification

- [x] `dotnet run --project src/LivingWorld.Tests/LivingWorld.Tests.csproj` - 334 passed.
- [x] `dotnet build LivingWorld.sln` - 0 warnings, 0 errors.
- [x] `git diff --check` - clean.
- [x] `tools/install-rimworld-mod.ps1` - installed to `C:\Games\RimWorld\Mods\LivingWorld`.
