# Drifter Flow: Live-Tick + Storyteller Materialization — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the committed drifter pipeline + faction extinction actually run in-game (Part A), and materialize ledger drifters as real colonists through a custom storyteller-driven incident (Part B).

**Architecture:** Part A wires four pure Core services into the daily `WorldComponentTick` (a scheduler, not a hot path). Part B adds Core `MaterializeDrifter`/`MaterializeNewArrival` + a `DrifterMaterialized` event, a custom `IncidentDef LivingWorld_DrifterArrival` (category `AllyArrival`) whose `IncidentWorker` gates on the ledger and attaches a `CompLivingWorldIdentity` to the spawned pawn. No Harmony patch on vanilla generation.

**Tech Stack:** C# (net8.0 Core + net472 RimWorld adapter), RimWorld 1.6 modding API (`WorldComponent`, `IncidentWorker`, `IncidentDef`, `ThingComp`), Harmony (unchanged), custom exe test runner in `LivingWorld.Tests`.

## Global Constraints

- **Ledger is the single authority; RimWorld patches/workers are thin adapters — all logic in `LivingWorld.Core`.** (population-flow §11, normalization spec)
- **Fail-open:** any failure or missing component → vanilla behaviour unchanged. (spec §4.2)
- **No per-tick / per-call `state.Citizens` LINQ in hot paths; `CanFireNowSub` reads O(1) cached aggregates.** (normalization §6)
- **Deterministic Core:** no `Math.Random`/`Date` in Core; aptitudes/ids are seeded/sequential. (existing convention)
- **All new player-visible strings localized EN + RU.** (existing convention)
- **Verification bar per task's end and final:** `dotnet run --project src/LivingWorld.Tests` all green; final also `dotnet build LivingWorld.sln` 0 warnings / 0 errors.
- **Base:** worktree `worktree-raid-missing` at `31db141` (Codex economy/faction/migration landed). Spec: `docs/superpowers/specs/2026-07-07-drifter-flow-materialization-design.md`.

Test runner note: tests are registered by adding a `(name, TestFn)` tuple to the `tests` list at the top of `src/LivingWorld.Tests/Program.cs` and a `static void TestFn()` below. Run all: `dotnet run --project src/LivingWorld.Tests`. There is no single-test filter — run the whole suite and read the PASS/FAIL lines.

---

## File Structure

New:
- `src/LivingWorld.RimWorld/CompLivingWorldIdentity.cs` — `ThingComp` carrying a ledger `EntityId`, serialized with the pawn (+ `CompProperties_LivingWorldIdentity`).
- `src/LivingWorld.RimWorld/IncidentWorker_LivingWorldDrifterArrival.cs` — custom arrival incident worker (gate + materialize).
- `mod/Defs/IncidentDefs/LivingWorld_DrifterArrival.xml` — the incident def.

Modified:
- `src/LivingWorld.Core/WorldEvent.cs` — `+ DrifterMaterialized` enum member.
- `src/LivingWorld.Core/WorldState.cs` — `MaterializeDrifter`, `MaterializeNewArrival`.
- `src/LivingWorld.RimWorld/LivingWorldSettings.cs` — drifter-flow knobs + `ExposeData`.
- `src/LivingWorld.RimWorld/LivingWorldSettingsDrawer.cs` — sliders.
- `src/LivingWorld.RimWorld/LivingWorldWorldComponent.cs` — daily pipeline wiring + cached aggregates + `WantsDrifterArrival`.
- `mod/Languages/English/Keyed/LivingWorld.xml`, `mod/Languages/Russian/Keyed/LivingWorld.xml` — incident + settings strings.
- `src/LivingWorld.Tests/Program.cs` — one tuple + one `Test...` method per task.
- `docs/simulation.md` — drifter live-flow + materialization section.

---

## Task 1: Core — `DrifterMaterialized` event + `MaterializeDrifter`

**Files:**
- Modify: `src/LivingWorld.Core/WorldEvent.cs` (enum `WorldEventKind`)
- Modify: `src/LivingWorld.Core/WorldState.cs` (add method near `CreateDrifter`, ~line 350)
- Test: `src/LivingWorld.Tests/Program.cs`

**Interfaces:**
- Consumes: `WorldState.CreateDrifter`, `WorldState.Drifters`, private `_drifters`, `AppendEvent`.
- Produces: `WorldEventKind.DrifterMaterialized`; `Drifter WorldState.MaterializeDrifter(EntityId drifterId, int pawnThingId, int tick)` — removes the drifter from the pool, appends a `DrifterMaterialized` event, returns the removed drifter; throws `InvalidOperationException` if the id is unknown.

- [ ] **Step 1: Register + write the failing test**

Add to the `tests` list (top of `Program.cs`), next to other drifter entries:

```csharp
    ("materializes a pooled drifter and drains the pool", TestMaterializeDrifterDrainsPool),
```

