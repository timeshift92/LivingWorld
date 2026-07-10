# Headless Finish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the remaining Living World work that can be verified without launching RimWorld.

**Architecture:** Keep Core ledger-first and deterministic. Replace instant settlement expansion with a real migration-backed settler expedition, add a Core activity summary for UI/debug consumers, and harden RimWorld-facing contracts with structural tests where live game behavior cannot be exercised headless.

**Tech Stack:** C#/.NET, `LivingWorld.Core`, `LivingWorld.RimWorld`, XML defs/localization, `LivingWorld.Tests`.

---

## Tasks

- [x] Travelling settler expeditions: world-war settler actions create a travelling `WorldMigrationGroup` backed by real citizens and only found the colony when the group arrives.
- [x] Daily activity summary: Core groups recent event activity into population, economy, construction, military, diplomacy, ecology and technology deltas for UI/debug surfaces.
- [x] Headless RimWorld contract guards: structural tests assert armory/mobilization/outfit/repair and world-map visibility hooks remain wired.
- [x] Verification: run tests, build, `git diff --check`, install the mod, rebase and push `main`.
