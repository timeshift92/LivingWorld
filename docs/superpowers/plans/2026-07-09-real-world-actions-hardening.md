# Real World Actions Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Turn visible world actions into conserved ledger actions: scouts, diplomats, caravans, and armies must use real citizens/resources, avoid inactive targets, collide when routes conflict, and stop leaking omniscient UI data.

**Architecture:** Keep the source of truth in `LivingWorld.Core`. Travelling non-combat actions get a real crew citizen owned by the travelling entity until arrival, recall, failure, or destruction. RimWorld UI reads only active/known data unless debug logging is enabled.

**Tech Stack:** C#/.NET, LivingWorld.Core ledger services, RimWorld UI windows, XML save codec, structural and behavioral tests in `LivingWorld.Tests`.

---

### Task 1: Travelling Actions Own Real Crew

**Files:**
- Modify: `C:\Games\LivingWorld\src\LivingWorld.Core\WorldMission.cs`
- Modify: `C:\Games\LivingWorld\src\LivingWorld.Core\WorldCaravan.cs`
- Create: `C:\Games\LivingWorld\src\LivingWorld.Core\TravelCrewService.cs`
- Modify: `C:\Games\LivingWorld\src\LivingWorld.Core\CaravanActionExecutor.cs`
- Modify: `C:\Games\LivingWorld\src\LivingWorld.Core\ScoutingActionExecutor.cs`
- Modify: `C:\Games\LivingWorld\src\LivingWorld.Core\DiplomacyActionExecutor.cs`
- Test: `C:\Games\LivingWorld\src\LivingWorld.Tests\Program.cs`

- [x] Add failing tests proving a scout mission and caravan remove one adult from the source owner while travelling and return that citizen on successful arrival.
- [x] Add `CrewCitizenId` to mission/caravan records and persist it.
- [x] Add deterministic crew reservation: first alive adult owned by source settlement.
- [x] Return crew on mission/caravan arrival.

### Task 2: Inactive Targets Do Not Receive Traffic

**Files:**
- Modify: `C:\Games\LivingWorld\src\LivingWorld.Core\WorldWarTargetSelector.cs`
- Modify: `C:\Games\LivingWorld\src\LivingWorld.Core\WorldState.cs`
- Modify: `C:\Games\LivingWorld\src\LivingWorld.Core\CaravanMovementService.cs`
- Modify: `C:\Games\LivingWorld\src\LivingWorld.Core\WorldMissionService.cs`
- Test: `C:\Games\LivingWorld\src\LivingWorld.Tests\Program.cs`

- [x] Add failing tests for destroyed/abandoned settlement exclusion in trade/scout/diplomacy selectors.
- [x] Reject create/dispatch APIs when source or target settlement is inactive.
- [x] Recall caravans to their source if the target is no longer active at arrival.
- [x] Fail missions without applying intel/goodwill if source or target is inactive.

### Task 3: Hostile Traffic Collides Before Dogpiling

**Files:**
- Modify: `C:\Games\LivingWorld\src\LivingWorld.Core\ArmyInterceptionService.cs`
- Modify: `C:\Games\LivingWorld\src\LivingWorld.Core\TransitEncounterService.cs`
- Test: `C:\Games\LivingWorld\src\LivingWorld.Tests\Program.cs`

- [x] Add failing tests for hostile armies converging on the same target settlement.
- [x] Treat same-target hostile army movements as contested routes.
- [x] Treat same-target hostile army vs caravan/mission routes as interceptable traffic.

### Task 4: UI Stops Leaking Full Ledger Knowledge

**Files:**
- Modify: `C:\Games\LivingWorld\src\LivingWorld.RimWorld\LivingWorldEconomyWindow.cs`
- Modify: `C:\Games\LivingWorld\src\LivingWorld.RimWorld\LivingWorldSettlementObserverWindow.cs`
- Modify: `C:\Games\LivingWorld\mod\Languages\English\Keyed\LivingWorld.xml`
- Modify: `C:\Games\LivingWorld\mod\Languages\Russian\Keyed\LivingWorld.xml`
- Test: `C:\Games\LivingWorld\src\LivingWorld.Tests\Program.cs`

- [x] Add structural tests requiring active-settlement filtering and player knowledge gating in the economy window.
- [x] Add structural tests requiring debug gating for exact settlement observer data.
- [x] Filter inactive settlements out of economy and observer rows.
- [x] In non-debug economy mode, aggregate only settlements known to the player through intel.
- [x] In non-debug observer mode, show a localized debug-only notice instead of exact ledger details.

### Task 5: Verification and Publish

**Files:**
- Modify: no additional files.

- [x] Run `dotnet run --project C:\Games\LivingWorld\src\LivingWorld.Tests\LivingWorld.Tests.csproj`.
- [x] Run `dotnet build C:\Games\LivingWorld\LivingWorld.sln`.
- [x] Run `git diff --check`.
- [x] Run `powershell -ExecutionPolicy Bypass -File C:\Games\LivingWorld\tools\install-rimworld-mod.ps1`.
- [x] Commit, fast-forward merge to `main`, push, and store ICM context.