Add the method (near the other drifter tests):

```csharp
static void TestMaterializeDrifterDrainsPool()
{
    var state = new WorldState(4242);
    var drifter = state.CreateDrifter("Wanderer", 30, Sex.Male);
    var before = state.Drifters.Count;

    var materialized = state.MaterializeDrifter(drifter.Id, pawnThingId: 777, tick: 60_000);

    AssertEqual(drifter.Id, materialized.Id);
    AssertEqual(before - 1, state.Drifters.Count);
    AssertEqual(null, state.GetDrifter(drifter.Id));
    AssertEqual(1, state.Events.Count(e => e.Kind == WorldEventKind.DrifterMaterialized));
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet run --project src/LivingWorld.Tests`
Expected: build error `'WorldState' does not contain a definition for 'MaterializeDrifter'` (and `WorldEventKind.DrifterMaterialized` missing).

- [ ] **Step 3: Add the enum member**

In `src/LivingWorld.Core/WorldEvent.cs`, append to `WorldEventKind` after `SettlementTradeRecorded`:

```csharp
    SettlementTradeRecorded,
    DrifterMaterialized
```

- [ ] **Step 4: Implement `MaterializeDrifter`**

In `src/LivingWorld.Core/WorldState.cs`, after `GetDrifter` (~line 357):

```csharp
    public Drifter MaterializeDrifter(EntityId drifterId, int pawnThingId, int tick)
    {
        AdvanceToTick(tick);

        if (!_drifters.TryGetValue(drifterId, out var drifter))
        {
            throw new InvalidOperationException($"Drifter {drifterId} does not exist.");
        }

        _drifters.Remove(drifterId);
        AppendEvent(
            WorldEventKind.DrifterMaterialized,
            drifterId,
            $"Drifter {drifterId} materialized as pawn {pawnThingId}.");

        return drifter;
    }
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet run --project src/LivingWorld.Tests`
Expected: line `PASS materializes a pooled drifter and drains the pool`, suite green.

- [ ] **Step 6: Commit**

```bash
git add src/LivingWorld.Core/WorldEvent.cs src/LivingWorld.Core/WorldState.cs src/LivingWorld.Tests/Program.cs
git commit -m "feat(core): DrifterMaterialized event + MaterializeDrifter drains pool"
```

---

## Task 2: Core — `MaterializeNewArrival` (empty-pool "always record")

**Files:**
- Modify: `src/LivingWorld.Core/WorldState.cs` (after `MaterializeDrifter`)
- Test: `src/LivingWorld.Tests/Program.cs`

**Interfaces:**
- Consumes: `WorldState.CreateDrifter`, `MaterializeDrifter` internals (`_drifters`, `AppendEvent`).
- Produces: `Drifter WorldState.MaterializeNewArrival(int pawnThingId, int tick, string name, int age, Sex sex)` — creates a transient origin drifter and materializes it in one step; net pool change zero; emits exactly one `DrifterMaterialized` event.

- [ ] **Step 1: Register + write the failing test**

Add to the `tests` list:

```csharp
    ("records an unpooled arrival with net-zero pool change", TestMaterializeNewArrivalRecordsWithoutGrowingPool),
```

Add the method:

```csharp
static void TestMaterializeNewArrivalRecordsWithoutGrowingPool()
{
    var state = new WorldState(4242);
    var before = state.Drifters.Count;

    state.MaterializeNewArrival(pawnThingId: 555, tick: 60_000, name: "Stray", age: 25, sex: Sex.Female);

    AssertEqual(before, state.Drifters.Count);
    AssertEqual(1, state.Events.Count(e => e.Kind == WorldEventKind.DrifterMaterialized));
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet run --project src/LivingWorld.Tests`
Expected: build error `'WorldState' does not contain a definition for 'MaterializeNewArrival'`.

- [ ] **Step 3: Implement `MaterializeNewArrival`**

In `src/LivingWorld.Core/WorldState.cs`, immediately after `MaterializeDrifter`:

```csharp
    public Drifter MaterializeNewArrival(int pawnThingId, int tick, string name, int age, Sex sex)
    {
        AdvanceToTick(tick);

        var drifter = CreateDrifter(name, age, sex);
        _drifters.Remove(drifter.Id);
        AppendEvent(
            WorldEventKind.DrifterMaterialized,
            drifter.Id,
            $"Drifter {drifter.Id} materialized as pawn {pawnThingId} (unpooled arrival).");

        return drifter;
    }
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet run --project src/LivingWorld.Tests`
Expected: `PASS records an unpooled arrival with net-zero pool change`, suite green.

- [ ] **Step 5: Commit**

```bash
git add src/LivingWorld.Core/WorldState.cs src/LivingWorld.Tests/Program.cs
git commit -m "feat(core): MaterializeNewArrival records unpooled arrivals (always-record)"
```

---

