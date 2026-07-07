# Drifter flow: live simulation + pawn materialization (design spec)

Status: **approved for planning.** Population-flow spec step 5 (RimWorld adapter),
plus wiring the already-built drifter Core pipeline into the live world tick.

Related: [population-flow.md](../../design/population-flow.md) (§5 space tap, §6
drifter pool, §7 founding, §13 phased delivery step 5),
[gap-analysis.md](../../design/gap-analysis.md) (§A ledger↔WorldPawns binding),
[simulation.md](../../simulation.md) (raid lifecycle — the binding pattern we mirror).

## 1. Problem

The drifter Core services (`DrifterArrivalService`, `DrifterAssimilationService`,
`DrifterFoundingService`) and `FactionLifecycleService` are pure, tested, and
committed — but **nothing invokes them in-game**. They are dead code: drifters
never flow, factions never collapse. Meanwhile the world's population arrivals
(wanderers joining the player) appear "from thin air", violating the project's
core principle (population-flow.md §1) that nothing appears without a real source.

This slice does two things:

- **Part A** — run the drifter pipeline (and faction extinction) in the daily
  world tick so the ledger population actually flows.
- **Part B** — tie player-facing vanilla arrivals (wanderer joins) to ledger
  drifters, so a colonist who wanders in is a tracked member of the world, not a
  spontaneous spawn.

## 2. Non-goals (explicit follow-ups, not this slice)

- **Incident frequency regulation** ("кран регулирует частоту", population-flow
  §5). Regulating *how often* vanilla arrival incidents fire needs a custom
  `StorytellerComp`. This slice only **binds** a pawn when vanilla fires an
  arrival on its own; it does not change incident cadence.
- **GC-robust binding** (gap-analysis §A). Binding is by `pawn.thingIDNumber`,
  the same tradeoff the raid pawn links already accept. A durable
  ledger↔WorldPawns contract is a separate slice.
- **Vacuum release on collapse** (population-flow §10→§7 linkage; tracked as gap
  "G2"). Faction collapse records status + event but does not free the
  collapsed faction's settlements for founding. Separate slice.
- **Additional arrival channels** (refugee quests, transport/drop pods, slaver
  stock). This slice hooks **wanderer-join only**; other channels reuse the same
  binding runtime later.
- **Materializing a ledger faction as a real RimWorld `Faction`** (population-flow
  §11) — remains out of scope by design.

## 3. Part A — drifter pipeline in the daily tick

### 3.1 Where

`LivingWorldWorldComponent.WorldComponentTick`, inside the existing once-per-day
gate (`currentDay > lastSimulatedDay`), after the current
`SettlementDailySimulationService.SimulateDay` and `MigrationService.SimulateDay`
calls. Skip entirely while `State.IsInitialWorldSeedingActive` is true so
bootstrap seeding does not trigger arrivals/collapses.

### 3.2 Daily sequence (order matters)

For simulated day tick `T = currentDay * TicksPerDay`:

1. `DrifterArrivalService.SimulateArrivals(State, new DrifterArrivalRequest(T, targetWorldPopulation, hardCeiling, maxArrivalsPerStep))`
   — homeostatic intake into the pool.
2. `DrifterFoundingService.SimulateFounding(State, new DrifterFoundingRequest(T, minFounders, leaderAptitudeThreshold))`
   — a capable leader splits off to found a settlement/raider band (consumes
   drifters). Runs **before** assimilation so a leader can break away before the
   pool is vacuumed into existing settlements.
3. `DrifterAssimilationService.SimulateAssimilation(State, new DrifterAssimilationRequest(T, maxAssimilationsPerStep))`
   — remaining drifters settle into the least-populated settlements.
4. `FactionLifecycleService.SimulateCollapses(State, new FactionLifecycleRequest(T))`
   — factions with zero living citizens collapse (closes gap G1). Runs last so a
   faction emptied earlier in the same day is recorded.

Metered rates (`maxArrivalsPerStep`, `maxAssimilationsPerStep` both small)
deliberately leave a small standing pool between days — this is the buffer Part B
draws from.

### 3.3 Tuning knobs (settings)

Add a drifter-flow block to `LivingWorldSettings` (+ `ExposeData` + sliders in
`LivingWorldSettingsDrawer`). Defaults chosen conservative:

| Field | Default | Meaning |
|---|---|---|
| `drifterFlowEnabled` | `true` | master switch for Part A |
| `targetWorldPopulationPerSettlement` | `baselineHumanSettlementAdults` | target basis; effective target = `Σ settlements × this` |
| `drifterHardCeiling` | `2000` | absolute population cap for the tap |
| `maxDrifterArrivalsPerDay` | `2` | metered intake ("1–2, rarely more") |
| `maxDrifterAssimilationsPerDay` | `2` | metered absorption |
| `drifterMinFounders` | `4` | group size to found |
| `drifterLeaderAptitudeThreshold` | `70` | leadership aptitude to found |

Effective target world population is computed from live settlement count so it
scales with the world; ceiling is the hard guard. Sensible clamping in the
component (never below 0, ceiling ≥ target).

