# Armory / Colony Mobilization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Colonists work in civvies unarmed and, on a threat or a manual toggle, autonomously fetch skill-appropriate gear from armory racks, equip, then disarm/re-dress on stand-down.

**Architecture:** New `LivingWorld.Armory` module inside the existing Living World mod (same assembly). Pure skill-aware selection lives in `LivingWorld.Core` (net8-testable via abstracted inputs); RimWorld state/buildings/AI live in `LivingWorld.RimWorld`. Foundation (state, racks, selection, UI, manual mechanics) is verifiable; the autonomous ThinkTree AI is headless-unverifiable and shipped last for live tuning.

**Tech Stack:** C#/.NET (net8 tests + net472 RimWorld), RimWorld 1.6 (`MapComponent`, `Building_Storage`, `ThinkNode`/`JobGiver`/`JobDriver`, `Pawn_EquipmentTracker`, `Pawn_ApparelTracker`), Harmony, XML Defs, EN/RU keyed.

## Global Constraints

- Player colony only; active-map feature. Never touch NPC/world-sim.
- Fail-open/fail-safe: any AI/job failure must not stick a pawn; behaves as vanilla.
- 0 build warnings (`net472`). EN↔RU keyed parity (`TestKeyedLanguageParity`).
- Deterministic Core (no RNG/DateTime); pure selection logic unit-tested.
- Build/test/ship each phase in an isolated git worktree off `origin/main` — NEVER commit in the shared main dir.
- Reflection-verify every RimWorld API used (`AssertRimWorldMethodExists`).

## File Structure

- `src/LivingWorld.Core/ArmoryLoadout.cs` — pure records + `LoadoutSelectionService` (skill→choice), `MobilizationTuning` constants.
- `src/LivingWorld.RimWorld/MobilizationMapComponent.cs` — per-map state: manual flag, threat auto-detect (cached), `IsMobilized`; persisted.
- `src/LivingWorld.RimWorld/Building_ArmoryRack.cs` — `Building_Storage` subclass for the three rack kinds (kind from def extension/filter).
- `src/LivingWorld.RimWorld/MobilizationGizmoPatch.cs` — colony toggle gizmo (Harmony on `Pawn.GetGizmos` for a colonist, or a `Designator`/`MainButton`).
- `src/LivingWorld.RimWorld/Armory/LoadoutAdapter.cs` — builds `WeaponOption`/`ArmorOption` from racks' stored things + reads pawn skills → calls Core.
- `src/LivingWorld.RimWorld/Armory/ThinkNode_Mobilized.cs`, `JobGiver_FetchKit.cs`, `JobGiver_ReturnKit.cs`, `JobDriver_FetchKit.cs`, `JobDriver_ReturnKit.cs` — autonomous AI.
- `mod/Defs/ThingDefs_Armory/LivingWorld_ArmoryRacks.xml` — 3 rack buildings.
- `mod/Defs/JobDefs/LivingWorld_ArmoryJobs.xml` — fetch/return job defs.
- `mod/Patches/LivingWorld_ThinkTree.xml` — inject `ThinkNode_Mobilized` into colonist think tree.
- `mod/Languages/{English,Russian}/Keyed/LivingWorld.xml` — keys.
- `src/LivingWorld.Tests/Program.cs` — unit + structural + reflection tests.

---

## Phase 1 — Mobilization state + manual toggle + threat detect (verifiable)

### Task 1: MobilizationMapComponent

**Files:**
- Create: `src/LivingWorld.RimWorld/MobilizationMapComponent.cs`
- Test: `src/LivingWorld.Tests/Program.cs` (structural)

**Interfaces:**
- Produces: `MobilizationMapComponent : MapComponent` with `bool ManualMobilized`, `bool ThreatPresent` (cached, recomputed on a throttle), `bool IsMobilized => ManualMobilized || ThreatPresent`, `void ToggleManual()`; persisted via `ExposeData`; static `Instance(Map)` helper.

Steps:
- [ ] Structural test asserts the class, `IsMobilized`, `ExposeData`, throttled recompute (`Find.TickManager`), fail-safe try/catch.
- [ ] Implement `MobilizationMapComponent`: `MapComponentTick` recomputes `ThreatPresent = map.attackTargetsCache/any hostile pawn` on a ~250-tick throttle inside try/catch (fail → false). `ExposeData` scribes `manualMobilized`.
- [ ] Reflection-verify `AssertRimWorldMethodExists("Verse.MapComponent","MapComponentTick")` and hostile-detection API.
- [ ] Build net472 (0/0), run tests, commit in worktree.

