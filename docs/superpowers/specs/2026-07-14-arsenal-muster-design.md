# Arsenal "Muster & Hold" — Design Spec

**Date:** 2026-07-14
**Module:** Living World / Arsenal (colony mobilization)
**Source:** synthesis of a 6-agent DLL-grounded analysis (CAI duty mechanics, vanilla mechanics, muster-point selection, player UX, phase machine, failure modes).

## Problem

Today, when a threat approaches, mobilized fighters engage **from wherever they stand** — they scatter to meet the enemy in the open. The player wants them to instead **muster**: fall back to a fortified defensive position (a door / wall / chokepoint), take cover, and **hold the line together**, engaging from that concentrated position, then stand down after the fight.

## Decisive technical findings (grounded in the real DLLs)

1. **`MobilizationDriver.Drive` already computes an `anchor` (`= map.Center`)** and passes it to CAI's `DefendPoint`. The muster point already exists as a plug — it is just the raw geometric map center, unrelated to the base or the threat. That is why fighters scatter.

2. **CAI cannot hold a static line under fire.** CAI's `DefendPoint` wraps vanilla's `Defend` duty, but CAI's reactive `ThingComp_CombatAI.TryAggro` auto-escalates any Defend-duty pawn that is *shot at or near-missed* into a **40–60 s `HuntDownEnemies` chase**, and virally drags nearby Defend-duty squadmates with it (halving chance per hop). `endOnTookDamage=false` does **not** prevent this — it is a separate code path gated only on `duty.Is(DutyDefOf.Defend)`. Additionally CAI patches `JobGiver_DuckOrRetreat` into every pawn under fire, so individuals dodge/retreat and the formation frays. **Conclusion: do not use CAI to hold.**

