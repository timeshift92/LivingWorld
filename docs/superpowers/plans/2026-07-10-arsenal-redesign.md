# Arsenal v2 — Colony Mobilization Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the Living World "Arsenal" so combat colonists reliably equip from their personal Odyssey outfit stand and fight autonomously (via CAI 5000) on a threat/toggle, then stand down to civvies — with a decoupled, unit-tested phase machine and an apparel-policy safety net that guarantees no accidental armor in peacetime.

**Architecture:** A pure, unit-tested phase machine in `LivingWorld.Core` (`MobilizationPlan.NextAction`) decides, per pawn per tick, the single next action from a plain-data snapshot of that pawn's state. Thin RimWorld glue builds the snapshot from a live `Pawn`, executes the one returned action (wake / policy swap / stand swap / CAI duty / draft), and logs every transition. Correctness of "armor state" is guaranteed by two auto-created apparel policies (combat allows armor, civilian forbids it) independent of the fragile stand swap.

**Tech Stack:** C# — `LivingWorld.Core` (netstandard2.0, net8.0 test host) + `LivingWorld.RimWorld` (net472, Harmony, RimWorld 1.6 + Odyssey). Optional soft dependency on CAI 5000 (`CombatAI.dll`) via reflection. Custom test harness in `src/LivingWorld.Tests/Program.cs` (tuple list + `AssertEqual<T>`), run with `dotnet run`.

## Global Constraints

- **Language of user-facing strings:** English keys in `mod/Languages/English/Keyed/LivingWorld.xml`, Russian in `mod/Languages/Russian/Keyed/LivingWorld.xml`. Every new `.Translate()` key MUST exist in both.
- **Zero build warnings.** `dotnet build src/LivingWorld.RimWorld/LivingWorld.RimWorld.csproj -c Debug` must end with `Предупреждений: 0` / `Ошибок: 0`.
- **All RimWorld-facing code is fail-safe:** every public entry point wraps its body in `try/catch` and logs `Log.Warning($"[LivingWorld] <what> failed safely: {ex.Message}")`; a failure never throws into the game loop.
- **Odyssey is a hard gate:** the whole module is inert when `!ModsConfig.OdysseyActive`.
- **CAI is a soft dependency:** never add an assembly reference to `CombatAI.dll`. All CAI access is reflection; absence degrades to drafting.
- **Only `LivingWorld.Core` is unit-testable** (the test project references Core only — no RimWorld assemblies). RimWorld glue is verified by a clean build + code review; live pawn behavior is verified by the diagnostic log, not automated tests.
- **Namespaces:** pure logic in `LivingWorld.Core`; RimWorld glue in `LivingWorld.RimWorld`. New `.cs` files are auto-included by the SDK-style projects (no csproj edits needed).
- **Never iterate a live pawn list while mutating it:** snapshot with `.ToList()` first.

---

## File Structure

**Create:**
- `src/LivingWorld.Core/MobilizationPlan.cs` — `MobPhase` enum, `PawnMobState` struct, pure `MobilizationPlan.NextAction`. The unit-tested decision core.
- `src/LivingWorld.RimWorld/MobilizationCandidates.cs` — "can this pawn fight / is it armed / is it on an urgent job" (RimWorld glue over Core `LoadoutSelectionService`).
- `src/LivingWorld.RimWorld/MobilizationPolicyService.cs` — two auto-created apparel policies (combat/civilian).
- `src/LivingWorld.RimWorld/OutfitStandKit.cs` — stand lookup + directional `UseOutfitStand` swap.
- `src/LivingWorld.RimWorld/CaiBridge.cs` — reflection soft-dep hook into CAI custom duties.
- `src/LivingWorld.RimWorld/MobilizationDriver.cs` — builds `PawnMobState`, calls `NextAction`, executes the one action, logs transitions.
- `src/LivingWorld.RimWorld/MobilizationMapComponent.cs` — per-map state (manual toggle + threat), Scribe, throttled tick calling the driver.
- `src/LivingWorld.RimWorld/LivingWorldMobilizationGizmoPatch.cs` — the "Mobilize" toggle gizmo.
- `src/LivingWorld.RimWorld/OutfitStandDebugActions.cs` — dev "Spawn combat test gear" + "Dump mobilization phases".

**Modify:**
- `src/LivingWorld.RimWorld/LivingWorldSettings.cs` — add `mobilizationDiagnostics`, `autoMobilizeOnThreat`; keep `armoryMobilizationEnabled`, `mobilizationSkillThreshold`.
- `src/LivingWorld.Tests/Program.cs` — register + implement Task 1's unit tests.
- `mod/Languages/English/Keyed/LivingWorld.xml` and `mod/Languages/Russian/Keyed/LivingWorld.xml` — add the two gizmo keys.

**Reuse note:** `LivingWorld.Core/ArmoryLoadout.cs` (`MobilizationTuning`, `LoadoutSelectionService`) already exists on the current branch tree and is kept as-is; several tasks paste proven code from prior arsenal branches (`claude-policy-armor`, `a72c27b`) verbatim so the implementer has complete code even reading tasks out of order.

---

### Task 1: Core phase machine (`MobilizationPlan`) — pure, unit-tested

**Files:**
- Create: `src/LivingWorld.Core/MobilizationPlan.cs`
- Test: `src/LivingWorld.Tests/Program.cs` (append tuples + methods)

**Interfaces:**
- Produces:
  - `enum LivingWorld.Core.MobPhase { None, Wake, SetCombatPolicy, Equip, Engage, Draft, SteadyCombat, ClearCombat, SetCivilianPolicy, ReturnKit, SteadyCivilian }`
  - `readonly struct LivingWorld.Core.PawnMobState` with `bool` init-only props: `IsCandidate, IsBusyUrgent, Asleep, PolicyIsCombat, PolicyIsCivilian, InCombatKit, HasStand, Drafted, DraftedByUs, HasLwDuty, CaiAvailable`.
  - `static MobPhase MobilizationPlan.NextAction(bool mobilized, in PawnMobState s)`.
- Consumes: nothing.

- [ ] **Step 1: Write the failing tests**

Append these registrations inside the `tests` list initializer in `src/LivingWorld.Tests/Program.cs` (add after the last existing tuple, keeping the trailing comma style):

