# Arsenal v2 — Colony Mobilization Redesign

**Date:** 2026-07-10
**Status:** Approved design, ready for implementation plan
**Supersedes:** `2026-07-09-armory-mobilization-design.md` (Outfit-Stand-swap module the user found unreliable)

## Problem

The current "Arsenal" (colony mobilization) module works poorly. It was built
iteratively and blind (Claude cannot playtest RimWorld), churning through many
mechanics (shared racks → outfit stands → weapon auto-pick → all-on-stand →
apparel policies). Pawns don't reliably mobilize, equip, fight, or stand down.
The only part the user is happy with is the dev-mode test-gear spawner.

The root cause is a "guess → deploy → user reports broken → guess again" loop
that never converges, because correctness depended on fragile custom pawn
behavior that cannot be verified headless.

## Goal

The colony works in civilian clothes in peacetime. On a **real threat** or a
**manual toggle**, combat colonists become **battle-ready** (equip their kit
from their personal outfit stand, then fight autonomously via CAI). After the
threat passes they **stand down** to civilian clothes and return the kit — with
**no per-pawn micromanagement** and, critically, **no accidental armor in
peacetime**.

## Design Principles (why this will converge where the old one didn't)

1. **Layer a robust safety net under the fragile mechanic.** The outfit-stand
   swap is the fragile part that kept breaking. We keep it (user wants personal
   mannequins) but add an independent apparel-policy layer that *guarantees* the
   correct armor state even when the swap glitches.
2. **One action per pawn per tick, idempotent.** Every past bug lived in
   sequencing. The driver is an explicit phase machine that re-derives each
   pawn's phase from live state every tick and issues at most one job.
3. **Equip strictly before combat.** Drafting disables vanilla auto-dress; a
   pawn must be fully equipped before it is drafted or given a combat duty.
4. **Diagnostics first.** Every phase transition emits one structured log line
   so failures are debugged from facts, not guesses.
5. **Fail-safe everywhere.** Missing mannequin, missing CAI, incapable pawn,
   pacifist — each degrades gracefully, never throws.

## Key Facts Established by Code Inspection

- `RimWorld.IApparelSource` is implemented **only** by `Building_OutfitStand` /
  `Building_KidOutfitStand`. Wearing apparel off a stand is the dedicated
  `JobDriver_UseOutfitStand` swap job — **`JobGiver_OptimizeApparel` does not
  auto-wear from a stand**. Vanilla apparel optimization only dresses/undresses
  from **general storage**, driven by the pawn's apparel policy.
- **User decision:** the combat kit lives **only on the pawn's personal outfit
  stand** (not general storage). Therefore the swap job is the equip mechanism;
  the apparel policy is a strip-only safety net.
- **CAI 5000** (`packageId Krkr.rule56`, `CombatAI.dll`) exposes
  `CombatAI.CustomDutyUtility` with `TryStartCustomDuty(Pawn, CustomPawnDuty,
  bool)`, and duty factories `HuntDownEnemies(...)`, `DefendPoint(...)`,
  `AssaultPoint(...)`. These give a pawn an **autonomous** combat duty (pawn
  stays undrafted and fights on its own). Requires **Prepatcher**
  (`zetrith.prepatcher`).

## Dependencies

- **Odyssey DLC** — **hard**. Outfit stands are required; the whole module is
  gated off when `!ModsConfig.OdysseyActive`.
- **CAI 5000 + Prepatcher** — **optional (soft)**. Accessed via reflection. When
  absent, the combat step falls back to drafting the pawn. No hard assembly
  reference to `CombatAI.dll`.

## Architecture

New/updated units under the existing `LivingWorld.RimWorld` assembly (plus pure
logic in `LivingWorld.Core`). Each unit has one responsibility.

### 1. `MobilizationState` (MapComponent) — trigger + state

- Fields: `manualMobilized` (toggle), `threatPresent` (derived), and a
  per-pawn phase cache keyed by `thingIDNumber` (Scribe `LookMode.Value`, never
  by Pawn reference — old saves broke on reference keys).
- `IsMobilized => manualMobilized || threatPresent`.
- Threat detection: throttled (~250 ticks). `threatPresent` is true when there
  is a genuine hostile threat on the map — reuse RimWorld's danger signal
  (`Map.dangerWatcher.DangerRating >= StoryDanger.Low`) rather than a hand-rolled
  hostile scan, so a single wandering manhunter does not mobilize the colony.
- Manual toggle gizmo (mod-agnostic label/icon) on the colony/UI, plus the
  existing settings flags.
- Emits a state-change diagnostic line when `IsMobilized` flips.

### 2. `MobilizationPolicyService` — the apparel-policy safety net

- Lazily creates and caches two `ApparelPolicy` objects via
  `Current.Game.outfitDatabase`:
  - **"LW Combat"** — `filter.SetAllow(ThingCategoryDefOf.ApparelArmor, true)`.
  - **"LW Civilian"** — `filter.SetAllow(ThingCategoryDefOf.ApparelArmor, false)`.
- `ApplyCombat(pawn)` / `ApplyCivilian(pawn)` set
  `pawn.outfits.CurrentApparelPolicy`. Idempotent (no-op if already set).
