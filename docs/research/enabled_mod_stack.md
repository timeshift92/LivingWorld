# Enabled Mod Stack

Date: 2026-07-07

Source: user screenshot from RimWorld mod manager plus local `About.xml` verification under `C:\Games\RimWorld\Mods`.

## Enabled order from screenshot

```text
Harmony
Core
Royalty
Ideology
Biotech
Anomaly
Odyssey
Rim War
RimHUD
RimHUD zh th jp pl pr ru
Economics & Demography
Animal Controls
Animal Control
Empire Refactored
Auto Translation
Русский перевод - Rim War
```

## Local package IDs

| UI name | packageId | Role for Living World |
|---|---|---|
| Harmony | `brrainz.harmony` | Required patching layer. Living World will need Harmony for RimWorld integration. |
| Core | `Ludeon.RimWorld` | Base game. |
| Royalty | `Ludeon.RimWorld.Royalty` | Official DLC. Support as baseline. |
| Ideology | `Ludeon.RimWorld.Ideology` | Official DLC. Support as baseline. |
| Biotech | `Ludeon.RimWorld.Biotech` | Official DLC. Important for children/reproduction/pregnancy integration. |
| Anomaly | `Ludeon.RimWorld.Anomaly` | Official DLC. Support as baseline, but not core to first milestones. |
| Odyssey | `Ludeon.RimWorld.Odyssey` | Official DLC. Support as baseline. |
| Rim War | `Torann.RimWar` | Idea/reference source and future adapter target; not a foundation. |
| RimHUD | `Jaxe.RimHUD` | UI mod; likely compatible if Living World exposes pawn info cleanly. |
| RimHUD zh th jp pl pr ru | `RimHUD.ru.zh` | Localization pack for RimHUD; no core simulation relevance. |
| Economics & Demography | `helldan.economicsdemography` | Deep research source for demographics/economy patches; future adapter target. Living World must own population. |
| Animal Controls | `avilmask.AnimalControls` | Active-map animal UI/control layer; not a foundation. |
| Animal Control | `Bisque.AnimalControl` | Active-map animal control layer; not a foundation. |
| Empire Refactored | `Matathias.Empire` | Important world/faction/settlement mod; future compatibility target and idea source for vassals/taxes/subject settlements. |
| Auto Translation | `seohyeon.autotranslation` | Localization utility; no simulation ownership. |
| Русский перевод - Rim War | `BotKerebs.RimWar.Russian` | Rim War localization; no simulation ownership. |

## Design consequences

### Foundation mods

Only these are foundation-level for early Living World development:

- `Ludeon.RimWorld`
- official DLCs installed in this environment;
- `brrainz.harmony` for integration patches.

### Research and adapter targets

These should influence design, but must not own Living World's state:

- `Torann.RimWar`
- `helldan.economicsdemography`
- `Matathias.Empire`

### UI/localization compatibility targets

These should be treated as low-risk compatibility targets:

- `Jaxe.RimHUD`
- `RimHUD.ru.zh`
- `seohyeon.autotranslation`
- `BotKerebs.RimWar.Russian`

### Animal-control compatibility targets

These work above Living World's animal lifecycle:

- `avilmask.AnimalControls`
- `Bisque.AnimalControl`

Living World should own inactive/global animal existence. Animal control mods may still affect materialized animals on active maps.

## Empire Refactored notes

Local metadata:

- package ID: `Matathias.Empire`
- version: `1.3.68`
- supports RimWorld `1.6`
- depends on `brrainz.harmony`
- incompatible with `Saakra.Empire`
- loads after Rim War and several world/faction mods.

Why it matters:

- It creates self-governing settlements loyal to the player.
- It has taxes in silver or goods.
- It has settlement/faction events.
- It includes production categories such as food, animals, apparel, mining, medicine, weapons, research, power and loyalty/happiness/unrest concepts.
- It ships a RimWar compatibility assembly under `Compat/1.6/RimWar`.

Living World direction:

- Treat Empire Refactored as a major compatibility target for player-owned subject settlements.
- Do not let Empire own the global truth for citizens/resources if Living World is active.
- Future adapter should map Empire settlements to Living World owners, tax flows to ownership transfers, and Empire military actions to `WorldArmy` withdrawals.

## Load order note

The screenshot order is acceptable for the current research phase:

- Harmony before patching mods.
- Official DLCs before content mods.
- Rim War before Rim War translation.
- RimHUD before RimHUD translation.
- E&D, Animal Control variants, Empire and Auto Translation after foundations.

Living World, when added, should likely load after Harmony/Core/DLC and before adapters/translations that depend on it. Exact load order should be decided once the RimWorld integration assembly exists.