```csharp
    ("mob plan: non-candidate is left alone when mobilized", TestMobPlanNonCandidateMobilized),
    ("mob plan: non-candidate is left alone when stood down", TestMobPlanNonCandidateStandDown),
    ("mob plan: a candidate on an urgent job is not yanked", TestMobPlanBusyUrgent),
    ("mob plan: a sleeping candidate is woken first", TestMobPlanWake),
    ("mob plan: an awake candidate is switched to the combat policy", TestMobPlanSetCombatPolicy),
    ("mob plan: a combat-policy candidate with a stand equips the kit", TestMobPlanEquip),
    ("mob plan: an equipped candidate engages via CAI when available", TestMobPlanEngage),
    ("mob plan: an already-engaged candidate holds steady", TestMobPlanSteadyCombatDuty),
    ("mob plan: without CAI an equipped candidate is drafted", TestMobPlanDraftFallback),
    ("mob plan: a drafted candidate without CAI holds steady", TestMobPlanSteadyCombatDrafted),
    ("mob plan: a standless candidate still engages so it does not idle", TestMobPlanStandlessEngages),
    ("mob plan: stand-down clears our CAI duty first", TestMobPlanStandDownClearsDuty),
    ("mob plan: stand-down undrafts a pawn we drafted", TestMobPlanStandDownClearsDraft),
    ("mob plan: stand-down switches to the civilian policy", TestMobPlanSetCivilianPolicy),
    ("mob plan: stand-down returns the kit to the stand", TestMobPlanReturnKit),
    ("mob plan: a stood-down civilian holds steady", TestMobPlanSteadyCivilian),
```

Append these test methods at the end of `src/LivingWorld.Tests/Program.cs` (they are top-level static methods like the others):

```csharp
static void TestMobPlanNonCandidateMobilized()
{
    var s = new PawnMobState { IsCandidate = false };
    AssertEqual(MobPhase.None, MobilizationPlan.NextAction(mobilized: true, s));
}

static void TestMobPlanNonCandidateStandDown()
{
    var s = new PawnMobState { IsCandidate = false };
    AssertEqual(MobPhase.None, MobilizationPlan.NextAction(mobilized: false, s));
}

static void TestMobPlanBusyUrgent()
{
    var s = new PawnMobState { IsCandidate = true, IsBusyUrgent = true, Asleep = true };
    AssertEqual(MobPhase.None, MobilizationPlan.NextAction(mobilized: true, s));
}

static void TestMobPlanWake()
{
    var s = new PawnMobState { IsCandidate = true, Asleep = true };
    AssertEqual(MobPhase.Wake, MobilizationPlan.NextAction(mobilized: true, s));
}

static void TestMobPlanSetCombatPolicy()
{
    var s = new PawnMobState { IsCandidate = true, Asleep = false, PolicyIsCombat = false };
    AssertEqual(MobPhase.SetCombatPolicy, MobilizationPlan.NextAction(mobilized: true, s));
}

static void TestMobPlanEquip()
{
    var s = new PawnMobState
    {
        IsCandidate = true, PolicyIsCombat = true, HasStand = true, InCombatKit = false,
    };
    AssertEqual(MobPhase.Equip, MobilizationPlan.NextAction(mobilized: true, s));
}

static void TestMobPlanEngage()
{
    var s = new PawnMobState
    {
        IsCandidate = true, PolicyIsCombat = true, HasStand = true, InCombatKit = true,
        CaiAvailable = true, HasLwDuty = false,
    };
    AssertEqual(MobPhase.Engage, MobilizationPlan.NextAction(mobilized: true, s));
}

static void TestMobPlanSteadyCombatDuty()
{
    var s = new PawnMobState
    {
        IsCandidate = true, PolicyIsCombat = true, HasStand = true, InCombatKit = true,
        CaiAvailable = true, HasLwDuty = true,
    };
    AssertEqual(MobPhase.SteadyCombat, MobilizationPlan.NextAction(mobilized: true, s));
}

static void TestMobPlanDraftFallback()
{
    var s = new PawnMobState
    {
        IsCandidate = true, PolicyIsCombat = true, HasStand = true, InCombatKit = true,
        CaiAvailable = false, Drafted = false,
    };
    AssertEqual(MobPhase.Draft, MobilizationPlan.NextAction(mobilized: true, s));
}

static void TestMobPlanSteadyCombatDrafted()
{
    var s = new PawnMobState
    {
        IsCandidate = true, PolicyIsCombat = true, HasStand = true, InCombatKit = true,
        CaiAvailable = false, Drafted = true,
    };
    AssertEqual(MobPhase.SteadyCombat, MobilizationPlan.NextAction(mobilized: true, s));
}

static void TestMobPlanStandlessEngages()
{
    var s = new PawnMobState
    {
        IsCandidate = true, PolicyIsCombat = true, HasStand = false, InCombatKit = false,
        CaiAvailable = true, HasLwDuty = false,
    };
    AssertEqual(MobPhase.Engage, MobilizationPlan.NextAction(mobilized: true, s));
}

static void TestMobPlanStandDownClearsDuty()
{
    var s = new PawnMobState { IsCandidate = true, HasLwDuty = true, PolicyIsCivilian = false };
    AssertEqual(MobPhase.ClearCombat, MobilizationPlan.NextAction(mobilized: false, s));
}

static void TestMobPlanStandDownClearsDraft()
{
    var s = new PawnMobState { IsCandidate = true, DraftedByUs = true, PolicyIsCivilian = false };
    AssertEqual(MobPhase.ClearCombat, MobilizationPlan.NextAction(mobilized: false, s));
}

static void TestMobPlanSetCivilianPolicy()
{
    var s = new PawnMobState
    {
        IsCandidate = true, HasLwDuty = false, DraftedByUs = false, PolicyIsCivilian = false,
    };
    AssertEqual(MobPhase.SetCivilianPolicy, MobilizationPlan.NextAction(mobilized: false, s));
}

static void TestMobPlanReturnKit()
{
    var s = new PawnMobState
    {
        IsCandidate = true, PolicyIsCivilian = true, HasStand = true, InCombatKit = true,
    };
    AssertEqual(MobPhase.ReturnKit, MobilizationPlan.NextAction(mobilized: false, s));
}

static void TestMobPlanSteadyCivilian()
{
    var s = new PawnMobState
    {
        IsCandidate = true, PolicyIsCivilian = true, HasStand = true, InCombatKit = false,
    };
    AssertEqual(MobPhase.SteadyCivilian, MobilizationPlan.NextAction(mobilized: false, s));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet run --project src/LivingWorld.Tests -c Debug`
Expected: FAIL — compile error `The type or namespace name 'MobPhase'/'PawnMobState'/'MobilizationPlan' could not be found`.

- [ ] **Step 3: Write the implementation**

Create `src/LivingWorld.Core/MobilizationPlan.cs`:

