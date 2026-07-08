# Economy Core E1-E3 + SD2 Slice

Date: 2026-07-08

## Goal

Give Living World a real Core economy foundation before RimWorld trader materialization:
owned silver, wealth aggregates, production depth, virtual inter-faction trade, and deeper
settlement development. This remains clean-room Living World code; reference mods only inform
mechanics, not implementation.

## Scope

- **E1:** Treat silver as a normal owned resource and expose deterministic settlement/faction
  wealth snapshots from ledger-owned resources. Wealth is computed from configured resource
  prices and can be refreshed/cached for UI or downstream systems.
- **E2:** Extend settlement production with labor efficiency, terrain/tech/scaling modifiers,
  production archetypes, and complexity penalty. Production still writes owned resources to the
  settlement ledger; nothing appears outside ownership.
- **E3:** Add virtual faction-to-faction trade that transfers actual owned goods and silver
  between settlements. Prices are deterministic and respond to stock pressure, tech demand, and
  settlement wealth.
- **SD2:** Add settlement tiers and deliberate development: prospering settlements grow specialist
  pools and capability tier, and births are blocked when housing is full.

## Non-Scope

- RimWorld trader caravan Harmony/materialization remains **E4b** and should use these Core APIs.
- No adapter for Empire/RimWar in this slice.
- No per-pawn or per-building RimWorld materialization.

## Invariants

- Resource and silver transfers conserve ledger quantity.
- Dynamic prices are deterministic for the same world state.
- Save/load remains backward-compatible: new fields have optional fallbacks.
- Births cannot push a settlement past housing capacity.
- Settlement development upgrades capability/specialists, not population from nowhere.

## Handoff To Claude

After this lands, E4b can materialize trader stock from:

- settlement owned resources;
- `SettlementWealthService` snapshots;
- `VirtualTradeService` price quotes.
