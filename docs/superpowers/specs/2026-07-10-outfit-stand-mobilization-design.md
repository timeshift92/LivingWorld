# Outfit-Stand mobilization — design

Status: **approved, ready for implementation plan.**

Pivot the armory/mobilization system away from custom shared racks to the Odyssey
**Outfit Stand** per-pawn model. Living World becomes the "mobilization brain"; the
per-pawn kit storage, equip, return and repair are handled by vanilla `Building_OutfitStand`
(and, when installed, the Outfit Stands Plus mod's mending/mechanized variants).

## 1. Goal

Each combat colonist has a personal outfit stand holding their full kit (apparel + weapon,
owner-assigned). On a manual mobilize toggle or a real threat, the colonist walks to their
stand and equips the kit; on stand-down they return it to the stand and re-dress in civvies.
Auto-draft on a real raid, the diagnostic log, and the settings are kept.

## 2. Decisions (agreed)

| Question | Decision |
|---|---|
| Odyssey dependency | **Hard** — the mod requires Odyssey (`Building_OutfitStand`). |
| Old rack system | **Retired** entirely (see §5). |
| How to equip | Push the **vanilla `JobDefOf.UseOutfitStand`** job — the pawn walks to the stand and equips via the game's own mechanic (animation, stand rules, mending/mechanized speed). |
| Stand-down | **Auto-return** — mobilized colonists return their kit to their stand and re-dress. |
| Outfit Stands Plus | Not a hard dependency; we target vanilla `Building_OutfitStand`, so plain Odyssey works and the Plus variants (still `Building_OutfitStand`) work too. |

## 3. Architecture

Living World = the mobilization brain (state + triggering). Outfit Stands = per-pawn kit
storage + equip + return + repair. The mod drives the stands through vanilla jobs; it does
not reimplement storage or selection.

## 4. Components

### 4.1 MobilizationMapComponent (kept, rewired)
Unchanged state: `manualMobilized`, throttled `threatPresent`, `IsMobilized`, `ToggleManual`,
persisted, the diagnostic log, `mobilizedByUs`, and `AutoDraftOnThreat`. `PushMobilizationJobs`
is rewritten to call the new `OutfitStandDriver` instead of the rack fetch/return logic:
- **Mobilized:** for each combat-eligible colonist that has an assigned stand still holding a
  kit and is not already wearing it → `OutfitStandDriver.EquipFromStand(pawn)`.
- **Stand-down:** for each colonist in `mobilizedByUs` → `OutfitStandDriver.ReturnToStand(pawn)`.

### 4.2 OutfitStandDriver (new, single responsibility)
Bridges mobilization to the vanilla outfit-stand jobs. Fail-safe throughout.
- `StandOf(pawn)` → the pawn's assigned `Building_OutfitStand` via
  `GetComp<CompAssignableToPawn>().AssignedPawnsForReading`.
- `HasKitOnStand(stand)` → the stand still holds gear (`HeldItems` non-empty or `HeldWeapon != null`).
- `EquipFromStand(pawn)` → if the pawn has a stand holding a kit and is not wearing it, wake the
  pawn if asleep and push `JobMaker.MakeJob(JobDefOf.UseOutfitStand, stand)` as an ordered job.
- `ReturnToStand(pawn)` → push the vanilla return interaction so the kit goes back on the stand
  and the pawn re-dresses (exact job resolved in implementation: `PutApparelOnOutfitStand` for
  apparel plus returning the weapon via the stand; fail-safe if unavailable).

### 4.3 Kept helpers
`LoadoutAdapter.IsMobilizationCandidate` / `IsArmed`, Core `IsCombatEligible`, the
"Mobilize colony" toggle gizmo, settings (`armoryMobilizationEnabled`,
`mobilizationSkillThreshold`, `autoDraftOnThreat`), and the pacifist/firefighter exclusions.

## 5. Retired

Deleted entirely: `Building_ArmoryRack` + rack `ThingDef`/`RecipeDef`/`JobDef` XML + textures +
DefInjected; `ArmorySources`; `ArmoryStorageRolesComponent`; `LivingWorldArmoryRoleGizmoPatch`;
`MobilizationOutfitService`; `LoadoutAdapter.ResolveKit/EquipKit/ReturnKit/FreeRackCell`;
`JobDriver_ArmoryKit` + `LivingWorldArmoryJobDefOf`; `ArmoryAssignmentComponent`;
`Dialog_ArmoryLoadout` + its gizmos; the repair bench (`ThingDef` + recipe + `RecipeWorker`);
`ArmoryDebugActions`; Core `ArmoryLoadout` weapon/armour selection (`RankArmor`, `SelectWeapon`,
`WeaponOption`/`ArmorOption`) — keep only `IsCombatEligible`. All corresponding tests and keyed
strings for the removed pieces are removed too.

## 6. Setup (player)

Build an outfit stand for each combat colonist, assign the owner (vanilla UI), and stock the
kit (weapon + armour). Mobilization does the rest. No Living World loadout UI is needed — the
stand's own assignment/UI is used.

## 7. Save compatibility

Retiring the rack/bench/job defs makes an existing save that has those buildings log
"could not load" errors and drop them. The player deconstructs the old Living World racks
before updating, or starts a fresh save. One-time, documented.

## 8. Error handling

Every driver operation is try/caught and fail-open: a missing stand, an empty stand, a pawn
with no stand, or a missing job def just skips that pawn — mobilization never throws into the
tick and the colonist behaves normally.

## 9. Testing

- Structural: `OutfitStandDriver` pushes `UseOutfitStand`, resolves the stand via
  `CompAssignableToPawn`, and is called from `MobilizationMapComponent`; the retired files no
  longer exist; kept pieces (toggle gizmo, auto-draft, settings) still present.
- Reflection-verify against the game: `RimWorld.Building_OutfitStand`,
  `JobDefOf.UseOutfitStand`, `CompAssignableToPawn.AssignedPawnsForReading`,
  `Building_OutfitStand.HeldItems`/`HeldWeapon`.
- Live test (headless-unverifiable): the actual walk-to-stand equip/return timing.

## 10. Non-goals (YAGNI)

No custom stand building, no per-pawn loadout UI, no skill-based selection (the player stocks
each stand), no repair mechanic (mending stands from the Plus mod), no non-Odyssey fallback.