## Task 3: Settings — drifter-flow knobs + `ExposeData`

**Files:**
- Modify: `src/LivingWorld.RimWorld/LivingWorldSettings.cs`
- Test: `src/LivingWorld.Tests/Program.cs`

**Interfaces:**
- Produces: public fields on `LivingWorldSettings`: `bool drifterFlowEnabled`, `int targetWorldPopulationPerSettlement`, `int drifterHardCeiling`, `int maxDrifterArrivalsPerDay`, `int maxDrifterAssimilationsPerDay`, `int drifterMinFounders`, `int drifterLeaderAptitudeThreshold` — each persisted in `ExposeData`.

- [ ] **Step 1: Register + write the failing structural test**

Add to the `tests` list:

```csharp
    ("defines drifter-flow settings persisted in ExposeData", TestRimWorldDrifterFlowSettings),
```

Add the method (near `TestRimWorldWorldGenSettingsWindow`):

```csharp
static void TestRimWorldDrifterFlowSettings()
{
    var path = Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldSettings.cs");
    AssertFileExists(path);
    var source = File.ReadAllText(path);

    AssertContains("public bool drifterFlowEnabled", source);
    AssertContains("public int targetWorldPopulationPerSettlement", source);
    AssertContains("public int drifterHardCeiling", source);
    AssertContains("public int maxDrifterArrivalsPerDay", source);
    AssertContains("public int maxDrifterAssimilationsPerDay", source);
    AssertContains("public int drifterMinFounders", source);
    AssertContains("public int drifterLeaderAptitudeThreshold", source);
    AssertContains("Scribe_Values.Look(ref drifterFlowEnabled", source);
    AssertContains("Scribe_Values.Look(ref drifterHardCeiling", source);
    AssertContains("Scribe_Values.Look(ref drifterLeaderAptitudeThreshold", source);
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet run --project src/LivingWorld.Tests`
Expected: `FAIL defines drifter-flow settings persisted in ExposeData` (AssertContains throws on the first missing field).

- [ ] **Step 3: Add fields + ExposeData**

In `src/LivingWorld.RimWorld/LivingWorldSettings.cs`, add fields after `debugLogging`:

```csharp
    public bool drifterFlowEnabled = true;
    public int targetWorldPopulationPerSettlement = 24;
    public int drifterHardCeiling = 2000;
    public int maxDrifterArrivalsPerDay = 2;
    public int maxDrifterAssimilationsPerDay = 2;
    public int drifterMinFounders = 4;
    public int drifterLeaderAptitudeThreshold = 70;
```

Add to `ExposeData` after the `debugLogging` line:

```csharp
        Scribe_Values.Look(ref drifterFlowEnabled, "drifterFlowEnabled", true);
        Scribe_Values.Look(ref targetWorldPopulationPerSettlement, "targetWorldPopulationPerSettlement", 24);
        Scribe_Values.Look(ref drifterHardCeiling, "drifterHardCeiling", 2000);
        Scribe_Values.Look(ref maxDrifterArrivalsPerDay, "maxDrifterArrivalsPerDay", 2);
        Scribe_Values.Look(ref maxDrifterAssimilationsPerDay, "maxDrifterAssimilationsPerDay", 2);
        Scribe_Values.Look(ref drifterMinFounders, "drifterMinFounders", 4);
        Scribe_Values.Look(ref drifterLeaderAptitudeThreshold, "drifterLeaderAptitudeThreshold", 70);
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet run --project src/LivingWorld.Tests`
Expected: `PASS defines drifter-flow settings persisted in ExposeData`.

- [ ] **Step 5: Commit**

```bash
git add src/LivingWorld.RimWorld/LivingWorldSettings.cs src/LivingWorld.Tests/Program.cs
git commit -m "feat(settings): drifter-flow tuning knobs persisted in ExposeData"
```

---

## Task 4: WorldComponent — daily pipeline wiring + cached aggregates

**Files:**
- Modify: `src/LivingWorld.RimWorld/LivingWorldWorldComponent.cs`
- Test: `src/LivingWorld.Tests/Program.cs`

**Interfaces:**
- Consumes: `DrifterArrivalService.SimulateArrivals`, `DrifterFoundingService.SimulateFounding`, `DrifterAssimilationService.SimulateAssimilation`, `FactionLifecycleService.SimulateCollapses`, `LivingWorldSettings` fields (Task 3), `State.Drifters.Count`, `State.Settlements.Count`.
- Produces: `bool LivingWorldWorldComponent.WantsDrifterArrival` — O(1), true when a player-facing arrival is warranted (pooled drifter exists **or** cached world population is below cached target). Backed by fields updated in the daily tick.

- [ ] **Step 1: Extend the existing component test**

`TestRimWorldWorldComponent` already asserts tick wiring. Add these lines inside it (after the `MigrationService.SimulateDay` assertion):

