# Settlement Real Map Contract

## Current status

The peaceful "open real NPC settlement map" entry point is disabled in the RimWorld UI.

The safe settlement observer remains available from selected NPC settlements. It records direct-visit intel and opens the Living World observer window, but it does not try to generate or enter a RimWorld map.

## Why it is disabled

The previous implementation exposed the feature before the full lifecycle was proven in a live save. It mixed several concerns in one patch:

- a settlement gizmo that could open a map without a player caravan;
- a caravan float-menu arrival action;
- ad hoc map generation through a generic map generator;
- ledger materialization on a partially generated map;
- player caravan entry after the map already existed;
- repeated visits to the same world object;
- save/load references to temporary pawns, lords, and world objects.

That produced the exact failure mode seen in live testing: the button existed, but the actual behavior was inconsistent. Some visits opened a generated map that did not behave like a real living settlement, some visits did not move the player's pawns correctly, and repeated visits could get stuck.

## What remains supported

- Direct settlement observer window.
- Fog-of-war gated settlement intel.
- Ledger-backed raids, caravans, missions, warbands, scouts, diplomats, settlement economy, migration, animals, facilities, and ruins.
- Attacked-settlement materialization paths that are already covered by tests and live-safe fallbacks.

## Required future contract

A real NPC settlement map can be re-enabled only as a single end-to-end slice with these rules.

## Entry point

- The only public entry must be a caravan arrival action.
- A selected settlement may show the observer command, but must not open a map directly.
- If no player caravan can enter, the option must be disabled with a visible reason.
- There must be no hidden no-op action.

## Map ownership

- One stable map parent must represent one Living World settlement visit target.
- Repeated visits must resolve the same map parent until the settlement is destroyed, abandoned, or explicitly reset.
- The map component must persist the Living World settlement id, materialization version, and visit state.

## Player caravan lifecycle

- Arrival action chooses the settlement.
- RimWorld creates or loads the stable map.
- Player pawns enter through `CaravanEnterMapUtility.Enter`.
- Leaving the map restores the caravan normally.
- Save/load between arrival and exit must not leave broken references.

## Ledger materialization

- Defenders come from materialization leases.
- Resources come from settlement-owned ledger stacks.
- Animals come from owned animal cohorts.
- Facilities produce rooms, props, storage cells, defenses, and damage trackers.
- The map must not rely on generic random settlement loot as the source of truth.

## Reconciliation

On map exit/deinit, the ledger must receive:

- pawn fate: returned, dead, captured, missing;
- animal fate: returned, killed, taken by player;
- resources: unlooted stacks returned, looted stacks removed;
- facility damage: destroyed walls, doors, floors, and props reduce `ConditionPercent`;
- settlement destruction or abandonment when the world object is removed.

## Live checklist before re-enabling

- Start from a clean save with Living World loaded.
- Send a caravan to a known NPC settlement.
- Arrival menu shows one visit option with a clear label.
- Selecting it creates or opens the settlement map.
- Player pawns are on the map and selectable.
- Ledger defenders, resources, animals, and facilities are visible.
- Save and reload while on the map.
- Exit the map and confirm the caravan returns.
- Visit the same settlement a second time and confirm consequences persist.
- Loot or destroy something and confirm the observer/ledger changes.
- Autosave/load produces no missing `Thing`, `Lord`, or `WorldObject` cross-reference warnings.

## Re-enable gate

The feature must stay hidden until all checklist items above pass in a live RimWorld session and the repo has tests that prevent the old split entry-point shape from returning.
