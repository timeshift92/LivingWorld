# LivingWorld Core Ledger Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first testable Living World milestone: a deterministic core ledger with stable entity IDs, citizen/settlement records, append-only events, and validation.

**Architecture:** This implements only `LivingWorld.Core` and `LivingWorld.Tests`. The core is RimWorld-independent so it can be tested without launching the game; future RimWorld/Harmony code will materialize pawns from these records instead of storing the world as `Pawn`.

**Tech Stack:** C#/.NET, SDK-style projects, no external NuGet packages for the first milestone, console-based deterministic test harness.

---

## File Structure

- Create: `LivingWorld.sln`
  - solution file for source and tests.
- Create: `src/LivingWorld.Core/LivingWorld.Core.csproj`
  - library project targeting `net8.0`.
- Create: `src/LivingWorld.Core/EntityId.cs`
  - stable value object for ledger IDs.
- Create: `src/LivingWorld.Core/WorldCitizen.cs`
  - compact citizen record, not a RimWorld pawn.
- Create: `src/LivingWorld.Core/WorldSettlement.cs`
  - compact settlement record with derived population counts.
- Create: `src/LivingWorld.Core/WorldEvent.cs`
  - append-only event record.
- Create: `src/LivingWorld.Core/WorldState.cs`
  - owns citizens, settlements, events, ID allocation, validation.
- Create: `src/LivingWorld.Core/LivingWorldApi.cs`
  - first in-process API surface for querying the ledger.
- Create: `src/LivingWorld.Tests/LivingWorld.Tests.csproj`
  - console test harness referencing `LivingWorld.Core`.
- Create: `src/LivingWorld.Tests/Program.cs`
  - red/green tests for ID allocation, no-Pawn citizen records, event sourcing, settlement population, and validation.

## Task 1: Scaffold Solution and Test Harness

**Files:**
- Create: `LivingWorld.sln`
- Create: `src/LivingWorld.Core/LivingWorld.Core.csproj`
- Create: `src/LivingWorld.Tests/LivingWorld.Tests.csproj`
- Create: `src/LivingWorld.Tests/Program.cs`

- [ ] **Step 1: Create solution and projects**

Run:

```powershell
dotnet new sln -n LivingWorld
dotnet new classlib -n LivingWorld.Core -o src/LivingWorld.Core --framework net8.0
dotnet new console -n LivingWorld.Tests -o src/LivingWorld.Tests --framework net8.0
dotnet sln LivingWorld.sln add src/LivingWorld.Core/LivingWorld.Core.csproj
dotnet sln LivingWorld.sln add src/LivingWorld.Tests/LivingWorld.Tests.csproj
dotnet add src/LivingWorld.Tests/LivingWorld.Tests.csproj reference src/LivingWorld.Core/LivingWorld.Core.csproj
```

- [ ] **Step 2: Write failing tests**

Replace `src/LivingWorld.Tests/Program.cs` with tests that reference the intended core API:

```csharp
using LivingWorld.Core;

var tests = new List<(string Name, Action Test)>
{
    ("allocates stable sequential citizen ids", TestSequentialCitizenIds),
    ("stores citizens as lightweight records instead of pawns", TestCitizenRecord),
    ("records append-only events when citizens are created", TestCitizenCreatedEvent),
    ("tracks settlement population from assigned citizens", TestSettlementPopulation),
    ("reports validation errors for orphan citizens", TestValidationErrors),
};

var failures = new List<string>();

foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{name}: {ex.Message}");
        Console.WriteLine($"FAIL {name}: {ex.Message}");
    }
}

if (failures.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine($"{failures.Count} test(s) failed.");
    Environment.Exit(1);
}

Console.WriteLine();
Console.WriteLine($"{tests.Count} test(s) passed.");

static void TestSequentialCitizenIds()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "pirates");

    var first = state.CreateCitizen("Ada", 20, Sex.Female, "farmer", settlement.Id);
    var second = state.CreateCitizen("Borin", 31, Sex.Male, "soldier", settlement.Id);

    AssertEqual("Citizen:1", first.Id.ToString());
    AssertEqual("Citizen:2", second.Id.ToString());
}

static void TestCitizenRecord()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("evos", "Evos Village", "civil");
    var citizen = state.CreateCitizen("Nara", 42, Sex.Female, "doctor", settlement.Id);

    AssertEqual("Nara", citizen.Name);
    AssertEqual(42, citizen.Age);
    AssertEqual(Sex.Female, citizen.Sex);
    AssertEqual("doctor", citizen.Profession);
    AssertEqual(CitizenStatus.Alive, citizen.Status);
    AssertEqual(settlement.Id, citizen.SettlementId);
}

static void TestCitizenCreatedEvent()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "pirates");
    var citizen = state.CreateCitizen("Kira", 27, Sex.Female, "soldier", settlement.Id);

    var created = state.Events.Single(e => e.Kind == WorldEventKind.CitizenCreated);

    AssertEqual(citizen.Id, created.SubjectId);
    AssertEqual("CitizenCreated", created.Kind.ToString());
    AssertEqual(0, created.Tick);
}

static void TestSettlementPopulation()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "pirates");

    state.CreateCitizen("Child", 8, Sex.Male, "child", settlement.Id);
    state.CreateCitizen("Adult", 40, Sex.Female, "farmer", settlement.Id);
    state.CreateCitizen("Elder", 71, Sex.Male, "elder", settlement.Id);

    var population = state.GetSettlementPopulation(settlement.Id);

    AssertEqual(3, population.Total);
    AssertEqual(1, population.Children);
    AssertEqual(1, population.Adults);
    AssertEqual(1, population.Elderly);
}

static void TestValidationErrors()
{
    var state = new WorldState(12345);
    var missingSettlement = EntityId.Create(EntityKind.Settlement, 404);

    state.ImportCitizen(new WorldCitizen(
        EntityId.Create(EntityKind.Citizen, 1),
        "Orphan",
        30,
        Sex.Male,
        "wanderer",
        missingSettlement,
        CitizenStatus.Alive));

    var errors = state.Validate().ToList();

    AssertEqual(1, errors.Count);
    AssertEqual("Citizen Citizen:1 references missing settlement Settlement:404.", errors[0]);
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}
```