```csharp
    AssertContains("DrifterArrivalService.SimulateArrivals", source);
    AssertContains("DrifterFoundingService.SimulateFounding", source);
    AssertContains("DrifterAssimilationService.SimulateAssimilation", source);
    AssertContains("FactionLifecycleService.SimulateCollapses", source);
    AssertContains("public bool WantsDrifterArrival", source);
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet run --project src/LivingWorld.Tests`
Expected: `FAIL` at `TestRimWorldWorldComponent` (missing `DrifterArrivalService.SimulateArrivals`).

- [ ] **Step 3: Add cached fields + `WantsDrifterArrival`**

In `LivingWorldWorldComponent.cs`, add fields near `lastSimulatedDay`:

```csharp
    private int cachedWorldPopulation;
    private int cachedTargetPopulation;
```

Add the property near `GetSummary`:

```csharp
    public bool WantsDrifterArrival
    {
        get
        {
            if (!bootstrapped)
            {
                return false;
            }

            var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
            if (!settings.drifterFlowEnabled)
            {
                return false;
            }

            return State.Drifters.Count > 0 || cachedWorldPopulation < cachedTargetPopulation;
        }
    }
```

- [ ] **Step 4: Wire the pipeline into the daily tick**

In `WorldComponentTick`, immediately after the `MigrationService.SimulateDay(...)` call and before `lastSimulatedDay = currentDay;`, add:

```csharp
        if (settings.drifterFlowEnabled && !State.IsInitialWorldSeedingActive)
        {
            var dayTick = currentDay * TicksPerDay;
            var target = Math.Max(0, State.Settlements.Count * Math.Max(0, settings.targetWorldPopulationPerSettlement));
            var ceiling = Math.Max(target, Math.Max(0, settings.drifterHardCeiling));

            DrifterArrivalService.SimulateArrivals(
                State,
                new DrifterArrivalRequest(dayTick, target, ceiling, settings.maxDrifterArrivalsPerDay));
            DrifterFoundingService.SimulateFounding(
                State,
                new DrifterFoundingRequest(dayTick, settings.drifterMinFounders, settings.drifterLeaderAptitudeThreshold));
            DrifterAssimilationService.SimulateAssimilation(
                State,
                new DrifterAssimilationRequest(dayTick, settings.maxDrifterAssimilationsPerDay));
            FactionLifecycleService.SimulateCollapses(
                State,
                new FactionLifecycleRequest(dayTick));

            cachedTargetPopulation = target;
            cachedWorldPopulation = State.Citizens.Count(citizen => citizen.Status == CitizenStatus.Alive) + State.Drifters.Count;
        }
```

(`settings` is already the local resolved earlier in the tick. The one `State.Citizens` pass here runs once per simulated day — the scheduler path — not per game tick, satisfying the hot-path constraint.)

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet run --project src/LivingWorld.Tests`
Expected: `PASS` at `TestRimWorldWorldComponent`, suite green.

- [ ] **Step 6: Commit**

```bash
git add src/LivingWorld.RimWorld/LivingWorldWorldComponent.cs src/LivingWorld.Tests/Program.cs
git commit -m "feat(rimworld): run drifter pipeline + faction extinction in daily tick"
```

---

## Task 5: Settings drawer — drifter-flow sliders

**Files:**
- Modify: `src/LivingWorld.RimWorld/LivingWorldSettingsDrawer.cs`
- Modify: `mod/Languages/English/Keyed/LivingWorld.xml`, `mod/Languages/Russian/Keyed/LivingWorld.xml`
- Test: `src/LivingWorld.Tests/Program.cs`

**Interfaces:**
- Consumes: `LivingWorldSettings` fields (Task 3).
- Produces: drawer rows binding those fields; keyed strings `LW_SettingDrifterFlow`, `LW_SettingDrifterArrivals`, `LW_SettingDrifterCeiling`.

- [ ] **Step 1: Register + write the failing test**

Add to the `tests` list:

```csharp
    ("draws drifter-flow settings with localized labels", TestRimWorldDrifterFlowDrawer),
