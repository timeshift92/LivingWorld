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

## Status (updated after Codex C4/C5)

- **G1 — DONE.** Primary RimWorld scanner guard landed earlier:
  `VanillaSettlementImporter` drops `Faction.OfPlayer` settlements. Codex C4 now adds
  Core defense-in-depth: `WorldState.PlayerFactionId`, planner exclusion, lifecycle
  collapse exclusion, and `WorldBattleService.TryResolve` returning
  `BlockedPlayerSettlement` instead of silently resolving a ledger battle against the
  player. Tests cover target selection, lifecycle collapse, battle blocking and save/load.
- **G2 (movement pruning) — DONE.** Codex C5 adds `ArmyMovementPruneService`, persisted
  movement `StatusTick`, daily world-war pruning through `WorldWarService`, and tests that
  old resolved movements are pruned while traveling/recent movements and history events
  remain.
- **G3 (war-loop scale) — NOT DONE.** `PlanDay`/`FactionPower` still scan citizens per
  faction each day; awaits C3 cached aggregates.

## Reviewer verdict — Codex non-warband execution (C1): APPROVED

Reviewed `WorldWarService` Caravan/Scout/Diplomat (Codex `009bfc4`) against the review
contract:

- **Conservation OK.** Caravan uses `TransferResource` (moves owned goods source→target,
  quantity clamped to `GetOwnedResourceQuantity`, net-zero — nothing created); Scout only
  records an intel report; Diplomat only adjusts goodwill. No population/resource is
  fabricated.
- **No double-drive.** None of the three reserve citizens, so they can't use army/raid-
  reserved population.
- **Sensible targeting.** Trade targets a non-`Hostile` other faction (no gifting enemies);
  diplomacy skips irreconcilable factions (no wasted overtures on pirates); scouting hits
  any other faction. After G1, none can target the player (player settlements are out of
  the ledger).
- **Tests present.** `TestWorldWarCaravanTransfersRealGoods`,
  `TestWorldWarScoutingRecordsIntel`, `TestWorldWarDiplomatChangesGoodwill`,
  `TestWorldWarNonWarbandEffectsPersistThroughSaveLoad` (conservation + save/load).

No blocking findings. Minor watch: caravans can flow to a neutral faction the sender is
not allied with — intended trade, not a bug.

## Owner / order (remaining)

1. **Codex — G3/O1 — DONE.** Cached settlement/faction resident population and
   combat power aggregates now back the war-loop power reads.
2. **Codex — O2** compact/cohort serialization for large saves.
