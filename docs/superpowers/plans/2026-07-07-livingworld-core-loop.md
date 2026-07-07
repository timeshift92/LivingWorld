# LivingWorld Core Loop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish the next playable LivingWorld loop: stable drifter arrival, ledger-owned raids via custom incidents, identity-based pawn sync, and readable consequences.

**Execution status (2026-07-07):** implemented through final verification. The mod now has pooled drifter arrival gating, `LivingWorld_FactionRaid`, guarded vanilla raid fallback, identity-first pawn sync, expanded consequence UI, English/Russian localization, and an installed build in `C:\Games\RimWorld\Mods\LivingWorld`. Verification: `dotnet run --project src\LivingWorld.Tests\LivingWorld.Tests.csproj` passed 133 tests; `dotnet build LivingWorld.sln` passed with 0 warnings and 0 errors.

**Architecture:** `WorldState` remains the source of truth. Core services decide ownership, population, raids and outcomes; RimWorld classes only materialize pawns, attach identity and translate active-map events back into the ledger. Compatibility adapters are intentionally out of scope until this core loop is stable.

**Tech Stack:** C#/.NET, RimWorld 1.6, Harmony, Verse/RimWorld APIs, XML Defs, RimWorld keyed localization, custom no-framework test runner in `src/LivingWorld.Tests/Program.cs`.

---

## Scope Rules

- Keep all visible UI strings localized in English and Russian.
- Prefer pure Core services and test them before RimWorld adapters.
- Do not add RimWar/Empire adapters in this phase.
- Do not make `Pawn` the source of truth for inactive entities.
- Fail open in RimWorld integration: if LivingWorld cannot safely provide data, vanilla behavior should continue or the custom incident should decline.

## Files Map

- `docs/superpowers/plans/2026-07-07-livingworld-core-loop.md` — this execution plan.
- `docs/simulation.md` — document final incident and sync semantics.
- `docs/architecture.md` — document identity and custom incident boundaries if the implementation changes them.
- `src/LivingWorld.Tests/Program.cs` — red/green behavior checks and source-shape checks.
- `src/LivingWorld.Core/RaidPopulationAllocator.cs` — existing reservation logic for real citizens.
- `src/LivingWorld.Core/RaidReconciliationService.cs` — release unused reservists.
- `src/LivingWorld.Core/RaidPawnBindingService.cs` — bind spawned pawns to citizens and resolve outcomes.
- `src/LivingWorld.RimWorld/IncidentWorker_LivingWorldDrifterArrival.cs` — harden drifter materialization.
- `src/LivingWorld.RimWorld/LivingWorldWorldComponent.cs` — daily scheduling, cached gates, public query helpers.
- `src/LivingWorld.RimWorld/CompLivingWorldIdentity.cs` — ledger id carrier on materialized pawns.
- `mod/Defs/IncidentDefs/LivingWorld_DrifterArrival.xml` — existing drifter incident.
- `mod/Defs/IncidentDefs/LivingWorld_FactionRaid.xml` — new custom raid incident.
- `src/LivingWorld.RimWorld/IncidentWorker_LivingWorldFactionRaid.cs` — new custom raid worker.
- `src/LivingWorld.RimWorld/LivingWorldRaidIncidentPatch.cs` — legacy vanilla raid reservation hook; keep as fallback in this phase unless the new incident is fully verified.
- `src/LivingWorld.RimWorld/LivingWorldRaidPawnGenerationPatch.cs` — legacy vanilla raid binding hook; keep as fallback in this phase unless the new incident is fully verified.
- `mod/Languages/English/Keyed/LivingWorld.xml` and `mod/Languages/Russian/Keyed/LivingWorld.xml` — new letters/status text.
- `mod/Languages/Russian/DefInjected/IncidentDef/LivingWorld_Incidents.xml` — Russian def-injected incident labels.

## Task 0: Baseline Verification

**Files:**
- Test: all current project files.

- [ ] **Step 1: Run current tests**

Run:

