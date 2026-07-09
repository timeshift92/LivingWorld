# Settlement Real Map Materialization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make attacked NPC settlement maps show ledger-backed facilities, room shells, real stockpiles and reconcile spawned animal pawns back into animal cohorts when they leave or die.

**Architecture:** Core owns the deterministic settlement map payload: existing facilities become bounded map features, room shells and storage cells with stable def names and layout slots. RimWorld consumes that payload on `MapGenerator.GenerateMap`, spawns real walls/doors/floors/things opportunistically without failing map generation, places spawned resource stacks into storage cells, and tracks spawned settlement animals by cohort until they die or safely leave the map.

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

## Non-Goals

- Full generated town planning with complete bedrooms, stockpiles, power networks and pawn schedules.
- Persistent per-building damage reconciliation for every spawned RimWorld thing.
- Map-end reconciliation that returns abandoned/unlooted spawned stacks to the settlement or ruin ledger.
- Full per-animal identity records. This slice tracks spawned animals by cohort stack, because animal cohorts are intentionally lightweight ledger records.

## Verification

- [x] `dotnet run --project src/LivingWorld.Tests/LivingWorld.Tests.csproj` - 314 passed.
- [x] `dotnet build LivingWorld.sln` - 0 warnings, 0 errors.
- [x] `git diff --check` - clean.
- [x] `tools/install-rimworld-mod.ps1` - installed to `C:\Games\RimWorld\Mods\LivingWorld`.