```csharp
namespace LivingWorld.Core;

/// <summary>
/// The single next action mobilization should take for one colonist this tick. The driver derives exactly one
/// of these from the pawn's live state and executes it; deriving is pure so the sequencing (the part that
/// historically broke) is unit-tested in isolation.
/// </summary>
public enum MobPhase
{
    None,
    Wake,
    SetCombatPolicy,
    Equip,
    Engage,
    Draft,
    SteadyCombat,
    ClearCombat,
    SetCivilianPolicy,
    ReturnKit,
    SteadyCivilian,
}

/// <summary>
/// A plain-data snapshot of one colonist's mobilization-relevant state. Built from a live RimWorld pawn by the
/// driver, but itself free of RimWorld types so the decision logic can be tested without the game.
/// </summary>
public readonly struct PawnMobState
{
    /// <summary>Combat-capable colonist we are allowed to act on (violence-able, draftable, skilled enough).</summary>
    public bool IsCandidate { get; init; }

    /// <summary>On a life-or-base-saving job (firefighting, tending, rescuing) — leave them on it.</summary>
    public bool IsBusyUrgent { get; init; }

    /// <summary>Currently asleep.</summary>
    public bool Asleep { get; init; }

    /// <summary>Current apparel policy is our combat policy (armor allowed).</summary>
    public bool PolicyIsCombat { get; init; }

    /// <summary>Current apparel policy is our civilian policy (armor forbidden).</summary>
    public bool PolicyIsCivilian { get; init; }

    /// <summary>Wearing/holding the combat kit (proxy: carrying a primary weapon).</summary>
    public bool InCombatKit { get; init; }

    /// <summary>Owns a personal outfit stand to swap the kit from.</summary>
    public bool HasStand { get; init; }

    /// <summary>Actually drafted right now (by anyone).</summary>
    public bool Drafted { get; init; }

    /// <summary>Drafted specifically by this system (so stand-down may undraft it).</summary>
    public bool DraftedByUs { get; init; }

    /// <summary>We have given this pawn a CAI combat duty this alert.</summary>
    public bool HasLwDuty { get; init; }

    /// <summary>CAI 5000 is loaded and its custom-duty API resolved.</summary>
    public bool CaiAvailable { get; init; }
}

/// <summary>
/// Pure decision core for colony mobilization. <see cref="NextAction"/> returns the one action to take for a
/// colonist this tick given whether the colony is mobilized and the colonist's state. One action per call keeps
/// the driver idempotent: it re-derives from live state every recheck and converges without stored step
/// counters.
/// </summary>
public static class MobilizationPlan
{
    public static MobPhase NextAction(bool mobilized, in PawnMobState s)
    {
        if (!s.IsCandidate || s.IsBusyUrgent)
        {
            return MobPhase.None;
        }

        return mobilized ? Mobilize(in s) : StandDown(in s);
    }

    private static MobPhase Mobilize(in PawnMobState s)
    {
        if (s.Asleep)
        {
            return MobPhase.Wake;
        }

        if (!s.PolicyIsCombat)
        {
            return MobPhase.SetCombatPolicy;
        }

        // Kit lives only on the stand, so only a stand owner can arm. Equip until armed.
        if (s.HasStand && !s.InCombatKit)
        {
            return MobPhase.Equip;
        }

        // Ready to fight once armed, or (no stand to arm from) immediately so they don't idle through a raid.
        var ready = s.InCombatKit || !s.HasStand;
        if (ready)
        {
            if (s.CaiAvailable && !s.HasLwDuty)
            {
                return MobPhase.Engage;
            }

            if (!s.CaiAvailable && !s.Drafted)
            {
                return MobPhase.Draft;
            }
        }

        return MobPhase.SteadyCombat;
    }

    private static MobPhase StandDown(in PawnMobState s)
    {
        // Release combat control first (our CAI duty / our draft), then re-dress as a civilian.
        if (s.HasLwDuty || s.DraftedByUs)
        {
            return MobPhase.ClearCombat;
        }

        if (!s.PolicyIsCivilian)
        {
            return MobPhase.SetCivilianPolicy;
        }

        if (s.HasStand && s.InCombatKit)
        {
            return MobPhase.ReturnKit;
        }

        return MobPhase.SteadyCivilian;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet run --project src/LivingWorld.Tests -c Debug`
Expected: PASS — final line `335 test(s) passed.` (319 existing + 16 new).

- [ ] **Step 5: Commit**

```bash
git add src/LivingWorld.Core/MobilizationPlan.cs src/LivingWorld.Tests/Program.cs
git commit -m "feat(arsenal): pure mobilization phase machine with unit tests"
```

---

### Task 2: Candidate eligibility (`MobilizationCandidates`)

**Files:**
- Create: `src/LivingWorld.RimWorld/MobilizationCandidates.cs`

**Interfaces:**
- Consumes: `LivingWorld.Core.LoadoutSelectionService.IsCombatEligible(int, int, int)`, `LivingWorld.Core.MobilizationTuning.CombatSkillThreshold`, `LivingWorldSettings.Instance.mobilizationSkillThreshold`.
- Produces:
  - `static bool MobilizationCandidates.IsCandidate(Pawn pawn)`
  - `static bool MobilizationCandidates.IsArmed(Pawn pawn)`
  - `static bool MobilizationCandidates.IsBusyUrgent(Pawn pawn)`

- [ ] **Step 1: Write the implementation**

Create `src/LivingWorld.RimWorld/MobilizationCandidates.cs`:

```csharp
using LivingWorld.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace LivingWorld.RimWorld;

/// <summary>
/// Decides which colonists mobilization acts on and reads the two live facts the driver needs about them.
/// Kit storage and equipping live on the outfit stand (<see cref="OutfitStandKit"/>); this only answers
/// "can this colonist fight", "is it armed", and "is it on an urgent job we must not interrupt". Fail-safe —
/// a bad state reads as not-a-candidate / not-busy.
/// </summary>
public static class MobilizationCandidates
{
    public static bool IsCandidate(Pawn pawn)
    {
        try
        {
            if (pawn == null || !pawn.IsColonist || pawn.Dead || pawn.Downed || pawn.InMentalState)
            {
                return false;
            }

            // Never mobilize a colonist who cannot fight: pacifists / violence-incapable pawns, and pawns that
            // cannot be drafted at all (children, etc.).
            if (pawn.WorkTagIsDisabled(WorkTags.Violent) || pawn.drafter == null)
            {
                return false;
            }

            var shooting = SkillLevel(pawn, SkillDefOf.Shooting);
            var melee = SkillLevel(pawn, SkillDefOf.Melee);
            var threshold = LivingWorldSettings.Instance?.mobilizationSkillThreshold
                            ?? MobilizationTuning.CombatSkillThreshold;
            return LoadoutSelectionService.IsCombatEligible(shooting, melee, threshold);
        }
        catch
        {
            return false;
        }
    }

    // True once the pawn is carrying a weapon (its combat kit is on).
    public static bool IsArmed(Pawn pawn)
    {
        return pawn?.equipment?.Primary != null;
    }

    // On a life-or-base-saving job we must not yank them off: firefighting, tending a patient, rescuing downed.
    public static bool IsBusyUrgent(Pawn pawn)
    {
        try
        {
            var job = pawn?.CurJobDef;
            return job == JobDefOf.BeatFire || job == JobDefOf.TendPatient || job == JobDefOf.Rescue;
        }
        catch
        {
            return false;
        }
    }

    private static int SkillLevel(Pawn pawn, SkillDef skill)
    {
        return pawn?.skills?.GetSkill(skill)?.Level ?? 0;
    }
}
```