```

Add the method:

```csharp
static void TestRimWorldDrifterFlowDrawer()
{
    var drawer = File.ReadAllText(Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "LivingWorldSettingsDrawer.cs"));
    AssertContains("drifterFlowEnabled", drawer);
    AssertContains("maxDrifterArrivalsPerDay", drawer);
    AssertContains("drifterHardCeiling", drawer);
    AssertContains("LW_SettingDrifterFlow", drawer);

    var en = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ru = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));
    AssertContains("<LW_SettingDrifterFlow>", en);
    AssertContains("<LW_SettingDrifterFlow>", ru);
    AssertContains("<LW_SettingDrifterArrivals>", en);
    AssertContains("<LW_SettingDrifterArrivals>", ru);
    AssertContains("<LW_SettingDrifterCeiling>", en);
    AssertContains("<LW_SettingDrifterCeiling>", ru);
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet run --project src/LivingWorld.Tests`
Expected: `FAIL draws drifter-flow settings with localized labels`.

- [ ] **Step 3: Add drawer rows**

Open `LivingWorldSettingsDrawer.cs`; follow the file's existing `Listing_Standard` pattern (it already renders `foodPerCitizen` etc.). Add, alongside the existing rows:

```csharp
        listing.CheckboxLabeled("LW_SettingDrifterFlow".Translate(), ref settings.drifterFlowEnabled);
        settings.maxDrifterArrivalsPerDay = (int)listing.SliderLabeled(
            "LW_SettingDrifterArrivals".Translate(settings.maxDrifterArrivalsPerDay.Named("value")),
            settings.maxDrifterArrivalsPerDay, 0, 10);
        settings.drifterHardCeiling = (int)listing.SliderLabeled(
            "LW_SettingDrifterCeiling".Translate(settings.drifterHardCeiling.Named("value")),
            settings.drifterHardCeiling, 100, 10000);
```

(Match the exact `listing`/`settings` variable names already in the file. If the file uses a different slider helper, mirror the existing rows' call shape.)

- [ ] **Step 4: Add keyed strings (EN then RU)**

In `mod/Languages/English/Keyed/LivingWorld.xml`, inside `<LanguageData>`:

```xml
    <LW_SettingDrifterFlow>Enable drifter population flow</LW_SettingDrifterFlow>
    <LW_SettingDrifterArrivals>Max drifter arrivals per day: {value}</LW_SettingDrifterArrivals>
    <LW_SettingDrifterCeiling>World population ceiling: {value}</LW_SettingDrifterCeiling>
```

In `mod/Languages/Russian/Keyed/LivingWorld.xml`, inside `<LanguageData>`:

```xml
    <LW_SettingDrifterFlow>Включить поток населения (дрифтеры)</LW_SettingDrifterFlow>
    <LW_SettingDrifterArrivals>Макс. приходов дрифтеров в день: {value}</LW_SettingDrifterArrivals>
    <LW_SettingDrifterCeiling>Потолок населения мира: {value}</LW_SettingDrifterCeiling>
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet run --project src/LivingWorld.Tests`
Expected: `PASS draws drifter-flow settings with localized labels`.

- [ ] **Step 6: Commit**

```bash
git add src/LivingWorld.RimWorld/LivingWorldSettingsDrawer.cs mod/Languages/English/Keyed/LivingWorld.xml mod/Languages/Russian/Keyed/LivingWorld.xml src/LivingWorld.Tests/Program.cs
git commit -m "feat(settings): drifter-flow sliders with EN/RU labels"
```

---

## Task 6: `CompLivingWorldIdentity` — the pawn identity spine (minimal)

**Files:**
- Create: `src/LivingWorld.RimWorld/CompLivingWorldIdentity.cs`
- Test: `src/LivingWorld.Tests/Program.cs`

**Interfaces:**
- Consumes: `LivingWorld.Core.EntityId`, `Verse.ThingComp`, `Verse.CompProperties`.
- Produces: `CompLivingWorldIdentity : ThingComp` with `public EntityId LedgerId { get; set; }` serialized in `PostExposeData`; `CompProperties_LivingWorldIdentity : CompProperties` with `compClass = typeof(CompLivingWorldIdentity)`.

- [ ] **Step 1: Register + write the failing structural test**

Add to the `tests` list:

```csharp
    ("defines a pawn identity comp round-tripping the ledger id", TestRimWorldIdentityComp),
```

Add the method:

```csharp
static void TestRimWorldIdentityComp()
{
    var path = Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "CompLivingWorldIdentity.cs");
    AssertFileExists(path);
    var source = File.ReadAllText(path);

    AssertContains("class CompLivingWorldIdentity : ThingComp", source);
    AssertContains("class CompProperties_LivingWorldIdentity : CompProperties", source);
    AssertContains("public EntityId LedgerId", source);
    AssertContains("public override void PostExposeData()", source);
    AssertContains("Scribe_Values.Look", source);
    AssertRimWorldMethodExists("Verse.ThingComp", "PostExposeData");
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet run --project src/LivingWorld.Tests`
Expected: `FAIL defines a pawn identity comp round-tripping the ledger id` (file missing).

- [ ] **Step 3: Create the comp**

`src/LivingWorld.RimWorld/CompLivingWorldIdentity.cs`:

```csharp
using LivingWorld.Core;
using Verse;

namespace LivingWorld.RimWorld;

public sealed class CompProperties_LivingWorldIdentity : CompProperties
{
    public CompProperties_LivingWorldIdentity()
    {
        compClass = typeof(CompLivingWorldIdentity);
    }
}

