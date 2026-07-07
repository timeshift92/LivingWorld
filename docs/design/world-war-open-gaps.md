# World War — Open Gaps Found In Post-R7 Review

Status: **review findings, cross-lane.** A step-back review of the shipped world-war
loop (R1–R7 + tuning + K1/K3) found gaps that were documented as invariants but never
implemented or tested. This file is the shared record so both Claude and Codex act on
the same list.

Related: [rimwar-absorption-edge-cases.md](rimwar-absorption-edge-cases.md) (§4 player
target, §8 anti-double-drive, I1 conservation), [agent task split](../superpowers/plans/2026-07-07-agent-task-split.md)
(K3 acceptance "no silent attack on player settlement").

## G1 — The player faction is unprotected in the world war (HIGH)

**The invariant we wrote and never enforced:** edge-cases §4 says a battle against a
player settlement must route to real raid materialization, never a silent ledger
battle; K3 acceptance repeats "no silent attack on player settlement." Neither was
implemented or tested.

**The live chain today:**
1. `WorldObjectScanner` / `VanillaSettlementImporter.ImportCandidate` imports **every**
   vanilla `Settlement`, including `Faction.OfPlayer`'s, into the ledger.
2. `FactionActionPlanner.FindEnemyTarget` picks any settlement whose `FactionId` differs
   from the acting faction — **including the player's** (no exclusion).
3. `WorldWarService` dispatches a warband at it; `WorldBattleService.Resolve` silently
   marks the player settlement's ledger citizens `Dead` and flips its `FactionId`.
4. `FactionLifecycleService.SimulateCollapses` can then **collapse the player faction**
   once its ledger settlements have no living citizens.

Setting the player faction's behavior to `FactionBehavior.Player` only stops it from
*acting* — it does not stop NPCs from *targeting* it (targets filter by faction id, not
behavior). Grep confirms **no player-faction exclusion anywhere** in the war/lifecycle
path.

**Impact:** the ledger's model of the player is corrupted (base changes faction, player
faction "collapses"), and the design's core promise (never silently harm the player) is
broken. Real colonist pawns are not directly killed (they live on the map, not as ledger
citizens), but the ledger↔player desync drives wrong downstream behavior (intel, raids,
faction relations).

**Fixes (both should land; scanner is the primary):**
- **Claude lane (primary, immediate):** `VanillaSettlementImporter` must **not import
  `Faction.OfPlayer` settlements** — the player's colony is the active map, not a ledger
  NPC settlement. This removes the whole class at the source.
- **Codex lane (defense in depth):** the ledger should know its player faction id and
  exclude it in `FindEnemyTarget` **and** `FactionLifecycleService` (never target,
  never collapse the player). Needed because a player settlement could still enter the
  ledger via another importer/mod path.
- **Test:** the world war never targets or collapses the player faction; the scanner
  drops player settlements.

## G2 — `_armyMovements` grow unbounded (MEDIUM)

`Disbanded` / `Arrived` / `Recalled` movements are never removed from
`WorldState._armyMovements`. Every warband ever launched stays forever → save bloat and
memory growth; the warband cooldown and the K1 war-UI both iterate the whole dictionary.

**Fix (Codex lane, Core):** prune resolved movements after a retention window (keep only
`Traveling` + a short tail for the UI/cooldown), with back-compat load. This overlaps
Codex C3 (save/perf) and the general `_events` growth concern.

## G3 — War-loop cost at scale (MEDIUM, pre-20k)

`FactionActionPlanner.PlanDay` + `FactionPower` compute settlement power via LINQ over
citizens per faction **every simulated day**. Fine now, a hot path at 20k. The war loop
specifically needs the cached power/strength aggregates from Codex C3 before scale
testing; until then, keep world counts modest.

## Process note

G1 is the textbook "acceptance written, guard/test never added." Adopt: **every
acceptance bullet gets a test that would fail without the change** — the split doc's
Review Contract should reject a task whose acceptance has no enforcing test.

## Ownership / order

1. **Claude — G1 scanner exclusion + test** (in progress). Closes the silent-player hole
   at the source.
2. **Codex — G1 Core defense-in-depth** (player-faction id + `FindEnemyTarget` /
   `FactionLifecycleService` exclusion + test).
3. **Codex — G2 movement pruning** (folds into C3).
4. **Codex — G3** covered by C3 cached aggregates.