- [ ] **Step 3: Run tests to verify RED**

Run:

```powershell
dotnet run --project src/LivingWorld.Tests/LivingWorld.Tests.csproj
```

Expected: FAIL to compile because `WorldState`, `EntityId`, `WorldCitizen`, and related types are not implemented.

## Task 2: Implement Core Ledger Types

**Files:**
- Create: `src/LivingWorld.Core/EntityId.cs`
- Create: `src/LivingWorld.Core/WorldCitizen.cs`
- Create: `src/LivingWorld.Core/WorldSettlement.cs`
- Create: `src/LivingWorld.Core/WorldEvent.cs`
- Create: `src/LivingWorld.Core/WorldState.cs`
- Modify: `src/LivingWorld.Core/LivingWorld.Core.csproj`

- [ ] **Step 1: Add minimal implementation**

Implement only the types and methods required by the failing tests:

- `EntityKind`
- `EntityId`
- `Sex`
- `CitizenStatus`
- `WorldCitizen`
- `WorldSettlement`
- `SettlementPopulation`
- `WorldEventKind`
- `WorldEvent`
- `WorldState`

- [ ] **Step 2: Run tests to verify GREEN**

Run:

```powershell
dotnet run --project src/LivingWorld.Tests/LivingWorld.Tests.csproj
```

Expected: `5 test(s) passed.`

## Task 3: Add First Public API Wrapper

**Files:**
- Modify: `src/LivingWorld.Tests/Program.cs`
- Create: `src/LivingWorld.Core/LivingWorldApi.cs`

- [ ] **Step 1: Add failing API test**

Add a test named `queries population through public api`:

```csharp
static void TestPublicApiPopulationQuery()
{
    var state = new WorldState(12345);
    var settlement = state.CreateSettlement("north-camp", "Northern Camp", "pirates");
    state.CreateCitizen("Raider", 28, Sex.Male, "soldier", settlement.Id);

    ILivingWorldApi api = new LivingWorldApi(state);
    var population = api.GetSettlementPopulation(settlement.Id);

    AssertEqual(1, population.Total);
}
```

Expected RED: compile failure because `ILivingWorldApi` and `LivingWorldApi` do not exist.

- [ ] **Step 2: Implement API wrapper**

Create:

- `ILivingWorldApi.GetCitizen(EntityId id)`
- `ILivingWorldApi.GetSettlement(EntityId id)`
- `ILivingWorldApi.GetSettlementPopulation(EntityId settlementId)`
- `LivingWorldApi`

- [ ] **Step 3: Run tests to verify GREEN**

Run:

```powershell
dotnet run --project src/LivingWorld.Tests/LivingWorld.Tests.csproj
```

Expected: `6 test(s) passed.`

## Task 4: Verify Whole Milestone

**Files:**
- All source and docs files from Tasks 1-3.

- [ ] **Step 1: Build solution**

Run:

```powershell
dotnet build LivingWorld.sln
```

Expected: build succeeds with 0 errors.

- [ ] **Step 2: Run test harness**

Run:

```powershell
dotnet run --project src/LivingWorld.Tests/LivingWorld.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 3: Inspect generated structure**

Run:

```powershell
Get-ChildItem -Recurse C:\Games\LivingWorld\src | Select-Object FullName
```

Expected: `LivingWorld.Core` and `LivingWorld.Tests` exist with the files listed above.

## Self-Review

Spec coverage:

- Implements the first roadmap stage: Core world ledger.
- Enforces no-Pawn source-of-truth by using `WorldCitizen` and `WorldSettlement` records only.
- Starts event sourcing with append-only `WorldEvent` records.
- Starts deterministic simulation foundation with world seed stored in `WorldState`.
- Starts API foundation with `ILivingWorldApi`.

Known deferred scope:

- RimWorld `Mod`, `GameComponent`, Harmony, pawn materialization, raids, animals, save format integration, and UI are intentionally deferred until the core ledger has a passing independent test baseline.
