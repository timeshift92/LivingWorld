# Original Implementation Policy

Living World is an original RimWorld mod project.

The project researches existing mods to understand successful ideas, design trade-offs, compatibility risks and architectural limitations. This does not mean that Living World copies or embeds those mods.

## Core rule

```text
Study ideas, learn lessons, write our own code.
```

## What is allowed

Living World may use the following as research input:

- high-level gameplay ideas;
- public feature descriptions;
- architectural lessons;
- compatibility lessons;
- performance lessons;
- observed gameplay behavior;
- general RimWorld modding patterns;
- known problems in existing implementations.

## What is not allowed

Living World must not copy from other mods:

- source code;
- compiled DLLs;
- XML definitions;
- formulas and balancing tables;
- UI graphics;
- icons;
- textures;
- preview images;
- text descriptions;
- unique naming schemes;
- internal class structures as direct clones.

## Research sources

### RimWar Threaded

Repository: https://github.com/TorannD/RimWar---Threaded

License: MIT.

Status for Living World:

- may be studied;
- ideas may inspire original systems;
- code should not be copied by default;
- if MIT code is ever reused, copyright and license notices must be preserved.

Research value:

- planet-layer activity;
- world objects;
- warbands;
- scouts;
- diplomats;
- settlement power abstraction;
- world update loop;
- Harmony integration risks.

### Economics & Demography

Repository: https://github.com/helldanpwnz/Economics-and-Demography

License status: no explicit license found during research.

Status for Living World:

- may be studied only as public prior art;
- code must not be copied;
- formulas must not be copied;
- XML/data must not be copied;
- assets/text must not be copied.

Research value:

- demographic counters;
- births and deaths;
- migration;
- faction expansion/decline;
- persistent goods;
- daily production/consumption;
- finite-money economy;
- background simulation.

### Empire Refactored / Empire 1.6 Continued

Steam Workshop: https://steamcommunity.com/workshop/filedetails/?id=3701480464

Repository: https://github.com/matathias/Empire-1_6-Continued

License: GPL-3.0.

Status for Living World:

- may be studied as an idea source;
- GPL code must not be copied unless Living World deliberately accepts GPL-compatible licensing;
- assets, XML and text must not be copied.

Research value:

- player colonies;
- taxes;
- edicts;
- squads;
- colony events;
- manual defense battles;
- XML-driven extensibility;
- low-Harmony design direction.

## Implementation approach

Living World must implement its own:

- `WorldState`;
- entity IDs;
- population model;
- demographic simulation;
- economy model;
- military model;
- ecology model;
- diplomacy model;
- event sourcing;
- materialization/dematerialization layer;
- Harmony patches;
- public API;
- UI;
- save format.

## Documentation wording

Use wording like:

```text
Inspired by the idea of persistent faction activity found in RimWar.
```

Do not use wording like:

```text
Uses RimWar system.
Based on RimWar code.
Ported from Economics & Demography.
Empire-derived implementation.
```

## Review checklist

Before merging code or data, ask:

1. Was this written from scratch?
2. Does it copy another mod's structure too closely?
3. Does it include text copied from another mod?
4. Does it include XML copied from another mod?
5. Does it include formulas copied from another mod?
6. Does it include art or preview assets from another mod?
7. Does the license of the source allow this usage?
8. Is credit given where research inspiration is discussed?

If the answer is unclear, do not merge until reviewed.