3. **Vanilla holds a line reliably.** `pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, cell), JobTag.DraftedOrder)` moves a drafted pawn to a cell; when the Goto ends, the drafted think-tree (`JobGiver_Orders`) drops the pawn into `JobDefOf.Wait_Combat` (`JobDriver_Wait.CheckForAutoAttack`), which **fires the equipped verb at anything in range/LOS without leaving the cell**. Melee verbs (~1 cell range) therefore do **not** chase — a mustered melee pawn stands and holds the chokepoint. This is the entire vanilla "hold position and fight from here" mechanism, and it works **with or without CAI** (when we do NOT set `aiAutoControl`, CAI's reactive layer is inert because `IsAIAutoControlled = aiAutoControl && Drafted`).

## Design

### Behavior overview

- **Peace / Nuisance:** unchanged. A lone nuisance (small critter pack) is just hunted where found — no muster for one squirrel.
- **Raid / Serious (line not yet breached):** fighters **muster** — march to the muster anchor and **hold** there in vanilla `Wait_Combat` (drafted, no CAI duty, no `aiAutoControl`). Ranged fire from the held cells; melee hold the front.
- **Release to free engage:** the moment the line is **breached** (enemy inside the perimeter / `EnemyAtBase` / sapper), OR enough fighters have gathered (**anti-trickle gate**, default ≥70%), the squad is **released**: hand to CAI (`CaiBridge.TryEngage`, existing path) for adaptive fighting when CAI is present, else leave them drafted in `Wait_Combat` (they still fight, just don't advance). Release is a **one-way latch** for the whole alert (no oscillation back to Hold mid-fight).
- **Stand-down:** unchanged (undraft, disengage, civvies).

### Muster anchor selection (priority order, fail-safe)

1. **Player-painted "LW Muster" area** (mirrors the existing "LW Shelter" convention in `ShelterAreaService`): if an `Area` labeled `LW Muster` exists, use the reachable cell in it nearest the fighter centroid. Deterministic, player-approved, best when the author cannot playtest.
2. **Auto perimeter-facing fallback:** if no painted area, pick the reachable `Home`-area boundary cell best aligned with the direction from base centroid → hostile centroid (faces the threat), lightly biased by adjacent cover (`CoverUtility` / `fillPercent >= 0.5`). If `EnemyAtBase` (drop-pod / interior threat), re-anchor the search on the fighter centroid instead.
3. **`map.Center` fallback:** no Home area / no reachable candidate → today's behavior. Never regress below the current baseline; never throw.

**Stability:** the chosen anchor is **cached on `MobilizationMapComponent`** (persisted via `Scribe_Values`), recomputed only on a **tier transition** (not every 250-tick recheck), and replaced only if a new candidate scores materially better or the cached cell becomes invalid/unreachable. **Reachability is a hard per-fighter gate** — a fighter that cannot reach the anchor falls back to normal engage rather than blocking the squad.

> **Deferred (not v1):** full door/Region chokepoint detection (Option C). The painted-area path already gives the player exact control of "the door"; the perimeter-facing auto-fallback is a good-enough default. Chokepoint auto-detection is the riskiest, least-testable piece and is deferred to a later iteration.

### Pure phase machine (`LivingWorld.Core`, unit-tested)

A new pure machine `MusterPlan` (kept separate from `MobilizationPlan` so the existing 12-field state and its tests are untouched), plus a tiny `MusterGate` for the anti-trickle release decision, mirroring the `ThreatClassifier` / `ThreatDebounce` split.

```
enum MusterPhase { None, March, Hold, Release }

readonly struct MusterState {
    bool IsFighter;        // active combatant (MobilizationCandidates.IsCandidate)
    bool Mobilized;        // colony is mobilized
    bool WantsMuster;      // tier is Raid/Serious AND line not breached AND musterEnabled
    bool AtAnchor;         // pawn within hold radius of the anchor
    bool AnchorReachable;  // this pawn can path to the anchor
    bool Released;         // squad-wide release latch is set (gate open or breach)
    bool IsBusyUrgent;     // firefight / tend / rescue — never interrupt
}

MusterPhase NextAction(in MusterState s):
    if !s.IsFighter || !s.Mobilized || s.IsBusyUrgent -> None
    if s.Released || !s.WantsMuster || !s.AnchorReachable -> Release   // free engage (existing CAI/vanilla path)
    if !s.AtAnchor -> March                                            // vanilla Goto to anchor
    return Hold                                                        // arrived: vanilla Wait_Combat, no CAI duty
```

```
readonly struct MusterSignals { int FightersTotal; int FightersAtAnchor; bool LineBreached; }
bool MusterGate.WantsRelease(in MusterSignals s, double readyFraction):
    if s.LineBreached -> true
    if s.FightersTotal <= 0 -> true
    return s.FightersAtAnchor >= ceil(FightersTotal * readyFraction)
```

`LineBreached` = `EnemyAtBase || AnySapper || AnyEntity || AnyMechanoid || AnyInsect` (from the existing `ThreatSignals`). A **release timeout** (safety valve, mirrors `ThreatDebounce.belowCount`): if the gate has not opened after N rechecks, force release so one unreachable straggler cannot freeze the squad forever.

### Driver integration (`LivingWorld.RimWorld`, thin glue)

`MobilizationDriver.Drive` becomes **two-pass** (both cheap, inside the existing throttled 250-tick recheck):
1. **Pass 1** (gather): for each fighter compute `AtAnchor` (distance to cached anchor) and `AnchorReachable`; aggregate `FightersTotal` / `FightersAtAnchor`.
2. `MobilizationMapComponent` computes `released = musterReleasedLatch || MusterGate.WantsRelease(...)` once, sets the latch.
3. **Pass 2** (act): for each fighter call `MusterPlan.NextAction`:
   - **March** → `TryTakeOrderedJob(Goto anchor)` if not already Goto-ing there; ensure drafted; **no** CAI duty, **no** `aiAutoControl`.
   - **Hold** → do nothing (the drafted think-tree self-drops into `Wait_Combat`); ensure drafted; no CAI duty. Guard: only (re)issue Goto when `pawn.Position != anchor-ish && CurJob != Wait_Combat` to avoid cancelling the auto-attack each tick.
   - **Release** → existing `CaiBridge.TryEngage` (drafts + CAI) when CAI available, else leave drafted (vanilla `Wait_Combat`).
   - **None** → stand-down / busy path unchanged.

**Damage-on-march escape hatch:** if a marching fighter takes damage repeatedly (or a hostile is closer to the fighter than to the anchor), force that pawn to Release (engage in place) rather than walking it through open fire.

Ghouls (`IsCombatCreature`) run through the same muster path (they are fighters).

### Player control & localization

- One new setting `musterEnabled` (default **true**) in the existing mobilization settings group. Also `musterReadyFraction` (default 0.7) and `musterHoldRadius` (default 8) as tunables. RU/EN keys added.
- Optional player-painted `LW Muster` area (documented in the setting tooltip, same as `LW Shelter`). No new gizmo, no overlay — mechanical feedback (fighters visibly walking to and holding the line) is the UI.
- Diagnostics: extend `DiagnosePawn` with `musterPhase`, `atAnchor`, `distToAnchor`, `anchorSource` (painted/auto/center); log lines mirror `[LivingWorld] Muster: <pawn> -> <action> (tier X, anchor=<cell>)` under `mobilizationDiagnostics`.

### Robustness (author cannot playtest headless)

- Every new path is fail-safe (try/catch → treat as "no muster", never lock the colony).
- Hold uses the **most reliable** primitive (vanilla drafted `Wait_Combat`) and works identically with and without CAI.
- Reachability hard-gates each fighter; release timeout prevents squad freeze.
- All decision logic is in pure, unit-tested `LivingWorld.Core`; the glue is build-verified with diagnostic logging.

## Out of scope (deferred)

Door/Region chokepoint auto-detection; ranged/melee sub-offsets along the line (v1 lets vanilla place them; melee hold front by range naturally); per-cell reservation deconfliction; retreat/regroup; muster for Nuisance.

## Success criteria

- With CAI on OR off: on a Raid, fighters march to the muster anchor and hold (`Wait_Combat`) rather than scattering; on breach or ≥70% gathered they free-engage; on stand-down they return to civvies.
- A painted `LW Muster` area is honored; absent it, an automatic perimeter-facing anchor is chosen; absent even that, behavior equals today's `map.Center`.
- Full unit-test coverage of `MusterPlan` + `MusterGate`; build 0/0; no regression in the existing 423 tests.