/// <summary>
/// Carries the ledger identity (<see cref="EntityId"/>) on a materialized pawn so the
/// world ledger — not the fragile thingIDNumber — is the durable link. Serialized with
/// the pawn, so it survives save/load and travel into WorldPawns.
/// </summary>
public sealed class CompLivingWorldIdentity : ThingComp
{
    private int ledgerKind;
    private long ledgerValue;

    public bool HasLedgerId => ledgerValue > 0;

    public EntityId LedgerId
    {
        get => EntityId.Create((EntityKind)ledgerKind, ledgerValue);
        set
        {
            ledgerKind = (int)value.Kind;
            ledgerValue = value.Value;
        }
    }

    public void SetLedgerId(EntityId id) => LedgerId = id;

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref ledgerKind, "livingWorld_ledgerKind", 0);
        Scribe_Values.Look(ref ledgerValue, "livingWorld_ledgerValue", 0L);
    }
}
```

Note: `EntityId` is `record struct (EntityKind Kind, long Value)` and `EntityId.Create` throws when value ≤ 0 — hence `Value` is stored as `long` and `LedgerId` is only read after `SetLedgerId` (guard with `HasLedgerId`). `SetLedgerId` is included here so Task 8's worker does not touch the kind/value split.

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet run --project src/LivingWorld.Tests`
Expected: `PASS defines a pawn identity comp round-tripping the ledger id`.

- [ ] **Step 5: Commit**

```bash
git add src/LivingWorld.RimWorld/CompLivingWorldIdentity.cs src/LivingWorld.Tests/Program.cs
git commit -m "feat(rimworld): CompLivingWorldIdentity carries ledger EntityId on pawns"
```

---

## Task 7: `IncidentDef LivingWorld_DrifterArrival` (XML) + localized letter

**Files:**
- Create: `mod/Defs/IncidentDefs/LivingWorld_DrifterArrival.xml`
- Modify: `mod/Languages/English/Keyed/LivingWorld.xml`, `mod/Languages/Russian/Keyed/LivingWorld.xml`
- Test: `src/LivingWorld.Tests/Program.cs`

**Interfaces:**
- Produces: `IncidentDef` `LivingWorld_DrifterArrival` (category `AllyArrival`, `workerClass` = `LivingWorld.RimWorld.IncidentWorker_LivingWorldDrifterArrival`, target `Map_PlayerHome`); keyed strings `LW_DrifterArrivalLetterLabel`, `LW_DrifterArrivalLetterText`.

- [ ] **Step 1: Register + write the failing test**

Add to the `tests` list:

```csharp
    ("defines the drifter arrival incident def", TestRimWorldDrifterArrivalIncidentDef),
```

Add the method:

```csharp
static void TestRimWorldDrifterArrivalIncidentDef()
{
    var path = Path.Combine(FindRepoRoot(), "mod", "Defs", "IncidentDefs", "LivingWorld_DrifterArrival.xml");
    AssertFileExists(path);
    var xml = File.ReadAllText(path);

    AssertContains("<defName>LivingWorld_DrifterArrival</defName>", xml);
    AssertContains("<category>AllyArrival</category>", xml);
    AssertContains("<workerClass>LivingWorld.RimWorld.IncidentWorker_LivingWorldDrifterArrival</workerClass>", xml);
    AssertContains("<targetTags>", xml);
    AssertContains("<li>Map_PlayerHome</li>", xml);

    var en = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "English", "Keyed", "LivingWorld.xml"));
    var ru = File.ReadAllText(Path.Combine(FindRepoRoot(), "mod", "Languages", "Russian", "Keyed", "LivingWorld.xml"));
    AssertContains("<LW_DrifterArrivalLetterLabel>", en);
    AssertContains("<LW_DrifterArrivalLetterLabel>", ru);
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet run --project src/LivingWorld.Tests`
Expected: `FAIL defines the drifter arrival incident def` (file missing).

- [ ] **Step 3: Create the IncidentDef XML**

`mod/Defs/IncidentDefs/LivingWorld_DrifterArrival.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Defs>
  <IncidentDef>
    <defName>LivingWorld_DrifterArrival</defName>
    <label>drifter arrival</label>
    <category>AllyArrival</category>
    <targetTags>
      <li>Map_PlayerHome</li>
    </targetTags>
    <workerClass>LivingWorld.RimWorld.IncidentWorker_LivingWorldDrifterArrival</workerClass>
    <baseChance>1.0</baseChance>
    <minRefireDays>1</minRefireDays>
  </IncidentDef>
</Defs>
```

- [ ] **Step 4: Add localized letter strings (EN then RU)**

English `LivingWorld.xml`:

```xml
    <LW_DrifterArrivalLetterLabel>Drifter arrival</LW_DrifterArrivalLetterLabel>
    <LW_DrifterArrivalLetterText>{0}, a drifter from beyond the world, has arrived and wishes to join your colony.</LW_DrifterArrivalLetterText>
```

Russian `LivingWorld.xml`:

