# Settlement Lifecycle Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep `WorldState.Settlements` in sync with real RimWorld settlements that other mods (Economics & Demography, Rim War, Empire) create, destroy, or capture at runtime — instead of only importing once at `FinalizeInit`.

**Architecture:** A pure, unit-tested reconciliation core in `LivingWorld.Core` (tile-keyed diff producing imports / destructions / faction-changes) drives all decisions. Thin Harmony postfixes on `WorldObjectsHolder.Add`, `WorldObjectsHolder.Remove`, and `WorldObject.SetFaction` are immediate triggers; a daily non-destructive pass plus a one-shot load-time pass are the safety net. All mutations reuse existing idempotent Core services.

**Tech Stack:** C# (net472), RimWorld 1.6 modding API, HarmonyLib, custom console test runner in `src/LivingWorld.Tests/Program.cs`.

## Global Constraints

- Target framework: **net472** (RimWorld 1.6). No newer language/runtime features than the existing code uses.
- Build must stay at **0 warnings, 0 errors**: `dotnet build LivingWorld.sln`.
- Tests run via `dotnet run --project src/LivingWorld.Tests/LivingWorld.Tests.csproj`; the runner prints `PASS`/`FAIL` and exits non-zero on any failure. Baseline is **372 tests passing**.
- Test assertion helper is `AssertEqual<T>(T expected, T actual)` (throws on mismatch). No xUnit/NUnit. Register each test as a `(string Name, Action Test)` tuple in the `tests` list at the top of `Program.cs` and define a `static void TestX()` function.
- Harmony patches must **never throw into vanilla** — wrap bodies in `try/catch`, log only under `LivingWorldSettings.debugLogging`.
- Living World never creates a physical `Settlement` itself; all sync logic filters to `obj is Settlement`.
- Reconciliation ledger↔world matching key is the **tile** (encoded in `WorldSettlement.Slug` = `worldobject:{defName}:{tile}:{factionId}`).

---

### Task 1: `SettlementSlug.ParseTile` (Core tile parser)

Introduce a single Core helper for extracting the tile from a settlement slug, and make the existing RimWorld-layer `ParseSettlementTile` delegate to it (DRY).

**Files:**
- Create: `src/LivingWorld.Core/SettlementSlug.cs`
- Modify: `src/LivingWorld.RimWorld/LivingWorldWorldComponent.cs:1950-1959` (delegate `ParseSettlementTile`)
- Test: `src/LivingWorld.Tests/Program.cs`

**Interfaces:**
- Produces: `LivingWorld.Core.SettlementSlug.ParseTile(string? slug) -> int` (returns `-1` when the slug is null/empty/malformed).

- [ ] **Step 1: Write the failing tests**

Add these three functions at the end of the `static void TestX` block in `src/LivingWorld.Tests/Program.cs`:

```csharp
static void TestSettlementSlugParsesTile()
{
    AssertEqual(4211, SettlementSlug.ParseTile("worldobject:Settlement:4211:Pirate"));
}

static void TestSettlementSlugRejectsMalformedSlug()
{
    AssertEqual(-1, SettlementSlug.ParseTile("not-a-slug"));
    AssertEqual(-1, SettlementSlug.ParseTile("worldobject:Settlement:notanumber:Pirate"));
}

static void TestSettlementSlugRejectsEmptySlug()
{
    AssertEqual(-1, SettlementSlug.ParseTile(null));
    AssertEqual(-1, SettlementSlug.ParseTile(""));
}
```

Register them in the `tests` list near the top of `Program.cs` (add after the last settlement-related tuple):

```csharp
    ("parses settlement tile from slug", TestSettlementSlugParsesTile),
    ("rejects malformed settlement slug", TestSettlementSlugRejectsMalformedSlug),
    ("rejects empty settlement slug", TestSettlementSlugRejectsEmptySlug),
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet run --project src/LivingWorld.Tests/LivingWorld.Tests.csproj`
Expected: FAIL to compile — `SettlementSlug` does not exist.

- [ ] **Step 3: Create the implementation**

Create `src/LivingWorld.Core/SettlementSlug.cs`:

```csharp
namespace LivingWorld.Core;

/// <summary>
/// Ledger settlement slugs equal the world-object scanner StableKey:
/// "worldobject:{defName}:{tile}:{factionId}". The physical RimWorld tile is the only
/// stable link between a ledger <see cref="WorldSettlement"/> and its world object.
/// </summary>
public static class SettlementSlug
{
    public static int ParseTile(string? slug)
    {
        if (string.IsNullOrEmpty(slug))
        {
            return -1;
        }

        var parts = slug!.Split(':');
        return parts.Length >= 3 && int.TryParse(parts[2], out var tile) ? tile : -1;
    }
}
```

- [ ] **Step 4: Delegate the existing RimWorld parser**

Replace the body of `ParseSettlementTile` in `src/LivingWorld.RimWorld/LivingWorldWorldComponent.cs` (currently lines 1950-1959):

```csharp
    private static int ParseSettlementTile(string? slug)
    {
        return SettlementSlug.ParseTile(slug);
    }
```

