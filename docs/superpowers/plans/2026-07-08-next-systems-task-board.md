# Living World — Next Systems: Task Board & Ownership

> **Purpose:** the execution tracker for
> [`2026-07-08-livingworld-next-systems-prp.md`](2026-07-08-livingworld-next-systems-prp.md).
> The PRP is the *strategy* (what & why); this board is the *status* (who owns what and where it
> stands). Drafted by Claude at the user's request while organizing the process. Note:
> `docs/superpowers/plans/*` is Codex's lane in the PRP, so **Codex is the authority on scope
> changes**; this board is a shared status surface both agents update.

**Owner legend:** `Core/Codex` = `LivingWorld.Core` + tests · `RW/Claude` = `LivingWorld.RimWorld`
+ `mod/Defs`/`Patches`/`Languages` · `Shared` = rebase before touching.

**Status legend:** ▢ not started · ◐ in progress · ✅ done · ⛔ blocked · ⏸ deferred (out of this milestone)

---

## Focus: Milestone Slice — "Why did this raid happen to me?"

The single vertical the milestone's own **acceptance story** needs. Build this end-to-end before
the broader roadmap. **Player-first:** the intel is about the *player's* colony, not NPC↔NPC chatter
the player never sees.

| ID | Task | Owner | Depends on | Status |
|----|------|-------|-----------|--------|
| P1-C1 | `RaidIntelFact` + `FactionKnowledgeService`: a hostile faction learns **bounded** facts about the player (from trade/scout/prisoner/rumor) with confidence + expiry. No exact wealth. | Core/Codex | — | ✅ Core API landed (`RaidIntelFact` via trade intel, value bands, confidence, expiry, save/load) |
| P1-C2 | `RaidIntent` + `RaidPreparationService`: turn intel into an intent, **reserve real citizens + supplies** from a source settlement, stale-release on expiry (exactly once). | Core/Codex | P1-C1 | ✅ Core API landed (`RaidIntentService`, `RaidPreparationService`, real citizens/supplies, stale release, save/load) |
| P1-R1 | Route the custom faction-raid incident through a **prepared expedition** from Core. **✅ DONE (commit `8390d6c`)** — the incident asks Core (`TryCreateBestIntent` → `PrepareRaid`); intel scales the raid, storyteller points set the floor; preparation released after launch/abort. | RW/Claude | P1-C2 | ✅ |
| P1-R2 | **Believable warning** before the raid. **✅ DONE (commit `8390d6c`)** — `MaybeSendRaidWarnings` sends a deterministic, source-labeled (scout/trader/rumor/…), rate-limited, per-fact-persisted letter when a hostile faction holds fresh player-targeted raid intel; silent when ceded to Rim War. | RW/Claude | P1-C1, P1-C2 | ✅ |
| P1-C-followup | **Codex (Core) follow-up surfaced by P1-R1:** add `WorldState.LaunchRaidPreparation` (mark Launched without releasing the deployed army), and wire `RaidPreparationService.ReleaseExpiredPreparations` into the daily tick so stale Ready preparations are cleaned. **✅ DONE** — launched preparations are terminal, released preparations cannot be re-launched, stale cleanup touches only Ready preparations, and the RimWorld daily loop runs cleanup before daily simulation. | Core/Codex | — | ✅ |
| P1-R3 | **Consequence surfacing** — after a raid: letter *"came from {settlement} of {faction}; it is weaker now"*. **✅ DONE (commit `492781d`)** — `MaybeSendRaidConsequenceLetters` sends one rate-limited, persisted letter per resolved `WorldRaidOutcome` (attribution + losses); EN/RU + test. Independent of Core, on the existing raid path. | RW/Claude | — | ✅ |
| P1-R4 | EN/RU localization + structural tests for the above. | RW/Claude | P1-R1..R3 | ▢ |

**Slice done when** the acceptance story runs: sell value → faction learns (bounded) → prepares from
a real settlement → warning → raid via leased/identity pawns → consequence letter + weakened
settlement → UI explains with bands & source labels (no omniscient exact values).