```powershell
dotnet run --project src\LivingWorld.Tests\LivingWorld.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 2: Run current build**

Run:

```powershell
dotnet build LivingWorld.sln
```

Expected: build succeeds with 0 warnings and 0 errors.

- [ ] **Step 3: Record any baseline issue**

If either command fails, fix the failing baseline first using TDD before continuing.

## Task 1: Harden `LivingWorld_DrifterArrival`

**Files:**
- Modify: `src/LivingWorld.Tests/Program.cs`
- Modify: `src/LivingWorld.RimWorld/IncidentWorker_LivingWorldDrifterArrival.cs`
- Modify: `src/LivingWorld.RimWorld/LivingWorldWorldComponent.cs`
- Create: `mod/Languages/Russian/DefInjected/IncidentDef/LivingWorld_Incidents.xml`
- Modify: `docs/simulation.md`

- [ ] **Step 1: Write failing tests**

Add source-shape tests that require:

```csharp
("drifter arrival requires a pooled drifter before firing", TestDrifterArrivalGateRequiresPooledDrifter),
("drifter arrival validates edge spawn cells", TestRimWorldDrifterArrivalWorkerValidatesSpawnCell),
("defines Russian incident def localization", TestRimWorldIncidentDefRussianLocalization),
("persists every drifter-flow setting", TestRimWorldDrifterFlowSettings),
```

The tests should assert:

```csharp
AssertContains("State.Drifters.Count > 0", source);
AssertDoesNotContain("cachedWorldPopulation < cachedTargetPopulation", source);
AssertContains("TryFind", workerSource);
AssertContains("Standable", workerSource);
AssertContains("LivingWorld_DrifterArrival.label", russianXml);
AssertContains("Scribe_Values.Look(ref targetWorldPopulationPerSettlement", settingsSource);
AssertContains("Scribe_Values.Look(ref maxDrifterArrivalsPerDay", settingsSource);
AssertContains("Scribe_Values.Look(ref maxDrifterAssimilationsPerDay", settingsSource);
AssertContains("Scribe_Values.Look(ref drifterMinFounders", settingsSource);
```

- [ ] **Step 2: Verify red**

Run:

```powershell
dotnet run --project src\LivingWorld.Tests\LivingWorld.Tests.csproj
```

Expected: the new tests fail because the gate still allows unpooled arrivals and Russian DefInjected incident labels are missing.

- [ ] **Step 3: Implement minimal fix**

Change `LivingWorldWorldComponent.WantsDrifterArrival` to require a pooled drifter:

```csharp
return State.Drifters.Count > 0;
```

Change `IncidentWorker_LivingWorldDrifterArrival` to choose a validated edge cell. If no valid edge cell is found, return `false` before mutating the ledger.

Add Russian DefInjected labels:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<LanguageData>
  <LivingWorld_DrifterArrival.label>прибытие дрифтера Living World</LivingWorld_DrifterArrival.label>
  <LivingWorld_DrifterArrival.letterLabel>Прибыл дрифтер</LivingWorld_DrifterArrival.letterLabel>
  <LivingWorld_DrifterArrival.letterText>{PAWN} прибыл из потока Living World и присоединился к колонии.</LivingWorld_DrifterArrival.letterText>
</LanguageData>
```

- [ ] **Step 4: Verify green**

Run:

```powershell
dotnet run --project src\LivingWorld.Tests\LivingWorld.Tests.csproj
dotnet build LivingWorld.sln
```

Expected: all tests pass and build is clean.

## Task 2: Add Custom `LivingWorld_FactionRaid` Incident

**Files:**
- Modify: `src/LivingWorld.Tests/Program.cs`
- Create: `mod/Defs/IncidentDefs/LivingWorld_FactionRaid.xml`
- Create: `src/LivingWorld.RimWorld/IncidentWorker_LivingWorldFactionRaid.cs`
- Modify: `mod/Languages/English/Keyed/LivingWorld.xml`
- Modify: `mod/Languages/Russian/Keyed/LivingWorld.xml`
- Modify: `mod/Languages/Russian/DefInjected/IncidentDef/LivingWorld_Incidents.xml`
- Modify: `docs/simulation.md`

