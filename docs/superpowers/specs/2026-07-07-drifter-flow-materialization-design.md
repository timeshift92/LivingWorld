# Drifter flow: live simulation + storyteller-driven materialization (design spec)

Status: **implemented; incident category corrected after playtest.**
Implementation note: early drafts below mentioned `AllyArrival`, but RimWorld
1.6 does not define that `IncidentCategoryDef`. The shipped
`LivingWorld_DrifterArrival` uses `Misc`; do not revert it to `AllyArrival`.
Population-flow spec step 5 (RimWorld adapter), plus wiring the already-built
drifter Core pipeline into the live world tick.

Revised so materialization uses a **custom `IncidentDef` + `IncidentWorker`**
driven by the storyteller, per
[storyteller-normalization.md](../../design/storyteller-normalization.md), instead
of a Harmony `Postfix` on `IncidentWorker_WandererJoin`.

Related: [population-flow.md](../../design/population-flow.md) (§5 space tap, §6
drifter pool, §7 founding, §13 phased delivery step 5),
[storyteller-normalization.md](../../design/storyteller-normalization.md)
(4-layer target architecture; this spec is migration steps A + 2),
[gap-analysis.md](../../design/gap-analysis.md) (§3 custom storyteller, §A
identity), [simulation.md](../../simulation.md) (raid lifecycle — incident pattern).

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
- **Part B** — add a **storyteller-driven arrival incident** that materializes a
  ledger drifter as a real colonist, so a pawn who arrives is a tracked member of
  the world, not a spontaneous spawn — and the arrival's cadence is controlled the
  sanctioned way (storyteller + ledger gate), not by patching vanilla.

## 2. Non-goals (explicit follow-ups, not this slice)

- **Custom `StorytellerComp` (normalization level 2).** This slice registers a
  custom `IncidentDef` in a vanilla category and lets the vanilla storyteller
  schedule it (level 1). A ledger-deficit MTB comp is a later refinement.
- **Full identity migration (normalization step 1, gap-analysis §A).** This slice
  attaches `CompLivingWorldIdentity` **only to the pawn our arrival incident
  spawns** — the one place we create a pawn. Retrofitting the raid pawn links
  (`thingIDNumber`) onto the comp is a separate slice.
- **Custom faction-raid incident + damping vanilla raids (normalization step 3).**
  Out of scope here; this slice touches the arrival channel only. The existing
  raid Harmony patches are left untouched.
- **Vacuum release on collapse** (population-flow §10→§7; gap "G2"). Faction
  collapse records status + event but does not free the collapsed faction's
  settlements for founding. Separate slice.
- **Additional arrival channels** (refugee quests, transport/drop pods, slaver
  stock). This slice adds the **drifter-arrival** incident only; other channels
  reuse the same worker pattern later.
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
deliberately leave a small standing pool between days — this is the buffer the
Part B arrival incident draws from.

### 3.3 Tuning knobs (settings)

Add a drifter-flow block to `LivingWorldSettings` (+ `ExposeData` + sliders in
`LivingWorldSettingsDrawer`). Defaults conservative:

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
scales with the world; ceiling is the hard guard. Clamp in the component
(never below 0, ceiling ≥ target).

## 4. Part B — storyteller-driven drifter-arrival incident

### 4.1 Custom incident (not a vanilla patch)

New `IncidentDef LivingWorld_DrifterArrival`:

- `category = Misc`, `targetTags = Map_PlayerHome`, sensible
  `baseChance` / `minRefireDays` so the vanilla storyteller schedules it natively
  (normalization level 1). → verify field names against `Assembly-CSharp.dll`.
- `workerClass = IncidentWorker_LivingWorldDrifterArrival` (new C# class in
  `LivingWorld.RimWorld`).

XML lives at `mod/Defs/IncidentDefs/LivingWorld_DrifterArrival.xml` (the mod
already ships Defs, e.g. `Defs/MainButtonDefs/`).

### 4.2 Worker behaviour

`IncidentWorker_LivingWorldDrifterArrival : IncidentWorker`:

- **`CanFireNowSub(parms)`** — true when the ledger wants a player-facing arrival:
  a pooled drifter exists **or** the world is below its population target
  ("always record" still applies — see §4.3). Reads **cached aggregates**, not a
  per-call `state.Citizens` scan (normalization §6). Fail-open: no component →
  false (let vanilla proceed).
- **`TryExecuteWorker(parms)`** — materialize:
  1. Resolve/consume a ledger drifter (or create-and-materialize if pool empty,
     §4.3) via the Core method.
  2. Generate a joining pawn (wanderer-like, colonist join), attach
     `CompLivingWorldIdentity` carrying the drifter's `EntityId`.
  3. Spawn/join through the standard join path and send the standard letter.
  Fail-open: any failure → return false, no partial world mutation.

Cadence — i.e. "кран регулирует частоту" (population-flow §5) — is now native:
storyteller scheduling × `CanFireNowSub` gate. No Harmony patch on vanilla
generation.

### 4.3 "Always record" semantics (Core, testable, no RimWorld)

