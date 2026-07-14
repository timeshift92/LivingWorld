# Arsenal Expansion — Phase A (Foundation & Correctness) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Arsenal's threat tiers real (pure `ThreatClassifier` + CAI `DefendPoint` + the required `aiAutoControl` fix + tier→duty selection), lock the pure decision core with a property-based invariant harness, and fix the correctness bugs surfaced by review (candidate-abandonment, unpersisted drafts, coarse `IsArmed`, caravan race, threat flicker).

**Architecture:** New pure logic in `LivingWorld.Core` (`ThreatClassifier`, `ThreatDebounce`) decides tiers and debounce from plain-data structs, fully unit-tested. Thin RimWorld glue computes those structs from the live map, threads the tier through `MobilizationDriver` into `CaiBridge` (which now sets CAI's `aiAutoControl` and picks `DefendPoint` vs `HuntDownEnemies`), and persists the auto-drafted set across save/load. The pure `MobilizationPlan` is corrected so a pawn that stops being a candidate mid-alert is still unwound.

**Tech Stack:** C# — `LivingWorld.Core` (netstandard2.0/net8.0 test host) + `LivingWorld.RimWorld` (net472, RimWorld 1.6 + Odyssey, reflection-only soft dep on CAI 5000). Custom test harness in `src/LivingWorld.Tests/Program.cs` (tuple list + `AssertEqual<T>`), run with `dotnet run`.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-07-14-arsenal-expansion-design.md` (builds on the merged v2 `2026-07-10-arsenal-redesign-design.md`).
- Only `LivingWorld.Core` is unit-testable (test project references Core only). New pure logic goes there; RimWorld glue is verified by clean `net472` build + review + in-game diagnostic logs.
- Every public RimWorld entry point is fail-safe: `try/catch` → `Log.Warning($"[LivingWorld] <what> failed safely: {ex.Message}")`; never throw into the game loop.
- CAI is a SOFT dependency: NO assembly reference to `CombatAI.dll`; all access via reflection; absence degrades to drafting.
- **CAI requires `aiAutoControl`:** after starting a CAI duty on a drafted pawn, set `ThingComp_CombatAI.aiAutoControl = true` (default is `false`; nothing else sets it) or the reactive layer (duck/retreat/evade) never runs. Clear it on stand-down.
- **Tier → duty:** `Serious` → `DefendPoint` (vanilla `Defend`, clean); everything else → `HuntDownEnemies`. NEVER use `AssaultPoint` for on-map defenders (its DutyDef trashes buildings when idle).
- Zero build warnings (`dotnet build src/LivingWorld.RimWorld/LivingWorld.RimWorld.csproj -c Debug` → `Предупреждений: 0` / `Ошибок: 0`).
- Never iterate a live pawn list while mutating it: snapshot with `.ToList()`.
- Test baseline before this plan: **369 test(s) passed.** New Core tests add to this.

---

## File Structure

**Create:**
- `src/LivingWorld.Core/ThreatClassifier.cs` — `ThreatTier` enum, `ThreatSignals` struct, pure `Classify`.
- `src/LivingWorld.Core/ThreatDebounce.cs` — pure de-escalation hysteresis.

**Modify:**
- `src/LivingWorld.Core/MobilizationPlan.cs` — candidate-abandonment fix in `NextAction`.
- `src/LivingWorld.RimWorld/CaiBridge.cs` — `aiAutoControl`, `DefendPoint`, tier-aware `TryEngage`, `Disengage`.
- `src/LivingWorld.RimWorld/MobilizationDriver.cs` — thread tier, tier-aware engage/re-engage, `Disengage` on clear, drafted-set export/import, tier in diagnostics.
- `src/LivingWorld.RimWorld/MobilizationMapComponent.cs` — compute `ThreatSignals` + tier + debounce, Scribe drafted set, pass tier to driver, tier in `DiagnosePawn`.
- `src/LivingWorld.RimWorld/MobilizationCandidates.cs` — `IsInCombatKit` (armored-aware).
- `src/LivingWorld.RimWorld/OutfitStandDebugActions.cs` — dump the derived tier.
- `src/LivingWorld.RimWorld/LivingWorldCaravanArmoryPatch.cs` — fix the async pre-arm race.
- `src/LivingWorld.RimWorld/LivingWorldSettings.cs` — add `mobilizationBigRaidThreshold`, `mobilizationAtBaseRadius`, `mobilizationDeescalateRechecks`.
- `src/LivingWorld.Tests/Program.cs` — tests for the Core tasks + the property harness.

---

### Task 1: `ThreatClassifier` (Core, pure, unit-tested)

**Files:**
- Create: `src/LivingWorld.Core/ThreatClassifier.cs`
- Test: `src/LivingWorld.Tests/Program.cs`

**Interfaces:**
- Produces:
  - `enum LivingWorld.Core.ThreatTier { None, Nuisance, Raid, Serious }`
  - `readonly struct LivingWorld.Core.ThreatSignals` with init-only props: `bool AnyHostile`, `bool OnlyAnimals`, `bool AnyMechanoid`, `bool AnyEntity`, `bool AnyInsect`, `bool AnySapper`, `bool EnemyAtBase`, `int HostileCount`, `bool BigRaid`.
  - `static ThreatTier ThreatClassifier.Classify(in ThreatSignals s)`

- [ ] **Step 1: Write the failing tests**

Append tuples to the `tests` list in `src/LivingWorld.Tests/Program.cs` (after the last tuple; `using LivingWorld.Core;` is already at the top):

```csharp
    ("threat: no hostiles is None", TestThreatNone),
    ("threat: a small animal pack is Nuisance", TestThreatNuisanceAnimals),
    ("threat: a big animal pack escalates past Nuisance", TestThreatBigAnimalPackSerious),
    ("threat: a normal humanlike raid is Raid", TestThreatRaid),
    ("threat: mechanoids are Serious", TestThreatMechSerious),
    ("threat: Anomaly entities are Serious", TestThreatEntitySerious),
    ("threat: insects are Serious", TestThreatInsectSerious),
    ("threat: sappers are Serious", TestThreatSapperSerious),
    ("threat: an enemy at the base is Serious", TestThreatAtBaseSerious),
```

Append these methods at the end of `src/LivingWorld.Tests/Program.cs`:

```csharp
static void TestThreatNone()
{
    AssertEqual(ThreatTier.None, ThreatClassifier.Classify(new ThreatSignals { AnyHostile = false }));
}

static void TestThreatNuisanceAnimals()
{
    var s = new ThreatSignals { AnyHostile = true, OnlyAnimals = true, HostileCount = 3 };
    AssertEqual(ThreatTier.Nuisance, ThreatClassifier.Classify(s));
}

static void TestThreatBigAnimalPackSerious()
{
    var s = new ThreatSignals { AnyHostile = true, OnlyAnimals = true, HostileCount = 20, BigRaid = true };
    AssertEqual(ThreatTier.Serious, ThreatClassifier.Classify(s));
}

static void TestThreatRaid()
{
    var s = new ThreatSignals { AnyHostile = true, OnlyAnimals = false, HostileCount = 4 };
    AssertEqual(ThreatTier.Raid, ThreatClassifier.Classify(s));
}

static void TestThreatMechSerious()
{
    var s = new ThreatSignals { AnyHostile = true, AnyMechanoid = true, HostileCount = 3 };
    AssertEqual(ThreatTier.Serious, ThreatClassifier.Classify(s));
}

static void TestThreatEntitySerious()
{
    var s = new ThreatSignals { AnyHostile = true, AnyEntity = true, HostileCount = 2 };
    AssertEqual(ThreatTier.Serious, ThreatClassifier.Classify(s));
}

static void TestThreatInsectSerious()
{
    var s = new ThreatSignals { AnyHostile = true, AnyInsect = true, HostileCount = 5 };
    AssertEqual(ThreatTier.Serious, ThreatClassifier.Classify(s));
}

static void TestThreatSapperSerious()
{
    var s = new ThreatSignals { AnyHostile = true, AnySapper = true, HostileCount = 6 };
    AssertEqual(ThreatTier.Serious, ThreatClassifier.Classify(s));
}

static void TestThreatAtBaseSerious()
{
    var s = new ThreatSignals { AnyHostile = true, EnemyAtBase = true, HostileCount = 4 };
    AssertEqual(ThreatTier.Serious, ThreatClassifier.Classify(s));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet run --project src/LivingWorld.Tests -c Debug`
Expected: FAIL — compile error `The type or namespace name 'ThreatTier'/'ThreatSignals'/'ThreatClassifier' could not be found`.

- [ ] **Step 3: Write the implementation**

Create `src/LivingWorld.Core/ThreatClassifier.cs`:

```csharp
namespace LivingWorld.Core;

/// <summary>How serious the current attack is, deciding the proportionate response.</summary>
public enum ThreatTier
{
    None,
    Nuisance,
    Raid,
    Serious,
}

/// <summary>
/// Plain-data snapshot of the current hostiles on the map. Built from live RimWorld pawns by the map
/// component, but itself free of RimWorld types so tier classification is unit-testable without the game.
/// </summary>
public readonly struct ThreatSignals
{
    /// <summary>Any real hostile is present.</summary>
    public bool AnyHostile { get; init; }

    /// <summary>Every hostile is a wild animal (manhunter pack).</summary>
    public bool OnlyAnimals { get; init; }

    /// <summary>A mechanoid is present.</summary>
    public bool AnyMechanoid { get; init; }

    /// <summary>An Anomaly entity / mutant (shambler etc.) is present.</summary>
    public bool AnyEntity { get; init; }

    /// <summary>An insectoid is present.</summary>
    public bool AnyInsect { get; init; }

    /// <summary>A wall-breaching sapper is present.</summary>
    public bool AnySapper { get; init; }

    /// <summary>The nearest hostile is already close to the colony.</summary>
    public bool EnemyAtBase { get; init; }

    /// <summary>Number of live hostiles.</summary>
    public int HostileCount { get; init; }

    /// <summary>The raid is large (count/points over the configured threshold).</summary>
    public bool BigRaid { get; init; }
}

/// <summary>
/// Pure, deterministic threat-tier classification. Proportionate: a lone squirrel is a nuisance the fighters
/// shrug off, a big pack or a serious enemy type puts the colony on a war footing. No RimWorld types.
/// </summary>
public static class ThreatClassifier
{
    public static ThreatTier Classify(in ThreatSignals s)
    {
        if (!s.AnyHostile)
        {
            return ThreatTier.None;
        }

        // Serious enemy types, a wall breach, an enemy already at the base, or sheer size — hold the line.
        if (s.AnyEntity || s.AnyMechanoid || s.AnyInsect || s.AnySapper || s.EnemyAtBase || s.BigRaid)
        {
            return ThreatTier.Serious;
        }

        // A small wild-animal pack is a nuisance — fighters handle it, the colony keeps working.
        if (s.OnlyAnimals)
        {
            return ThreatTier.Nuisance;
        }

        // Otherwise a normal humanlike raid.
        return ThreatTier.Raid;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet run --project src/LivingWorld.Tests -c Debug`
Expected: PASS — `378 test(s) passed.` (369 + 9).

- [ ] **Step 5: Commit**

```bash
git add src/LivingWorld.Core/ThreatClassifier.cs src/LivingWorld.Tests/Program.cs
git commit -m "feat(arsenal): pure threat-tier classifier with unit tests"
```

---

### Task 2: `ThreatDebounce` (Core, pure, unit-tested)

**Files:**
- Create: `src/LivingWorld.Core/ThreatDebounce.cs`
- Test: `src/LivingWorld.Tests/Program.cs`

**Interfaces:**
- Consumes: `ThreatTier`.
- Produces: `static (ThreatTier tier, int belowCount) ThreatDebounce.Step(ThreatTier current, ThreatTier raw, int belowCount, int required)`

- [ ] **Step 1: Write the failing tests**

Append tuples:

```csharp
    ("debounce: escalation is immediate", TestDebounceEscalatesImmediately),
    ("debounce: de-escalation waits for required clear rechecks", TestDebounceDeescalatesAfterRequired),
    ("debounce: a single low recheck does not drop the tier", TestDebounceHoldsThroughFlicker),
    ("debounce: a fresh high recheck resets the clear counter", TestDebounceResetsCounterOnHigh),
```

Append methods:

```csharp
static void TestDebounceEscalatesImmediately()
{
    // current None, raw Serious -> jump to Serious now, counter reset.
    var (tier, below) = ThreatDebounce.Step(ThreatTier.None, ThreatTier.Serious, 0, 3);
    AssertEqual(ThreatTier.Serious, tier);
    AssertEqual(0, below);
}

static void TestDebounceDeescalatesAfterRequired()
{
    // current Serious, raw None, this is the 3rd consecutive low recheck (belowCount was 2) -> drop.
    var (tier, below) = ThreatDebounce.Step(ThreatTier.Serious, ThreatTier.None, 2, 3);
    AssertEqual(ThreatTier.None, tier);
    AssertEqual(0, below);
}

static void TestDebounceHoldsThroughFlicker()
{
    // current Serious, raw None, only the 1st low recheck -> hold Serious, count it.
    var (tier, below) = ThreatDebounce.Step(ThreatTier.Serious, ThreatTier.None, 0, 3);
    AssertEqual(ThreatTier.Serious, tier);
    AssertEqual(1, below);
}

static void TestDebounceResetsCounterOnHigh()
{
    // current Raid, raw Raid (still hostile) after some low flicker -> stay, reset counter to 0.
    var (tier, below) = ThreatDebounce.Step(ThreatTier.Raid, ThreatTier.Raid, 2, 3);
    AssertEqual(ThreatTier.Raid, tier);
    AssertEqual(0, below);
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet run --project src/LivingWorld.Tests -c Debug`
Expected: FAIL — `'ThreatDebounce' could not be found`.

- [ ] **Step 3: Write the implementation**

Create `src/LivingWorld.Core/ThreatDebounce.cs`:

```csharp
namespace LivingWorld.Core;

/// <summary>
/// Pure hysteresis for the threat tier so mobilization does not flicker at a boundary (pawns yo-yoing to the
/// stand and back). Escalation is immediate; de-escalation to a lower tier only happens after the raw reading
/// has stayed at-or-below the current tier for <c>required</c> consecutive rechecks. The caller holds the
/// running <c>belowCount</c> and feeds it back in each recheck.
/// </summary>
public static class ThreatDebounce
{
    public static (ThreatTier tier, int belowCount) Step(ThreatTier current, ThreatTier raw, int belowCount, int required)
    {
        // Raw threat is as-bad-or-worse than what we hold -> commit immediately, reset the clear counter.
        if (raw >= current)
        {
            return (raw, 0);
        }

        // Raw is lower than current -> only step down after enough consecutive lower rechecks.
        var next = belowCount + 1;
        if (next >= required)
        {
            return (raw, 0);
        }

        return (current, next);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet run --project src/LivingWorld.Tests -c Debug`
Expected: PASS — `382 test(s) passed.` (378 + 4).

- [ ] **Step 5: Commit**

```bash
git add src/LivingWorld.Core/ThreatDebounce.cs src/LivingWorld.Tests/Program.cs
git commit -m "feat(arsenal): pure threat de-escalation hysteresis with unit tests"
```

---

### Task 3: Candidate-abandonment fix in `MobilizationPlan` (Core, unit-tested)

**Files:**
- Modify: `src/LivingWorld.Core/MobilizationPlan.cs`
- Test: `src/LivingWorld.Tests/Program.cs`

**Interfaces:**
- `MobilizationPlan.NextAction(bool mobilized, in PawnMobState s)` signature unchanged; behavior changed for non-candidates that were already touched.

- [ ] **Step 1: Write the failing tests**

Append tuples:

```csharp
    ("mob plan: a non-candidate we drafted is still unwound", TestMobPlanNonCandidateDraftedUnwinds),
    ("mob plan: a non-candidate we engaged is still unwound", TestMobPlanNonCandidateEngagedUnwinds),
    ("mob plan: a non-candidate on combat policy is still unwound", TestMobPlanNonCandidatePolicyUnwinds),
    ("mob plan: a busy-urgent pawn is never touched even if engaged", TestMobPlanBusyUrgentNeverTouched),
```

Append methods:

```csharp
static void TestMobPlanNonCandidateDraftedUnwinds()
{
    // Was drafted-by-us, then stopped being a candidate mid-alert (downed / removed from roster).
    var s = new PawnMobState { IsCandidate = false, DraftedByUs = true, PolicyIsCivilian = false };
    AssertEqual(MobPhase.ClearCombat, MobilizationPlan.NextAction(mobilized: true, s));
}

static void TestMobPlanNonCandidateEngagedUnwinds()
{
    var s = new PawnMobState { IsCandidate = false, HasLwDuty = true, PolicyIsCivilian = false };
    AssertEqual(MobPhase.ClearCombat, MobilizationPlan.NextAction(mobilized: true, s));
}

static void TestMobPlanNonCandidatePolicyUnwinds()
{
    // No duty/draft left, but still on the combat apparel policy -> must be put back to civilian.
    var s = new PawnMobState { IsCandidate = false, PolicyIsCombat = true, PolicyIsCivilian = false };
    AssertEqual(MobPhase.SetCivilianPolicy, MobilizationPlan.NextAction(mobilized: true, s));
}

static void TestMobPlanBusyUrgentNeverTouched()
{
    // Busy-urgent short-circuits to None regardless of engagement (never yank off firefighting/tending/rescue).
    var s = new PawnMobState { IsCandidate = false, IsBusyUrgent = true, DraftedByUs = true };
    AssertEqual(MobPhase.None, MobilizationPlan.NextAction(mobilized: true, s));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet run --project src/LivingWorld.Tests -c Debug`
Expected: FAIL — `TestMobPlanNonCandidateDraftedUnwinds` etc. get `Expected 'ClearCombat', got 'None'` (current code returns None for any non-candidate).

- [ ] **Step 3: Write the implementation**

In `src/LivingWorld.Core/MobilizationPlan.cs`, replace the current `NextAction` body:

```csharp
    public static MobPhase NextAction(bool mobilized, in PawnMobState s)
    {
        if (!s.IsCandidate || s.IsBusyUrgent)
        {
            return MobPhase.None;
        }

        return mobilized ? Mobilize(in s) : StandDown(in s);
    }
```

with:

```csharp
    public static MobPhase NextAction(bool mobilized, in PawnMobState s)
    {
        // Never yank a pawn off a life-or-base-saving job, even to unwind.
        if (s.IsBusyUrgent)
        {
            return MobPhase.None;
        }

        if (!s.IsCandidate)
        {
            // Stopped being a candidate mid-alert (downed -> recovered, removed from the roster, mental break
            // ended). If we had already touched them (duty / draft / combat policy / kit), unwind through
            // stand-down so they are never left stranded drafted or armored. A never-touched non-candidate is
            // left alone.
            var touched = s.HasLwDuty || s.DraftedByUs || s.InCombatKit || s.PolicyIsCombat;
            return touched ? StandDown(in s) : MobPhase.None;
        }

        return mobilized ? Mobilize(in s) : StandDown(in s);
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet run --project src/LivingWorld.Tests -c Debug`
Expected: PASS — `386 test(s) passed.` (382 + 4).

- [ ] **Step 5: Commit**

```bash
git add src/LivingWorld.Core/MobilizationPlan.cs src/LivingWorld.Tests/Program.cs
git commit -m "fix(arsenal): unwind pawns that stop being a candidate mid-alert"
```

---

### Task 4: Property-based invariant harness over `PawnMobState` (Core test, headless)

**Files:**
- Modify: `src/LivingWorld.Tests/Program.cs`

**Interfaces:**
- Consumes: `MobilizationPlan.NextAction`, `PawnMobState`, `MobPhase`.

- [ ] **Step 1: Write the harness test**

Append one tuple:

```csharp
    ("mob plan: exhaustive invariants over the whole state space", TestMobPlanExhaustiveInvariants),
```

Append this method at the end of `src/LivingWorld.Tests/Program.cs`. It enumerates all 2^12 `PawnMobState` combinations × both `mobilized` values (8192 cases) and asserts the machine's invariants — mechanically covering the entire input space, not just hand-picked examples:

```csharp
static void TestMobPlanExhaustiveInvariants()
{
    var mobilizePhases = new System.Collections.Generic.HashSet<MobPhase>
    {
        MobPhase.Wake, MobPhase.SetCombatPolicy, MobPhase.Equip,
        MobPhase.Engage, MobPhase.Draft, MobPhase.SteadyCombat,
    };
    var standDownPhases = new System.Collections.Generic.HashSet<MobPhase>
    {
        MobPhase.ClearCombat, MobPhase.SetCivilianPolicy, MobPhase.ReturnKit, MobPhase.SteadyCivilian,
    };

    for (var bits = 0; bits < 4096; bits++)
    {
        var s = new PawnMobState
        {
            IsCandidate = (bits & 1) != 0,
            IsBusyUrgent = (bits & 2) != 0,
            Asleep = (bits & 4) != 0,
            PolicyIsCombat = (bits & 8) != 0,
            PolicyIsCivilian = (bits & 16) != 0,
            InCombatKit = (bits & 32) != 0,
            HasStand = (bits & 64) != 0,
            KitAvailable = (bits & 128) != 0,
            Drafted = (bits & 256) != 0,
            DraftedByUs = (bits & 512) != 0,
            HasLwDuty = (bits & 1024) != 0,
            CaiAvailable = (bits & 2048) != 0,
        };

        foreach (var mobilized in new[] { true, false })
        {
            var phase = MobilizationPlan.NextAction(mobilized, s);

            // Invariant 1: the result is always a defined enum value (never throws, never garbage).
            AssertEqual(true, System.Enum.IsDefined(typeof(MobPhase), phase));

            // Invariant 2: a busy-urgent pawn is never acted on.
            if (s.IsBusyUrgent)
            {
                AssertEqual(MobPhase.None, phase);
            }

            // Invariant 3: a never-touched non-candidate (and not busy) is left alone.
            if (!s.IsCandidate && !s.IsBusyUrgent
                && !s.HasLwDuty && !s.DraftedByUs && !s.InCombatKit && !s.PolicyIsCombat)
            {
                AssertEqual(MobPhase.None, phase);
            }

            // Invariant 4: a mobilized, non-busy candidate only ever gets a mobilize-side phase.
            if (mobilized && s.IsCandidate && !s.IsBusyUrgent)
            {
                AssertEqual(true, mobilizePhases.Contains(phase));
            }

            // Invariant 5: a stood-down, non-busy candidate only ever gets a stand-down-side phase.
            if (!mobilized && s.IsCandidate && !s.IsBusyUrgent)
            {
                AssertEqual(true, standDownPhases.Contains(phase));
            }
        }
    }
}
```

- [ ] **Step 2: Run the suite to verify the harness passes on the fixed machine**

Run: `dotnet run --project src/LivingWorld.Tests -c Debug`
Expected: PASS — `387 test(s) passed.` (386 + 1). (The harness relies on Task 3's fix; if run before it, Invariant 4 would fail on the abandoned-pawn cases.)

- [ ] **Step 3: Commit**

```bash
git add src/LivingWorld.Tests/Program.cs
git commit -m "test(arsenal): exhaustive property invariants over the phase-machine state space"
```

---

### Task 5: `CaiBridge` — aiAutoControl, DefendPoint, tier-aware engage/disengage (RimWorld glue)

**Files:**
- Modify: `src/LivingWorld.RimWorld/CaiBridge.cs` (replace whole file)

**Interfaces:**
- Consumes: `LivingWorld.Core.ThreatTier`.
- Produces:
  - `static bool CaiBridge.Available { get; }`
  - `static bool CaiBridge.TryEngage(Pawn pawn, ThreatTier tier, IntVec3 anchor)`
  - `static void CaiBridge.Disengage(Pawn pawn)`

- [ ] **Step 1: Replace the file**

Replace all of `src/LivingWorld.RimWorld/CaiBridge.cs` with:

```csharp
using System;
using System.Linq;
using System.Reflection;
using LivingWorld.Core;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Optional integration with CAI 5000 (packageId Krkr.rule56), reached purely by reflection — no assembly
/// reference to CombatAI.dll, so the mod builds and runs with or without CAI. On engage we (a) start a CAI
/// custom duty matched to the threat tier (Serious -> hold a line via DefendPoint; otherwise hunt enemies
/// down), and (b) enable CAI's reactive layer by setting the pawn's ThingComp_CombatAI.aiAutoControl = true —
/// without that flag CAI only follows the top-level objective and never ducks / retreats / evades (the field
/// defaults to false and nothing else sets it). On stand-down we clear the duty and the flag. Any reflection
/// failure returns false / no-ops, and the driver drafts instead.
/// </summary>
public static class CaiBridge
{
    // ~ half an in-game day. Long enough to cover a raid; the driver re-engages if the tier changes.
    private const int DutyExpireTicks = 30000;

    // Radius CAI holds around the defend anchor for a Serious threat.
    private const int DefendRadius = 10;

    private static readonly MethodInfo? HuntMethod;
    private static readonly MethodInfo? DefendMethod;
    private static readonly MethodInfo? StartMethod;
    private static readonly MethodInfo? GetTrackerMethod;
    private static readonly MethodInfo? FinishAllDutiesMethod;
    private static readonly PropertyInfo? CurDutyDefProp;
    private static readonly Type? CompType;
    private static readonly FieldInfo? AutoControlField;

    static CaiBridge()
    {
        try
        {
            var utility = GenTypes.GetTypeInAnyAssembly("CombatAI.CustomDutyUtility");
            if (utility != null)
            {
                // HuntDownEnemies(IntVec3 fallbackPosition, int expireAfter, int startAfter)
                HuntMethod = utility.GetMethod(
                    "HuntDownEnemies", BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(IntVec3), typeof(int), typeof(int) }, null);

                // DefendPoint(IntVec3 dest, int radius, bool endOnTookDamage, int expireAfter, int startAfter)
                DefendMethod = utility.GetMethod(
                    "DefendPoint", BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(IntVec3), typeof(int), typeof(bool), typeof(int), typeof(int) }, null);

                // TryStartCustomDuty(Pawn pawn, CustomPawnDuty duty, bool returnCurDutyToQueue)
                StartMethod = utility.GetMethod("TryStartCustomDuty", BindingFlags.Public | BindingFlags.Static);

                // GetPawnCustomDutyTracker(Pawn pawn)
                GetTrackerMethod = utility.GetMethod("GetPawnCustomDutyTracker", BindingFlags.Public | BindingFlags.Static);
            }

            var trackerType = GenTypes.GetTypeInAnyAssembly("CombatAI.Pawn_CustomDutyTracker");
            if (trackerType != null)
            {
                CurDutyDefProp = trackerType.GetProperty("CurDutyDef", BindingFlags.Public | BindingFlags.Instance);
                FinishAllDutiesMethod = trackerType.GetMethod("FinishAllDuties", BindingFlags.Public | BindingFlags.Instance);
            }

            CompType = GenTypes.GetTypeInAnyAssembly("CombatAI.Comps.ThingComp_CombatAI");
            if (CompType != null)
            {
                AutoControlField = CompType.GetField("aiAutoControl", BindingFlags.Public | BindingFlags.Instance);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] CAI bridge resolution failed safely (CAI disabled): {ex.Message}");
        }
    }

    public static bool Available => HuntMethod != null && StartMethod != null;

    public static bool TryEngage(Pawn pawn, ThreatTier tier, IntVec3 anchor)
    {
        if (!Available || pawn?.Spawned != true)
        {
            return false;
        }

        try
        {
            object? duty;
            if (tier == ThreatTier.Serious && DefendMethod != null)
            {
                // Hold the line at the anchor rather than chasing (don't run into sappers/mechs in the open).
                duty = DefendMethod.Invoke(null, new object[] { anchor, DefendRadius, false, DutyExpireTicks, 0 });
            }
            else
            {
                // Nuisance / Raid: fall back to the pawn's own position; CAI drives it toward sensed enemies.
                duty = HuntMethod!.Invoke(null, new object[] { pawn.Position, DutyExpireTicks, 0 });
            }

            if (duty == null || StartMethod!.Invoke(null, new object[] { pawn, duty, true }) is not true)
            {
                return false;
            }

            // Required: enable CAI's reactive tactical layer for this drafted pawn.
            SetAutoControl(pawn, true);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] CAI engage failed safely (will draft instead): {ex.Message}");
            return false;
        }
    }

    public static void Disengage(Pawn pawn)
    {
        if (pawn == null)
        {
            return;
        }

        try
        {
            SetAutoControl(pawn, false);

            if (GetTrackerMethod != null && FinishAllDutiesMethod != null && CurDutyDefProp != null)
            {
                var tracker = GetTrackerMethod.Invoke(null, new object[] { pawn });
                var curDef = tracker != null ? CurDutyDefProp.GetValue(tracker) : null;
                if (tracker != null && curDef != null)
                {
                    // FinishAllDuties(DutyDef def, Thing focus = null)
                    FinishAllDutiesMethod.Invoke(tracker, new[] { curDef, null });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] CAI disengage failed safely: {ex.Message}");
        }
    }

    private static void SetAutoControl(Pawn pawn, bool value)
    {
        if (CompType == null || AutoControlField == null || pawn?.AllComps == null)
        {
            return;
        }

        var comp = pawn.AllComps.FirstOrDefault(c => CompType.IsInstanceOfType(c));
        if (comp != null)
        {
            AutoControlField.SetValue(comp, value);
        }
    }
}
```

- [ ] **Step 2: Build to verify zero warnings**

Run: `dotnet build src/LivingWorld.RimWorld/LivingWorld.RimWorld.csproj -c Debug`
Expected: `Предупреждений: 0` / `Ошибок: 0`.

- [ ] **Step 3: Commit**

```bash
git add src/LivingWorld.RimWorld/CaiBridge.cs
git commit -m "feat(arsenal): CAI aiAutoControl, DefendPoint, tier-aware engage/disengage"
```

---

### Task 6: `MobilizationDriver` — thread tier, tier-aware engage, disengage on clear, persistable drafts (RimWorld glue)

**Files:**
- Modify: `src/LivingWorld.RimWorld/MobilizationDriver.cs` (replace whole file)

**Interfaces:**
- Consumes: `ThreatTier`, `CaiBridge.TryEngage(Pawn, ThreatTier, IntVec3)`, `CaiBridge.Disengage(Pawn)`.
- Produces:
  - `void Drive(Map map, bool mobilized, ThreatTier tier)`
  - `MobPhase PeekPhase(Pawn pawn, bool mobilized, ThreatTier tier)`
  - `bool IsEngagedByUs(Pawn pawn)`, `bool IsDraftedByUs(Pawn pawn)`
  - `List<int> ExportDraftedIds()`, `void ImportDraftedIds(IEnumerable<int> ids, Map map)`

- [ ] **Step 1: Replace the file**

Replace all of `src/LivingWorld.RimWorld/MobilizationDriver.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace LivingWorld.RimWorld;

/// <summary>
/// Executes the mobilization phase machine against the live colony each recheck: snapshot the colonists, build
/// a <see cref="PawnMobState"/> for each (tier-aware), ask <see cref="MobilizationPlan.NextAction"/> for the
/// one action, and perform exactly that. One action per pawn per tick keeps everything idempotent. Two
/// transient maps track who we engaged (with the tier we engaged them at, so a tier change re-issues the right
/// CAI duty) and who we drafted (persisted across save/load so stand-down still releases them). Fail-safe.
/// </summary>
public sealed class MobilizationDriver
{
    private readonly Dictionary<Pawn, ThreatTier> engagedByUs = new();
    private readonly HashSet<Pawn> draftedByUs = new();

    public void Drive(Map map, bool mobilized, ThreatTier tier)
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        if (!settings.armoryMobilizationEnabled || !ModsConfig.OdysseyActive)
        {
            return;
        }

        try
        {
            var colonists = map?.mapPawns?.FreeColonistsSpawned;
            if (colonists == null)
            {
                return;
            }

            PrunePawns();
            var anchor = map.Center;

            // Snapshot: equipping/dropping gear can mutate the live colonist list mid-loop.
            foreach (var pawn in colonists.ToList())
            {
                if (pawn == null)
                {
                    continue;
                }

                var state = Snapshot(pawn, tier);
                var action = MobilizationPlan.NextAction(mobilized, state);
                Execute(pawn, action, tier, anchor, settings.mobilizationDiagnostics);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Mobilization drive failed safely: {ex.Message}");
        }
    }

    // Diagnostics: the phase the machine would pick for this pawn right now at the given tier.
    public MobPhase PeekPhase(Pawn pawn, bool mobilized, ThreatTier tier)
        => MobilizationPlan.NextAction(mobilized, Snapshot(pawn, tier));

    public bool IsEngagedByUs(Pawn pawn) => engagedByUs.ContainsKey(pawn);

    public bool IsDraftedByUs(Pawn pawn) => draftedByUs.Contains(pawn);

    // Persist only the drafted set (thingIDNumbers) — CAI duties expire on their own, but a drafted pawn stays
    // drafted forever across a reload unless we remember we drafted it.
    public List<int> ExportDraftedIds()
        => draftedByUs.Where(p => p != null).Select(p => p.thingIDNumber).ToList();

    public void ImportDraftedIds(IEnumerable<int> ids, Map map)
    {
        draftedByUs.Clear();
        if (ids == null || map?.mapPawns == null)
        {
            return;
        }

        var wanted = new HashSet<int>(ids);
        foreach (var pawn in map.mapPawns.AllPawns)
        {
            if (pawn != null && wanted.Contains(pawn.thingIDNumber))
            {
                draftedByUs.Add(pawn);
            }
        }
    }

    private void PrunePawns()
    {
        foreach (var dead in engagedByUs.Keys.Where(p => p == null || !p.Spawned).ToList())
        {
            engagedByUs.Remove(dead);
        }

        draftedByUs.RemoveWhere(p => p == null || !p.Spawned);
    }

    private PawnMobState Snapshot(Pawn pawn, ThreatTier tier)
    {
        // A pawn keeps its "engaged" status only while the tier it was engaged at still matches — a tier change
        // makes HasLwDuty read false so the machine re-issues the duty that fits the new tier.
        var hasDuty = engagedByUs.TryGetValue(pawn, out var engagedTier) && engagedTier == tier;

        return new PawnMobState
        {
            IsCandidate = MobilizationCandidates.IsCandidate(pawn),
            IsBusyUrgent = MobilizationCandidates.IsBusyUrgent(pawn),
            Asleep = !RestUtility.Awake(pawn),
            PolicyIsCombat = MobilizationPolicyService.IsCombatPolicy(pawn),
            PolicyIsCivilian = MobilizationPolicyService.IsCivilianPolicy(pawn),
            InCombatKit = MobilizationCandidates.IsInCombatKit(pawn),
            HasStand = OutfitStandKit.HasStand(pawn),
            KitAvailable = OutfitStandKit.StandHasWeapon(pawn),
            Drafted = pawn.Drafted,
            DraftedByUs = draftedByUs.Contains(pawn),
            HasLwDuty = hasDuty,
            CaiAvailable = CaiBridge.Available,
        };
    }

    private void Execute(Pawn pawn, MobPhase action, ThreatTier tier, IntVec3 anchor, bool diagnostics)
    {
        switch (action)
        {
            case MobPhase.None:
            case MobPhase.SteadyCombat:
            case MobPhase.SteadyCivilian:
                return;

            case MobPhase.Wake:
                RestUtility.WakeUp(pawn, startNewJob: false);
                break;

            case MobPhase.SetCombatPolicy:
                MobilizationPolicyService.ApplyCombat(pawn);
                break;

            case MobPhase.Equip:
                OutfitStandKit.PushEquip(pawn);
                break;

            case MobPhase.Engage:
                if (CaiBridge.TryEngage(pawn, tier, anchor))
                {
                    engagedByUs[pawn] = tier;
                }
                else if (pawn.drafter != null)
                {
                    // CAI present but could not take this pawn — draft as fallback so it still fights. Track in
                    // both maps: engagedByUs (at this tier) stops the re-Engage loop, draftedByUs lets stand-down
                    // undraft it.
                    pawn.drafter.Drafted = true;
                    draftedByUs.Add(pawn);
                    engagedByUs[pawn] = tier;
                }
                break;

            case MobPhase.Draft:
                if (pawn.drafter != null)
                {
                    pawn.drafter.Drafted = true;
                    draftedByUs.Add(pawn);
                }
                break;

            case MobPhase.ClearCombat:
                if (engagedByUs.Remove(pawn))
                {
                    CaiBridge.Disengage(pawn);
                }

                if (draftedByUs.Remove(pawn) && pawn.drafter != null && pawn.Drafted)
                {
                    pawn.drafter.Drafted = false;
                }
                break;

            case MobPhase.SetCivilianPolicy:
                MobilizationPolicyService.ApplyCivilian(pawn);
                break;

            case MobPhase.ReturnKit:
                OutfitStandKit.PushReturn(pawn);
                break;
        }

        if (diagnostics)
        {
            Log.Message($"[LivingWorld] Mobilization: {pawn.LabelShort} -> {action} (tier {tier})");
        }
    }
}
```

- [ ] **Step 2: Build to verify zero warnings**

Run: `dotnet build src/LivingWorld.RimWorld/LivingWorld.RimWorld.csproj -c Debug`
Expected: initially FAILS with errors about `MobilizationCandidates.IsInCombatKit` (added in Task 9) and the callers of `PeekPhase`/`Drive` (updated in Task 7). That is expected mid-plan — Tasks 7 and 9 close them. If you are running tasks strictly in order, temporarily keep `InCombatKit = MobilizationCandidates.IsArmed(pawn)` here and switch it to `IsInCombatKit` in Task 9; otherwise implement Task 9 first. Prefer implementing **Task 9 before Task 6**, then Task 7 — the reviewer/executor should reorder so each task builds. Once Tasks 6, 7, 9 are all in: `Предупреждений: 0` / `Ошибок: 0`.

- [ ] **Step 3: Commit**

```bash
git add src/LivingWorld.RimWorld/MobilizationDriver.cs
git commit -m "feat(arsenal): tier-threaded driver with CAI disengage and persistable drafts"
```

---

### Task 7: `MobilizationMapComponent` — compute signals, tier, debounce, Scribe drafts, pass tier (RimWorld glue)

**Files:**
- Modify: `src/LivingWorld.RimWorld/MobilizationMapComponent.cs`
- Modify: `src/LivingWorld.RimWorld/OutfitStandDebugActions.cs`

**Interfaces:**
- Consumes: `ThreatClassifier`, `ThreatSignals`, `ThreatDebounce`, `ThreatTier`, the driver's new `Drive`/`PeekPhase`/`ExportDraftedIds`/`ImportDraftedIds`.
- Produces: `ThreatTier MobilizationMapComponent.CurrentTier { get; }` (for diagnostics).

- [ ] **Step 1: Add the signal computation, tier, debounce, and Scribe to the component**

In `src/LivingWorld.RimWorld/MobilizationMapComponent.cs`:

Add these usings at the top if not present: `using System.Collections.Generic;`, `using System.Linq;`, `using LivingWorld.Core;`.

Add fields alongside the existing `manualMobilized`/`threatPresent` fields:

```csharp
    private ThreatTier currentTier;
    private int belowCount;
    private List<int> savedDraftedIds = new();
```

Add a public accessor near the other properties:

```csharp
    public ThreatTier CurrentTier => currentTier;
```

Replace the body of `MapComponentTick` (the `try/catch` that sets `threatPresent` and the `driver.Drive(...)` call) with:

```csharp
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();

        try
        {
            var rawTier = settings.autoMobilizeOnThreat ? ThreatClassifier.Classify(ComputeSignals(map, settings)) : ThreatTier.None;
            (currentTier, belowCount) = ThreatDebounce.Step(currentTier, rawTier, belowCount, settings.mobilizationDeescalateRechecks);
            threatPresent = currentTier != ThreatTier.None;
        }
        catch (Exception ex)
        {
            currentTier = ThreatTier.None;
            threatPresent = false;
            Log.Warning($"[LivingWorld] Mobilization threat check failed safely: {ex.Message}");
        }

        driver.Drive(map, IsMobilized, currentTier);
```

Add these methods to the class (the `ComputeSignals` reads the live hostiles; `AnySapper` is left false in Phase A — sapper detection is a Phase D refinement):

```csharp
    private ThreatSignals ComputeSignals(Map liveMap, LivingWorldSettings settings)
    {
        var player = Faction.OfPlayer;
        var pawns = liveMap.mapPawns?.AllPawnsSpawned;
        if (player == null || pawns == null)
        {
            return default;
        }

        var hostiles = pawns.Where(p => p != null && !p.Downed && !p.IsPrisoner && p.HostileTo(player)).ToList();
        if (hostiles.Count == 0)
        {
            return default;
        }

        var center = liveMap.Center;
        var atBaseRadius = settings.mobilizationAtBaseRadius;

        return new ThreatSignals
        {
            AnyHostile = true,
            OnlyAnimals = hostiles.All(p => p.RaceProps?.Animal == true),
            AnyMechanoid = hostiles.Any(p => p.RaceProps?.IsMechanoid == true),
            AnyEntity = hostiles.Any(p => p.IsEntity || p.IsMutant),
            AnyInsect = hostiles.Any(p => p.RaceProps?.Insect == true),
            AnySapper = false,
            EnemyAtBase = hostiles.Any(p => (p.Position - center).LengthHorizontal <= atBaseRadius),
            HostileCount = hostiles.Count,
            BigRaid = hostiles.Count >= settings.mobilizationBigRaidThreshold,
        };
    }
```

Replace `ExposeData` with (persist the drafted set as thingIDNumbers so stand-down releases them after a reload):

```csharp
    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref manualMobilized, "livingWorld_manualMobilized", false);

        if (Scribe.mode == LoadSaveMode.Saving)
        {
            savedDraftedIds = driver.ExportDraftedIds();
        }

        Scribe_Collections.Look(ref savedDraftedIds, "livingWorld_draftedByUs", LookMode.Value);
        savedDraftedIds ??= new List<int>();
    }
```

Add a re-hydration hook (RimWorld calls `FinalizeInit` after load, when `map.mapPawns` is populated):

```csharp
    public override void FinalizeInit()
    {
        base.FinalizeInit();
        driver.ImportDraftedIds(savedDraftedIds, map);
    }
```

Update `DiagnosePawn` to pass the current tier into `PeekPhase`:

```csharp
    public string DiagnosePawn(Pawn pawn)
    {
        return $"phase={driver.PeekPhase(pawn, IsMobilized, currentTier)}, tier={currentTier}, "
               + $"engagedByUs={driver.IsEngagedByUs(pawn)}, draftedByUs={driver.IsDraftedByUs(pawn)}";
    }
```

- [ ] **Step 2: Update the dev dump header to show the derived tier**

In `src/LivingWorld.RimWorld/OutfitStandDebugActions.cs`, in `DumpMobilizationPhases`, change the summary `Log.Message` line so it includes the tier. Replace the existing summary line:

```csharp
        Log.Message($"[LivingWorld] Mobilization dump — mobilized={component.IsMobilized} "
                    + $"(manual={component.ManualMobilized}, threat={component.ThreatPresent}), CAI={CaiBridge.Available}");
```

with:

```csharp
        Log.Message($"[LivingWorld] Mobilization dump — mobilized={component.IsMobilized} "
                    + $"(manual={component.ManualMobilized}, threat={component.ThreatPresent}, tier={component.CurrentTier}), "
                    + $"CAI={CaiBridge.Available}");
```

- [ ] **Step 3: Build to verify zero warnings**

Run: `dotnet build src/LivingWorld.RimWorld/LivingWorld.RimWorld.csproj -c Debug`
Expected: `Предупреждений: 0` / `Ошибок: 0` (with Tasks 6 and 9 also applied).

- [ ] **Step 4: Commit**

```bash
git add src/LivingWorld.RimWorld/MobilizationMapComponent.cs src/LivingWorld.RimWorld/OutfitStandDebugActions.cs
git commit -m "feat(arsenal): threat signals, tier + hysteresis, persisted drafts, tier diagnostics"
```

---

### Task 8: Settings for the new thresholds (RimWorld glue)

**Files:**
- Modify: `src/LivingWorld.RimWorld/LivingWorldSettings.cs`

**Interfaces:**
- Produces: `LivingWorldSettings` fields `int mobilizationBigRaidThreshold` (default 12), `float mobilizationAtBaseRadius` (default 18f), `int mobilizationDeescalateRechecks` (default 2), each Scribed.

- [ ] **Step 1: Add the fields**

In `src/LivingWorld.RimWorld/LivingWorldSettings.cs`, add alongside the existing mobilization fields:

```csharp
    public int mobilizationBigRaidThreshold = 12;
    public float mobilizationAtBaseRadius = 18f;
    public int mobilizationDeescalateRechecks = 2;
```

In `ExposeData()`, add alongside the existing mobilization `Scribe_Values.Look` calls:

```csharp
        Scribe_Values.Look(ref mobilizationBigRaidThreshold, "mobilizationBigRaidThreshold", 12);
        Scribe_Values.Look(ref mobilizationAtBaseRadius, "mobilizationAtBaseRadius", 18f);
        Scribe_Values.Look(ref mobilizationDeescalateRechecks, "mobilizationDeescalateRechecks", 2);
```

- [ ] **Step 2: Build to verify zero warnings**

Run: `dotnet build src/LivingWorld.RimWorld/LivingWorld.RimWorld.csproj -c Debug`
Expected: `Предупреждений: 0` / `Ошибок: 0`.

- [ ] **Step 3: Commit**

```bash
git add src/LivingWorld.RimWorld/LivingWorldSettings.cs
git commit -m "feat(arsenal): settings for raid-size, at-base radius, de-escalation thresholds"
```

---

### Task 9: `IsInCombatKit` — armored-aware equipped check (RimWorld glue)

**Files:**
- Modify: `src/LivingWorld.RimWorld/MobilizationCandidates.cs`

**Interfaces:**
- Produces: `static bool MobilizationCandidates.IsInCombatKit(Pawn pawn)` — armed AND wearing armor.

**Why:** The `IsArmed`-only proxy treats a colonist who habitually carries a weapon (a Simple Sidearms sidearm, a hunter's rifle) as "already equipped," so they skip the stand and fight with no armor. `IsInCombatKit` additionally requires the pawn to be wearing an `ApparelArmor` piece — which the stand swap dons — so an un-armored weapon carrier is still sent to equip. Re-equip oscillation is prevented by `KitAvailable` (the stand's weapon is gone after the swap, so `Equip` is not re-selected) and `PushEquip`'s own `HeldWeapon == null` guard.

- [ ] **Step 1: Add the method**

In `src/LivingWorld.RimWorld/MobilizationCandidates.cs`, add (keep `IsArmed` as-is; it is still used elsewhere, e.g. the caravan patch):

```csharp
    // "In the combat kit" = holding a weapon AND wearing at least one armor piece — the state the outfit-stand
    // swap produces. Stricter than IsArmed so a colonist habitually carrying a weapon (e.g. Simple Sidearms, a
    // hunter's rifle) but wearing no armor still reads as "not equipped" and is sent to their stand to gear up.
    public static bool IsInCombatKit(Pawn pawn)
    {
        try
        {
            if (pawn?.equipment?.Primary == null)
            {
                return false;
            }

            var worn = pawn.apparel?.WornApparel;
            if (worn == null)
            {
                return false;
            }

            foreach (var apparel in worn)
            {
                var cats = apparel?.def?.thingCategories;
                if (cats != null && cats.Contains(ThingCategoryDefOf.ApparelArmor))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
```

Ensure `using RimWorld;` (for `ThingCategoryDefOf`) is present in the file — it already is.

- [ ] **Step 2: Build to verify zero warnings**

Run: `dotnet build src/LivingWorld.RimWorld/LivingWorld.RimWorld.csproj -c Debug`
Expected: `Предупреждений: 0` / `Ошибок: 0` (with Task 6 consuming `IsInCombatKit`).

- [ ] **Step 3: Commit**

```bash
git add src/LivingWorld.RimWorld/MobilizationCandidates.cs
git commit -m "feat(arsenal): armored-aware in-combat-kit check (fixes sidearm false positive)"
```

---

### Task 10: Fix the caravan pre-arm race (RimWorld glue)

**Files:**
- Modify: `src/LivingWorld.RimWorld/LivingWorldCaravanArmoryPatch.cs`

**Why:** `Prefix` runs `CaravanArmoryService.ArmDepartingPawns`, which calls `OutfitStandKit.PushEquip` — a multi-tick walk-and-swap job. But `ExitMapAndCreateCaravan` forms the caravan synchronously right after the prefix returns, so the pawn is pulled into the caravan long before it reaches its stand. The equip silently never happens. Phase A does the honest thing: only report/no-op cleanly rather than pretend, and log the limitation. (A real "hold caravan formation until equipped" is out of Phase A scope.)

- [ ] **Step 1: Make the caravan pre-arm honest**

In `src/LivingWorld.RimWorld/LivingWorldCaravanArmoryPatch.cs`, replace the `foreach` loop body inside `ArmDepartingPawns` so it only counts pawns that are ALREADY equipped (synchronous truth) and logs when it cannot arm someone in time, instead of pushing a job that will not complete:

Replace:

```csharp
            foreach (var pawn in pawns.Where(MobilizationCandidates.IsCandidate))
            {
                if (pawn.Map == null || MobilizationCandidates.IsArmed(pawn) || !OutfitStandKit.HasStand(pawn))
                {
                    continue;
                }

                // Send them to their outfit stand to gear up before the caravan forms.
                OutfitStandKit.PushEquip(pawn);
                armed++;
            }
```

with:

```csharp
            foreach (var pawn in pawns.Where(MobilizationCandidates.IsCandidate))
            {
                if (pawn.Map == null || !OutfitStandKit.HasStand(pawn))
                {
                    continue;
                }

                if (MobilizationCandidates.IsInCombatKit(pawn))
                {
                    // Already geared up (mobilized before departing) — counts as armed for the expedition.
                    armed++;
                    continue;
                }

                // The caravan forms synchronously after this prefix, so a walk-to-stand equip job would not
                // finish in time — pushing it would silently do nothing. Log so the player knows to mobilize
                // the expedition party first; do not issue a job that cannot complete.
                Log.Message($"[LivingWorld] Caravan armory: {pawn.LabelShort} left without gearing up "
                            + "(mobilize the party before forming the caravan to arm from stands).");
            }
```

- [ ] **Step 2: Build to verify zero warnings**

Run: `dotnet build src/LivingWorld.RimWorld/LivingWorld.RimWorld.csproj -c Debug`
Expected: `Предупреждений: 0` / `Ошибок: 0`.

- [ ] **Step 3: Run the full test suite (nothing regressed)**

Run: `dotnet run --project src/LivingWorld.Tests -c Debug`
Expected: `387 test(s) passed.`

- [ ] **Step 4: Commit**

```bash
git add src/LivingWorld.RimWorld/LivingWorldCaravanArmoryPatch.cs
git commit -m "fix(arsenal): stop the caravan pre-arm from silently no-oping (honest log instead)"
```

---

## Build-order note for the executor
Tasks 1–4 are pure Core/tests and build independently. Among the glue tasks, the net472 project only compiles cleanly once **Tasks 5, 6, 7, 8, 9** are all applied (they reference each other's new members: `CaiBridge` new signatures, `IsInCombatKit`, the new settings fields, `Drive(map,mobilized,tier)`). Implement them in the order **8 → 5 → 9 → 6 → 7 → 10** for a clean build after each of 8/5/9, and accept that 6 does not build standalone until 7 and 9 land (its Step 2 says so). Run the full `dotnet build` after Task 7 and again after Task 10.

---

## Self-Review

**1. Spec coverage** (against `2026-07-14-arsenal-expansion-design.md`, Phase A):
- `ThreatClassifier` (pure, tier, animal-size-aware) → Task 1. ✅
- Wire `DefendPoint` + `aiAutoControl` + tier→duty → Task 5 (CaiBridge) + Task 6 (driver threads tier, tier-aware engage). ✅
- Property-based `PawnMobState` harness → Task 4. ✅
- Candidate-abandonment fix → Task 3. ✅
- Transient-set (drafted) persistence → Task 6 (export/import) + Task 7 (Scribe). ✅
- `IsArmed`/stand-weapon proxy → Task 9 (`IsInCombatKit` armored-aware). ✅
- Caravan race → Task 10. ✅
- Threat hysteresis → Task 2 (`ThreatDebounce`) + Task 7 (wired). ✅
- Diagnostics (tier in dump + per-pawn) → Task 6 (log line) + Task 7 (dump + `DiagnosePawn`). ✅
- Driver-loop sequencing harness: deferred (needs an injectable seam) — noted for Phase B, not a Phase A spec item. Acceptable.

**2. Placeholder scan:** No TBD/TODO; every code step contains complete code; every command has expected output. The build-order caveat in Task 6 is explicit, not a placeholder. ✅

**3. Type consistency:** `ThreatTier`/`ThreatSignals` field names match across Tasks 1, 5, 6, 7. `CaiBridge.TryEngage(Pawn, ThreatTier, IntVec3)` / `Disengage(Pawn)` match between Task 5 and Task 6. `MobilizationDriver.Drive(Map, bool, ThreatTier)` / `PeekPhase(Pawn, bool, ThreatTier)` / `ExportDraftedIds` / `ImportDraftedIds` match between Task 6 and Task 7. `MobilizationCandidates.IsInCombatKit` defined in Task 9, consumed in Tasks 6 and 10. Settings fields (`mobilizationBigRaidThreshold`/`mobilizationAtBaseRadius`/`mobilizationDeescalateRechecks`) defined in Task 8, consumed in Task 7. `CurrentTier` defined in Task 7, consumed in the Task 7 dump. Test counts chain: 369 → 378 → 382 → 386 → 387. ✅