- On first use, stores the pawn's original policy id keyed by `thingIDNumber`
  so it can be restored when the pawn stops being a mobilization candidate.
- **Role:** Combat policy prevents vanilla from stripping just-donned armor;
  Civilian policy *strips* any lingering armor when the swap fails. It is a
  guarantee, not the equip mechanism.

### 3. `OutfitStandKit` — stand lookup + swap driver

- `FindStand(pawn)` → the pawn's assigned `Building_OutfitStand` via
  `GetComp<CompAssignableToPawn>().AssignedPawnsForReading`.
- `IsInCombatKit(pawn, stand)` → true when the pawn is wearing the stand's
  stored armor and holding its stored weapon (i.e., the swap has completed).
- `PushEquip(pawn, stand)` / `PushReturn(pawn, stand)` → push a single
  `JobDefOf.UseOutfitStand` job in the correct direction; no-op if a matching
  job is already queued/running (idempotent).
- Direction is derived from `IsInCombatKit`, not from a stored flag (avoids the
  "dressed backwards" bug).

### 4. `CaiBridge` — optional autonomous-combat hook (reflection)

- Resolves `CombatAI.CustomDutyUtility` once and caches the `MethodInfo`s. If
  CAI is not loaded, `Available == false`.
- `TryEngage(pawn)` → builds a `HuntDownEnemies`/`DefendPoint` duty aimed at the
  current threat's area and calls `TryStartCustomDuty`. Returns false if
  unavailable or on any reflection error (caller then drafts).
- Duties expire; the driver re-invokes `TryEngage` while the pawn is mobilized
  and equipped and has no active LW duty.

### 5. `MobilizationCandidates` — eligibility (pure, in Core where possible)

- `IsCandidate(pawn)` → colonist, spawned, **not** `WorkTagIsDisabled(
  WorkTags.Violent)`, has a `drafter`, meets the configured skill threshold
  (`LivingWorldSettings.mobilizationSkillThreshold`).
- `IsBusyUrgent(pawn)` → currently fighting fire / tending / rescuing — the
  driver leaves these alone (does not yank them mid-task).

### 6. `MobilizationDriver` — the phase machine (called from MapComponent tick)

Throttled tick. For each candidate pawn (snapshot with `.ToList()` — never
iterate live `FreeColonistsSpawned` while mutating), derive the phase from live
state and issue **at most one** action:

**When `IsMobilized`:**
1. If asleep → wake (`RestUtility.WakeUp`). *(one action; return)*
2. If policy != Combat → `ApplyCombat`. *(return)*
3. If `!IsInCombatKit` → `PushEquip`. *(return)*
4. Equipped and CAI available and no active LW duty → `CaiBridge.TryEngage`.
   *(return)*
5. Equipped and CAI unavailable and not drafted → draft. *(return)*
6. Otherwise: nothing to do (steady combat state).

**When not `IsMobilized` (stand-down):**
1. Has active LW duty / drafted-by-us → clear duty / undraft. *(return)*
2. If policy != Civilian → `ApplyCivilian`. *(return)*
3. If `IsInCombatKit` → `PushReturn`. *(return)*
4. Civilian policy safety net strips any lingering armor automatically (no
   explicit action).

Every state transition (wake, policy, equip, engage/draft, standdown) emits one
diagnostic line: pawn name, phase, action, and the deciding predicate values.

### 7. Settings (`LivingWorldSettings`)

- `armoryMobilizationEnabled` (default true) — master gate.
- `mobilizationSkillThreshold` (default 4) — min combat skill for a candidate.
- `autoMobilizeOnThreat` (default true) — whether real threats auto-trigger.
- `mobilizationDiagnostics` (default true) — verbose per-pawn logging.

### 8. Dev / diagnostics

- Keep the existing **"Spawn combat test gear"** DebugAction (the one part that
  works well).
- Add a DebugAction that dumps the current phase table (each candidate's derived
  phase + predicate values) for one-shot inspection.

## What Is Retired

- `OutfitStandDriver.cs`, `MobilizationOutfitService.cs`, the old auto-draft-only
  logic, and any remaining shared-rack / custom-job remnants — replaced by the
  units above.
- The `2026-07-09-armory-mobilization-design.md` spec is superseded by this
  document.

## Testing Strategy (honest about headless limits)

- **Unit-testable (Core):** `MobilizationCandidates.IsCandidate/IsBusyUrgent`
  decision logic; phase-derivation given a mocked pawn-state struct. These get
  real xUnit tests.
- **Structural/reflection tests:** the two apparel policies are created with the
  right filter; `CaiBridge` resolves gracefully to `Available == false` when
  `CombatAI` is absent; DefOf/JobDef references resolve; EN/RU translation keys
  exist; zero build warnings.
- **Headless-unverifiable (flagged for live iteration):** the actual swap
  completing, vanilla stripping armor on policy change, CAI taking over. These
  are covered by the diagnostic log rather than automated tests — the user
  plays, the log tells us exactly which phase failed, and we fix from facts.

## Non-Goals (YAGNI)

- No skill-based weapon auto-selection (weapon is whatever the player put on the
  stand).
- No repair/mending mechanic (dropped from the old design).
- No loadout-editor UI.
- No general-storage armor path (user chose stand-only).
- No caravan/expedition pre-arming in this module.