- [ ] **Step 1: Write failing tests**

Add tests:

```csharp
("defines the faction raid incident def", TestRimWorldFactionRaidIncidentDef),
("defines the faction raid incident worker", TestRimWorldFactionRaidWorker),
("localizes faction raid letters", TestRimWorldFactionRaidLocalization),
```

Expected source shape:

```csharp
AssertContains("<defName>LivingWorld_FactionRaid</defName>", xml);
AssertContains("<category>ThreatBig</category>", xml);
AssertContains("<workerClass>LivingWorld.RimWorld.IncidentWorker_LivingWorldFactionRaid</workerClass>", xml);
AssertContains("class IncidentWorker_LivingWorldFactionRaid : IncidentWorker_RaidEnemy", source);
AssertContains("RaidPopulationAllocator.ReserveForRaid", source);
AssertContains("RaidPawnBindingService.BindRaidPawns", source);
AssertContains("CompLivingWorldIdentity", source);
AssertContains("RaidReconciliationService.ReleaseUndeployedReserves", source);
AssertContains("LW_FactionRaidLetterLabel", englishXml);
AssertContains("LW_FactionRaidLetterText", russianXml);
```

- [ ] **Step 2: Verify red**

Run:

```powershell
dotnet run --project src\LivingWorld.Tests\LivingWorld.Tests.csproj
```

Expected: tests fail because the new incident and worker do not exist.

- [ ] **Step 3: Implement XML Def**

Create `LivingWorld_FactionRaid.xml` with:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<Defs>
  <IncidentDef>
    <defName>LivingWorld_FactionRaid</defName>
    <label>Living World faction raid</label>
    <category>ThreatBig</category>
    <targetTags>
      <li>Map_PlayerHome</li>
    </targetTags>
    <workerClass>LivingWorld.RimWorld.IncidentWorker_LivingWorldFactionRaid</workerClass>
  </IncidentDef>