### Task 2: Colony toggle gizmo + indicator

**Files:**
- Create: `src/LivingWorld.RimWorld/MobilizationGizmoPatch.cs`
- Modify: `mod/Languages/.../LivingWorld.xml` (keys `LW_MobilizeToggle`, `LW_MobilizeTooltip`, `LW_MobilizedBy_Threat`, `LW_MobilizedBy_Manual`, `LW_StandDown`)

**Interfaces:**
- Consumes: `MobilizationMapComponent`.
- Produces: a `Command_Toggle` gizmo on colonists reflecting/flipping `ManualMobilized`, label shows current cause.

Steps:
- [ ] Structural test: gizmo patch references `MobilizationMapComponent`, `Command_Toggle`, the keys; EN/RU keys present.
- [ ] Harmony Postfix on `Pawn.GetGizmos` (colonist, player faction) appending a `Command_Toggle` bound to the map's `ManualMobilized`; tooltip shows `IsMobilized` + cause.
- [ ] Reflection-verify `AssertRimWorldMethodExists("Verse.Pawn","GetGizmos")`.
- [ ] Build/test/commit.

---

## Phase 2 — Armory racks + skill-aware selection (verifiable)

### Task 3: LoadoutSelectionService (Core, pure)

**Files:**
- Create: `src/LivingWorld.Core/ArmoryLoadout.cs`
- Test: `src/LivingWorld.Tests/Program.cs` (unit)

**Interfaces:**
- Produces:
  - `record WeaponOption(string DefName, bool IsRanged, int Value)`
  - `record ArmorOption(string DefName, int Value, bool IsHeavy)`
  - `static class MobilizationTuning { const int CombatSkillThreshold = 4; }`
  - `static bool LoadoutSelectionService.IsCombatEligible(int shooting, int melee)` → `Math.Max(shooting,melee) >= threshold`
  - `static WeaponOption? SelectWeapon(int shooting, int melee, WeaponOption? assigned, IReadOnlyList<WeaponOption> pool)` — assigned wins; else prefer ranged when `shooting>=melee`, else melee; best = highest Value, tie by DefName ordinal.
  - `static ArmorOption? SelectArmor(int shooting, int melee, ArmorOption? assigned, IReadOnlyList<ArmorOption> pool)` — assigned wins; else best; heavy preferred when melee>shooting.

Steps:
- [ ] Unit tests: eligibility threshold; shooter gets ranged; brawler gets melee; assigned overrides; empty pool → null; tie-break by DefName; heavy-armor preference for brawler.
- [ ] Implement the pure functions (deterministic, LINQ with stable `ThenBy(DefName, Ordinal)`).
- [ ] Run tests (net8), commit.

### Task 4: Armory rack buildings

**Files:**
- Create: `src/LivingWorld.RimWorld/Building_ArmoryRack.cs`
- Create: `mod/Defs/ThingDefs_Armory/LivingWorld_ArmoryRacks.xml` (WeaponRack, ArmorRack, ApparelRack)
- Modify: EN/RU keys (labels/descriptions handled via DefInjected for RU)

**Interfaces:**
- Produces: `Building_ArmoryRack : Building_Storage` with a `RackKind { Weapon, Armor, Apparel }` from a `DefModExtension`; exposes `IEnumerable<Thing> StoredItems`.

Steps:
- [ ] Structural test: def file has 3 racks with `thingClass` = `LivingWorld.RimWorld.Building_ArmoryRack`, `<building><fixedStorageSettings>` filtered by category (Weapons / Apparel-armor / Apparel-clothing); `Building_ArmoryRack : Building_Storage`.
- [ ] Implement the class + defs (reuse a vanilla shelf graphic for v1; craftable at a workbench, steel cost).
- [ ] RU DefInjected for the 3 rack labels/descriptions.
- [ ] Reflection-verify `AssertRimWorldMethodExists("RimWorld.Building_Storage","Notify_ReceivedThing")` (or a Building_Storage member).
- [ ] Build/test/commit.

---

## Phase 3 — Reliable equip/return mechanics (verifiable-ish)

### Task 5: LoadoutAdapter + manual equip/return

**Files:**
- Create: `src/LivingWorld.RimWorld/Armory/LoadoutAdapter.cs`