```xml
    <LW_DrifterArrivalLetterLabel>Приход дрифтера</LW_DrifterArrivalLetterLabel>
    <LW_DrifterArrivalLetterText>{0} — дрифтер из-за края мира — прибыл и хочет вступить в вашу колонию.</LW_DrifterArrivalLetterText>
```

- [ ] **Step 5: Run to verify it passes**

Run: `dotnet run --project src/LivingWorld.Tests`
Expected: `PASS defines the drifter arrival incident def`.

- [ ] **Step 6: Commit**

```bash
git add mod/Defs/IncidentDefs/LivingWorld_DrifterArrival.xml mod/Languages/English/Keyed/LivingWorld.xml mod/Languages/Russian/Keyed/LivingWorld.xml src/LivingWorld.Tests/Program.cs
git commit -m "feat(defs): LivingWorld_DrifterArrival incident def + EN/RU letter"
```

---

## Task 8: `IncidentWorker_LivingWorldDrifterArrival` — gate + materialize

**Files:**
- Create: `src/LivingWorld.RimWorld/IncidentWorker_LivingWorldDrifterArrival.cs`
- Test: `src/LivingWorld.Tests/Program.cs`

**Interfaces:**
- Consumes: `LivingWorldWorldComponent.Instance`, `LivingWorldWorldComponent.WantsDrifterArrival` (Task 4), `WorldState.Drifters`, `WorldState.MaterializeDrifter`/`MaterializeNewArrival` (Tasks 1–2), `CompLivingWorldIdentity` (Task 6), RimWorld `IncidentWorker` (`CanFireNowSub`, `TryExecuteWorker`).
- Produces: `IncidentWorker_LivingWorldDrifterArrival : IncidentWorker` — fail-open gate + one-shot materialization that attaches the identity comp.

- [ ] **Step 1: Register + write the failing structural test**

Add to the `tests` list:

```csharp
    ("defines the drifter arrival incident worker", TestRimWorldDrifterArrivalWorker),
```

Add the method:

```csharp
static void TestRimWorldDrifterArrivalWorker()
{
    var path = Path.Combine(FindRepoRoot(), "src", "LivingWorld.RimWorld", "IncidentWorker_LivingWorldDrifterArrival.cs");
    AssertFileExists(path);
    var source = File.ReadAllText(path);

    AssertContains("class IncidentWorker_LivingWorldDrifterArrival : IncidentWorker", source);
    AssertContains("protected override bool CanFireNowSub(IncidentParms parms)", source);
    AssertContains("protected override bool TryExecuteWorker(IncidentParms parms)", source);
    AssertContains("WantsDrifterArrival", source);
    AssertContains("MaterializeDrifter", source);
    AssertContains("MaterializeNewArrival", source);
    AssertContains("CompLivingWorldIdentity", source);
    // fail-open: never throws out, no world mutation on a failed execute
    AssertContains("Instance", source);
    AssertRimWorldMethodExists("RimWorld.IncidentWorker", "TryExecuteWorker");
    AssertRimWorldMethodExists("RimWorld.IncidentWorker", "CanFireNowSub");
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet run --project src/LivingWorld.Tests`
Expected: `FAIL defines the drifter arrival incident worker` (file missing).

- [ ] **Step 3: Create the worker**

`src/LivingWorld.RimWorld/IncidentWorker_LivingWorldDrifterArrival.cs`:

```csharp
using System;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Storyteller-scheduled arrival that materializes a ledger drifter as a colony joiner.
/// Cadence is native (vanilla storyteller picks it from the AllyArrival category) and
/// gated by the ledger via <see cref="CanFireNowSub"/>. Fail-open throughout: any failure
/// leaves the game unchanged.
/// </summary>
public sealed class IncidentWorker_LivingWorldDrifterArrival : IncidentWorker
{
    protected override bool CanFireNowSub(IncidentParms parms)
    {
        if (!base.CanFireNowSub(parms))
        {
            return false;
        }

        var component = LivingWorldWorldComponent.Instance;
        return component != null && component.WantsDrifterArrival;
    }

    protected override bool TryExecuteWorker(IncidentParms parms)
    {
        var component = LivingWorldWorldComponent.Instance;
        if (component == null)
        {
            return false;
        }

        var map = parms.target as Map ?? Find.AnyPlayerHomeMap;
        if (map == null)
        {
            return false;
        }

        try
        {
            var pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.SpaceRefugee,
                faction: Faction.OfPlayer,
                context: PawnGenerationContext.NonPlayer));

            var tick = Find.TickManager?.TicksGame ?? 0;
            var state = component.State;

            var pooled = state.Drifters
                .OrderBy(drifter => drifter.ArrivalTick)
                .ThenBy(drifter => drifter.Id.Value)
                .FirstOrDefault();

            EntityId ledgerId;
            if (pooled != null)
            {
                ledgerId = state.MaterializeDrifter(pooled.Id, pawn.thingIDNumber, tick).Id;
            }
            else
            {
                ledgerId = state
                    .MaterializeNewArrival(pawn.thingIDNumber, tick, pawn.LabelShortCap, pawn.ageTracker.AgeBiologicalYears, Sex.Male)
                    .Id;
            }

            pawn.GetComp<CompLivingWorldIdentity>()?.SetLedgerId(ledgerId);

            GenSpawn.Spawn(pawn, CellFinder.RandomEdgeCell(map), map);
            SendStandardLetter(
                "LW_DrifterArrivalLetterLabel".Translate(),
                "LW_DrifterArrivalLetterText".Translate(pawn.LabelShortCap.Named("PAWN")),
                LetterDefOf.PositiveEvent,
                parms,
                pawn);
            return true;
        }
        catch (Exception error)
        {
            Log.Warning($"[LivingWorld] Drifter arrival failed, deferring to vanilla: {error}");
            return false;
        }
    }
}
```