Materialization is **one-shot**: unlike a raid pawn (whose later fate keeps
updating the ledger, so its link persists), a drifter who joins the player is
simply a colonist afterward. We do not track the pawn further, so **no persisted
pawn↔drifter link and no codec change** are needed — drain the pool and record.

Add to `WorldState`:

- `MaterializeDrifter(EntityId drifterId, int pawnThingId, int tick)` — removes
  the drifter from the pool and appends a `DrifterMaterialized` world event
  (`pawnThingId` recorded in the event summary for traceability).
- `MaterializeNewArrival(int pawnThingId, int tick, ...)` — the empty-pool path:
  creates the origin record and materializes it in one step (net pool change
  zero, one `DrifterMaterialized` event).

Add `WorldEventKind.DrifterMaterialized`. No `WorldStateCodec` /
`WorldStateSnapshot` change. The worker fires once per arrival, so no cross-call
idempotency state is required.

### 4.4 Identity comp (minimal slice of the normalization spine)

New `CompLivingWorldIdentity : ThingComp` + `CompProperties_LivingWorldIdentity`,
carrying an `EntityId`, `PostExposeData`-serialized with the pawn. Attached in
`TryExecuteWorker` to the pawn we spawn. This is the **only** identity work in
this slice; the durable identity map and raid-link migration are a separate slice
(normalization step 1). The comp is added to the human pawn kinds we spawn via a
`<comps>` entry or programmatically at spawn — decide in planning; programmatic
attach avoids a broad XML patch.

## 5. Testing (TDD)

Core (`LivingWorld.Tests` exe-runner):

- `MaterializeDrifter`: consumes a pooled drifter, emits `DrifterMaterialized`,
  pool count drops by one.
- `MaterializeNewArrival`: empty pool still yields a `DrifterMaterialized` event
  with net-zero pool change.
- Combined daily step (if a helper is extracted): arrival→founding→assimilation→
  collapse on a seeded world produces expected pool/settlement/faction deltas,
  deterministic.

RimWorld-facing (the project's reflection/structure tests in `Program.cs`):

- `IncidentDef LivingWorld_DrifterArrival` exists with `workerClass =
  IncidentWorker_LivingWorldDrifterArrival` and category `Misc`.
- The worker derives from `IncidentWorker`, gates via `CanFireNowSub`, and is
  fail-open (no component → cannot fire / returns false).
- `CompLivingWorldIdentity` exists, is a `ThingComp`, round-trips its `EntityId`
  in `PostExposeData`.
- `WorldComponentTick` invokes the four pipeline services under the daily gate.
- New settings fields exist and are persisted in `ExposeData`.

Verification bar (project convention): `dotnet run --project src/LivingWorld.Tests`
all green, `dotnet build LivingWorld.sln` 0 warnings / 0 errors, install script
succeeds, installed DLLs + Def XML verified.

## 6. Files touched

New:

- `mod/Defs/IncidentDefs/LivingWorld_DrifterArrival.xml` — the incident def.
- `src/LivingWorld.RimWorld/IncidentWorker_LivingWorldDrifterArrival.cs` — worker.
- `src/LivingWorld.RimWorld/CompLivingWorldIdentity.cs` — identity comp +
  `CompProperties_LivingWorldIdentity`.

Modified:

- `src/LivingWorld.Core/WorldEvent.cs` — `+ DrifterMaterialized` enum member.
- `src/LivingWorld.Core/WorldState.cs` — `MaterializeDrifter` /
  `MaterializeNewArrival` (no codec/snapshot change).
- `src/LivingWorld.RimWorld/LivingWorldWorldComponent.cs` — daily pipeline wiring
  + expose cached aggregates the worker's `CanFireNowSub` reads.
- `src/LivingWorld.RimWorld/LivingWorldSettings.cs` — drifter-flow knobs +
  `ExposeData`.
- `src/LivingWorld.RimWorld/LivingWorldSettingsDrawer.cs` — sliders.
- `mod/Languages/English|Russian/Keyed/LivingWorld.xml` — incident label/letter
  text + settings labels (EN + RU).
- `docs/simulation.md` — drifter lifecycle + materialization section.

No new Harmony patch. No change to the existing raid patches.

## 7. Risks

- **Incident field/API drift**: `IncidentDef`/`IncidentWorker`/`ThingComp` member
  names are version-sensitive — verify against the installed `Assembly-CSharp.dll`;
  the mod already references `$(RimWorldManagedPath)`.
- **Empty-pool churn**: "always record" creates a ledger record on each arrival.
  Bounded by storyteller cadence (rare) + `CanFireNowSub` gate; no per-tick cost.
- **Comp coverage**: only pawns our incident spawns get `CompLivingWorldIdentity`
  in this slice. Vanilla/other-mod arrivals stay untracked until the full identity
  slice — acceptable and documented.
- **Codex overlap**: `WorldEvent.cs` / `WorldState.cs` are shared with Codex's
  economy work, now landed at base `31db141`. Our additions are additive (one
  enum member, two methods) — trivial to merge.