(`LivingWorld.Core` is already imported in this file — it uses `WorldState` etc.)

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet run --project src/LivingWorld.Tests/LivingWorld.Tests.csproj`
Expected: PASS — `375 test(s) passed.`

- [ ] **Step 6: Verify build is clean**

Run: `dotnet build LivingWorld.sln`
Expected: `0 errors, 0 warnings`.

- [ ] **Step 7: Commit**

```bash
git add src/LivingWorld.Core/SettlementSlug.cs src/LivingWorld.RimWorld/LivingWorldWorldComponent.cs src/LivingWorld.Tests/Program.cs
git commit -m "feat(core): add SettlementSlug.ParseTile and delegate ParseSettlementTile"
```

---

### Task 2: `WorldState.FindActiveSettlementByTile` (tile resolver)

One authoritative resolver from a physical tile to its active ledger settlement, replacing the defeat patch's inline logic.

**Files:**
- Modify: `src/LivingWorld.Core/WorldState.cs` (add method near the other settlement queries, e.g. after `GetSettlement` at line 1615)
- Modify: `src/LivingWorld.RimWorld/LivingWorldSettlementDefeatPatch.cs:107-116` (delegate the tile lookup)
- Test: `src/LivingWorld.Tests/Program.cs`

**Interfaces:**
- Consumes: `SettlementSlug.ParseTile` (Task 1).
- Produces: `WorldState.FindActiveSettlementByTile(int tile) -> WorldSettlement?` — the single active settlement whose slug parses to `tile`, else `null`. Returns `null` for `tile < 0`.

- [ ] **Step 1: Write the failing tests**

Add to `Program.cs`:

```csharp
static void TestFindActiveSettlementByTileMatches()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("worldobject:Settlement:512:Pirate", "Redwater", "Pirate");

    var found = state.FindActiveSettlementByTile(512);

    AssertEqual(settlement.Id, found!.Id);
}

static void TestFindActiveSettlementByTileReturnsNullWhenNoTile()
{
    var state = new WorldState(12345);
    state.CreateSettlement("worldobject:Settlement:512:Pirate", "Redwater", "Pirate");

    AssertEqual(true, state.FindActiveSettlementByTile(999) is null);
    AssertEqual(true, state.FindActiveSettlementByTile(-1) is null);
}

static void TestFindActiveSettlementByTileIgnoresDestroyed()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("worldobject:Settlement:512:Pirate", "Redwater", "Pirate");
    SettlementLifecycleService.DestroySettlement(state, settlement.Id, 100, "test");

    AssertEqual(true, state.FindActiveSettlementByTile(512) is null);
}
```

Register:

```csharp
    ("finds active settlement by tile", TestFindActiveSettlementByTileMatches),
    ("find by tile returns null without match", TestFindActiveSettlementByTileReturnsNullWhenNoTile),
    ("find by tile ignores destroyed settlements", TestFindActiveSettlementByTileIgnoresDestroyed),
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet run --project src/LivingWorld.Tests/LivingWorld.Tests.csproj`
Expected: FAIL to compile — `FindActiveSettlementByTile` does not exist.

- [ ] **Step 3: Add the method**

In `src/LivingWorld.Core/WorldState.cs`, immediately after the `GetSettlement(EntityId id)` method (ends around line 1620), add:

```csharp
    public WorldSettlement? FindActiveSettlementByTile(int tile)
    {
        if (tile < 0)
        {
            return null;
        }

        return _settlements.Values.FirstOrDefault(settlement =>
            settlement.IsActive && SettlementSlug.ParseTile(settlement.Slug) == tile);
    }
```

(`System.Linq` is already imported in `WorldState.cs`.)

- [ ] **Step 4: Delegate the defeat patch resolver**

In `src/LivingWorld.RimWorld/LivingWorldSettlementDefeatPatch.cs`, replace the body of `ResolveLedgerSettlement` (lines 107-116) with:

```csharp
    // Ledger settlement slugs embed the RimWorld tile ("worldobject:{defName}:{tile}:{factionId}"),
    // so match the destroyed base by tile, then confirm the faction still agrees. Returns null when unmatched.
    private static WorldSettlement? ResolveLedgerSettlement(WorldState state, Settlement worldObject)
    {
        var ledger = state.FindActiveSettlementByTile(worldObject.Tile);
        if (ledger == null)
        {
            return null;
        }

        var factionId = worldObject.Faction?.def?.defName ?? "UnknownFaction";
        return string.Equals(ledger.FactionId, factionId, StringComparison.Ordinal) ? ledger : null;
    }
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet run --project src/LivingWorld.Tests/LivingWorld.Tests.csproj`
Expected: PASS — `378 test(s) passed.`

- [ ] **Step 6: Verify build is clean**

Run: `dotnet build LivingWorld.sln`
Expected: `0 errors, 0 warnings`.

- [ ] **Step 7: Commit**

```bash
git add src/LivingWorld.Core/WorldState.cs src/LivingWorld.RimWorld/LivingWorldSettlementDefeatPatch.cs src/LivingWorld.Tests/Program.cs
git commit -m "feat(core): add FindActiveSettlementByTile and reuse it in the defeat patch"
```

---

### Task 3: `ChangeSettlementFaction` and `AbandonSettlement` (Core lifecycle ops)

Two new idempotent operations on the existing `SettlementLifecycleService` for runtime capture handling.

**Files:**
- Modify: `src/LivingWorld.Core/SettlementLifecycleService.cs`
- Test: `src/LivingWorld.Tests/Program.cs`

**Interfaces:**
- Consumes: `WorldState.SetSettlementFactionAndStatusForLedger` (internal, exists), `WorldState.SetSettlementLifecycleStatusForLedger` (internal, exists), `WorldState.RecordEvent`, `WorldState.AdvanceToTick`, `WorldState.GetSettlement`.
- Produces:
  - `SettlementLifecycleService.ChangeSettlementFaction(WorldState state, EntityId settlementId, string newFactionId, int tick, string reason) -> WorldSettlement` — updates `FactionId`, records `SettlementCaptured`; no-op (returns unchanged) when the faction already matches.
  - `SettlementLifecycleService.AbandonSettlement(WorldState state, EntityId settlementId, int tick, string reason) -> WorldSettlement` — marks `Abandoned`, records `SettlementAbandoned`, no ruin/refugees; no-op when not `Active`.

- [ ] **Step 1: Write the failing tests**

Add to `Program.cs`:

```csharp
static void TestChangeSettlementFactionUpdatesFactionAndRecordsCapture()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("worldobject:Settlement:512:Pirate", "Redwater", "Pirate");

    var updated = SettlementLifecycleService.ChangeSettlementFaction(
        state, settlement.Id, "Outlander", 100, "captured");

    AssertEqual("Outlander", updated.FactionId);
    AssertEqual(SettlementLifecycleStatus.Active, updated.Status);
    var captured = state.Events.Single(e => e.Kind == WorldEventKind.SettlementCaptured);
    AssertEqual(settlement.Id, captured.SubjectId);
}