Notes for the implementer:
- `PawnKindDefOf.SpaceRefugee` and `PawnGenerationRequest`/`PawnGenerationContext` names are RimWorld 1.6 API — verify against `Assembly-CSharp.dll`. If `GetComp<T>()` returns null (comp not on the kind), attach programmatically: `pawn.AllComps.Add(new CompLivingWorldIdentity { parent = pawn })` before `SetLedgerId` (`SetLedgerId` already lives on the comp from Task 6).
- Read `Sex` from the pooled drifter when available (`pooled.Sex`) instead of hardcoding `Sex.Male`; the empty-pool path may keep `Sex.Male` or randomize by `tick % 2`.

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet run --project src/LivingWorld.Tests`
Expected: `PASS defines the drifter arrival incident worker`, suite green.

- [ ] **Step 5: Build the RimWorld assembly to confirm the API compiles**

Run: `dotnet build LivingWorld.sln`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`. If any RimWorld API name mismatches, fix per the installed `Assembly-CSharp.dll` and re-run.

- [ ] **Step 6: Commit**

```bash
git add src/LivingWorld.RimWorld/IncidentWorker_LivingWorldDrifterArrival.cs src/LivingWorld.RimWorld/CompLivingWorldIdentity.cs src/LivingWorld.Tests/Program.cs
git commit -m "feat(rimworld): drifter-arrival incident worker materializes + binds identity"
```

---

## Task 9: Docs — drifter live-flow + materialization in simulation.md

**Files:**
- Modify: `docs/simulation.md`
- Test: `src/LivingWorld.Tests/Program.cs` (docs presence check, mirroring existing doc tests if any) — optional; otherwise skip the test and just update docs.

- [ ] **Step 1: Add a section to `docs/simulation.md`**

Add a "Drifter live flow and materialization" section documenting: the daily tick sequence (arrival→founding→assimilation→collapse), the `LivingWorld_DrifterArrival` incident (AllyArrival, gated by `WantsDrifterArrival`, one-shot materialization, always-record), `CompLivingWorldIdentity` as the durable pawn link, and the explicit non-goals (frequency comp, full identity migration, raid-incident normalization) with a pointer to `docs/design/storyteller-normalization.md`.

- [ ] **Step 2: Run the suite (ensure nothing regressed)**

Run: `dotnet run --project src/LivingWorld.Tests`
Expected: suite green.

- [ ] **Step 3: Commit**

```bash
git add docs/simulation.md
git commit -m "docs: drifter live-flow + materialization in simulation.md"
```

---

## Final verification

- [ ] `dotnet run --project src/LivingWorld.Tests` — all PASS (new: 6 tests — Tasks 1,2,3,5,6,7,8 add tests; Task 4 extends an existing test).
- [ ] `dotnet build LivingWorld.sln` — 0 warnings / 0 errors.
- [ ] Run the project install script (as prior slices did) and confirm the new `Defs/IncidentDefs/LivingWorld_DrifterArrival.xml` and updated DLLs land in `C:\Games\RimWorld\Mods\LivingWorld`.
- [ ] Manual smoke (optional but recommended): load a save, confirm no red errors on load, and that the incident appears in dev-mode "Execute incident → LivingWorld_DrifterArrival" and spawns a joiner with the letter.

## Spec coverage self-check

- Part A tick wiring → Task 4. Settings knobs → Tasks 3, 5. Daily sequence order → Task 4 Step 4.
- Part B custom incident → Tasks 7 (def), 8 (worker). Gate on ledger → Tasks 4 (`WantsDrifterArrival`), 8 (`CanFireNowSub`). Always-record → Tasks 2, 8. `DrifterMaterialized` + Core methods → Tasks 1, 2. Identity comp → Tasks 6, 8.
- Non-goals (frequency comp, full identity migration, raid normalization, vacuum release, extra channels) → untouched by design; documented in Task 9.