> The acceptance story's "leased/materialized citizens with stable identity" is **already partly
> satisfied** by the existing raid path (`CompLivingWorldIdentity`). The full lease generalization
> (Task 3) is **not a blocker** for this slice — do it afterward, when a second consumer (settlement
> visits) needs it. Avoid premature abstraction.

**Core handoff after P1-C1/C2:** Claude can now route `IncidentWorker_LivingWorldFactionRaid`
through `RaidIntentService.TryCreateBestIntent(...)` and `RaidPreparationService.PrepareRaid(...)`
instead of ad-hoc reservation. The Core path keeps the old vanilla raid fallback intact:
`RaidOpportunity` is still available for legacy flavor/consumption, while `RaidIntelFact` is the
bounded, expiring faction knowledge record for the new prepared-expedition flow.

---

## Broader roadmap (after the slice) — PRP Systems 3–10

| PRP Task | System | Owner | Status | Note |
|----------|--------|-------|--------|------|
| Task 3 | Materialization leases | Codex core + Claude pawn-gen | ◐ | Core foundation landed: `MaterializationLease`, lease lifecycle, fate sync, expiry release, save/load. RW pawn-gen consumer still pending. |
| Task 4 | Facilities & settlement projects | Codex core + Claude bands | ▢ | Economy depth beyond population |
| Task 5 | Ruins & relocation | Codex core + Claude markers | ▢ | Settlement physical lifecycle |
| Task 6 | Conflict campaigns (`WorldConflict`) | Codex core + Claude summaries | ▢ | Wars as long-form politics, not isolated battles |
| Task 9 | Settings reorganization by player intent | RW/Claude | ✅ | **DONE (commit `9ef05bd`)** — Options→Mod Settings grouped into Population / Faction Activity / Development / Baseline / Performance / Compatibility / Debug; ongoing world-war+development knobs exposed; compat section states Rim War/Empire cede; scrolls; EN/RU + 3 tests. (Codex's world-map speed test folded into Performance.) |
| Sys 1 | Central materialization-intent policy | Codex core + Claude consume | ⏸ | **Defer the central engine** — extract it from concrete cases (raid, drifter) once 2–3 real consumers exist |
| Task 7 | Animal cohorts & ecology | Codex core | ⏸ | **Deferred** — far from player-felt value until an interaction hook exists |
| Task 8 | Technology, selection & incubation | Codex core + Claude gates | ⏸ | **Deferred** — same reason; needs facilities + ecology first |
| Phase 8 | Optional storyteller component | RW/Claude | ⏸ | Only after the cause chains are stable |

---

## Global invariants (must hold for every task)

- Nothing (people/animals/goods/silver) created without a ledger source; every carrier has a
  terminal cleanup path.
- Player-faction settlements are never silently attacked, captured, collapsed, or inspected as
  omniscient NPC ledger objects.
- Rim War active → Living World does not double-drive world war or custom raids. RimWorld layer
  fails open (preserve vanilla rather than corrupt the ledger).
- Save/load stays backward-compatible; new fields are additive.
- UI uses bands + source labels unless the value is directly known or debug is on.
- Core tests must fail without the behavior they claim to protect.

## Process

- **One implementation branch per task**; rebase on `origin/main` first; FF-merge; the **other agent
  reviews** before merge (see the PRP's Cross-Agent Review Checklist).
- Update this board's **Status** column as tasks move (▢ → ◐ → ✅).
- Commit author/committer `Nurbek Akhmedov <timeshift92@outlook.com>`; no co-author trailers.
- Legacy status for shipped work (UI-1..5, world-gen, F-1, economy wiring, review follow-ups,
  Codex O1/O2/caravan/drifter batch) lives in
  [`2026-07-07-agent-task-split.md`](2026-07-07-agent-task-split.md); this board tracks the
  next-systems milestone only.
