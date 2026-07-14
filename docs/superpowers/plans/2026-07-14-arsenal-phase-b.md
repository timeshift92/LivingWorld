# Arsenal Expansion — Phase B (Fighters Roster & Non-Combatant Shelter) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the player a **Fighters roster** (who arms up) and make **non-combatants retreat to a shelter allowed-area** on a Raid-or-worse threat, then return afterward — with the decision logic in a pure, unit-tested `ShelterPlan` and reliable vanilla allowed-area mechanics for the effect.

**Architecture:** A game-wide `FightersRoster` GameComponent decides `IsFighter` (skill-eligible by default, player-overridable via a per-pawn gizmo). Mobilization keys on the roster instead of the raw skill threshold. A pure `ShelterPlan.NextAction` (in `LivingWorld.Core`) decides per non-combatant whether to Flee to shelter or Restore their area; a thin `ShelterDriver` executes it by setting `pawn.playerSettings.AreaRestrictionInPawnCurrentMap` to the player-painted "LW Shelter" area (fallback: Home), recording the previous area so it can be restored on stand-down.

**Tech Stack:** C# — `LivingWorld.Core` (netstandard2.0/net8.0 test host) + `LivingWorld.RimWorld` (net472, RimWorld 1.6 + Odyssey). Custom test harness in `src/LivingWorld.Tests/Program.cs`.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-07-14-arsenal-expansion-design.md` (Phase B). Builds on Phase A (merged locally: threat tiers, CAI, correctness fixes).
- Only `LivingWorld.Core` is unit-testable. New pure logic (`ShelterPlan`) goes there; RimWorld glue verified by clean net472 build + review + in-game diagnostic logs.
- Every public RimWorld entry point fail-safe (try/catch → `Log.Warning($"[LivingWorld] <what> failed safely: {ex.Message}")`; never throw into the game loop).
- Zero build warnings (`dotnet build src/LivingWorld.RimWorld/LivingWorld.RimWorld.csproj -c Debug` → `Предупреждений: 0` / `Ошибок: 0`).
- Every new `.Translate()` key exists in BOTH `mod/Languages/English/Keyed/LivingWorld.xml` and `mod/Languages/Russian/Keyed/LivingWorld.xml`.
- Fighter roster model: `IsFighter(pawn) = explicitlyIncluded OR (skill-eligible AND NOT explicitlyExcluded)`. No empty-roster surprise; player edits from a sensible default.
- Non-combatant = colonist AND NOT `IsFighter` (children/incapable/non-rostered all shelter).
- Shelter applies at tier **Raid or worse** (`>= ThreatTier.Raid`) — Nuisance never shelters (matches Phase A's mobilization gate).
- Test baseline before this plan: **389 test(s) passed.**

---

## File Structure

**Create:**
- `src/LivingWorld.Core/ShelterPlan.cs` — `ShelterPhase` enum, `NonCombatantState` struct, pure `NextAction`.
- `src/LivingWorld.RimWorld/FightersRoster.cs` — GameComponent: included/excluded id sets, `IsFighter`, `Toggle`, `Get`.
- `src/LivingWorld.RimWorld/ShelterAreaService.cs` — shelter-area lookup (labeled "LW Shelter" else Home), get/set area helpers.
- `src/LivingWorld.RimWorld/ShelterDriver.cs` — non-combatant pass: snapshot → `ShelterPlan.NextAction` → set/restore area; export/import the changed-area map for persistence.
- `src/LivingWorld.RimWorld/LivingWorldFighterGizmoPatch.cs` — per-pawn "Fighter" toggle gizmo.

**Modify:**
- `src/LivingWorld.RimWorld/MobilizationCandidates.cs` — `IsCandidate` keys on `FightersRoster`; add `IsNonCombatant`.
- `src/LivingWorld.RimWorld/MobilizationMapComponent.cs` — drive the shelter pass with the current tier; Scribe the shelter changed-area map.
- `src/LivingWorld.RimWorld/OutfitStandDebugActions.cs` — dump fighter/non-combatant + shelter state.
- `src/LivingWorld.RimWorld/LivingWorldSettings.cs` — (none required; `mobilizationSkillThreshold` reused for the roster default).
- `mod/Languages/English/Keyed/LivingWorld.xml` + Russian — fighter-gizmo keys.
- `src/LivingWorld.Tests/Program.cs` — `ShelterPlan` tests.

---

### Task 1: `ShelterPlan` (Core, pure, unit-tested)

**Files:**
- Create: `src/LivingWorld.Core/ShelterPlan.cs`
- Test: `src/LivingWorld.Tests/Program.cs`

**Interfaces:**
- Produces:
  - `enum LivingWorld.Core.ShelterPhase { None, Flee, Restore }`
  - `readonly struct LivingWorld.Core.NonCombatantState` with init-only bool props: `IsNonCombatant, IsBusyUrgent, TierWantsShelter, InShelterArea, AreaChangedByUs`.
  - `static ShelterPhase ShelterPlan.NextAction(in NonCombatantState s)`

- [ ] **Step 1: Write the failing tests**

Append tuples to the `tests` list in `src/LivingWorld.Tests/Program.cs`:

```csharp
    ("shelter: a fighter (not a non-combatant) is left alone", TestShelterNotNonCombatant),
    ("shelter: a busy-urgent non-combatant is not moved", TestShelterBusyUrgent),
    ("shelter: needed and not yet in shelter -> Flee", TestShelterFlee),
    ("shelter: needed and already in shelter -> None", TestShelterAlreadyInShelter),
    ("shelter: not needed and we changed the area -> Restore", TestShelterRestore),
    ("shelter: not needed and we never touched the area -> None", TestShelterNoRestoreNeeded),
```

Append methods:

```csharp
static void TestShelterNotNonCombatant()
{
    var s = new NonCombatantState { IsNonCombatant = false, TierWantsShelter = true };
    AssertEqual(ShelterPhase.None, ShelterPlan.NextAction(s));
}

static void TestShelterBusyUrgent()
{
    var s = new NonCombatantState { IsNonCombatant = true, IsBusyUrgent = true, TierWantsShelter = true };
    AssertEqual(ShelterPhase.None, ShelterPlan.NextAction(s));
}

static void TestShelterFlee()
{
    var s = new NonCombatantState { IsNonCombatant = true, TierWantsShelter = true, InShelterArea = false };
    AssertEqual(ShelterPhase.Flee, ShelterPlan.NextAction(s));
}

static void TestShelterAlreadyInShelter()
{
    var s = new NonCombatantState { IsNonCombatant = true, TierWantsShelter = true, InShelterArea = true };
    AssertEqual(ShelterPhase.None, ShelterPlan.NextAction(s));
}

static void TestShelterRestore()
{
    var s = new NonCombatantState { IsNonCombatant = true, TierWantsShelter = false, AreaChangedByUs = true };
    AssertEqual(ShelterPhase.Restore, ShelterPlan.NextAction(s));
}

static void TestShelterNoRestoreNeeded()
{
    var s = new NonCombatantState { IsNonCombatant = true, TierWantsShelter = false, AreaChangedByUs = false };
    AssertEqual(ShelterPhase.None, ShelterPlan.NextAction(s));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet run --project src/LivingWorld.Tests -c Debug`
Expected: FAIL — `'ShelterPhase'/'NonCombatantState'/'ShelterPlan' could not be found`.

- [ ] **Step 3: Write the implementation**

Create `src/LivingWorld.Core/ShelterPlan.cs`:

```csharp
namespace LivingWorld.Core;

/// <summary>The one shelter action for a non-combatant this tick.</summary>
public enum ShelterPhase
{
    None,
    Flee,
    Restore,
}

/// <summary>
/// Plain-data snapshot of a non-combatant colonist's shelter-relevant state. Built from a live pawn by the
/// shelter driver, but free of RimWorld types so the decision is unit-testable without the game.
/// </summary>
public readonly struct NonCombatantState
{
    /// <summary>A colonist who is not on the Fighters roster (children/incapable/non-rostered included).</summary>
    public bool IsNonCombatant { get; init; }

    /// <summary>On a life-or-base-saving job (firefighting, tending, rescuing) — do not move them.</summary>
    public bool IsBusyUrgent { get; init; }

    /// <summary>The current threat tier is serious enough to shelter (Raid or worse).</summary>
    public bool TierWantsShelter { get; init; }

    /// <summary>Their allowed area is already the shelter area.</summary>
    public bool InShelterArea { get; init; }

    /// <summary>We changed their allowed area (so we owe them a restore when the threat passes).</summary>
    public bool AreaChangedByUs { get; init; }
}

/// <summary>
/// Pure decision for a non-combatant: flee to the shelter area while a real threat is up, restore their normal
/// area afterwards, and never yank someone off an urgent job. One action per call keeps the driver idempotent.
/// </summary>
public static class ShelterPlan
{
    public static ShelterPhase NextAction(in NonCombatantState s)
    {
        if (!s.IsNonCombatant || s.IsBusyUrgent)
        {
            return ShelterPhase.None;
        }

        if (s.TierWantsShelter)
        {
            return s.InShelterArea ? ShelterPhase.None : ShelterPhase.Flee;
        }

        // No shelter needed: put their area back only if we were the ones who changed it.
        return s.AreaChangedByUs ? ShelterPhase.Restore : ShelterPhase.None;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet run --project src/LivingWorld.Tests -c Debug`
Expected: PASS — `395 test(s) passed.` (389 + 6).

- [ ] **Step 5: Commit**

```bash
git add src/LivingWorld.Core/ShelterPlan.cs src/LivingWorld.Tests/Program.cs
git commit -m "feat(arsenal): pure non-combatant shelter phase machine with unit tests"
```

---

### Task 2: `FightersRoster` (GameComponent)

**Files:**
- Create: `src/LivingWorld.RimWorld/FightersRoster.cs`

**Interfaces:**
- Consumes: `LivingWorld.Core.LoadoutSelectionService.IsCombatEligible`, `MobilizationTuning.CombatSkillThreshold`, `LivingWorldSettings.mobilizationSkillThreshold`.
- Produces:
  - `sealed class FightersRoster : GameComponent`, ctor `FightersRoster(Game game)`
  - `static FightersRoster? Get()`
  - `bool IsFighter(Pawn pawn)`
  - `void Toggle(Pawn pawn)`

- [ ] **Step 1: Write the implementation**

Create `src/LivingWorld.RimWorld/FightersRoster.cs`:

```csharp
using System;
using System.Collections.Generic;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Game-wide "who is a fighter" roster. A colonist is a fighter by default when their best combat skill meets
/// the threshold; the player overrides that per pawn (add a weak pawn, or pull a skilled crafter out) via the
/// Fighter gizmo. Stored as two id sets so the default tracks skill changes while explicit choices stick.
/// Auto-created by RimWorld (any GameComponent with a (Game) ctor is instantiated) and persisted with the save.
/// Fail-safe: any error reads as "not a fighter".
/// </summary>
public sealed class FightersRoster : GameComponent
{
    private HashSet<int> includedIds = new();
    private HashSet<int> excludedIds = new();

    public FightersRoster(Game game)
    {
    }

    public static FightersRoster? Get() => Current.Game?.GetComponent<FightersRoster>();

    public bool IsFighter(Pawn pawn)
    {
        try
        {
            if (pawn == null)
            {
                return false;
            }

            var id = pawn.thingIDNumber;
            if (includedIds.Contains(id))
            {
                return true;
            }

            if (excludedIds.Contains(id))
            {
                return false;
            }

            return SkillEligible(pawn);
        }
        catch
        {
            return false;
        }
    }

    public void Toggle(Pawn pawn)
    {
        try
        {
            if (pawn == null)
            {
                return;
            }

            var id = pawn.thingIDNumber;
            if (IsFighter(pawn))
            {
                // Was a fighter -> exclude.
                excludedIds.Add(id);
                includedIds.Remove(id);
            }
            else
            {
                // Was not -> include.
                includedIds.Add(id);
                excludedIds.Remove(id);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Fighter roster toggle failed safely: {ex.Message}");
        }
    }

    private static bool SkillEligible(Pawn pawn)
    {
        var shooting = pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
        var melee = pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
        var threshold = LivingWorldSettings.Instance?.mobilizationSkillThreshold
                        ?? MobilizationTuning.CombatSkillThreshold;
        return LoadoutSelectionService.IsCombatEligible(shooting, melee, threshold);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref includedIds, "livingWorld_fighterIncluded", LookMode.Value);
        Scribe_Collections.Look(ref excludedIds, "livingWorld_fighterExcluded", LookMode.Value);
        includedIds ??= new HashSet<int>();
        excludedIds ??= new HashSet<int>();
    }
}
```

- [ ] **Step 2: Build to verify zero warnings**

Run: `dotnet build src/LivingWorld.RimWorld/LivingWorld.RimWorld.csproj -c Debug`
Expected: `Предупреждений: 0` / `Ошибок: 0`.

- [ ] **Step 3: Commit**

```bash
git add src/LivingWorld.RimWorld/FightersRoster.cs
git commit -m "feat(arsenal): game-wide fighters roster (skill-default, player-overridable)"
```

---

### Task 3: Candidate/non-combatant classification keys on the roster

**Files:**
- Modify: `src/LivingWorld.RimWorld/MobilizationCandidates.cs`

**Interfaces:**
- Produces: `static bool MobilizationCandidates.IsNonCombatant(Pawn pawn)`; `IsCandidate` now roster-based.

- [ ] **Step 1: Rewire `IsCandidate` and add `IsNonCombatant`**

In `src/LivingWorld.RimWorld/MobilizationCandidates.cs`, replace the body of `IsCandidate` (keep the method signature) with a roster-based check — a fighter who is actually able to fight:

```csharp
    public static bool IsCandidate(Pawn pawn)
    {
        try
        {
            if (pawn == null || !pawn.IsColonist || pawn.Dead || pawn.Downed || pawn.InMentalState)
            {
                return false;
            }

            // Hard floor: never mobilize a colonist who cannot fight, even if rostered.
            if (pawn.WorkTagIsDisabled(WorkTags.Violent) || pawn.drafter == null)
            {
                return false;
            }

            // The Fighters roster decides who arms up (skill-eligible by default, player-overridable).
            var roster = FightersRoster.Get();
            return roster != null && roster.IsFighter(pawn);
        }
        catch
        {
            return false;
        }
    }
```

Add a new method (a non-combatant is any colonist not on the roster — children/incapable/non-rostered all count):

```csharp
    // Everyone the shelter system moves: a colonist who is not a fighter. Deliberately broad — children and
    // violence-incapable colonists are non-combatants too. Fail-safe: unknown state reads as non-combatant so
    // they are protected (sheltered) rather than left in the open.
    public static bool IsNonCombatant(Pawn pawn)
    {
        try
        {
            if (pawn == null || !pawn.IsColonist || pawn.Dead)
            {
                return false;
            }

            var roster = FightersRoster.Get();
            return roster == null || !roster.IsFighter(pawn);
        }
        catch
        {
            return false;
        }
    }
```

- [ ] **Step 2: Build to verify zero warnings**

Run: `dotnet build src/LivingWorld.RimWorld/LivingWorld.RimWorld.csproj -c Debug`
Expected: `Предупреждений: 0` / `Ошибок: 0`.

- [ ] **Step 3: Commit**

```bash
git add src/LivingWorld.RimWorld/MobilizationCandidates.cs
git commit -m "feat(arsenal): mobilization candidacy keyed on the fighters roster"
```

---

### Task 4: Fighter toggle gizmo + translations

**Files:**
- Create: `src/LivingWorld.RimWorld/LivingWorldFighterGizmoPatch.cs`
- Modify: `mod/Languages/English/Keyed/LivingWorld.xml`, `mod/Languages/Russian/Keyed/LivingWorld.xml`

**Interfaces:**
- Consumes: `FightersRoster.Get()/IsFighter/Toggle`.

- [ ] **Step 1: Create the gizmo patch**

Create `src/LivingWorld.RimWorld/LivingWorldFighterGizmoPatch.cs`:

```csharp
using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Adds a per-colonist "Fighter" toggle to player colonists — the roster the mobilization system arms up.
/// Skill-eligible colonists show checked by default; the player checks/unchecks to override. Gated by the
/// armory setting; fail-open — any error just omits the gizmo.
/// </summary>
[HarmonyPatch(typeof(Pawn), "GetGizmos")]
public static class LivingWorldFighterGizmoPatch
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
            if (__instance != null && __instance.IsColonist && __instance.Faction == Faction.OfPlayer
                && __instance.drafter != null)
            {
                var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
                var roster = FightersRoster.Get();
                if (settings.armoryMobilizationEnabled && roster != null)
                {
                    var pawn = __instance;
                    gizmos.Add(new Command_Toggle
                    {
                        defaultLabel = "LW_FighterToggle".Translate(),
                        defaultDesc = "LW_FighterTooltip".Translate(),
                        icon = TexCommand.Attack,
                        isActive = () => roster.IsFighter(pawn),
                        toggleAction = () => roster.Toggle(pawn),
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Fighter gizmo skipped safely: {ex.Message}");
            gizmos.Clear();
        }

        foreach (var gizmo in gizmos)
        {
            yield return gizmo;
        }
    }
}
```

- [ ] **Step 2: Add English keys**

In `mod/Languages/English/Keyed/LivingWorld.xml`, inside `<LanguageData>`:

```xml
  <LW_FighterToggle>Fighter</LW_FighterToggle>
  <LW_FighterTooltip>Mark this colonist as a fighter. Fighters arm up from their outfit stand and fight when the colony mobilizes; everyone else takes shelter. Combat-skilled colonists are fighters by default — toggle to override.</LW_FighterTooltip>
```

- [ ] **Step 3: Add Russian keys**

In `mod/Languages/Russian/Keyed/LivingWorld.xml`, inside `<LanguageData>`:

```xml
  <LW_FighterToggle>Боец</LW_FighterToggle>
  <LW_FighterTooltip>Пометить колониста как бойца. Бойцы экипируются с манекена и воюют при мобилизации; остальные уходят в укрытие. Колонисты с боевым навыком — бойцы по умолчанию, переключи чтобы изменить.</LW_FighterTooltip>
```

- [ ] **Step 4: Build to verify zero warnings**

Run: `dotnet build src/LivingWorld.RimWorld/LivingWorld.RimWorld.csproj -c Debug`
Expected: `Предупреждений: 0` / `Ошибок: 0`.

- [ ] **Step 5: Commit**

```bash
git add src/LivingWorld.RimWorld/LivingWorldFighterGizmoPatch.cs mod/Languages/English/Keyed/LivingWorld.xml mod/Languages/Russian/Keyed/LivingWorld.xml
git commit -m "feat(arsenal): per-colonist Fighter toggle gizmo, EN/RU strings"
```

---

### Task 5: `ShelterAreaService` (area lookup + apply/restore)

**Files:**
- Create: `src/LivingWorld.RimWorld/ShelterAreaService.cs`

**Interfaces:**
- Produces:
  - `static Area? ShelterAreaService.ShelterAreaFor(Map map)` (labeled "LW Shelter" else Home)
  - `static bool ShelterAreaService.IsInShelter(Pawn pawn)`
  - `static int ShelterAreaService.CurrentAreaId(Pawn pawn)` (Area.ID, or -1 for none)
  - `static void ShelterAreaService.SetArea(Pawn pawn, Area? area)`
  - `static Area? ShelterAreaService.AreaById(Map map, int id)` (null for -1 / not found)

- [ ] **Step 1: Write the implementation**

Create `src/LivingWorld.RimWorld/ShelterAreaService.cs`:

```csharp
using System;
using System.Linq;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Reads and sets a pawn's allowed area for the shelter mechanic, via the vanilla area system. The shelter is
/// the player-painted allowed area labelled "LW Shelter"; if there is none, non-combatants fall back to the
/// Home area (large enough that rescues are rarely blocked). Fail-safe throughout.
/// </summary>
public static class ShelterAreaService
{
    private const string ShelterLabel = "LW Shelter";

    public static Area? ShelterAreaFor(Map map)
    {
        try
        {
            var mgr = map?.areaManager;
            if (mgr == null)
            {
                return null;
            }

            var painted = mgr.AllAreas.FirstOrDefault(a => a != null && a.Label == ShelterLabel);
            return painted ?? mgr.Home;
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Shelter area lookup failed safely: {ex.Message}");
            return null;
        }
    }

    public static bool IsInShelter(Pawn pawn)
    {
        try
        {
            var current = pawn?.playerSettings?.AreaRestrictionInPawnCurrentMap;
            var shelter = pawn?.Map != null ? ShelterAreaFor(pawn.Map) : null;
            return current != null && shelter != null && current == shelter;
        }
        catch
        {
            return false;
        }
    }

    // The pawn's current allowed-area ID, or -1 when unrestricted (no area).
    public static int CurrentAreaId(Pawn pawn)
    {
        try
        {
            return pawn?.playerSettings?.AreaRestrictionInPawnCurrentMap?.ID ?? -1;
        }
        catch
        {
            return -1;
        }
    }

    public static void SetArea(Pawn pawn, Area? area)
    {
        try
        {
            if (pawn?.playerSettings != null)
            {
                pawn.playerSettings.AreaRestrictionInPawnCurrentMap = area;
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Shelter area set failed safely: {ex.Message}");
        }
    }

    public static Area? AreaById(Map map, int id)
    {
        try
        {
            if (id < 0 || map?.areaManager == null)
            {
                return null;
            }

            return map.areaManager.AllAreas.FirstOrDefault(a => a != null && a.ID == id);
        }
        catch
        {
            return null;
        }
    }
}
```

- [ ] **Step 2: Build to verify zero warnings**

Run: `dotnet build src/LivingWorld.RimWorld/LivingWorld.RimWorld.csproj -c Debug`
Expected: `Предупреждений: 0` / `Ошибок: 0`. (If `Area.ID` reports a type mismatch, it is an `int` in RimWorld 1.6 — no cast needed.)

- [ ] **Step 3: Commit**

```bash
git add src/LivingWorld.RimWorld/ShelterAreaService.cs
git commit -m "feat(arsenal): shelter allowed-area lookup and get/set helpers"
```

---

### Task 6: `ShelterDriver` (non-combatant pass)

**Files:**
- Create: `src/LivingWorld.RimWorld/ShelterDriver.cs`

**Interfaces:**
- Consumes: `ShelterPlan`, `NonCombatantState`, `ShelterPhase`, `ThreatTier`, `MobilizationCandidates.IsNonCombatant/IsBusyUrgent`, `ShelterAreaService.*`.
- Produces:
  - `sealed class ShelterDriver` with `void Drive(Map map, ThreatTier tier)`, `Dictionary<int,int> ExportPrevAreas()`, `void ImportPrevAreas(Dictionary<int,int> data)`, `bool WeChangedArea(Pawn pawn)`.

- [ ] **Step 1: Write the implementation**

Create `src/LivingWorld.RimWorld/ShelterDriver.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Executes the shelter phase machine over non-combatants: while the threat tier is Raid-or-worse, move each
/// non-combatant's allowed area to the shelter (recording their previous area), and restore it when the threat
/// passes. One action per pawn per recheck; the previous-area map is exported for save/load persistence so a
/// reload mid-raid still restores correctly. Fail-safe throughout.
/// </summary>
public sealed class ShelterDriver
{
    // pawn thingIDNumber -> the Area.ID they had before we sheltered them (-1 = was unrestricted).
    private readonly Dictionary<int, int> prevAreaByPawn = new();

    public void Drive(Map map, ThreatTier tier)
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        if (!settings.armoryMobilizationEnabled)
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

            var wantsShelter = tier >= ThreatTier.Raid;
            var shelter = wantsShelter ? ShelterAreaService.ShelterAreaFor(map) : null;

            foreach (var pawn in colonists.ToList())
            {
                if (pawn == null)
                {
                    continue;
                }

                var state = new NonCombatantState
                {
                    IsNonCombatant = MobilizationCandidates.IsNonCombatant(pawn),
                    IsBusyUrgent = MobilizationCandidates.IsBusyUrgent(pawn),
                    TierWantsShelter = wantsShelter,
                    InShelterArea = ShelterAreaService.IsInShelter(pawn),
                    AreaChangedByUs = prevAreaByPawn.ContainsKey(pawn.thingIDNumber),
                };

                switch (ShelterPlan.NextAction(state))
                {
                    case ShelterPhase.Flee:
                        if (!prevAreaByPawn.ContainsKey(pawn.thingIDNumber))
                        {
                            prevAreaByPawn[pawn.thingIDNumber] = ShelterAreaService.CurrentAreaId(pawn);
                        }

                        ShelterAreaService.SetArea(pawn, shelter);
                        if (settings.mobilizationDiagnostics)
                        {
                            Log.Message($"[LivingWorld] Shelter: {pawn.LabelShort} -> Flee");
                        }
                        break;

                    case ShelterPhase.Restore:
                        RestoreArea(pawn, map);
                        if (settings.mobilizationDiagnostics)
                        {
                            Log.Message($"[LivingWorld] Shelter: {pawn.LabelShort} -> Restore");
                        }
                        break;
                }
            }

            // Drop tracking for pawns that despawned/left so the map does not grow unbounded.
            var live = new HashSet<int>(colonists.Where(p => p != null).Select(p => p.thingIDNumber));
            foreach (var goneId in prevAreaByPawn.Keys.Where(id => !live.Contains(id)).ToList())
            {
                prevAreaByPawn.Remove(goneId);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Shelter drive failed safely: {ex.Message}");
        }
    }

    public bool WeChangedArea(Pawn pawn) => pawn != null && prevAreaByPawn.ContainsKey(pawn.thingIDNumber);

    public Dictionary<int, int> ExportPrevAreas() => new(prevAreaByPawn);

    public void ImportPrevAreas(Dictionary<int, int> data)
    {
        prevAreaByPawn.Clear();
        if (data == null)
        {
            return;
        }

        foreach (var kv in data)
        {
            prevAreaByPawn[kv.Key] = kv.Value;
        }
    }

    private void RestoreArea(Pawn pawn, Map map)
    {
        if (!prevAreaByPawn.TryGetValue(pawn.thingIDNumber, out var prevId))
        {
            return;
        }

        // Resolve the old area by id; if it was deleted (or was "unrestricted"), restore to null (whole map).
        var prevArea = ShelterAreaService.AreaById(map, prevId);
        ShelterAreaService.SetArea(pawn, prevArea);
        prevAreaByPawn.Remove(pawn.thingIDNumber);
    }
}
```

- [ ] **Step 2: Build to verify zero warnings**

Run: `dotnet build src/LivingWorld.RimWorld/LivingWorld.RimWorld.csproj -c Debug`
Expected: `Предупреждений: 0` / `Ошибок: 0`.

- [ ] **Step 3: Commit**

```bash
git add src/LivingWorld.RimWorld/ShelterDriver.cs
git commit -m "feat(arsenal): shelter driver moves non-combatants to safety and restores after"
```

---

### Task 7: Wire the shelter pass into `MobilizationMapComponent` + persistence + diagnostics

**Files:**
- Modify: `src/LivingWorld.RimWorld/MobilizationMapComponent.cs`
- Modify: `src/LivingWorld.RimWorld/OutfitStandDebugActions.cs`

**Interfaces:**
- Consumes: `ShelterDriver`, `MobilizationCandidates.IsNonCombatant`, `ShelterAreaService.IsInShelter`.

- [ ] **Step 1: Add the shelter driver + persistence to the component**

In `src/LivingWorld.RimWorld/MobilizationMapComponent.cs`:

Add a field alongside `driver`:

```csharp
    private readonly ShelterDriver shelterDriver = new();
    private Dictionary<int, int> savedShelterAreas = new();
```

Add `using System.Collections.Generic;` if not already present.

In `MapComponentTick`, after the existing `driver.Drive(map, IsMobilized, currentTier);` line, add:

```csharp
        shelterDriver.Drive(map, currentTier);
```

In `ExposeData`, after the existing drafted-set Scribe block, add persistence for the shelter previous-area map:

```csharp
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            savedShelterAreas = shelterDriver.ExportPrevAreas();
        }

        Scribe_Collections.Look(ref savedShelterAreas, "livingWorld_shelterPrevAreas", LookMode.Value, LookMode.Value);
        savedShelterAreas ??= new Dictionary<int, int>();
```

In `FinalizeInit`, after the existing `driver.ImportDraftedIds(...)` line, add:

```csharp
        shelterDriver.ImportPrevAreas(savedShelterAreas);
```

Update `DiagnosePawn` to also show role + shelter state — replace it with:

```csharp
    public string DiagnosePawn(Pawn pawn)
    {
        return $"phase={driver.PeekPhase(pawn, IsMobilized, currentTier)}, tier={currentTier}, "
               + $"fighter={!MobilizationCandidates.IsNonCombatant(pawn)}, "
               + $"engagedByUs={driver.IsEngagedByUs(pawn)}, draftedByUs={driver.IsDraftedByUs(pawn)}, "
               + $"aiAutoControl={CaiBridge.IsAutoControlled(pawn)}, "
               + $"inShelter={ShelterAreaService.IsInShelter(pawn)}, shelteredByUs={shelterDriver.WeChangedArea(pawn)}";
    }
```

- [ ] **Step 2: Update the dev dump to list non-combatants' shelter state too**

In `src/LivingWorld.RimWorld/OutfitStandDebugActions.cs`, the `DumpMobilizationPhases` loop currently only prints combat predicates per colonist. It already calls `component.DiagnosePawn(pawn)`; since Task 7 Step 1 enriched `DiagnosePawn` with `fighter=`/`inShelter=`/`shelteredByUs=`, no code change is strictly required here — but confirm the per-pawn dump line includes `component.DiagnosePawn(pawn)` (it does from Phase A). If it does not, append `+ component.DiagnosePawn(pawn)` to the per-pawn `Log.Message`. No commit-only-if-changed.

- [ ] **Step 3: Build to verify zero warnings**

Run: `dotnet build src/LivingWorld.RimWorld/LivingWorld.RimWorld.csproj -c Debug`
Expected: `Предупреждений: 0` / `Ошибок: 0`.

- [ ] **Step 4: Run the full test suite (nothing regressed)**

Run: `dotnet run --project src/LivingWorld.Tests -c Debug`
Expected: `395 test(s) passed.`

- [ ] **Step 5: Commit**

```bash
git add src/LivingWorld.RimWorld/MobilizationMapComponent.cs src/LivingWorld.RimWorld/OutfitStandDebugActions.cs
git commit -m "feat(arsenal): drive non-combatant shelter each recheck, persist + diagnose it"
```

---

## Phase B simplifications (documented, deferred to Phase D)
- **Rescue-lockout escape hatch:** the shelter uses the Home area as the fallback (large, so a downed colonist is usually reachable), and doctors mid-tend/rescue are protected by the `IsBusyUrgent` guard — but a *tight painted* shelter zone can still leave a colonist downed outside it unreachable. The full "detect downed-outside-shelter → lift the nearest non-combatant's restriction" hatch is Phase D.
- **Shelter-is-the-danger-zone** (infestation / containment breach inside the shelter) — Phase D.
- **Animals sheltering** — Phase D (same allowed-area mechanic on player animals).

---

## Self-Review

**1. Spec coverage** (against `2026-07-14-arsenal-expansion-design.md`, Phase B):
- Fighters roster (assignment + persistence) → Task 2 (data) + Task 4 (gizmo). ✅
- `IsFighter`/`IsNonCombatant` → Task 2 + Task 3. ✅
- `ShelterPlan` (pure) → Task 1. ✅
- Non-combatant allowed-area driver with save/restore → Task 5 (area service) + Task 6 (driver) + Task 7 (persistence). ✅
- Urgent-job guard (doctors keep working) → `ShelterPlan` uses `IsBusyUrgent`; wired in Task 6. ✅
- Rescue-not-blocked hatch → documented as Phase D simplification (Home-area fallback mitigates). Acceptable per spec's phasing.
- Diagnostics (role + shelter) → Task 7. ✅

**2. Placeholder scan:** No TBD/TODO; every code step has complete code; commands have expected output. ✅

**3. Type consistency:** `ShelterPhase`/`NonCombatantState` fields match across Tasks 1 and 6. `FightersRoster.Get/IsFighter/Toggle` (Task 2) used in Tasks 3, 4. `MobilizationCandidates.IsNonCombatant` (Task 3) used in Tasks 6, 7. `ShelterAreaService.*` (Task 5) used in Tasks 6, 7. `ShelterDriver.Drive/ExportPrevAreas/ImportPrevAreas/WeChangedArea` (Task 6) used in Task 7. Test count chains 389 → 395. ✅