- [ ] **Step 2: Build to verify it compiles with zero warnings**

Run: `dotnet build src/LivingWorld.RimWorld/LivingWorld.RimWorld.csproj -c Debug`
Expected: `Предупреждений: 0` / `Ошибок: 0`.

- [ ] **Step 3: Commit**

```bash
git add src/LivingWorld.RimWorld/MobilizationCandidates.cs
git commit -m "feat(arsenal): mobilization candidate eligibility and urgent-job guard"
```

---

### Task 3: Apparel-policy safety net (`MobilizationPolicyService`)

**Files:**
- Create: `src/LivingWorld.RimWorld/MobilizationPolicyService.cs`

**Interfaces:**
- Produces:
  - `static void MobilizationPolicyService.ApplyCombat(Pawn pawn)` (allows `ApparelArmor`)
  - `static void MobilizationPolicyService.ApplyCivilian(Pawn pawn)` (forbids `ApparelArmor`)
  - `static bool MobilizationPolicyService.IsCombatPolicy(Pawn pawn)`
  - `static bool MobilizationPolicyService.IsCivilianPolicy(Pawn pawn)`

- [ ] **Step 1: Write the implementation**

Create `src/LivingWorld.RimWorld/MobilizationPolicyService.cs`:

```csharp
using System;
using System.Linq;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// The apparel-policy safety net. Two auto-created policies decide, independently of the outfit-stand swap,
/// whether a colonist may wear combat armor: the combat policy permits it (so a just-equipped kit is not
/// stripped), the civilian policy forbids it (so a colonist can never end up in armor in peacetime — even if
/// the stand swap glitched, vanilla apparel optimization strips it). This is the guarantee; the stand swap is
/// only the convenience. Fail-safe.
/// </summary>
public static class MobilizationPolicyService
{
    private const string CombatLabel = "LivingWorld_Combat";
    private const string CivilianLabel = "LivingWorld_Civilian";

    public static void ApplyCombat(Pawn pawn) => SetPolicy(pawn, CombatLabel, allowArmor: true);

    public static void ApplyCivilian(Pawn pawn) => SetPolicy(pawn, CivilianLabel, allowArmor: false);

    public static bool IsCombatPolicy(Pawn pawn) => pawn?.outfits?.CurrentApparelPolicy?.label == CombatLabel;

    public static bool IsCivilianPolicy(Pawn pawn) => pawn?.outfits?.CurrentApparelPolicy?.label == CivilianLabel;

    private static void SetPolicy(Pawn pawn, string label, bool allowArmor)
    {
        try
        {
            var tracker = pawn?.outfits;
            if (tracker == null || tracker.CurrentApparelPolicy?.label == label)
            {
                return;
            }

            var policy = EnsurePolicy(label, allowArmor);
            if (policy != null)
            {
                tracker.CurrentApparelPolicy = policy;
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Armory apparel-policy switch failed safely: {ex.Message}");
        }
    }

    private static ApparelPolicy? EnsurePolicy(string label, bool allowArmor)
    {
        var db = Current.Game?.outfitDatabase;
        if (db?.AllOutfits == null)
        {
            return null;
        }

        var existing = db.AllOutfits.FirstOrDefault(policy => policy != null && policy.label == label);
        if (existing != null)
        {
            existing.filter.SetAllow(ThingCategoryDefOf.ApparelArmor, allowArmor, null, null);
            return existing;
        }

        var created = db.MakeNewOutfit();
        created.label = label;
        created.filter.SetAllow(ThingCategoryDefOf.Apparel, true, null, null);
        created.filter.SetAllow(ThingCategoryDefOf.ApparelArmor, allowArmor, null, null);
        return created;
    }
}
```

- [ ] **Step 2: Build to verify zero warnings**

Run: `dotnet build src/LivingWorld.RimWorld/LivingWorld.RimWorld.csproj -c Debug`
Expected: `Предупреждений: 0` / `Ошибок: 0`.

- [ ] **Step 3: Commit**

```bash
git add src/LivingWorld.RimWorld/MobilizationPolicyService.cs
git commit -m "feat(arsenal): apparel-policy safety net (combat allows armor, civilian forbids)"
```

---

### Task 4: Outfit-stand kit driver (`OutfitStandKit`)

**Files:**
- Create: `src/LivingWorld.RimWorld/OutfitStandKit.cs`

**Interfaces:**
- Produces:
  - `static Building_OutfitStand? OutfitStandKit.StandOf(Pawn pawn)`
  - `static bool OutfitStandKit.HasStand(Pawn pawn)`
  - `static void OutfitStandKit.PushEquip(Pawn pawn)` (swap toward combat kit)
  - `static void OutfitStandKit.PushReturn(Pawn pawn)` (swap back to civvies)

- [ ] **Step 1: Write the implementation**

Create `src/LivingWorld.RimWorld/OutfitStandKit.cs`:

```csharp
using System;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace LivingWorld.RimWorld;

/// <summary>
/// Bridges mobilization to the Odyssey Outfit Stand: each combat colonist has a personal stand holding their
/// kit (apparel + weapon), and mobilization makes them walk to it and swap into (or out of) that kit through
/// the game's own jobs. Living World only decides <em>when</em>. The swap is directional — combat on equip,
/// civvies on return — derived from whether the pawn is already armed, never a blind toggle. Fail-safe: a
/// missing/empty/unassigned stand or a missing job def just skips.
/// </summary>
public static class OutfitStandKit
{
    public static Building_OutfitStand? StandOf(Pawn pawn)
    {
        try
        {
            if (pawn?.Map == null)
            {
                return null;
            }

            foreach (var stand in pawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_OutfitStand>())
            {
                var owners = stand?.GetComp<CompAssignableToPawn>()?.AssignedPawnsForReading;
                if (owners != null && owners.Contains(pawn))
                {
                    return stand;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Outfit-stand lookup failed safely: {ex.Message}");
        }

        return null;
    }

    public static bool HasStand(Pawn pawn) => StandOf(pawn) != null;

    // Carrying a weapon is our proxy for "wearing the combat kit" — the stand swap arms and armors together.
    private static bool InCombatKit(Pawn pawn) => pawn?.equipment?.Primary != null;

    // Send the colonist to their stand to don the stored combat kit. No-op if the kit is not on the stand (no
    // stored weapon) or they are already armed (swapping would strip them into civvies).
    public static void PushEquip(Pawn pawn)
    {
        try
        {
            var stand = StandOf(pawn);
            if (stand == null || stand.HeldWeapon == null || InCombatKit(pawn))
            {
                return;
            }

            PushStandJob(pawn, JobDefOf.UseOutfitStand, stand);
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Outfit-stand equip failed safely: {ex.Message}");
        }
    }

    // Send the colonist back to return the kit and re-dress in civvies. No-op if not currently armed (swapping
    // would arm them). Prefers the Outfit Stands Plus dedicated return job when present, else the vanilla swap.
    public static void PushReturn(Pawn pawn)
    {
        try
        {
            var stand = StandOf(pawn);
            if (stand == null || !InCombatKit(pawn))
            {
                return;
            }

            var returnJob = DefDatabase<JobDef>.GetNamedSilentFail("OutfitStandsPlus_JobReturnToStand")
                            ?? JobDefOf.UseOutfitStand;
            PushStandJob(pawn, returnJob, stand);
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Outfit-stand return failed safely: {ex.Message}");
        }
    }

    private static void PushStandJob(Pawn pawn, JobDef jobDef, Building_OutfitStand stand)
    {
        if (pawn?.jobs == null || jobDef == null)
        {
            return;
        }

        // Already walking to the stand for this — don't re-issue the order every recheck.
        if (pawn.CurJobDef == jobDef)
        {
            return;
        }

        if (!RestUtility.Awake(pawn))
        {
            RestUtility.WakeUp(pawn, startNewJob: false);
        }

        pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(jobDef, stand), JobTag.Misc);
    }
}
```

