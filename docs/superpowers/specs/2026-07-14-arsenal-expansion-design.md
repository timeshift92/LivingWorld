# Arsenal Expansion (v3) — Roster, Shelter, Threat Tiers & Autonomous Combat Depth

**Date:** 2026-07-14
**Status:** Approved scenario + agent-reviewed; ready to plan Phase A
**Builds on:** `2026-07-10-arsenal-redesign-design.md` (v2 — built, tested, merged: mobilize combat colonists from Outfit Stands, CAI/draft, apparel-policy safety net, pure phase machine). This document is the expansion layer, not a rewrite.

## Goal
Turn the Arsenal from "combat-capable colonists arm on any threat" into a **proportionate, whole-colony raid response**: the player designates a **Fighters roster**; on an attack, response scales by **threat tier**; **fighters** gear up and fight (autonomously via CAI, correctly this time); **non-combatants** retreat to a **shelter**; everyone stands down and recovers afterward — with **no per-pawn micromanagement**, and staying **unit-testable under the "cannot playtest headless" constraint**.

## Core concepts

### Roles — one Fighters roster
- The player assigns colonists to a single **"Fighters"** roster (a list, mirroring vanilla assignment UX — select colonists, one toggle). Everyone else is a **non-combatant**.
- `IsFighter(pawn)` is roster membership, **not** the skill threshold. The old skill-threshold `IsCandidate` becomes the *default suggestion* for who to put on the roster, plus a hard floor (never auto-mobilize a violence-incapable or non-draftable pawn even if rostered).
- Non-combatant = colonist AND not on the roster (defined by inversion, so children/incapable pawns are always non-combatants, never "neither").
- Per-pawn **"never mobilize"** is implicit (just don't add them to the roster).

### Threat tiers — proportionate response (3 tiers)
A pure `ThreatClassifier.Classify(...)` in `LivingWorld.Core` returns `ThreatTier { None, Nuisance, Raid, Serious }` from plain-data signals (no RimWorld types):

| Tier | Trigger | Fighters | Non-combatants |
|---|---|---|---|
| **None** | no real hostiles (weather, mental break, friendly caravan present) | nothing | nothing |
| **Nuisance** | a few wild manhunter animals (small pack) | arm + `HuntDownEnemies` (or a *partial* call-up: nearest N) | keep working (NO shelter) |
| **Raid** | normal hostile raid (humanlikes), not at the walls | arm + `HuntDownEnemies` | → shelter |
| **Serious** | Anomaly entities, mechanoids, sappers/breach, big raid (points/count), or nearest hostile already at the base | arm + `DefendPoint` (hold, don't chase) | → shelter (deeper) |

Classification signals (read once per recheck, map-wide — never per-pawn): hostile kinds (`RaceProps.Animal` only → Nuisance unless big; `IsMechanoid`; Anomaly entity defs → force Serious; `RaceProps.Insect` → Serious/funnel), raid size (count/points), nearest-hostile distance to colony center, `DangerWatcher.DangerRating`, and — where available — raid strategy (`Siege` → hold-under-cover, do NOT auto-charge). **Animal-pack SIZE matters**: a 20-wolf pack escalates past Nuisance; a lone squirrel does not. Thresholds are settings.

### Behaviors map to reliable mechanics (unchanged principle from v2)
- **Fighters:** existing gear-up (apparel policy + Outfit Stand swap) + CAI duty (see CAI integration below) or draft fallback.
- **Non-combatants:** set their **allowed area** to a player-painted `LW Shelter` area; restored on stand-down. Fallback if unpainted: restrict to home/interior. A separate small pure machine (`ShelterPlan`) decides Flee / Restore / None.
- **Stand location for `DefendPoint` (Serious):** default colony center; dynamic anchor at the breach/hive cell for sappers/infestations (Phase D); optional manual "defense point" marker (Phase D).

## CAI 5000 integration — corrected (from live API study)
The v2 `CaiBridge` was **incomplete**. Findings that must land:
1. **`aiAutoControl` is required.** After drafting a fighter and starting a CAI duty, set `pawn.GetComp<ThingComp_CombatAI>().aiAutoControl = true` (reflection). Without it, CAI's reactive layer (duck / retreat / evade / reposition) never runs — the pawn only gets the top-level objective. Default is `false`, flipped nowhere but a debug gizmo. On stand-down, set it back to `false`.
2. **Tier → duty mapping** (exact `CombatAI.CustomDutyUtility` factories, all resolved by reflection):
   - Nuisance/Raid → `HuntDownEnemies(fallbackPos, expireAfter, startAfter)` (vanilla `HuntEnemiesIndividual`).
   - Serious → `DefendPoint(dest, radius, endOnTookDamage:false, expireAfter, startAfter)` (vanilla `Defend` — clean, no side effects).
   - **Do NOT use `AssaultPoint` for interior defenders** — its DutyDef includes `JobGiver_AITrashBuildings*`, so idle assault-duty pawns start smashing structures. Reserve for expeditionary/leave-map tiers only.
   - `Escort(escortee, ...)` reserved for guarding a rescuer/VIP (Phase D).
3. **Clean teardown:** on stand-down, `CustomDutyUtility.GetPawnCustomDutyTracker(pawn).FinishAllDuties(...)` + `aiAutoControl=false` + undraft (in that order), rather than just clearing our tracking set.
4. **Don't over-promise:** CAI's `Settings` lets the user disable `React_Enabled`/`Retreat_Enabled` per pawn-kind; degrade gracefully and say so in diagnostics.
5. Access is still **reflection-only, soft dependency** (no `CombatAI.dll` reference); the existing try/catch already covers "CAI present, Prepatcher absent" (its accessors are extern stubs that throw). No Prepatcher dependency for Living World.

## Outfit Stands Plus — no conflict, free wins (from study)
OSP has **zero autonomous job-issuing** (all 37 types verified) — `OutfitStandKit` stays the sole driver; no detect-and-defer needed. Two free wins to document (no code): **Mending stands** passively repair stored (unworn) gear on `TickRare` — this covers the "repair" feature v2 dropped; **Mechanized stands** give ~4× swap speed (faster mobilization). Recommend players use them.

## Correctness fixes surfaced by the multi-lens review (do these regardless)
- **Mid-alert candidate abandonment (bug):** `NextAction` short-circuits to `None` when `!IsCandidate`, so a pawn that becomes non-candidate mid-alert (downed→recovered, or removed from the roster during a raid) is stranded drafted/armored forever. Route any pawn with `HasLwDuty`/`DraftedByUs`/`InCombatKit` through StandDown even when it just stopped being a candidate.
- **Transient sets not persisted (bug):** `engagedByUs`/`draftedByUs` are in-memory only; save/reload mid-raid loses them, so after stand-down those pawns are never released. Scribe a `HashSet<int>` of `thingIDNumber` (rehydrate on load), and/or make `ClearCombat` unconditional-idempotent (clearing a duty we didn't grant is harmless).
- **`IsArmed` "any weapon" proxy (bug, Simple Sidearms):** compare against the pawn's stand's specific `HeldWeapon`, not "carries any weapon" — else a colonist with a peacetime sidearm reads as equipped and skips armor. Fully headless-fixable.
- **Caravan pre-arm race:** `LivingWorldCaravanArmoryPatch` pushes a multi-tick `PushEquip` job immediately before the caravan forms synchronously — arming silently no-ops. Either check inventory synchronously or defer caravan formation until equipped.
- **Threat hysteresis/debounce:** raw `DangerRating` every 250 ticks flickers at the boundary → pawns yo-yo to stand and back. Immediate escalation, debounced de-escalation (N consecutive clean rechecks). Pure, testable.

## Scenario refinements to bake in
- **Shelter must not become a wall:** allowed-area restriction blocks vanilla rescue/tend of a colonist downed *outside* the zone. Detect "downed colonist, no rescuer en route" and lift the nearest non-combatant's restriction (or push a forced `Rescue`/`TendPatient` job bypassing area checks).
- **Shelter-is-the-danger-zone:** for infestations / containment breaches inside the base, if the threat source is inside the shelter/home fallback, non-combatants fall back further, not into it.
- **Urgent-job guard for non-combatants too:** doctors/firefighters/wardens finish `TendPatient`/`BeatFire`/`Rescue`/warden work before fleeing (reuse/extend `IsBusyUrgent`).
- **Animals shelter too** (Phase B/D): same allowed-area mechanic for player animals. Note: *Animal Controls* (avilmask) writes `AreaRestrictionInPawnCurrentMap` on newborn animals (inherits from parent) — benign/additive, but use the own-vs-foreign save/restore discipline (record pre-mobilization area by `thingIDNumber`, never blind-clobber).
- **Post-raid recovery:** un-forbid & haul corpses, ensure wounded get tended, resume wall repair, rearm & return kits, un-shelter.
- **Trust/visibility:** fire vanilla **letters** on mobilize / stand-down / tier escalation (the author can't playtest — the player's letters/log ARE the feedback loop); a non-dev at-a-glance readout of tier + roster + per-pawn phase.

## Architecture — keep it testable
- **Two parallel pure machines**, not one giant struct: keep `MobilizationPlan.NextAction` (fighters) as-is; add `ShelterPlan.NextAction(tier, in NonCombatantState)` (`None`/`Flee`/`Restore`). Add `ThreatClassifier.Classify(...)` (pure) → `ThreatTier`, computed once per recheck and threaded down as a parameter (never computed per-pawn — O(candidates×hostiles) trap).
- Thread the tier into `MobilizationPlan` as a derived `WantsDefendPoint`/`DesiredDuty` flag so the pure machine picks Hunt vs Defend without embedding classification.
- **Property-based test harness** over the full `PawnMobState` space (11 bools → enumerate all combinations) asserting invariants (non-candidate⇒None unless unwinding; mobilized⇒never a stand-down phase; determinism; no self-loops). This is the highest-leverage investment under "cannot playtest" — every future field plugs in and is provably safe.
- **Driver sequencing harness:** extract `MobilizationDriver`'s per-pawn loop behind a seam (`IEnumerable<PawnMobState>` + `Action<MobPhase>`) so the snapshot-then-execute + transient-set bookkeeping is testable across scripted tick sequences without RimWorld types.
- **Richer diagnostics:** extend "Dump mobilization phases" to print derived tier, desired duty, and `engagedByUs`/`draftedByUs` membership; distinguish "no stand" vs "empty stand" vs "unreachable stand"; per-pawn retry counter.

## Phased roadmap

**Phase A — Foundation & correctness (mostly headless-verifiable). Do first.**
- `ThreatClassifier` (pure, tier, animal-size-aware) + wire `DefendPoint` into `CaiBridge` + **`aiAutoControl` fix** + tier→duty selection. Makes tiers real.
- Property-based `PawnMobState` test harness + driver sequencing harness.
- Fix the correctness bugs: candidate-abandonment, transient-set persistence, `IsArmed`/stand-weapon proxy, caravan race, threat hysteresis.
- Extend diagnostics (tier/duty/tracking in the dump).

**Phase B — Roster & shelter.**
- Fighters roster (assignment UX + persistence) + `IsFighter`/`IsNonCombatant`.
- `ShelterPlan` (pure) + non-combatant allowed-area driver with save/restore + urgent-job guard + rescue-not-blocked escape hatch.

**Phase C — Trust & polish (cheap; our only live feedback).**
- Letters/alerts on mobilize/stand-down/tier-change; at-a-glance readout; settings for tiers; diagnostics upgrades.

**Phase D — Depth (behind flags, opt-in, live-tuned). YAGNI until A–C are solid in-game.**
- Dynamic `DefendPoint` anchor (breach/hive); manual defense-point marker; ranged/melee split; retreat/regroup for wounded (flagged, may fight CAI's own retreat); siege/infestation/Anomaly special handling; mechanitor mechs; post-raid recovery automation; animal sheltering; Escort-for-rescue.

## Out of scope (this module) / handed elsewhere
World-sim mod-compat bugs found during the study belong to Living World core (Codex's domain), NOT the Arsenal: Empire `PColony` ledger pollution, Economics&Demography settlement desync, Rim War `TryAffectGoodwillWith` dampening, Auto Translation RulePack grammar corruption (user fix: blacklist `nakhmedov.livingworld` in Auto Translation). Tracked separately.

## Testing honesty
Core (`ThreatClassifier`, `ShelterPlan`, `MobilizationPlan`, harnesses) = fully unit-tested. RimWorld glue (`CaiBridge` incl. `aiAutoControl`, shelter area driver, roster UI) = clean `net472` build + review + in-game diagnostic logs. Live pawn behavior (does `DefendPoint` hold, does `aiAutoControl` deliver duck/retreat, does the shelter not trap rescuers) = playtested by the user, debugged from the diagnostic dump — never blind iteration.