static void TestChangeSettlementFactionIsNoOpWhenUnchanged()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("worldobject:Settlement:512:Pirate", "Redwater", "Pirate");

    SettlementLifecycleService.ChangeSettlementFaction(state, settlement.Id, "Pirate", 100, "captured");

    AssertEqual(0, state.Events.Count(e => e.Kind == WorldEventKind.SettlementCaptured));
}

static void TestAbandonSettlementMarksAbandonedWithoutRuin()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("worldobject:Settlement:512:Pirate", "Redwater", "Pirate");

    var updated = SettlementLifecycleService.AbandonSettlement(state, settlement.Id, 100, "captured by player");

    AssertEqual(SettlementLifecycleStatus.Abandoned, updated.Status);
    AssertEqual(1, state.Events.Count(e => e.Kind == WorldEventKind.SettlementAbandoned));
    AssertEqual(0, state.Ruins.Count);
}

static void TestAbandonSettlementIsNoOpWhenNotActive()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("worldobject:Settlement:512:Pirate", "Redwater", "Pirate");
    SettlementLifecycleService.DestroySettlement(state, settlement.Id, 100, "destroyed");

    var abandonEventsBefore = state.Events.Count(e => e.Kind == WorldEventKind.SettlementAbandoned);
    SettlementLifecycleService.AbandonSettlement(state, settlement.Id, 200, "captured by player");

    AssertEqual(abandonEventsBefore, state.Events.Count(e => e.Kind == WorldEventKind.SettlementAbandoned));
}
```

Register:

```csharp
    ("change settlement faction records capture", TestChangeSettlementFactionUpdatesFactionAndRecordsCapture),
    ("change settlement faction no-op when unchanged", TestChangeSettlementFactionIsNoOpWhenUnchanged),
    ("abandon settlement marks abandoned without ruin", TestAbandonSettlementMarksAbandonedWithoutRuin),
    ("abandon settlement no-op when not active", TestAbandonSettlementIsNoOpWhenNotActive),
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet run --project src/LivingWorld.Tests/LivingWorld.Tests.csproj`
Expected: FAIL to compile — the two methods do not exist.

- [ ] **Step 3: Add the two methods**

In `src/LivingWorld.Core/SettlementLifecycleService.cs`, add before the private `MoveAllResources` helper:

```csharp
    public static WorldSettlement ChangeSettlementFaction(
        WorldState state,
        EntityId settlementId,
        string newFactionId,
        int tick,
        string reason)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        ThrowIfNullOrWhiteSpace(newFactionId, nameof(newFactionId));
        ThrowIfNullOrWhiteSpace(reason, nameof(reason));
        state.AdvanceToTick(Math.Max(0, tick));

        var settlement = state.GetSettlement(settlementId)
            ?? throw new InvalidOperationException($"Settlement {settlementId} does not exist.");
        if (string.Equals(settlement.FactionId, newFactionId, StringComparison.Ordinal))
        {
            return settlement;
        }

        var updated = state.SetSettlementFactionAndStatusForLedger(
            settlementId,
            newFactionId,
            SettlementLifecycleStatus.Active);
        state.RecordEvent(
            WorldEventKind.SettlementCaptured,
            settlementId,
            $"Settlement {settlementId} captured by {newFactionId}: {reason}.");

        return updated;
    }

    public static WorldSettlement AbandonSettlement(
        WorldState state,
        EntityId settlementId,
        int tick,
        string reason)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        ThrowIfNullOrWhiteSpace(reason, nameof(reason));
        state.AdvanceToTick(Math.Max(0, tick));

        var settlement = state.GetSettlement(settlementId)
            ?? throw new InvalidOperationException($"Settlement {settlementId} does not exist.");
        if (settlement.Status != SettlementLifecycleStatus.Active)
        {
            return settlement;
        }

        var updated = state.SetSettlementLifecycleStatusForLedger(
            settlementId,
            SettlementLifecycleStatus.Abandoned);
        state.RecordEvent(
            WorldEventKind.SettlementAbandoned,
            settlementId,
            $"Settlement {settlementId} abandoned: {reason}.");

        return updated;
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet run --project src/LivingWorld.Tests/LivingWorld.Tests.csproj`
Expected: PASS — `382 test(s) passed.`

- [ ] **Step 5: Verify build is clean**

Run: `dotnet build LivingWorld.sln`
Expected: `0 errors, 0 warnings`.

- [ ] **Step 6: Commit**

```bash
git add src/LivingWorld.Core/SettlementLifecycleService.cs src/LivingWorld.Tests/Program.cs
git commit -m "feat(core): add ChangeSettlementFaction and AbandonSettlement lifecycle ops"
```

---

### Task 4: `SettlementReconciliationService` (pure diff core)

The heart of the fix: a pure function diffing physical settlement facts against the ledger.

**Files:**
- Create: `src/LivingWorld.Core/SettlementReconciliationService.cs`
- Test: `src/LivingWorld.Tests/Program.cs`

**Interfaces:**
- Consumes: `WorldState.Settlements`, `WorldSettlement`, `SettlementSlug.ParseTile` (Task 1).
- Produces:
  - `readonly record struct PhysicalSettlementFact(string StableKey, string Name, string FactionId, int Tile)`
  - `sealed record SettlementFactionChange(EntityId SettlementId, string NewFactionId)`
  - `sealed record SettlementReconciliationPlan(IReadOnlyList<PhysicalSettlementFact> Imports, IReadOnlyList<EntityId> Destructions, IReadOnlyList<SettlementFactionChange> FactionChanges)`
  - `SettlementReconciliationService.ComputePlan(IReadOnlyList<PhysicalSettlementFact> physical, WorldState state, bool includeDestructions) -> SettlementReconciliationPlan`

- [ ] **Step 1: Write the failing tests**

Add to `Program.cs`:

```csharp
static PhysicalSettlementFact Fact(int tile, string faction) =>
    new($"worldobject:Settlement:{tile}:{faction}", $"Town{tile}", faction, tile);

