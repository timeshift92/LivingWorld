# Living World: criteria for a genuinely living simulation

This document is the completion gate for the core concept. A feature is not complete merely because
the ledger changes or a marker is drawn. Every strategic action must close the same causal loop:

`knowledge -> decision -> reservation -> physical transit -> contact/effect -> return or loss -> persistence`

## Non-negotiable invariants

1. **One source of truth.** NPC citizens, animals, cargo and settlement facilities originate in the
   Living World ledger. A RimWorld pawn, item or building is a materialized representation, not a
   second owner.
2. **No traffic from thin air.** Every visible group reserves a real crew and, where relevant, real
   cargo. Cancellation, failure, interception and return each have exactly one terminal path.
3. **No omniscient targets.** Warbands and caravans require faction knowledge. Player UI reveals
   traffic and conflict detail only through direct observation or fresh non-public intelligence.
4. **Neutral means neutral.** Different faction IDs do not imply combat. Encounters resolve as
   combat only for hostile relations or an active declared conflict; truces suppress combat.
5. **Maps are projections of persistent settlements.** An NPC map is generated from ledger
   population, facilities, warehouse stock and ecology. Loot, deaths, captures and structural damage
   reconcile back so a second visit observes the first visit's consequences.
6. **The player is not an NPC settlement.** Player contact is a dedicated physical endpoint. It owns
   no NPC population or resources and exists only to route scouts and diplomats to the real colony.
7. **Save/load is part of every lifecycle.** Active transit, leases, map manifests, processed letters
   and terminal outcomes survive a round trip without stale RimWorld object references.
8. **Determinism.** Core simulation uses persisted ticks and stable hashes; no `DateTime`, runtime
   `GetHashCode`, GUID or unseeded random source may affect world outcomes.

## Acceptance matrix

| System | Source | Physical phase | Terminal reconciliation | Visibility gate |
|---|---|---|---|---|
| Warband / raid | settlement adults + supplies | world army marker, then real raid pawns | dead/captured/missing/returned citizens and cargo | faction intel + physical observation |
| Caravan | settlement crew + goods | outbound, trade audience, return | purchased goods/silver and crew return or destruction | fresh intel + physical observation |
| Scout | settlement citizen | outbound, observation, return | intel only after observation/return; interception loses the real scout | physical proximity and acquired reports |
| Diplomat | settlement citizen | outbound, audience, return | goodwill/offer only after arrival; crew returns or is lost | approaching-player missions are observable |
| Settlers / refugees | source citizens + cargo | bound destination and world transit | found/migrate, reroute, return or explicit loss | fresh intel + physical observation |
| NPC settlement map | settlement ledger | persistent generated map | pawn/animal/resource/facility/warehouse reconciliation | direct visit/attack |
| Animals | lightweight cohorts | bounded map sample | death/taming/escape returned to the cohort exactly once | settlement knowledge or direct map view |
| Mech threat | finite node units + steel + energy | world site and raid | launch consumes the node; failure rolls back reservation | physically visible world site |

## Implemented completion state (2026-07-15)

The repository now closes the causal loop for the supported strategic systems rather than only
changing counters:

- daily simulation commits its day watermark before applying non-idempotent effects, so a partial
  failure is never replayed as duplicated production, births or projects;
- raids reserve concrete adults, food and equipment material; only pawns actually bound to those
  citizens consume equipment, while unused reserves return to a surviving same-faction settlement;
- scouts, diplomats, caravans, settlers and refugees revalidate ownership, hostility and destination
  viability while travelling and either complete, reroute, return or record an explicit loss;
- hostile physical traffic can intercept armies, caravans and missions, while allied or neutral
  traffic does not become combat merely because markers overlap;
- a loaded NPC settlement remains a persistent projection of the same ledger settlement. Resident
  deaths, captures and departures, animals, warehouse withdrawals, facility condition and map
  damage are checkpointed while loaded and reconciled on deinit;
- settlement destruction stops local ecology, breeding and production, moves remaining cohorts and
  resources to the ruin, and launches real refugee groups toward viable same-faction destinations;
- normal UI is knowledge-gated and localized. Exact global diagnostics and raw event reasons are
  available only in debug/developer mode.

Automated status at this revision: `521` tests pass; the RimWorld adapter builds in Release with
zero warnings and zero errors; English and Russian keyed files contain the same `418` unique keys.

## Automated release gate

- Core solution Release build: zero warnings and zero errors.
- RimWorld adapter Release build: zero warnings and zero errors.
- All repository tests pass, including conservation, save/load, fog, transit collision and repeat-visit cases.
- EN/RU keyed localization parity passes.
- Installed mod DLLs and assets match the release worktree.
- A clean RimWorld launch and save load produce no new Living World error or stale-reference warning.

## Live smoke gate

The release is ready for player verification only after these observable checks succeed on the
installed build: world traffic is bounded, unknown traffic is hidden, a known marker moves through
outbound/return phases, hostile physical contact resolves, a player-contact scout/diplomat reaches
the colony, and an NPC settlement can be revisited with its prior casualties, loot and damage intact.
This live smoke gate remains distinct from automated completion because RimWorld object lifetime,
map deinitialization and third-party Harmony interactions cannot be proven by the headless suite.