- [ ] **Step 2: Build to verify zero warnings**

Run: `dotnet build src/LivingWorld.RimWorld/LivingWorld.RimWorld.csproj -c Debug`
Expected: `Предупреждений: 0` / `Ошибок: 0`.

- [ ] **Step 3: Commit**

```bash
git add src/LivingWorld.RimWorld/OutfitStandKit.cs
git commit -m "feat(arsenal): directional outfit-stand kit equip/return driver"
```

---

### Task 5: CAI soft-dependency bridge (`CaiBridge`)

**Files:**
- Create: `src/LivingWorld.RimWorld/CaiBridge.cs`

**Interfaces:**
- Produces:
  - `static bool CaiBridge.Available { get; }`
  - `static bool CaiBridge.TryEngage(Pawn pawn)` — give the pawn an autonomous CAI "hunt down enemies" duty; returns true if the duty was started, false if CAI is absent or anything failed (caller then drafts).

**Reflection targets (verified against CombatAI.dll, packageId Krkr.rule56):**
- `CombatAI.CustomDutyUtility.HuntDownEnemies(IntVec3 fallbackPosition, int expireAfter, int startAfter)` → returns a `CombatAI.Pawn_CustomDutyTracker+CustomPawnDuty`.
- `CombatAI.CustomDutyUtility.TryStartCustomDuty(Pawn pawn, CustomPawnDuty duty, bool returnCurDutyToQueue)` → bool.

- [ ] **Step 1: Write the implementation**

Create `src/LivingWorld.RimWorld/CaiBridge.cs`:

```csharp
using System;
using System.Reflection;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Optional integration with CAI 5000 (Combat Extended-style advanced AI, packageId Krkr.rule56). When CAI is
/// loaded, a mobilized-and-equipped colonist is given CAI's autonomous "hunt down enemies" custom duty, so it
/// fights intelligently on its own with no player micromanagement. Accessed purely by reflection — there is no
/// assembly reference to CombatAI.dll — so the mod builds and runs whether or not CAI is installed. When CAI is
/// absent (or any reflection call fails), <see cref="TryEngage"/> returns false and the driver drafts instead.
///
/// EXPIRY IS A LIVE-TUNING VALUE: DutyExpireTicks is set long enough to outlast a normal engagement and is
/// re-issued only once per alert (the driver tracks who it engaged). If future playtesting shows pawns going
/// passive mid-fight, shorten it and have the driver re-engage on a timer.
/// </summary>
public static class CaiBridge
{
    // ~ half an in-game day. Long enough that a single HuntDownEnemies duty covers a whole raid.
    private const int DutyExpireTicks = 30000;

    private static readonly MethodInfo? HuntMethod;
    private static readonly MethodInfo? StartMethod;

    static CaiBridge()
    {
        try
        {
            var utility = GenTypes.GetTypeInAnyAssembly("CombatAI.CustomDutyUtility");
            if (utility == null)
            {
                return;
            }

            // HuntDownEnemies(IntVec3 fallbackPosition, int expireAfter, int startAfter)
            HuntMethod = utility.GetMethod(
                "HuntDownEnemies",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(IntVec3), typeof(int), typeof(int) },
                modifiers: null);

            // TryStartCustomDuty(Pawn pawn, CustomPawnDuty duty, bool returnCurDutyToQueue)
            StartMethod = utility.GetMethod("TryStartCustomDuty", BindingFlags.Public | BindingFlags.Static);
        }
        catch (Exception ex)
        {
            HuntMethod = null;
            StartMethod = null;
            Log.Warning($"[LivingWorld] CAI bridge resolution failed safely (CAI disabled): {ex.Message}");
        }
    }

    public static bool Available => HuntMethod != null && StartMethod != null;

    public static bool TryEngage(Pawn pawn)
    {
        if (!Available || pawn?.Spawned != true)
        {
            return false;
        }

        try
        {
            // Fall back to the pawn's own position; CAI drives it toward whatever enemies it can sense.
            var duty = HuntMethod!.Invoke(null, new object[] { pawn.Position, DutyExpireTicks, 0 });
            if (duty == null)
            {
                return false;
            }

            var result = StartMethod!.Invoke(null, new object[] { pawn, duty, true });
            return result is true;
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] CAI engage failed safely (will draft instead): {ex.Message}");
            return false;
        }
    }
}
```

- [ ] **Step 2: Build to verify zero warnings**

Run: `dotnet build src/LivingWorld.RimWorld/LivingWorld.RimWorld.csproj -c Debug`
Expected: `Предупреждений: 0` / `Ошибок: 0`. (Builds even though CombatAI.dll is not referenced — everything is reflection.)

- [ ] **Step 3: Commit**

```bash
git add src/LivingWorld.RimWorld/CaiBridge.cs
git commit -m "feat(arsenal): optional CAI 5000 autonomous-combat bridge via reflection"
```

---

### Task 6: Mobilization driver (`MobilizationDriver`)

**Files:**
- Create: `src/LivingWorld.RimWorld/MobilizationDriver.cs`

**Interfaces:**
- Consumes: `MobilizationPlan.NextAction`, `PawnMobState`, `MobPhase`, `MobilizationCandidates.*`, `MobilizationPolicyService.*`, `OutfitStandKit.*`, `CaiBridge.*`, `LivingWorldSettings.Instance`.
- Produces:
  - `sealed class MobilizationDriver` with:
    - ctor `MobilizationDriver()`
    - `void Drive(Map map, bool mobilized)` — snapshot colonists, act one action each, log transitions.
    - `void ResetTransient()` — clear per-alert tracking (called when leaving mobilization).