**Interfaces:**
- Consumes: `LoadoutSelectionService`, `Building_ArmoryRack`, pawn skills.
- Produces:
  - `static (Thing? weapon, Thing? armor) LoadoutAdapter.ResolveKit(Pawn pawn, Map map)` — scans racks, builds options, calls Core selection, maps back to real `Thing`s.
  - `static bool EquipKit(Pawn pawn, Thing? weapon, Thing? armor)` — `Equip` weapon, `Wear` armor, dropping conflicting civvie apparel to an apparel rack. Fail-safe.
  - `static void ReturnKit(Pawn pawn, Map map)` — unequip weapon + armor, haul to racks, re-wear civvies. Fail-safe.

Steps:
- [ ] Structural test: adapter references `LoadoutSelectionService`, `Pawn_EquipmentTracker`, `Pawn_ApparelTracker`, `Building_ArmoryRack`.
- [ ] Implement `ResolveKit`/`EquipKit`/`ReturnKit` (all try/caught; skill read via `pawn.skills.GetSkill(SkillDefOf.Shooting/Melee).Level`).
- [ ] Reflection-verify `Pawn_EquipmentTracker.AddEquipment/MakeRoomFor`, `Pawn_ApparelTracker.Wear/TryDrop`, `SkillDefOf.Shooting/Melee`.
- [ ] Build/test/commit.

---

## Phase 4 — Autonomous AI (headless-unverifiable → live tuning)

### Task 6: ThinkNode + JobGivers + JobDrivers + ThinkTree patch + job defs

**Files:**
- Create: `src/LivingWorld.RimWorld/Armory/ThinkNode_Mobilized.cs`, `JobGiver_FetchKit.cs`, `JobGiver_ReturnKit.cs`, `JobDriver_FetchKit.cs`, `JobDriver_ReturnKit.cs`
- Create: `mod/Defs/JobDefs/LivingWorld_ArmoryJobs.xml`
- Create: `mod/Patches/LivingWorld_ThinkTree.xml`

**Interfaces:**
- Consumes: `MobilizationMapComponent`, `LoadoutAdapter`.
- Produces: think-tree behaviour — mobilized+eligible+unarmed → fetch job; stood-down+armed → return job.

Steps:
- [ ] Structural test: think-tree PatchOperation targets the colonist tree and inserts `ThinkNode_Mobilized` above work; job defs exist; JobGivers gate on `MobilizationMapComponent.IsMobilized`.
- [ ] Implement `ThinkNode_Mobilized` (returns a fetch/return JobGiver result only when state matches, else `ThinkResult.NoJob`); JobDrivers compose path→take→`EquipKit`/`ReturnKit` via `LoadoutAdapter`, all fail-safe (bad state → `EndJobWith(JobCondition.Incompletable)`).
- [ ] Reflection-verify `Verse.AI.ThinkNode`, `JobGiver_Work`, `JobDriver`, `Toils_Goto`.
- [ ] Build/test/commit.
- [ ] **LIVE TUNING REQUIRED** — verify in-game: mobilize → colonists fetch/equip; stand-down → return/redress; no stuck pawns. Iterate.

---

## Phase 5 — Pre-expedition arming + polish

### Task 7: Arm before caravan/expedition

**Files:**
- Create/Modify: a Harmony hook on caravan formation to force `FetchKit` on departing colonists first.

Steps:
- [ ] Structural test for the caravan hook.
- [ ] Implement: on `Dialog_FormCaravan` confirm (or `CaravanFormingUtility`), queue fetch-kit for eligible departing pawns.
- [ ] Reflection-verify the caravan-formation API.
- [ ] Build/test/commit + settings toggle `armoryMobilizationEnabled` + EN/RU + final polish.

---

## Self-Review

- **Spec coverage:** state (T1) ✓, toggle+trigger (T1/T2) ✓, racks (T4) ✓, skill loadout (T3/T5) ✓, autonomous AI (T6) ✓, expedition arming (T7) ✓, UI (T2/assignment — assignment tab deferred to a follow-up, noted YAGNI in spec §5), tests each task ✓.
- **Placeholders:** none — concrete files, interfaces, and behaviours per task.
- **Type consistency:** `WeaponOption`/`ArmorOption`/`LoadoutSelectionService.Select*` names match across T3→T5→T6.
- **Gap noted:** per-pawn assignment UI (spec §3.5) is minimized to "assigned kit wins if present" in selection; the assignment *tab* is a follow-up task, not blocking the autonomous foundation.

## Execution

Foundation phases (1–3) are verifiable and shipped first; Phase 4 (AI) ships last and needs live tuning. Each task built/tested/committed in an isolated worktree off `origin/main`.
