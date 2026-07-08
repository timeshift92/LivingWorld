# Player War Participation — Design Spec

**Status:** approved-approach (user authorized building all slices in order, autonomously, report at end).
**Owner:** Claude (both Core + RimWorld lanes, by explicit user authorization overriding the usual Core=Codex split).
**Date:** 2026-07-08.

## Build status (as of 2026-07-08)

- **Slice 1 — DONE** (merged `051834e`): player attacks register in the war ledger (third party). Core `PlayerBelligerenceService` + RW `LivingWorldSettlementDefeatPatch` (reflection-verified hook). Unit + structural tests.
- **Slice 2 — DONE** (merged `2df4a85`): diplomat-gated alliances. Core `AllianceService` (alliance = Ally-stance goodwill, **no new save state**) + main-tab "Ally with X against Y" buttons + defeat-hook ally credit. EN/RU + tests.
- **Slice 3 — DEFERRED (needs live iteration):** war objectives from an ally. Heavy RimWorld quest-system integration that cannot be verified headless; build with the user available to test.
- **Slice 4 — BLOCKED (needs a Core prerequisite):** victory & spoils. **Nothing in Core currently sets `WorldConflictStatus.Resolved` — wars never end**, so there is no victory trigger to hook. Requires a Core war-resolution mechanism (when/how a war concludes), which overlaps Codex's war-balance work and must be coordinated, not built solo. Building victory rewards before it exists would be dead code that never fires.

## Goal

Let the player become a real belligerent in the Living World's NPC-vs-NPC wars — not just an observer. The player can fight in a war as an independent **third party**, and can formally **ally** with a faction (gated behind a diplomat exchange) to fight alongside it, earn reputation and rewards, and help decide the war's outcome.

## Vision (user's words, decomposed)

- The player can join as a **third party** (fight independently — attacks register in the war ledger).
- To fight **alongside** a faction (alliance), a **diplomat** must be exchanged first: either an NPC sends its diplomat to the player, or the player sends a diplomat to a faction **while both are neutral**.
- All of: attacks-count, war-quests-from-ally, and full-alliance-with-shared-victory.

This is 4–5 subsystems, so it is decomposed into ordered slices. Each slice is independently shippable and testable.

## Core primitives that already exist (reused, not rebuilt)

- `WorldConflict(FactionA, FactionB, Status, WarExhaustionA/B, TruceExpiresTick, RefugeesCreated, ...)` + `ConflictClaim(ConflictId, SettlementId, ClaimantFactionId, Tick)`.
- `ConflictService.RecordBattleOutcome(state, attackerFactionId, defenderFactionId, attackerLosses, defenderLosses, capturedSettlementId?, tick)` — creates/gets the conflict, adds exhaustion, records a capture claim. **Accepts any faction id as attacker, including the player's.**
- `ConflictService.GetOrCreateConflict / StartTruce / IsTruceActive / RecordWarRefugees`.
- `DiplomacyService.GetStance/GetGoodwill/AdjustGoodwill/RecordAggression`, `RelationStance {Hostile, Neutral, Ally}`.
- `WorldMission` (`WorldMissionKind.Diplomat`, `Scout`), `WorldMissionService`, `DiplomacyActionExecutor` (NPC diplomat missions already travel to targets).
- RimWorld side: player faction id = the ledger id of the player faction (`WorldState.IsPlayerFaction`, `PlayerFactionId`). Settlements carry `FactionId` and a slug encoding the tile.

## Slices (build order)

### Slice 1 — Player attacks register in the war ledger (third-party participation) [FOUNDATION]

**What the player feels:** "When I attack an NPC settlement, it actually matters to the world's wars." Destroying/defeating an enemy settlement weakens its faction in every war it is fighting, and the player shows up as a belligerent in the World Conflicts tab.

**Core (new, thin):** `PlayerBelligerenceService.RecordPlayerAttack(state, defenderFactionId, defenderLosses, capturedSettlementId?, tick)` — a purpose-named wrapper that:
- resolves the player faction id (`state.PlayerFactionId`), no-ops if unset;
- calls `ConflictService.RecordBattleOutcome(state, playerId, defenderFactionId, attackerLosses:0, defenderLosses, capturedSettlementId, tick)` — the player is the attacker;
- returns the updated conflict (or null on no-op).
Deterministic, conservation-safe (only touches conflict records). No RNG/DateTime.

