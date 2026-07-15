# Living World Causal Loop

This document records the gameplay invariant behind the living-world slice.

## Target State

Faction activity must form a causal loop:

```text
knowledge -> decision -> visible travel -> encounter/arrival -> ledger consequence -> saved history -> repeat
```

Actions should not appear as free-floating markers. If a faction sends a scout, diplomat,
caravan or warband, the plan must already know the target settlement, and that target must
remain valid when the executor materializes the action.

## Current Core Contract

- Warbands require faction settlement intel before targeting an enemy settlement.
- A faction with no actionable enemy intel scouts first instead of blindly attacking.
- Scout, caravan and diplomat plans carry concrete target settlements.
- Same-day planning uses target pressure and stable per-faction scoring so actions spread
  across the world instead of dogpiling the lowest settlement id.
- Scouts do not target player settlements or allied settlements.
- Caravans do not target player settlements or hostile settlements.
- Diplomats do not target player factions or irreconcilable factions.
- Executors revalidate the planned target before dispatching a world object.
- Travelling actions revalidate again before arrival. Scouts recall if their target becomes owned by
  their faction or allied; caravans recall instead of trading with a hostile captor; invalid migration
  destinations reroute or return their conserved people and cargo.
- Travelling scouts, diplomats, caravans and warbands survive save/load.
- Arrival or interception writes consequences to the ledger and to world events.
- A visible NPC settlement map remains bound to one ledger settlement. Loaded-map checkpoints and
  deinit reconciliation persist resident/animal fate, warehouse stock and facility damage.
- Terminal armies release all citizen ownership, including casualties, and consume or return cargo
  through an explicit terminal path.

## Acceptance Test

`TestWorldWarScoutAttackConsequenceLoop` covers the minimum living loop:

1. Raiders have no intel and plan a scout with a concrete target.
2. The scout persists through save/load.
3. On arrival, scout intel unlocks a warband.
4. The warband persists through save/load.
5. On arrival, battle resolves, citizens die, the settlement can be captured, diplomacy changes,
   and the history remains visible through events/activity summary.

NPC settlements now have a bounded active-map projection generated from facilities, population,
warehouse stock, technology, biome and defense posture. It is not a permanently ticking off-screen
RimWorld map: the lightweight ledger remains authoritative while unloaded, and the same persistent
projection is synchronized while loaded and reconciled on exit. This is the intended performance
boundary rather than a second simulation.