- [ ] **Step 1: Write the implementation**

Create `src/LivingWorld.RimWorld/MobilizationDriver.cs`:

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
/// Executes the mobilization phase machine against the live colony. Each recheck it snapshots the colonists,
/// builds a <see cref="PawnMobState"/> for each, asks <see cref="MobilizationPlan.NextAction"/> for the one
/// action to take, and performs exactly that action. One action per pawn per tick keeps everything idempotent:
/// the driver stores no step counters, only two transient sets tracking who it engaged / drafted so it can
/// release them on stand-down and not re-issue every recheck. Every executed transition is logged when
/// diagnostics are on, so live behavior is debugged from facts. Fail-safe throughout.
/// </summary>
public sealed class MobilizationDriver
{
    private readonly HashSet<Pawn> engagedByUs = new();
    private readonly HashSet<Pawn> draftedByUs = new();

    public void Drive(Map map, bool mobilized)
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

            engagedByUs.RemoveWhere(p => p == null || !p.Spawned);
            draftedByUs.RemoveWhere(p => p == null || !p.Spawned);

            // Snapshot: equipping/dropping gear can mutate the live colonist list mid-loop.
            foreach (var pawn in colonists.ToList())
            {
                if (pawn == null)
                {
                    continue;
                }

                var state = Snapshot(pawn);
                var action = MobilizationPlan.NextAction(mobilized, state);
                Execute(pawn, action, settings.mobilizationDiagnostics);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Mobilization drive failed safely: {ex.Message}");
        }
    }

    public void ResetTransient()
    {
        engagedByUs.Clear();
        draftedByUs.Clear();
    }

    private PawnMobState Snapshot(Pawn pawn)
    {
        return new PawnMobState
        {
            IsCandidate = MobilizationCandidates.IsCandidate(pawn),
            IsBusyUrgent = MobilizationCandidates.IsBusyUrgent(pawn),
            Asleep = !RestUtility.Awake(pawn),
            PolicyIsCombat = MobilizationPolicyService.IsCombatPolicy(pawn),
            PolicyIsCivilian = MobilizationPolicyService.IsCivilianPolicy(pawn),
            InCombatKit = MobilizationCandidates.IsArmed(pawn),
            HasStand = OutfitStandKit.HasStand(pawn),
            Drafted = pawn.Drafted,
            DraftedByUs = draftedByUs.Contains(pawn),
            HasLwDuty = engagedByUs.Contains(pawn),
            CaiAvailable = CaiBridge.Available,
        };
    }

    private void Execute(Pawn pawn, MobPhase action, bool diagnostics)
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
                if (CaiBridge.TryEngage(pawn))
                {
                    engagedByUs.Add(pawn);
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
                engagedByUs.Remove(pawn);
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
            Log.Message($"[LivingWorld] Mobilization: {pawn.LabelShort} -> {action}");
        }
    }
}
```

- [ ] **Step 2: Build to verify zero warnings**

Run: `dotnet build src/LivingWorld.RimWorld/LivingWorld.RimWorld.csproj -c Debug`
Expected: `Предупреждений: 0` / `Ошибок: 0`.

- [ ] **Step 3: Commit**

```bash
git add src/LivingWorld.RimWorld/MobilizationDriver.cs
git commit -m "feat(arsenal): mobilization driver executes the phase machine with diagnostics"
```

---

### Task 7: Map component (`MobilizationMapComponent`)

**Files:**
- Create: `src/LivingWorld.RimWorld/MobilizationMapComponent.cs`

**Interfaces:**
- Consumes: `MobilizationDriver`, `LivingWorldSettings.Instance`.
- Produces:
  - `sealed class MobilizationMapComponent : MapComponent`
  - ctor `MobilizationMapComponent(Map map)`
  - `bool ManualMobilized { get; }`, `bool ThreatPresent { get; }`, `bool IsMobilized { get; }`
  - `void ToggleManual()`
  - `static MobilizationMapComponent? For(Map map)`

- [ ] **Step 1: Write the implementation**

Create `src/LivingWorld.RimWorld/MobilizationMapComponent.cs`:

```csharp
using System;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Per-map mobilization state for the player colony: whether combat colonists should gear up and fight rather
/// than work in civvies. Mobilized when the player toggles it manually, or automatically while the map is
/// genuinely in danger. Danger is read from RimWorld's own <see cref="DangerWatcher"/> so a single wandering
/// manhunter does not put the whole colony on alert. The recheck is throttled and fail-safe — any error reads
/// as "no threat", so the colony is never locked in alert. Auto-created for every map (a MapComponent with a
/// (Map) constructor is instantiated by RimWorld) and persisted per map.
/// </summary>
public sealed class MobilizationMapComponent : MapComponent
{
    private const int RecheckInterval = 250;

    private bool manualMobilized;
    private bool threatPresent;
    private bool wasMobilized;
    private readonly MobilizationDriver driver = new();

    public MobilizationMapComponent(Map map)
        : base(map)
    {
    }

    public bool ManualMobilized => manualMobilized;

    public bool ThreatPresent => threatPresent;

    public bool IsMobilized => manualMobilized || threatPresent;

    public void ToggleManual()
    {
        manualMobilized = !manualMobilized;
    }

    public override void MapComponentTick()
    {
        base.MapComponentTick();

        var tick = Find.TickManager?.TicksGame ?? 0;
        if (tick % RecheckInterval != 0)
        {
            return;
        }

        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();

        try
        {
            threatPresent = settings.autoMobilizeOnThreat
                && map?.dangerWatcher != null
                && map.dangerWatcher.DangerRating >= StoryDanger.Low;
        }
        catch (Exception ex)
        {
            threatPresent = false;
            Log.Warning($"[LivingWorld] Mobilization threat check failed safely: {ex.Message}");
        }

        var mobilized = IsMobilized;

        // When the alert ends, drop per-alert tracking so the next alert engages/drafts afresh.
        if (!mobilized && wasMobilized)
        {
            driver.ResetTransient();
        }

        wasMobilized = mobilized;
        driver.Drive(map, mobilized);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref manualMobilized, "livingWorld_manualMobilized", false);
    }

    public static MobilizationMapComponent? For(Map map)
    {
        return map?.GetComponent<MobilizationMapComponent>();
    }
}
```

- [ ] **Step 2: Build to verify zero warnings**

Run: `dotnet build src/LivingWorld.RimWorld/LivingWorld.RimWorld.csproj -c Debug`
Expected: `Предупреждений: 0` / `Ошибок: 0`.

- [ ] **Step 3: Commit**

```bash
git add src/LivingWorld.RimWorld/MobilizationMapComponent.cs
git commit -m "feat(arsenal): per-map mobilization state driven by DangerWatcher"
```

---

### Task 8: Settings, toggle gizmo, and translations

**Files:**
- Modify: `src/LivingWorld.RimWorld/LivingWorldSettings.cs`
- Create: `src/LivingWorld.RimWorld/LivingWorldMobilizationGizmoPatch.cs`
- Modify: `mod/Languages/English/Keyed/LivingWorld.xml`
- Modify: `mod/Languages/Russian/Keyed/LivingWorld.xml`

**Interfaces:**
- Consumes: `MobilizationMapComponent.For`, `MobilizationMapComponent.ToggleManual`, `MobilizationMapComponent.ManualMobilized`.
- Produces (settings fields on `LivingWorldSettings`): `bool armoryMobilizationEnabled`, `int mobilizationSkillThreshold`, `bool autoMobilizeOnThreat`, `bool mobilizationDiagnostics`.

- [ ] **Step 1: Add the settings fields**

In `src/LivingWorld.RimWorld/LivingWorldSettings.cs`, locate the mobilization fields. Ensure exactly these four exist (add `autoMobilizeOnThreat` / `mobilizationDiagnostics` and remove any leftover `autoDraftOnThreat`); if `armoryMobilizationEnabled` / `mobilizationSkillThreshold` already exist, leave them:

```csharp
    public bool armoryMobilizationEnabled = true;
    public int mobilizationSkillThreshold = 4;
    public bool autoMobilizeOnThreat = true;
    public bool mobilizationDiagnostics = false;