static void TestReconcilePlansImportForNewPhysicalSettlement()
{
    var state = new WorldState(12345);
    var physical = new List<PhysicalSettlementFact> { Fact(700, "Pirate") };

    var plan = SettlementReconciliationService.ComputePlan(physical, state, includeDestructions: true);

    AssertEqual(1, plan.Imports.Count);
    AssertEqual(700, plan.Imports[0].Tile);
    AssertEqual(0, plan.Destructions.Count);
    AssertEqual(0, plan.FactionChanges.Count);
}

static void TestReconcilePlansDestructionForMissingPhysicalWhenEnabled()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("worldobject:Settlement:700:Pirate", "Redwater", "Pirate");

    var plan = SettlementReconciliationService.ComputePlan(
        new List<PhysicalSettlementFact>(), state, includeDestructions: true);

    AssertEqual(1, plan.Destructions.Count);
    AssertEqual(settlement.Id, plan.Destructions[0]);
}

static void TestReconcileSkipsDestructionWhenDisabled()
{
    var state = new WorldState(12345);
    state.CreateSettlement("worldobject:Settlement:700:Pirate", "Redwater", "Pirate");

    var plan = SettlementReconciliationService.ComputePlan(
        new List<PhysicalSettlementFact>(), state, includeDestructions: false);

    AssertEqual(0, plan.Destructions.Count);
}

static void TestReconcilePlansFactionChangeOnMismatch()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("worldobject:Settlement:700:Pirate", "Redwater", "Pirate");

    var plan = SettlementReconciliationService.ComputePlan(
        new List<PhysicalSettlementFact> { Fact(700, "Outlander") }, state, includeDestructions: true);

    AssertEqual(0, plan.Imports.Count);
    AssertEqual(0, plan.Destructions.Count);
    AssertEqual(1, plan.FactionChanges.Count);
    AssertEqual(settlement.Id, plan.FactionChanges[0].SettlementId);
    AssertEqual("Outlander", plan.FactionChanges[0].NewFactionId);
}

static void TestReconcileNoActionWhenMatched()
{
    var state = new WorldState(12345);
    state.CreateSettlement("worldobject:Settlement:700:Pirate", "Redwater", "Pirate");

    var plan = SettlementReconciliationService.ComputePlan(
        new List<PhysicalSettlementFact> { Fact(700, "Pirate") }, state, includeDestructions: true);

    AssertEqual(0, plan.Imports.Count);
    AssertEqual(0, plan.Destructions.Count);
    AssertEqual(0, plan.FactionChanges.Count);
}

static void TestReconcileIgnoresDestroyedLedgerEntries()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("worldobject:Settlement:700:Pirate", "Redwater", "Pirate");
    SettlementLifecycleService.DestroySettlement(state, settlement.Id, 100, "gone");

    var plan = SettlementReconciliationService.ComputePlan(
        new List<PhysicalSettlementFact>(), state, includeDestructions: true);

    AssertEqual(0, plan.Destructions.Count);
}

static void TestReconcileMixedScenarioProducesAllActions()
{
    var state = new WorldState(12345);
    var captured = state.CreateSettlement("worldobject:Settlement:100:Pirate", "Cap", "Pirate");
    var missing = state.CreateSettlement("worldobject:Settlement:200:Pirate", "Miss", "Pirate");
    state.CreateSettlement("worldobject:Settlement:300:Pirate", "Same", "Pirate");

    var physical = new List<PhysicalSettlementFact>
    {
        Fact(100, "Outlander"), // faction change
        Fact(300, "Pirate"),    // unchanged
        Fact(400, "Tribe"),     // import
    };

    var plan = SettlementReconciliationService.ComputePlan(physical, state, includeDestructions: true);

    AssertEqual(1, plan.Imports.Count);
    AssertEqual(400, plan.Imports[0].Tile);
    AssertEqual(1, plan.Destructions.Count);
    AssertEqual(missing.Id, plan.Destructions[0]);
    AssertEqual(1, plan.FactionChanges.Count);
    AssertEqual(captured.Id, plan.FactionChanges[0].SettlementId);
}
```

Register:

```csharp
    ("reconcile imports new physical settlement", TestReconcilePlansImportForNewPhysicalSettlement),
    ("reconcile destroys missing physical when enabled", TestReconcilePlansDestructionForMissingPhysicalWhenEnabled),
    ("reconcile skips destruction when disabled", TestReconcileSkipsDestructionWhenDisabled),
    ("reconcile plans faction change on mismatch", TestReconcilePlansFactionChangeOnMismatch),
    ("reconcile no action when matched", TestReconcileNoActionWhenMatched),
    ("reconcile ignores destroyed ledger entries", TestReconcileIgnoresDestroyedLedgerEntries),
    ("reconcile mixed scenario produces all actions", TestReconcileMixedScenarioProducesAllActions),
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet run --project src/LivingWorld.Tests/LivingWorld.Tests.csproj`
Expected: FAIL to compile — `SettlementReconciliationService` and the records do not exist.

- [ ] **Step 3: Create the implementation**

Create `src/LivingWorld.Core/SettlementReconciliationService.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace LivingWorld.Core;

public readonly record struct PhysicalSettlementFact(
    string StableKey,
    string Name,
    string FactionId,
    int Tile);

public sealed record SettlementFactionChange(EntityId SettlementId, string NewFactionId);

public sealed record SettlementReconciliationPlan(
    IReadOnlyList<PhysicalSettlementFact> Imports,
    IReadOnlyList<EntityId> Destructions,
    IReadOnlyList<SettlementFactionChange> FactionChanges);

