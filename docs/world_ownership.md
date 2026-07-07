# Living World - World Ownership

## Why Ownership Comes Before Population

`Population` answers "how many people exist?"

`World Ownership` answers "who owns every meaningful thing in the world?"

Living World should model ownership as a core primitive from the start, because raids, caravans, trade, migration, war, settlement collapse, and ecology all become simpler when every entity/resource has an owner.

## Ownership Principle

Every persistent object that matters should be owned by another world entity.

Examples:

```text
Citizen 15234
  Owner: Settlement 18

Muffalo 821
  Owner: Settlement 18

Steel x400
  Owner: Settlement 18

Corn Field 42
  Owner: Settlement 18

Warband 91
  Owner: Faction 3
  Source: Settlement 18
```

Ownership is not only legal possession. It is the simulation answer to where a thing belongs when it is not materialized on an active map.

## Core Ownership Types

Initial owner kinds:

- `World`
- `Faction`
- `Settlement`
- `Household`
- `Army`
- `Caravan`
- `Individual`
- `WildernessRegion`
- `UnknownExternal`

Initial owned asset kinds:

- `Citizen`
- `Animal`
- `ResourceStack`
- `Equipment`
- `Food`
- `Medicine`
- `Vehicle`
- `Building`
- `Field`
- `LivestockHerd`
- `Army`
- `Caravan`

## Ownership Transfers

All major world actions become ownership transfers.

### Raid launch

```text
Settlement 18 withdraws:
  40 citizens
  12 muffalo
  350 meals
  500 steel
  60 components
  weapons and armor

Ownership moves:
  Settlement 18 -> Army 91
```

If the raid is destroyed:

```text
Army 91 destroyed
  citizens dead/captured/missing
  animals dead/captured/stolen
  supplies destroyed/looted
  equipment dropped/looted

Settlement 18 does not get those assets back.
```

### Trade caravan

```text
Settlement 18 withdraws:
  traders
  pack animals
  goods
  silver

Ownership moves:
  Settlement 18 -> Caravan 44
```

If the caravan reaches the player, goods are materialized from `Caravan 44`. If the player buys goods, ownership moves from caravan/faction to player. If the caravan is robbed, the source settlement loses those resources.

### Migration

```text
Household 21 leaves Settlement 18
  citizens
  animals
  carried resources

Ownership moves:
  Settlement 18 -> MigrantGroup 7
```

On arrival:

```text
MigrantGroup 7 -> Settlement 31
```

### Settlement destruction

When a settlement is destroyed:

- citizens become dead, prisoners, refugees, or migrants;
- animals are killed, captured, released, or transferred;
- resources are destroyed, looted, or abandoned;
- fields/buildings become ruins;
- history records cause and participants.

No resource should vanish without an event.

## Ownership Ledger

Living World should keep a ledger of ownership records.

Conceptual record:

```text
OwnershipRecord
  AssetId
  AssetKind
  OwnerId
  OwnerKind
  Quantity
  SinceTick
  SourceEventId
```

For unique entities like citizens and named animals, quantity is always 1.

For stackable resources, quantity can be greater than 1.

## Current Core Baseline

The first implemented ownership slice in `LivingWorld.Core` is intentionally small:

- `WorldCitizen` receives a settlement owner when created/imported.
- `WorldArmy` can be created from a source settlement.
- stackable resources are tracked by `(ownerId, resourceKey)`.
- resources can be transferred from one owner to another.
- failed transfers return `OwnershipTransferResult` with a reason code.
- ownership/resource changes emit world events.
- `ILivingWorldApi` exposes owner and resource quantity queries.

This is enough for the next milestone: `RaidPlanner` can withdraw real resources from a settlement into a `WorldArmy` before any RimWorld `Pawn` is materialized.

## Event Sourcing Link

Ownership changes should always produce events:

- `OwnershipAssigned`
- `OwnershipTransferred`
- `OwnershipSplit`
- `OwnershipMerged`
- `OwnershipDestroyed`
- `OwnershipCaptured`
- `OwnershipLooted`
- `OwnershipAbandoned`

This lets history answer:

- where did this army come from?
- what did this caravan carry?
- which settlement paid for this raid?
- what resources did the player destroy?
- why did this faction become weak?

## API Requirements

`ILivingWorldApi` should eventually expose ownership operations:

```text
GetOwner(assetId)
GetOwnedAssets(ownerId)
TransferOwnership(assetId, fromOwner, toOwner, reason)
WithdrawAssets(ownerId, request)
ReturnAssets(ownerId, assets, reason)
DestroyOwnedAssets(ownerId, assets, reason)
```

All write operations should validate invariants and emit events.

## Design Consequences

1. Raids are no longer abstract points.
   - They are owned bundles of real citizens and resources.

2. Economy and military become connected.
   - Sending a raid spends the same settlement resources that families and workers depend on.

3. Trade becomes persistent.
   - Goods sold to a trader enter another owner ledger instead of disappearing.

4. Player actions get real consequences.
   - Destroying a caravan destroys real assets.

5. Compatibility becomes cleaner.
   - Other mods can ask Living World who owns something instead of guessing through pawn/map state.

## Implementation Order

Ownership should be added before full raid replacement.

Recommended order:

1. Add `WorldOwnerId` and `OwnershipRecord` to `LivingWorld.Core`.
2. Assign every `WorldCitizen` to a settlement owner.
3. Add basic resource stack ownership.
4. Add ownership transfer events.
5. Make `RaidPlanner` withdraw citizens and supplies into `WorldArmy`.
6. Materialize `WorldArmy` only when it reaches an active map.
