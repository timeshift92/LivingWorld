# Settlement Lifecycle Sync — Design

**Date:** 2026-07-14
**Branch:** `codex/settlement-lifecycle-sync` (off `origin/main`)
**Status:** Design approved (pending spec review)

## Problem

Living World imports vanilla NPC settlements into its ledger (`WorldState.Settlements`)
**exactly once**, at `LivingWorldWorldComponent.FinalizeInit` →
`BootstrapFromRimWorldSettlements` (via `WorldObjectScanner`). There is no incremental
sync afterward.

Other mods create and destroy real `RimWorld.Planet.Settlement` world objects at
runtime. Confirmed example: **Economics & Demography** (`helldan.economicsdemography`)
in `WorldPopulationManager.ProcessDailyGrowth` calls
`WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement)` and `settlement.Destroy()`.
Rim War and Empire do the same (add/remove settlements; Empire/Rim War also **capture**
settlements, changing a settlement's `Faction` on the existing world object).

Consequences today:

- **(a) Missed additions.** Settlements created after `FinalizeInit` never enter the
  ledger. They are invisible to the world simulation (no citizens, no production, no
  events, never selectable as war targets).
- **(b) Ghost ledger entries.** Settlements destroyed after `FinalizeInit` leave an
  orphaned **active** `WorldSettlement` whose `Slug` still encodes a now-missing
  faction+tile. `ResolveLedgerSettlement` (defeat patch) then returns `null`. No crash —
  LW null-checks — but the ledger entry lingers forever, never receiving a
  `SettlementDestroyed` event.
- **(c) Stale ownership.** When a settlement is captured (faction changed on the same
  world object), the ledger keeps the old `FactionId`.

This is a general mod-compatibility gap, not ED-specific.

## Key facts established from the code

- `WorldSettlement` (Core) is keyed by `EntityId` and carries `Slug`, `Name`,
  `FactionId`, `Status`. **It has no Tile field.** The physical tile is encoded in the
  `Slug`, which equals the scanner `StableKey`: `worldobject:{defName}:{tile}:{factionId}`.
  `LivingWorldWorldComponent.ParseSettlementTile` extracts `{tile}` from position 2.
- `SettlementLifecycleService.DestroySettlement` **already exists**, is **idempotent**
  (returns the existing ruin when `Status == Destroyed`), and produces ruin + refugees +
  `SettlementDestroyed` event.
- `WorldEventKind` already defines `SettlementFounded`, `SettlementCaptured`,
  `SettlementDestroyed`, `SettlementAbandoned`. `WorldState.SetSettlementFactionAndStatusForLedger`
  (internal) already exists and is used by `ReclaimRuin`.
- LW **never creates a physical `Settlement`** itself. All its `WorldObjectMaker`
  usages are custom types (`WorldObject_LivingWorldArmy`, visit sites, ruin `Site`s).
  ⇒ An `Add` hook filtered to `Settlement` will never re-import LW's own objects.
- **Visiting a settlement does not remove its world object.**
  `LivingWorldSettlementMapMaterializationService` only destroys materialized *pawns*
  and calls `Building.SetFactionDirect` (not `WorldObject.SetFaction`). The physical
  `Settlement` persists throughout a visit. ⇒ A daily scan never transiently "loses" a
  visited settlement, and the SetFaction hook never fires for map buildings.
- The existing `SettlementDefeatUtility.CheckDefeated` prefix
  (`LivingWorldSettlementDefeatPatch`) marks the ledger `Destroyed` (via the same
  `DestroySettlement`) **before** vanilla removes the settlement, and
  `ResolveLedgerSettlement` filters `IsActive`. ⇒ Our removal hook and the defeat patch
  compose safely in any order.

## Design decisions (approved)

1. **Removal semantics:** full destruction — reuse `DestroySettlement` (ruin + refugees +
   event). Consistent with the player-defeat path.
2. **Add seeding:** full seed — population + production profile + starting resources, the
   same code path bootstrap uses.
3. **Faction change:** in scope. Update the ledger `FactionId` when a settlement is
   captured; if captured **by the player**, remove it from the NPC simulation (LW
   excludes player settlements by design, gap G1).
4. **Mechanism:** **hybrid** — a pure, testable reconciliation core in
   `LivingWorld.Core` plus thin Harmony postfixes as immediate triggers, plus a daily
   safety-net pass aligned with LW's existing daily simulation cadence.

## Architecture

```
RimWorld layer (thin glue, not unit-tested)          Core (pure, unit-tested)
────────────────────────────────────────             ────────────────────────
Harmony postfixes:                                    SettlementSlug.ParseTile(slug)
  WorldObjectsHolder.Add     ─┐                       WorldState.FindActiveSettlementByTile
  WorldObjectsHolder.Remove  ─┤ guard + try/catch     SettlementReconciliationService
  WorldObject.SetFaction     ─┘  → coordinator          .ComputePlan(physicalFacts, state)
                                                          → {Imports, Destructions, FactionChanges}
SettlementSyncCoordinator                             SettlementLifecycleService
  OnSettlementAdded/Removed/FactionChanged              .DestroySettlement   (exists)
  ReconcileNonDestructive()  (daily)                    .ChangeSettlementFaction (new)
  ReconcileWithDestructions() (once, on load)         WorldState.SetSettlementFactionAndStatusForLedger (exists)
  → builds facts via WorldObjectScanner
  → applies plan via component seeding + services
LivingWorldWorldComponent
  SeedImportedSettlement(candidate)  (extracted from bootstrap, reused)
```

### Core components (testable, no RimWorld dependency)

**`SettlementSlug` (new, `LivingWorld.Core`)**
Static helper. `int ParseTile(string? slug)` — extracts `{tile}` from
`worldobject:{defName}:{tile}:{factionId}` (returns `-1` on failure). Replaces the
duplicated `LivingWorldWorldComponent.ParseSettlementTile` and the defeat patch's
inline tile-token logic.

**`WorldState.FindActiveSettlementByTile(int tile)` (new method)**
Returns the single active `WorldSettlement` whose `Slug` parses to `tile`, else `null`.
The one resolver used by the coordinator, reconciliation, and (refactored) the defeat
patch. Tile is unique per physical settlement world object, so this is unambiguous.

**`SettlementLifecycleService.ChangeSettlementFaction(state, id, newFactionId, tick, reason)` (new)**
Updates the ledger `FactionId` via `SetSettlementFactionAndStatusForLedger(id,
newFactionId, Active)` and records a `SettlementCaptured` event. Idempotent: no-op when
the faction already matches. (Note: the `Slug`'s embedded faction token becomes stale,
but resolution keys on the **tile** token, not faction, so future resolves keep working.)

**`SettlementLifecycleService.AbandonSettlement(state, id, tick, reason)` (new)**
Marks the ledger entry `Abandoned` via `SetSettlementLifecycleStatusForLedger` and
records a `SettlementAbandoned` event — **no ruin, no refugees**. Used when a settlement
is captured by the player: the physical settlement still exists (now the player's) but
must leave the NPC simulation. Idempotent when already `Abandoned`/`Destroyed`.

**`SettlementReconciliationService` (new, `LivingWorld.Core`)** — the pure diff core.
- Input record `PhysicalSettlementFact(string StableKey, string Name, string FactionId, int Tile)`.
- `SettlementReconciliationPlan ComputePlan(IReadOnlyList<PhysicalSettlementFact> physical, WorldState state, bool includeDestructions)`.
- Output `SettlementReconciliationPlan(IReadOnlyList<PhysicalSettlementFact> Imports, IReadOnlyList<EntityId> Destructions, IReadOnlyList<(EntityId Id, string NewFactionId)> FactionChanges)`.
- Matching is by **tile**:
  - physical tile with no active ledger settlement → **Import**.
  - active ledger settlement whose tile has no physical fact → **Destruction** (only when
    `includeDestructions` is `true`).
  - matched pair, differing `FactionId` → **FactionChange**.
  - matched pair, same faction → no action.
  - already-`Destroyed`/`Abandoned` ledger entries are ignored (never re-destroyed).
- No mutation, no RimWorld types → fully unit-testable.

### RimWorld components (thin glue)

**`LivingWorldWorldComponent.SeedImportedSettlement(WorldObjectSettlementCandidate)` (extracted)**
The per-candidate seeding currently inlined in `BootstrapFromRimWorldSettlements`
(faction lookup → `CreateSettlement(StableKey, …)` → population seeding → production
profile → resources) is extracted into one reusable method. `BootstrapFromRimWorldSettlements`
calls it per candidate (wrapped in `RunInitialWorldSeeding`, unchanged). Runtime imports
call it **without** `RunInitialWorldSeeding`, so a genuine `SettlementCreated`/`Founded`
event fires for the newly-appeared settlement.

**`SettlementSyncCoordinator` (new, `LivingWorld.RimWorld`)**
Owns the RimWorld↔ledger glue and a re-entrancy guard flag.
- `OnSettlementAdded(Settlement)` — build a candidate via `VanillaSettlementImporter`;
  if no active ledger entry at its tile, `SeedImportedSettlement`.
- `OnSettlementRemoved(Settlement)` — resolve active ledger by faction+tile; if found,
  `DestroySettlement`. (Fires on the actual removal event; authoritative for removals.)
- `OnSettlementFactionChanged(Settlement)` — resolve by tile; if the new faction is the
  player → remove from NPC sim (abandon, no ruin/refugees); else `ChangeSettlementFaction`.
- `ReconcileNonDestructive()` — daily: scan physical facts, `ComputePlan(includeDestructions:false)`,
  apply Imports + FactionChanges. Safety net for events the hooks missed; never destroys
  (avoids any false positive from transient scan state).
- `ReconcileWithDestructions()` — run **once** at `FinalizeInit(fromLoad)` after bootstrap:
  `ComputePlan(includeDestructions:true)` and apply all three. At load time every real
  settlement is present and scannable, so destruction is safe — this migrates existing
  saves by clearing pre-fix ghost entries.

**Harmony patches (new `LivingWorldSettlementLifecyclePatch.cs`, `LivingWorld.RimWorld`)**
Three postfixes, each thin:
- `[HarmonyPatch(typeof(WorldObjectsHolder), nameof(Add))]` → `OnSettlementAdded`.
- `[HarmonyPatch(typeof(WorldObjectsHolder), nameof(Remove))]` → `OnSettlementRemoved`.
- `[HarmonyPatch(typeof(WorldObject), nameof(SetFaction))]` → `OnSettlementFactionChanged`.

Shared guard for every patch (fail-closed on guard, fail-open on error):
1. `Current.ProgramState == ProgramState.Playing` — skips world generation (`Entry`) and
   save-load/teardown (`MapInitializing`); those are covered by bootstrap.
2. `LivingWorldWorldComponent.Instance` non-null, `bootstrapped`, `State != null`.
3. object `is Settlement` and `!Faction.IsPlayer`.
4. Whole body in `try/catch` → log a warning under `debugLogging`, never throw into vanilla.
5. Coordinator re-entrancy flag prevents nested processing.

### Wiring into `LivingWorldWorldComponent`

- `FinalizeInit`: after the existing bootstrap/repair chain, call
  `coordinator.ReconcileWithDestructions()` once (ghost cleanup / save migration).
- Daily simulation gate (where `ProcessDailyGrowth`-style daily work runs): call
  `coordinator.ReconcileNonDestructive()`.
- Refactor `LivingWorldSettlementDefeatPatch.ResolveLedgerSettlement` to delegate to
  `WorldState.FindActiveSettlementByTile` (dedupe; behavior preserved).

## Interactions & edge cases

- **Player defeats an NPC base:** `CheckDefeated` prefix marks the ledger `Destroyed`
  first; the subsequent `Remove` postfix resolves `IsActive == false` → no-op. Safe.
- **Capture to player (Empire):** `SetFaction` postfix sees `IsPlayer` → abandon the
  ledger entry (no ruin/refugees). The physical settlement remains, now player-owned and
  outside the NPC sim, per design G1.
- **Runtime creation ordering (ED):** `MakeWorldObject` → `SetFaction` → `Add`. The
  `SetFaction` postfix runs before the entry exists (resolve-by-tile finds nothing →
  no-op); the `Add` postfix then imports it with the correct faction. No double work.
- **LW's own world objects:** armies, visit sites, ruin `Site`s are not `Settlement` →
  filtered out of all three hooks. No re-entrancy through Add.
- **Daily pass never destroys** → a briefly-unscannable settlement is never wrongly
  ruined. Removal is driven solely by the authoritative `Remove` event (plus the
  one-shot load-time migration pass).

## Testing strategy

**Core unit tests (`LivingWorld.Tests`, the existing console runner):**
- `SettlementSlug.ParseTile`: valid slug, malformed slug, empty/null.
- `WorldState.FindActiveSettlementByTile`: match, no-match, ignores destroyed/abandoned.
- `SettlementLifecycleService.ChangeSettlementFaction`: updates faction + records
  `SettlementCaptured`; no-op when unchanged.
- `SettlementLifecycleService.AbandonSettlement`: marks `Abandoned` + records
  `SettlementAbandoned`; no ruin/refugees; idempotent.
- `SettlementReconciliationService.ComputePlan`, covering each branch:
  - new physical, no ledger → Import.
  - active ledger, no physical, `includeDestructions:true` → Destruction;
    `includeDestructions:false` → nothing.
  - matched, faction differs → FactionChange.
  - matched, same faction → nothing.
  - destroyed/abandoned ledger entry + no physical → nothing (no re-destroy).
  - mixed multi-settlement scenario producing all three action kinds at once.

**Not unit-tested (require the running game):** the three Harmony postfixes and the
coordinator's RimWorld glue. Kept deliberately thin. Verified manually in-game:
install alongside ED, observe (with `debugLogging`) a runtime-created settlement gaining
a ledger entry + citizens, a runtime-destroyed settlement producing a ruin + refugees +
`SettlementDestroyed` event, and a captured settlement's `FactionId` updating.

## Out of scope (YAGNI)

- Mods that mutate a settlement's faction field directly, bypassing `SetFaction` (rare;
  the daily faction-sync pass still catches these on the next day).
- Reconciling any world-object type other than `Settlement`.
- New persisted save state — reconciliation is fully derived from the physical world +
  ledger; nothing new to serialize.

## Files touched

- New: `src/LivingWorld.Core/SettlementSlug.cs`
- New: `src/LivingWorld.Core/SettlementReconciliationService.cs`
- Edit: `src/LivingWorld.Core/WorldState.cs` (add `FindActiveSettlementByTile`)
- Edit: `src/LivingWorld.Core/SettlementLifecycleService.cs` (add `ChangeSettlementFaction`)
- New: `src/LivingWorld.RimWorld/SettlementSyncCoordinator.cs`
- New: `src/LivingWorld.RimWorld/LivingWorldSettlementLifecyclePatch.cs`
- Edit: `src/LivingWorld.RimWorld/LivingWorldWorldComponent.cs` (extract
  `SeedImportedSettlement`; wire FinalizeInit + daily reconcile; use `SettlementSlug`)
- Edit: `src/LivingWorld.RimWorld/LivingWorldSettlementDefeatPatch.cs` (delegate resolve)
- Edit: `src/LivingWorld.Tests/Program.cs` (register new tests)