## 4. Part B — materialize wanderer joins against the ledger

### 4.1 Hook

Harmony `Postfix` on `IncidentWorker_WandererJoin.TryExecuteWorker` (mirrors
`LivingWorldRaidIncidentPatch` on `IncidentWorker_RaidEnemy`). New file
`LivingWorldDrifterArrivalPatch.cs`. Fail-open on any exception or missing
component — the vanilla wanderer is left untouched.

### 4.2 Semantics ("always record")

When a wanderer successfully joins the player colony:

1. Resolve the joined `Pawn` (the newest player pawn produced by the incident;
   located the same defensive way the raid patch reads incident state).
2. Consume one drifter from the ledger pool if available; **otherwise create a
   drifter-origin record on the fly** so every player-facing arrival is tracked.
3. Bind the pawn to the ledger record by `pawn.thingIDNumber` and mark it
   materialized.

Every wanderer who joins the player is therefore a member of the world ledger —
honoring "nothing from thin air" even when the standing pool is empty.

### 4.3 Core support (testable, no RimWorld)

Materialization is **one-shot**: unlike a raid pawn (whose later fate —
return/capture/death — keeps updating the ledger, so its link persists), a
wanderer who joins the player is simply a colonist afterward. We do not track the
pawn further, so **no persisted pawn↔drifter link and no codec change** are
needed — just drain the pool and record the event.

Add to `WorldState`:

- `MaterializeDrifter(EntityId drifterId, int pawnThingId, int tick)` — removes
  the drifter from the pool and appends a `DrifterMaterialized` world event
  (the `pawnThingId` is recorded in the event summary for traceability).
- `MaterializeNewArrival(int pawnThingId, int tick, ...)` — the empty-pool path:
  creates the origin record and materializes it in one step (net effect: pool
  unchanged, one `DrifterMaterialized` event).

Add `WorldEventKind.DrifterMaterialized`. No `WorldStateCodec` /
`WorldStateSnapshot` change (no new persisted collection). The incident `Postfix`
fires once per join, so no cross-call idempotency state is required.

The RimWorld patch is a thin adapter: it only reads the pawn and calls the Core
method. All decision logic and state live in `LivingWorld.Core`.

## 5. Testing (TDD)

Core (in `LivingWorld.Tests`, the existing exe-runner):

- Part A tick sequence is expressed as Core service calls, already unit-tested per
  service. Add integration-style Core tests for the **combined daily step**
  helper if one is extracted (arrival→founding→assimilation→collapse on a seeded
  world produces expected pool/settlement/faction deltas, deterministic).
- `MaterializeDrifter`: consumes a pooled drifter, emits `DrifterMaterialized`,
  pool count drops by one.
- `MaterializeNewArrival`: empty pool still yields a `DrifterMaterialized` event
  with net-zero pool change.

RimWorld-facing (the project's reflection/structure tests in `Program.cs`):

- `LivingWorldDrifterArrivalPatch` exists, is a `HarmonyPatch` on
  `IncidentWorker_WandererJoin`, and is fail-open.
- `WorldComponentTick` invokes the four pipeline services under the daily gate.
- New settings fields exist and are persisted in `ExposeData`.

Verification bar (matches project convention): `dotnet run --project
src/LivingWorld.Tests` all green, `dotnet build LivingWorld.sln` 0 warnings /
0 errors, install script succeeds, installed DLLs verified.

## 6. Files touched

New:

- `src/LivingWorld.RimWorld/LivingWorldDrifterArrivalPatch.cs` — the only new
  RimWorld file. No binding runtime needed (materialization is one-shot, no
  ongoing pawn↔drifter link to hold).

Modified:

- `src/LivingWorld.Core/WorldEvent.cs` — `+ DrifterMaterialized` enum member.
- `src/LivingWorld.Core/WorldState.cs` — `MaterializeDrifter` /
  `MaterializeNewArrival` (no codec/snapshot change — one-shot, nothing new
  persisted).
- `src/LivingWorld.RimWorld/LivingWorldWorldComponent.cs` — daily pipeline wiring.
- `src/LivingWorld.RimWorld/LivingWorldSettings.cs` — drifter-flow knobs +
  `ExposeData`.
- `src/LivingWorld.RimWorld/LivingWorldSettingsDrawer.cs` — sliders.
- `mod/Languages/English|Russian/Keyed/LivingWorld.xml` — settings labels + any
  new event/UI strings (EN + RU).
- `docs/simulation.md` — drifter lifecycle + materialization section.

## 7. Risks

- **Empty-pool churn**: "always record" creates a ledger record on every
  wanderer join. Bounded by vanilla incident cadence (rare), so acceptable; no
  per-tick cost.
- **thingIDNumber reuse/GC** (gap-analysis §A): same known limitation as raid
  links; documented, deferred.
- **Codex overlap**: `WorldEvent.cs` / `WorldState.cs` are shared with Codex's
  economy work, now landed at base `31db141`. Our additions are additive (one
  enum member, new methods) — trivial to merge if Codex adds more.