</Defs>
```

- [ ] **Step 4: Implement worker**

Worker rules:

1. `CanFireNowSub` returns false when there is no bootstrapped component, no humanlike hostile faction with LivingWorld population, or no available combatants.
2. `TryExecuteWorker` chooses a hostile humanlike faction, reserves real citizens through `RaidPopulationAllocator.ReserveForRaid`, calls vanilla raid generation through base raid flow, binds spawned pawns to reserved citizens, attaches `CompLivingWorldIdentity` to each pawn where possible, releases undeployed reserves, and sends a localized letter.
3. If any step fails before pawn spawn, release undeployed reserves and return `false`.

- [ ] **Step 5: Verify green**

Run:

```powershell
dotnet run --project src\LivingWorld.Tests\LivingWorld.Tests.csproj
dotnet build LivingWorld.sln
```

Expected: all tests pass and build is clean.

## Task 3: Keep Legacy Raid Patches As Fallback, But Stop Double Driving

**Files:**
- Modify: `src/LivingWorld.Tests/Program.cs`
- Modify: `src/LivingWorld.RimWorld/LivingWorldRaidIncidentPatch.cs`
- Modify: `src/LivingWorld.RimWorld/LivingWorldRaidPawnGenerationPatch.cs`
- Modify: `docs/simulation.md`

- [ ] **Step 1: Write failing tests**

Add tests that require:

```csharp
AssertContains("custom raid incident owns primary Living World raid path", docs);
AssertContains("legacy vanilla raid patches are fallback", docs);
AssertContains("LivingWorld_FactionRaid", raidPatchSource);
```

- [ ] **Step 2: Verify red**

Run tests and confirm the docs/source checks fail.

- [ ] **Step 3: Implement guarded fallback**

Keep the old patches active only as compatibility fallback. Add comments and guards so future reviewers understand that `LivingWorld_FactionRaid` is the primary path and the vanilla patch path should not reserve citizens when the custom incident has already reserved an army for the same incident.

- [ ] **Step 4: Verify green**

Run tests and build.

## Task 4: Identity-Based Pawn Sync Foundation

**Files:**
- Modify: `src/LivingWorld.Tests/Program.cs`
- Create: `src/LivingWorld.RimWorld/LivingWorldPawnIdentityService.cs`
- Modify: `src/LivingWorld.RimWorld/LivingWorldPawnKillPatch.cs`
- Modify: `src/LivingWorld.RimWorld/LivingWorldPawnCapturePatch.cs`
- Modify: `src/LivingWorld.RimWorld/LivingWorldPawnExitPatch.cs`
- Modify: `docs/simulation.md`

- [ ] **Step 1: Write failing tests**

Add source-shape tests:

```csharp
("defines identity based pawn sync service", TestRimWorldPawnIdentityService),
("pawn death sync checks identity comp before thing id", TestRimWorldPawnKillPatchUsesIdentity),
("pawn capture sync checks identity comp before thing id", TestRimWorldPawnCapturePatchUsesIdentity),
("pawn exit sync checks identity comp before thing id", TestRimWorldPawnExitPatchUsesIdentity),
```

Expected checks:

```csharp
AssertContains("class LivingWorldPawnIdentityService", source);
AssertContains("TryGetLedgerId", source);
AssertContains("GetComp<CompLivingWorldIdentity>", source);
AssertContains("CompLivingWorldIdentity", killPatch);
AssertContains("CompLivingWorldIdentity", capturePatch);
AssertContains("CompLivingWorldIdentity", exitPatch);
```

- [ ] **Step 2: Verify red**

Run tests and confirm failures.

- [ ] **Step 3: Implement identity service**

Create a service with:

```csharp
public static bool TryGetLedgerId(Pawn pawn, out EntityId id)
```

It should read `CompLivingWorldIdentity` first and fall back to the existing `thingIDNumber` raid link path only where required.

- [ ] **Step 4: Wire existing patches**

Update kill/capture/exit patches to call the identity service first. Raid outcomes still use `RaidPawnLink` for now; identity sync is the resolver layer, not a new outcome model.

- [ ] **Step 5: Verify green**

Run tests and build.

## Task 5: Consequence Visibility In Main Tab

**Files:**
- Modify: `src/LivingWorld.Tests/Program.cs`
- Modify: `src/LivingWorld.RimWorld/MainTabWindow_LivingWorld.cs`
- Modify: `mod/Languages/English/Keyed/LivingWorld.xml`
- Modify: `mod/Languages/Russian/Keyed/LivingWorld.xml`

- [ ] **Step 1: Write failing tests**

Require the main tab to display:

```text
Faction collapses
Recent raids
Drifters
Known settlement intel freshness
```

with localized keys for English and Russian.

- [ ] **Step 2: Verify red**

Run tests and confirm missing UI/localization assertions.

- [ ] **Step 3: Implement minimal UI sections**

Use existing `WorldState` collections:

- `FactionRecords`
- `RaidOutcomes`
- `Drifters`
- `KnownSettlementInfos`

Keep rendering capped to avoid UI stalls.

- [ ] **Step 4: Verify green**

Run tests and build.

## Task 6: Final Verification and Install

**Files:**
- All.

- [ ] **Step 1: Run full tests**

```powershell
dotnet run --project src\LivingWorld.Tests\LivingWorld.Tests.csproj
```

- [ ] **Step 2: Run build**

```powershell
dotnet build LivingWorld.sln
```

- [ ] **Step 3: Install mod**

```powershell
tools\install-rimworld-mod.ps1
```

- [ ] **Step 4: Check installed artifacts**

```powershell
Get-Item C:\Games\RimWorld\Mods\LivingWorld\1.6\Assemblies\LivingWorld.Core.dll,
         C:\Games\RimWorld\Mods\LivingWorld\1.6\Assemblies\LivingWorld.RimWorld.dll
```

Expected: timestamps reflect the current run.

## Out Of Scope For This Plan

- RimWar adapter.
- Empire adapter.
- Full custom storyteller selection screen.
- Compact binary save chunks.
- Full slave/recruit/emancipation simulation. This plan only establishes identity-based sync foundations so those later transitions have a reliable ledger id.