/// <summary>
/// Pure diff between the physical RimWorld NPC settlements (facts gathered by the RimWorld
/// layer) and the active ledger settlements. Matching is by tile — the only stable link,
/// since <see cref="WorldSettlement"/> has no tile field and encodes it in its slug.
/// No mutation: the RimWorld layer applies the returned plan via the lifecycle services.
/// </summary>
public static class SettlementReconciliationService
{
    public static SettlementReconciliationPlan ComputePlan(
        IReadOnlyList<PhysicalSettlementFact> physical,
        WorldState state,
        bool includeDestructions)
    {
        if (physical == null)
        {
            throw new ArgumentNullException(nameof(physical));
        }

        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var activeByTile = new Dictionary<int, WorldSettlement>();
        foreach (var settlement in state.Settlements.Where(s => s.IsActive))
        {
            var tile = SettlementSlug.ParseTile(settlement.Slug);
            if (tile >= 0 && !activeByTile.ContainsKey(tile))
            {
                activeByTile[tile] = settlement;
            }
        }

        var physicalTiles = new HashSet<int>(physical.Where(f => f.Tile >= 0).Select(f => f.Tile));

        var imports = new List<PhysicalSettlementFact>();
        var factionChanges = new List<SettlementFactionChange>();
        foreach (var fact in physical.Where(f => f.Tile >= 0).OrderBy(f => f.Tile))
        {
            if (!activeByTile.TryGetValue(fact.Tile, out var ledger))
            {
                imports.Add(fact);
            }
            else if (!string.Equals(ledger.FactionId, fact.FactionId, StringComparison.Ordinal))
            {
                factionChanges.Add(new SettlementFactionChange(ledger.Id, fact.FactionId));
            }
        }

        var destructions = new List<EntityId>();
        if (includeDestructions)
        {
            foreach (var pair in activeByTile.OrderBy(p => p.Key))
            {
                if (!physicalTiles.Contains(pair.Key))
                {
                    destructions.Add(pair.Value.Id);
                }
            }
        }

        return new SettlementReconciliationPlan(imports, destructions, factionChanges);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet run --project src/LivingWorld.Tests/LivingWorld.Tests.csproj`
Expected: PASS — `389 test(s) passed.`

- [ ] **Step 5: Verify build is clean**

Run: `dotnet build LivingWorld.sln`
Expected: `0 errors, 0 warnings`.

- [ ] **Step 6: Commit**

```bash
git add src/LivingWorld.Core/SettlementReconciliationService.cs src/LivingWorld.Tests/Program.cs
git commit -m "feat(core): add pure SettlementReconciliationService diff"
```

---

### Task 5: Extract `SeedImportedSettlement` (reusable import path)

Extract the per-candidate seeding currently inlined in `BootstrapFromRimWorldSettlements` so both bootstrap and runtime imports share one path. Pure refactor — behavior preserved.

**Files:**
- Modify: `src/LivingWorld.RimWorld/LivingWorldWorldComponent.cs` (the import loop at ~2313-2380 in `BootstrapFromRimWorldSettlements`)

**Interfaces:**
- Produces: `LivingWorldWorldComponent.SeedImportedSettlement(WorldObjectSettlementCandidate candidate, LivingWorldSettings settings) -> void` — creates the ledger settlement (via `CreateSettlement(candidate.StableKey, …)`), seeds baseline population + production profile + starting resources exactly as bootstrap does today.

- [ ] **Step 1: Read the current import loop**

Read `src/LivingWorld.RimWorld/LivingWorldWorldComponent.cs` lines 2285-2420 to see the full `foreach (var settlement in scan.Candidates)` body inside `BootstrapFromRimWorldSettlements`.

- [ ] **Step 2: Move the loop body into a new method**

Create a new private method (place it directly below `BootstrapFromRimWorldSettlements`). Move the **entire body** of the existing `foreach (var settlement in scan.Candidates) { … }` loop into it verbatim, renaming the loop variable `settlement` to the parameter `candidate`:

```csharp
    public void SeedImportedSettlement(WorldObjectSettlementCandidate candidate, LivingWorldSettings settings)
    {
        // <-- the exact body that was inside `foreach (var settlement in scan.Candidates)`,
        //     with every `settlement.` (the candidate) reference now reading `candidate.`
    }
```

Then replace the loop in `BootstrapFromRimWorldSettlements` with:

```csharp
            foreach (var candidate in scan.Candidates)
            {
                SeedImportedSettlement(candidate, settings);
            }
```

Notes for the extraction:
- The `settings` local used inside the loop becomes the method parameter.
- Keep the `State.RunInitialWorldSeeding(() => { … })` block **inside** `SeedImportedSettlement` unchanged — bootstrap and runtime both want baseline citizens created as bulk seeding while the `SettlementCreated` event from `CreateSettlement` still fires.
- Do not change any seeding math.

- [ ] **Step 3: Verify build is clean**

Run: `dotnet build LivingWorld.sln`
Expected: `0 errors, 0 warnings`.

- [ ] **Step 4: Run the full test suite (behavior preserved)**

Run: `dotnet run --project src/LivingWorld.Tests/LivingWorld.Tests.csproj`
Expected: PASS — `389 test(s) passed.` (Source-assertion tests that check `RunInitialWorldSeeding` appears in the component still pass — it is retained inside the new method.)

- [ ] **Step 5: Commit**

```bash
git add src/LivingWorld.RimWorld/LivingWorldWorldComponent.cs
git commit -m "refactor(rimworld): extract SeedImportedSettlement from bootstrap loop"
```

---

### Task 6: `SettlementSyncCoordinator` (RimWorld glue)

The orchestrator that applies reconciliation to the live game. Not unit-tested (RimWorld-coupled); verified by build + full suite + manual play.

**Files:**
- Create: `src/LivingWorld.RimWorld/SettlementSyncCoordinator.cs`
- Modify: `src/LivingWorld.RimWorld/LivingWorldWorldComponent.cs:61-73` (construct and expose `SettlementSync`)

**Interfaces:**
- Consumes: `LivingWorldWorldComponent.State`, `.IsBootstrapped`, `.SeedImportedSettlement` (Task 5), `.EnsureRuinSites` (existing public), `WorldObjectScanner`, `VanillaSettlementImporter`, `SettlementReconciliationService` (Task 4), `SettlementLifecycleService.DestroySettlement`/`ChangeSettlementFaction`/`AbandonSettlement` (Task 3), `WorldState.FindActiveSettlementByTile` (Task 2).
- Produces: `SettlementSyncCoordinator` with `OnSettlementAdded(Settlement)`, `OnSettlementRemoved(Settlement)`, `OnSettlementFactionChanged(Settlement)`, `ReconcileNonDestructive()`, `ReconcileWithDestructions()`; and `LivingWorldWorldComponent.SettlementSync -> SettlementSyncCoordinator`.

- [ ] **Step 1: Create the coordinator**

Create `src/LivingWorld.RimWorld/SettlementSyncCoordinator.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Keeps <see cref="WorldState"/> settlements in sync with real RimWorld settlements that other
/// mods add, remove, or capture at runtime. Hook entry points do targeted single-settlement work;
/// the reconcile passes do a full tile-keyed diff as a safety net. All decisions come from the
/// pure <see cref="SettlementReconciliationService"/>; this class is glue only.
/// </summary>
public sealed class SettlementSyncCoordinator
{
    private readonly LivingWorldWorldComponent component;
    private bool syncing;

    public SettlementSyncCoordinator(LivingWorldWorldComponent component)
    {
        this.component = component ?? throw new ArgumentNullException(nameof(component));
    }

    public void OnSettlementAdded(Settlement settlement)
    {
        if (!Enter(requirePlaying: true))
        {
            return;
        }

        try
        {
            var candidate = ToCandidate(settlement);
            if (candidate == null)
            {
                return; // player settlement or unreadable — VanillaSettlementImporter returned null.
            }

            if (component.State.FindActiveSettlementByTile(candidate.Tile) != null)
            {
                return; // already tracked (e.g. bootstrap imported it).
            }

            component.SeedImportedSettlement(candidate, Settings());
        }
        finally
        {
            Leave();
        }
    }

    public void OnSettlementRemoved(Settlement settlement)
    {
        if (!Enter(requirePlaying: true))
        {
            return;
        }

        try
        {
            var ledger = component.State.FindActiveSettlementByTile(settlement.Tile);
            if (ledger == null)
            {
                return;
            }

            SettlementLifecycleService.DestroySettlement(
                component.State, ledger.Id, CurrentTick(), "settlement removed at runtime");
            component.EnsureRuinSites();
        }
        finally
        {
            Leave();
        }
    }

    public void OnSettlementFactionChanged(Settlement settlement)
    {
        if (!Enter(requirePlaying: true))
        {
            return;
        }

        try
        {
            var ledger = component.State.FindActiveSettlementByTile(settlement.Tile);
            if (ledger == null)
            {
                return; // not tracked yet; the Add hook will import it.
            }

            if (settlement.Faction?.IsPlayer == true)
            {
                // Player captured the base: it leaves the NPC simulation (LW never sims player bases).
                SettlementLifecycleService.AbandonSettlement(
                    component.State, ledger.Id, CurrentTick(), "settlement captured by player");
                return;
            }

            var newFactionId = settlement.Faction?.def?.defName;
            if (string.IsNullOrEmpty(newFactionId))
            {
                return;
            }

            SettlementLifecycleService.ChangeSettlementFaction(
                component.State, ledger.Id, newFactionId!, CurrentTick(), "settlement captured at runtime");
        }
        finally
        {
            Leave();
        }
    }

    public void ReconcileNonDestructive() => Reconcile(includeDestructions: false);

    public void ReconcileWithDestructions() => Reconcile(includeDestructions: true);

    private void Reconcile(bool includeDestructions)
    {
        if (!Enter(requirePlaying: false))
        {
            return;
        }

        try
        {
            var scan = new WorldObjectScanner().Scan();
            var facts = scan.Candidates
                .Select(c => new PhysicalSettlementFact(c.StableKey, c.Name, c.FactionId, c.Tile))
                .ToList();
            var plan = SettlementReconciliationService.ComputePlan(facts, component.State, includeDestructions);

            var candidatesByTile = new Dictionary<int, WorldObjectSettlementCandidate>();
            foreach (var candidate in scan.Candidates)
            {
                candidatesByTile[candidate.Tile] = candidate;
            }

            var settings = Settings();
            foreach (var import in plan.Imports)
            {
                if (candidatesByTile.TryGetValue(import.Tile, out var candidate))
                {
                    component.SeedImportedSettlement(candidate, settings);
                }
            }

            foreach (var change in plan.FactionChanges)
            {
                SettlementLifecycleService.ChangeSettlementFaction(
                    component.State, change.SettlementId, change.NewFactionId, CurrentTick(), "settlement ownership reconciled");
            }

            var destroyedAny = false;
            foreach (var id in plan.Destructions)
            {
                SettlementLifecycleService.DestroySettlement(
                    component.State, id, CurrentTick(), "settlement missing at reconcile");
                destroyedAny = true;
            }

            if (destroyedAny)
            {
                component.EnsureRuinSites();
            }
        }
        finally
        {
            Leave();
        }
    }

    private WorldObjectSettlementCandidate? ToCandidate(Settlement settlement)
    {
        var errors = 0;
        return new VanillaSettlementImporter().ImportCandidate(settlement, ref errors);
    }

    private static LivingWorldSettings Settings() => LivingWorldSettings.Instance ?? new LivingWorldSettings();

    private static int CurrentTick() => Find.TickManager?.TicksGame ?? 0;

    private bool Enter(bool requirePlaying)
    {
        if (syncing)
        {
            return false;
        }

        if (requirePlaying && Current.ProgramState != ProgramState.Playing)
        {
            return false;
        }

        if (!component.IsBootstrapped || component.State == null)
        {
            return false;
        }

        syncing = true;
        return true;
    }

    private void Leave() => syncing = false;
}
```

- [ ] **Step 2: Construct and expose the coordinator**

In `src/LivingWorld.RimWorld/LivingWorldWorldComponent.cs`, update the constructor (lines 61-67) and add the property:

```csharp
    public LivingWorldWorldComponent(World world)
        : base(world)
    {
        rimWorld = world;
        Instance = this;
        State = new WorldState(ResolveWorldSeed(rimWorld));
        SettlementSync = new SettlementSyncCoordinator(this);
    }

    public static LivingWorldWorldComponent? Instance { get; private set; }

    public WorldState State { get; private set; }

    public SettlementSyncCoordinator SettlementSync { get; }

    public bool IsBootstrapped => bootstrapped;
```

- [ ] **Step 3: Verify build is clean**

Run: `dotnet build LivingWorld.sln`
Expected: `0 errors, 0 warnings`.

- [ ] **Step 4: Run the full test suite (no regressions)**

Run: `dotnet run --project src/LivingWorld.Tests/LivingWorld.Tests.csproj`
Expected: PASS — `389 test(s) passed.`

- [ ] **Step 5: Commit**

```bash
git add src/LivingWorld.RimWorld/SettlementSyncCoordinator.cs src/LivingWorld.RimWorld/LivingWorldWorldComponent.cs
git commit -m "feat(rimworld): add SettlementSyncCoordinator glue"
```

---

### Task 7: Harmony postfixes (immediate triggers)

Three thin postfixes that route world-object lifecycle events to the coordinator.

**Files:**
- Create: `src/LivingWorld.RimWorld/LivingWorldSettlementLifecyclePatch.cs`

**Interfaces:**
- Consumes: `LivingWorldWorldComponent.Instance`, `.SettlementSync` (Task 6).
- Produces: three Harmony patch classes on `WorldObjectsHolder.Add`, `WorldObjectsHolder.Remove`, `WorldObject.SetFaction`, plus an internal `SettlementLifecycleHooks.Dispatch` helper.

- [ ] **Step 1: Create the patch file**

Create `src/LivingWorld.RimWorld/LivingWorldSettlementLifecyclePatch.cs`:

```csharp
using System;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Immediate triggers that keep the ledger in sync when other mods add/remove/capture real
/// settlement world objects at runtime. Each postfix is thin, guarded, and fail-open — a daily
/// reconcile pass is the safety net. Player-ownership is decided inside the coordinator, not here,
/// because a capture *to* the player must still be handled (abandon), unlike adds/removes.
/// </summary>
internal static class SettlementLifecycleHooks
{
    internal static void Dispatch(WorldObject? worldObject, Action<SettlementSyncCoordinator, Settlement> action)
    {
        try
        {
            if (worldObject is not Settlement settlement)
            {
                return;
            }

            var component = LivingWorldWorldComponent.Instance;
            if (component?.SettlementSync == null)
            {
                return;
            }

            action(component.SettlementSync, settlement);
        }
        catch (Exception ex)
        {
            if ((LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging)
            {
                Log.Warning($"[LivingWorld] Settlement lifecycle hook failed safely: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}

[HarmonyPatch(typeof(WorldObjectsHolder), nameof(WorldObjectsHolder.Add))]
public static class LivingWorldWorldObjectAddPatch
{
    public static void Postfix(WorldObject o)
    {
        SettlementLifecycleHooks.Dispatch(o, (coordinator, settlement) => coordinator.OnSettlementAdded(settlement));
    }
}

[HarmonyPatch(typeof(WorldObjectsHolder), nameof(WorldObjectsHolder.Remove))]
public static class LivingWorldWorldObjectRemovePatch
{
    public static void Postfix(WorldObject o)
    {
        SettlementLifecycleHooks.Dispatch(o, (coordinator, settlement) => coordinator.OnSettlementRemoved(settlement));
    }
}

[HarmonyPatch(typeof(WorldObject), nameof(WorldObject.SetFaction))]
public static class LivingWorldWorldObjectSetFactionPatch
{
    public static void Postfix(WorldObject __instance)
    {
        SettlementLifecycleHooks.Dispatch(__instance, (coordinator, settlement) => coordinator.OnSettlementFactionChanged(settlement));
    }
}
```

- [ ] **Step 2: Verify build is clean**

Run: `dotnet build LivingWorld.sln`
Expected: `0 errors, 0 warnings`.

If the build reports an ambiguous match on `Add`/`Remove`/`SetFaction`, disambiguate the offending `[HarmonyPatch]` with explicit argument types, e.g. `[HarmonyPatch(typeof(WorldObjectsHolder), nameof(WorldObjectsHolder.Add), new[] { typeof(WorldObject) })]` and `[HarmonyPatch(typeof(WorldObject), nameof(WorldObject.SetFaction), new[] { typeof(Faction) })]`.

- [ ] **Step 3: Confirm Harmony discovers the patches**

The mod already calls `harmony.PatchAll()` in `LivingWorldMod.cs` (verify with a quick read). No change needed — annotated patches in the assembly are auto-discovered.

- [ ] **Step 4: Run the full test suite (no regressions)**

Run: `dotnet run --project src/LivingWorld.Tests/LivingWorld.Tests.csproj`
Expected: PASS — `389 test(s) passed.`

- [ ] **Step 5: Commit**

```bash
git add src/LivingWorld.RimWorld/LivingWorldSettlementLifecyclePatch.cs
git commit -m "feat(rimworld): add Harmony postfixes for runtime settlement lifecycle"
```

---

### Task 8: Wire reconcile passes into the world component

Run the one-shot migration pass on load and the non-destructive pass daily.

**Files:**
- Modify: `src/LivingWorld.RimWorld/LivingWorldWorldComponent.cs:151-168` (`FinalizeInit`)
- Modify: `src/LivingWorld.RimWorld/LivingWorldWorldComponent.cs:197-208` (daily catch-up in `WorldComponentTick`)

**Interfaces:**
- Consumes: `SettlementSync.ReconcileWithDestructions()`, `SettlementSync.ReconcileNonDestructive()` (Task 6).

- [ ] **Step 1: Add the load-time migration pass**

In `FinalizeInit`, add the reconcile call after `LivingWorldOrphanedLordReferenceCleaner.CleanAllMaps();` (line 167), so it runs once after bootstrap/repair have populated the ledger:

```csharp
        LivingWorldOrphanedLordReferenceCleaner.CleanAllMaps();
        // One-shot: clear ghost ledger entries for settlements removed by other mods before this
        // fix existed, and import/re-faction any that drifted while saved. Safe at load time — every
        // real settlement is present and scannable.
        SettlementSync.ReconcileWithDestructions();
```

- [ ] **Step 2: Add the daily non-destructive pass**

In `WorldComponentTick`, add the reconcile call right after the catch-up `while` loop (after line 203, before the capped-catch-up warning at line 205). This block runs at most once per in-game day because of the early return at line 192-195:

```csharp
        var simulatedDays = 0;
        while (lastSimulatedDay < currentDay && simulatedDays < MaxCatchUpSimulationDays)
        {
            lastSimulatedDay++;
            SimulateWorldDay(lastSimulatedDay);
            simulatedDays++;
        }

        // Safety net for any add/capture the hooks missed (mods bypassing the standard API). Never
        // destroys — removal is driven solely by the authoritative Remove hook — so a transiently
        // unscannable settlement is never wrongly ruined.
        SettlementSync.ReconcileNonDestructive();
```

- [ ] **Step 3: Verify build is clean**

Run: `dotnet build LivingWorld.sln`
Expected: `0 errors, 0 warnings`.

- [ ] **Step 4: Run the full test suite (no regressions)**

Run: `dotnet run --project src/LivingWorld.Tests/LivingWorld.Tests.csproj`
Expected: PASS — `389 test(s) passed.`

- [ ] **Step 5: Commit**

```bash
git add src/LivingWorld.RimWorld/LivingWorldWorldComponent.cs
git commit -m "feat(rimworld): run load-time and daily settlement reconcile passes"
```

---

### Task 9: Manual in-game verification

The Harmony glue (Tasks 6-8) cannot be unit-tested. Verify end-to-end in RimWorld.

**Files:** none (verification only).

- [ ] **Step 1: Build and install the mod**

Run the project's install path (see prior sessions / `docs`): build `LivingWorld.sln`, then copy `LivingWorld.Core.dll` and `LivingWorld.RimWorld.dll` into `C:\Games\RimWorld\Mods\LivingWorld\1.6\Assemblies`. Enable `debugLogging` in Living World settings.

- [ ] **Step 2: Verify runtime addition**

Load a save with Economics & Demography active. Let ED create a settlement (or use its dev tools). In `Player.log`, confirm a `SettlementCreated` event and that the new settlement appears in the Living World economy window with citizens and a production profile.

- [ ] **Step 3: Verify runtime destruction**

Have ED (or dev-tool `Destroy`) remove an NPC settlement. Confirm a `SettlementDestroyed` event, a ruin site appears at the tile, refugees are recorded, and the ledger no longer lists it as active — no "ghost".

- [ ] **Step 4: Verify capture**

With Empire/Rim War, capture an NPC settlement (faction change). Confirm a `SettlementCaptured` event and the ledger entry's faction updates. If the player captures it, confirm a `SettlementAbandoned` event and that it leaves the active NPC set (no ruin).

- [ ] **Step 5: Verify the player-defeat path is unbroken**

Attack and defeat an NPC base yourself. Confirm the existing defeat behavior still fires exactly once (no duplicate ruin/refugees from the new Remove hook — the defeat prefix marks it destroyed first, so the Remove hook no-ops).

- [ ] **Step 6: Record results**

Note the observed log lines in the PR description / session memory. If any step misbehaves, use superpowers:systematic-debugging before adjusting code.

---

## Self-Review

**Spec coverage:**
- Missed additions → Tasks 4 (plan), 5 (seed path), 6 (`OnSettlementAdded` + reconcile imports), 7 (Add hook), 8 (daily pass). ✓
- Ghost entries on removal → Tasks 3 (reuse `DestroySettlement`), 6 (`OnSettlementRemoved`), 7 (Remove hook), 8 (load-time destruction pass migrates old saves). ✓
- Stale ownership / capture → Tasks 3 (`ChangeSettlementFaction`/`AbandonSettlement`), 6 (`OnSettlementFactionChanged`), 7 (SetFaction hook). ✓
- Tile-keyed matching, no new Tile field → Tasks 1, 2, 4. ✓
- Full-seed on add → Task 5 reuses bootstrap seeding. ✓
- Full-destruction on remove (ruin + refugees) → Task 3/6 via existing `DestroySettlement`. ✓
- Capture-to-player = abandon → Task 3/6. ✓
- Hybrid mechanism (core + hooks + daily + load-time) → Tasks 4/6/7/8. ✓
- Guards (Playing, bootstrapped, is Settlement, try/catch, re-entrancy) → Tasks 6 (`Enter`), 7 (`Dispatch`). ✓
- Defeat-patch composition unchanged → Task 2 (resolver reuse), Task 9 Step 5 (verify). ✓
- Daily pass never destroys → Task 8 uses `ReconcileNonDestructive`. ✓
- No new persisted state → nothing added to `ExposeData`. ✓

**Placeholder scan:** No TBD/TODO. Task 5 is a mechanical move of existing code; its exact source is identified by line range and the reader is told precisely what to rename and preserve. All code steps show concrete code.

**Type consistency:** `PhysicalSettlementFact(StableKey, Name, FactionId, Tile)`, `SettlementFactionChange(SettlementId, NewFactionId)`, `SettlementReconciliationPlan(Imports, Destructions, FactionChanges)`, and `ComputePlan(physical, state, includeDestructions)` are used identically across Tasks 4 and 6. `SeedImportedSettlement(candidate, settings)` signature matches between Tasks 5 and 6. `FindActiveSettlementByTile(int)`, `ChangeSettlementFaction(state, id, newFactionId, tick, reason)`, `AbandonSettlement(state, id, tick, reason)`, `DestroySettlement(state, id, tick, reason)` consistent across tasks. `SettlementSync` property name consistent across Tasks 6, 7, 8.