**RimWorld (new):** a hook that fires when the player **defeats/destroys an NPC settlement** on a map (the clearest, least-ambiguous "player attacked" signal). Candidate hook: a Harmony patch on RimWorld's settlement-defeat path (verified to exist via reflection, like the Task 3 hook). On fire:
- identify the defeated settlement's faction defName → ledger `defenderFactionId`;
- estimate `defenderLosses` from the defeated settlement (e.g., the ledger settlement population, or a fixed decisive value) and `capturedSettlementId` = the matching ledger settlement id (via the existing slug→settlement lookup);
- call `PlayerBelligerenceService.RecordPlayerAttack(...)`.
Fail-safe: no matching ledger settlement / no player faction → no-op.

**Player-visible:** the World Conflicts tab shows the player's conflicts (the section already renders `state.Conflicts`; player-involved conflicts will now appear). A debug log line records each registered player attack.

**Tests:** Core unit test — RecordPlayerAttack creates a player-vs-faction conflict, adds defender exhaustion, records the capture claim, and is a no-op with no player faction. RW structural test — the hook file targets the verified RimWorld method and calls PlayerBelligerenceService; the marker/section already tested.

**Done when:** attacking and destroying an enemy settlement produces a `WorldConflict` with the player as a participant and a capture claim, visible in the World Conflicts tab.

### Slice 2 — Diplomat-gated alliance

**What the player feels:** "I can formally ally with a faction against a common enemy — but only after diplomatic contact."

**Gate:** an alliance offer requires a completed diplomat exchange between the player and the faction:
- **NPC → player:** an NPC diplomat mission targeting the player colony completes → a letter offers alliance (accept/decline).
- **Player → NPC:** the player sends a diplomat (a new player-initiated diplomat action) to a **neutral** faction; on arrival an alliance becomes available.

**Core (new):** `AllianceService` over a new `WarAlliance(PlayerFactionId, AllyFactionId, EnemyFactionId, StartedTick, Status)` record (additive save section). Guards: alliance only forms if a diplomat exchange is recorded and the ally is currently at war with the enemy. While allied, the player's attacks on the enemy (Slice 1 hook) also **credit the ally** (goodwill up via `DiplomacyService.AdjustGoodwill`) and optionally boost the ally's war position.

**RimWorld (new):** alliance-offer letters (accept/decline), a player-initiated "send diplomat" action (reuse `WorldMission` Diplomat, player as origin), and an alliance indicator in the World Conflicts tab. EN/RU.

**Tests:** Core — alliance forms only with a diplomat exchange + ally-at-war-with-enemy; player attack on enemy raises ally goodwill while allied. RW structural + EN/RU parity.

### Slice 3 — War objectives from an ally [assess-then-build]

Allied faction sends the player war objectives ("weaken/destroy enemy settlement Z", "survive the next enemy raid") with rewards on completion, tracked in the ledger. Uses RimWorld's quest/letter surface. **Risk:** RimWorld quest integration is heavy and hard to verify headless — build a minimal, letter-driven objective (no full QuestScriptDef) if the full quest path proves unverifiable; flag for live testing.

### Slice 4 — Victory & spoils [assess-then-build]

When an allied war resolves in the coalition's favor (enemy exhaustion tips / enemy collapses), the player shares in the outcome: a goodwill windfall, and a claim/relocation opportunity on a captured enemy settlement. Reuses `ConflictClaim` and the existing lifecycle (ruins/relocation). **Risk:** balance + end-condition detection; build the reward-on-resolution path solidly, keep territory transfer minimal.

## Cross-cutting constraints (must hold)

- Player-faction settlements are never silently attacked/collapsed by this system (existing invariant).
- Conservation: no people/goods created without a ledger source; alliances/claims only move existing records.
- Save/load additive (WarAlliance is a new optional codec section; conflicts already persist).
- Rim War active → this system stands down (do not double-drive world war), same as the rest of the war layer.
- Deterministic Core (no RNG/DateTime); RimWorld layer fails open (a missing hook target or unmatched settlement is a no-op, never a throw).
- UI uses faction display names (resolved) and bands, consistent with the existing tab.

## Testing strategy

- Core logic is unit-tested in the custom runner (create state → act → assert conflict/alliance/goodwill).
- RimWorld hooks are structural-tested + their Harmony targets verified against the real RimWorld assembly via `AssertRimWorldMethodExists` (the Task 3 pattern), since spawning/defeat cannot run headless.
- EN/RU keys covered by the whole-set parity gate (`TestKeyedLanguageParity`).
- Live-test-pending items (the actual in-game defeat→record loop, alliance letters) are flagged, not assumed working.