```

In the same file's `ExposeData()` (where the other `Scribe_Values.Look` calls are), ensure exactly these four Scribe lines exist (replace any `autoDraftOnThreat` line):

```csharp
        Scribe_Values.Look(ref armoryMobilizationEnabled, "armoryMobilizationEnabled", true);
        Scribe_Values.Look(ref mobilizationSkillThreshold, "mobilizationSkillThreshold", 4);
        Scribe_Values.Look(ref autoMobilizeOnThreat, "autoMobilizeOnThreat", true);
        Scribe_Values.Look(ref mobilizationDiagnostics, "mobilizationDiagnostics", false);
```

- [ ] **Step 2: Create the toggle gizmo patch**

Create `src/LivingWorld.RimWorld/LivingWorldMobilizationGizmoPatch.cs`:

```csharp
using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Adds a colony "Mobilize" toggle to player colonists. It flips the per-map manual mobilization flag; the
/// colony is also mobilized automatically while the map is in danger (see <see cref="MobilizationMapComponent"/>).
/// Gated by the armory setting; fail-open — any error just omits the gizmo.
/// </summary>
[HarmonyPatch(typeof(Pawn), "GetGizmos")]
public static class LivingWorldMobilizationGizmoPatch
{
    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
    {
        foreach (var gizmo in __result)
        {
            yield return gizmo;
        }

        var gizmos = new List<Gizmo>();
        try
        {
            if (__instance != null && __instance.IsColonist && __instance.Faction == Faction.OfPlayer)
            {
                var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
                var component = MobilizationMapComponent.For(__instance.Map);
                if (settings.armoryMobilizationEnabled && component != null)
                {
                    gizmos.Add(new Command_Toggle
                    {
                        defaultLabel = "LW_MobilizeToggle".Translate(),
                        defaultDesc = "LW_MobilizeTooltip".Translate(),
                        icon = TexCommand.Draft,
                        isActive = () => component.ManualMobilized,
                        toggleAction = () => component.ToggleManual(),
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Mobilization gizmos skipped safely: {ex.Message}");
            gizmos.Clear();
        }

        foreach (var gizmo in gizmos)
        {
            yield return gizmo;
        }
    }
}
```

- [ ] **Step 3: Add the English translation keys**

In `mod/Languages/English/Keyed/LivingWorld.xml`, add inside `<LanguageData>` (before the closing `</LanguageData>`):

```xml
  <LW_MobilizeToggle>Mobilize colony</LW_MobilizeToggle>
  <LW_MobilizeTooltip>Send combat-capable colonists to their outfit stand to gear up and fight. The colony also mobilizes automatically while the map is in danger, and stands down to civilian clothes afterwards.</LW_MobilizeTooltip>
```

- [ ] **Step 4: Add the Russian translation keys**

In `mod/Languages/Russian/Keyed/LivingWorld.xml`, add inside `<LanguageData>` (before the closing `</LanguageData>`):

```xml
  <LW_MobilizeToggle>Мобилизовать колонию</LW_MobilizeToggle>
  <LW_MobilizeTooltip>Отправить боеспособных колонистов к их манекену — экипироваться и в бой. Колония также мобилизуется сама, пока на карте опасность, и после отбоя возвращается к гражданской одежде.</LW_MobilizeTooltip>
```

- [ ] **Step 5: Build to verify zero warnings**

Run: `dotnet build src/LivingWorld.RimWorld/LivingWorld.RimWorld.csproj -c Debug`
Expected: `Предупреждений: 0` / `Ошибок: 0`.

- [ ] **Step 6: Commit**

```bash
git add src/LivingWorld.RimWorld/LivingWorldSettings.cs src/LivingWorld.RimWorld/LivingWorldMobilizationGizmoPatch.cs mod/Languages/English/Keyed/LivingWorld.xml mod/Languages/Russian/Keyed/LivingWorld.xml
git commit -m "feat(arsenal): mobilization settings, toggle gizmo, EN/RU strings"
```

---

### Task 9: Dev diagnostics (`OutfitStandDebugActions`)

**Files:**
- Create: `src/LivingWorld.RimWorld/OutfitStandDebugActions.cs`

**Interfaces:**
- Consumes: `MobilizationMapComponent.For`, `MobilizationCandidates.*`, `MobilizationPolicyService.*`, `OutfitStandKit.HasStand`, `CaiBridge.Available`.

- [ ] **Step 1: Write the implementation**

Create `src/LivingWorld.RimWorld/OutfitStandDebugActions.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;
using Verse.AI;

namespace LivingWorld.RimWorld;

/// <summary>
/// Developer-mode helpers for the outfit-stand mobilization flow, reachable through the in-game dev "Debug
/// actions" menu. "Spawn combat test gear" drops a pile of weapons, a full armour set and civilian clothing at
/// the map centre so the player can quickly stock outfit stands. "Dump mobilization phases" prints, for every
/// combat colonist, the exact state the phase machine sees — for debugging live behaviour without guessing.
/// </summary>
public static class OutfitStandDebugActions
{
    private const int CopiesPerItem = 6;

    private static readonly string[] Weapons =
    {
        "Gun_AssaultRifle", "Gun_BoltActionRifle", "Gun_Autopistol", "Gun_PumpShotgun", "MeleeWeapon_LongSword",
    };

    private static readonly string[] Armor =
    {
        "Apparel_FlakVest", "Apparel_FlakPants", "Apparel_FlakJacket", "Apparel_ArmorHelmet", "Apparel_SimpleHelmet",
    };

    private static readonly string[] Civilian =
    {
        "Apparel_BasicShirt", "Apparel_CollarShirt", "Apparel_Pants", "Apparel_CowboyHat",
        "Apparel_Duster", "Apparel_Parka", "Apparel_Tuque",
    };

    [DebugAction("LivingWorld", "Spawn combat test gear", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void SpawnCombatTestGear()
    {
        var map = Find.CurrentMap;
        if (map == null)
        {
            return;
        }

        var cell = map.Center;
        Spawn(map, cell, Weapons);
        Spawn(map, cell, Armor);
        Spawn(map, cell, Civilian);

        Messages.Message("[LivingWorld] Spawned combat test gear at the map centre — haul it onto your outfit stands.",
            MessageTypeDefOf.TaskCompletion, historical: false);
    }

    [DebugAction("LivingWorld", "Dump mobilization phases", allowedGameStates = AllowedGameStates.PlayingOnMap)]
    private static void DumpMobilizationPhases()
    {
        var map = Find.CurrentMap;
        var component = MobilizationMapComponent.For(map);
        if (map == null || component == null)
        {
            return;
        }

        Log.Message($"[LivingWorld] Mobilization dump — mobilized={component.IsMobilized} "
                    + $"(manual={component.ManualMobilized}, threat={component.ThreatPresent}), CAI={CaiBridge.Available}");

        foreach (var pawn in map.mapPawns.FreeColonistsSpawned.ToList())
        {
            if (pawn == null)
            {
                continue;
            }

            Log.Message($"[LivingWorld]   {pawn.LabelShort}: candidate={MobilizationCandidates.IsCandidate(pawn)}, "
                        + $"busyUrgent={MobilizationCandidates.IsBusyUrgent(pawn)}, "
                        + $"asleep={!RestUtility.Awake(pawn)}, armed={MobilizationCandidates.IsArmed(pawn)}, "
                        + $"stand={OutfitStandKit.HasStand(pawn)}, drafted={pawn.Drafted}, "
                        + $"combatPolicy={MobilizationPolicyService.IsCombatPolicy(pawn)}, "
                        + $"civilianPolicy={MobilizationPolicyService.IsCivilianPolicy(pawn)}");
        }
    }

    private static void Spawn(Map map, IntVec3 cell, IReadOnlyList<string> defNames)
    {
        foreach (var name in defNames)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
            if (def == null)
            {
                continue;
            }

            for (var i = 0; i < CopiesPerItem; i++)
            {
                var stuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
                var thing = ThingMaker.MakeThing(def, stuff);
                GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near);
            }
        }
    }
}
```

- [ ] **Step 2: Build to verify zero warnings**

Run: `dotnet build src/LivingWorld.RimWorld/LivingWorld.RimWorld.csproj -c Debug`
Expected: `Предупреждений: 0` / `Ошибок: 0`.

- [ ] **Step 3: Run the full test suite to confirm nothing regressed**

Run: `dotnet run --project src/LivingWorld.Tests -c Debug`
Expected: final line `335 test(s) passed.`

- [ ] **Step 4: Commit**

```bash
git add src/LivingWorld.RimWorld/OutfitStandDebugActions.cs
git commit -m "feat(arsenal): dev actions to spawn test gear and dump mobilization phases"
```

---

## Post-Implementation: Deploy & live verification (manual, by the user)

Automated tests cannot verify live pawn behavior. After the branch builds clean:

1. Deploy the mod: `pwsh tools/install-rimworld-mod.ps1` (copies the built assembly + Languages into the RimWorld Mods folder).
2. In RimWorld, enable in load order: `Harmony` → `Prepatcher` → Core + DLCs → … → `CAI 5000` → `Living World`. Restart when Prepatcher asks (it prepatches on startup).
3. Enable **Mobilization diagnostics** in mod settings.
4. On a colony map: run dev action **"Spawn combat test gear"**, stock each combat colonist's outfit stand, assign owners.
5. Toggle **Mobilize colony** (and/or trigger a raid). Watch the log: each colonist should walk to its stand, arm, and (with CAI) hunt enemies. Run **"Dump mobilization phases"** if anyone is stuck — the dump shows exactly which predicate is blocking them.
6. Stand down (untoggle / raid ends): colonists should return the kit and end up in civvies; nobody should remain in armor.

Report any stuck phase from the dump; fixes target that specific predicate, not blind iteration.

---

## Self-Review

**1. Spec coverage** (against `2026-07-10-arsenal-redesign-design.md`):
- MobilizationState (trigger + state + Scribe) → Task 7. DangerWatcher threat → Task 7. ✅
- MobilizationPolicyService (two policies, strip safety net) → Task 3. ✅
- OutfitStandKit (stand lookup, directional swap) → Task 4. ✅
- CaiBridge (reflection soft-dep, HuntDownEnemies + fallback) → Task 5. ✅
- MobilizationCandidates (violence-able, draftable, skill, urgent-job guard) → Task 2. ✅
- MobilizationDriver (phase machine, one action/tick, snapshot, diagnostics) → Tasks 1 (pure) + 6 (glue). ✅
- Settings (4 flags) + toggle gizmo → Task 8. ✅
- Dev spawner + phase dump → Task 9. ✅
- Equip-before-combat ordering, idempotence, fail-safe, Odyssey gate, EN/RU → enforced across tasks + Global Constraints. ✅
- Retired old files (OutfitStandDriver/MobilizationOutfitService/auto-draft-only) → they are not on this branch tree; the new files supersede them by name. ✅

**2. Placeholder scan:** No TBD/TODO; every code step contains complete code; every command has expected output. ✅

**3. Type consistency:** `MobPhase` values and `PawnMobState` field names in Task 1's tests, the enum/struct definition, and Task 6's `Snapshot`/`Execute` all match (`IsCandidate, IsBusyUrgent, Asleep, PolicyIsCombat, PolicyIsCivilian, InCombatKit, HasStand, Drafted, DraftedByUs, HasLwDuty, CaiAvailable`; `None/Wake/SetCombatPolicy/Equip/Engage/Draft/SteadyCombat/ClearCombat/SetCivilianPolicy/ReturnKit/SteadyCivilian`). `MobilizationPolicyService` method names (`ApplyCombat/ApplyCivilian/IsCombatPolicy/IsCivilianPolicy`), `OutfitStandKit` (`HasStand/PushEquip/PushReturn`), `CaiBridge` (`Available/TryEngage`), `MobilizationCandidates` (`IsCandidate/IsArmed/IsBusyUrgent`) are used consistently across Tasks 6, 8, 9. Settings fields (`armoryMobilizationEnabled/mobilizationSkillThreshold/autoMobilizeOnThreat/mobilizationDiagnostics`) match across Tasks 6, 7, 8. ✅
