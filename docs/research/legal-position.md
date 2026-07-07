# Legal Position and Research Safety

This document records the project's position on using existing RimWorld mods as research references.

## Short answer

Living World should not have licensing or authorship issues with other mods as long as the project follows one rule:

```text
Ideas may be studied. Implementation must be original.
```

This document is not legal advice. It is a project policy for clean-room style development and open-source hygiene.

## Project position

Living World is an independent RimWorld mod project.

The project studies existing mods only to understand:

- which gameplay ideas already exist;
- which architectural approaches work well;
- which approaches create compatibility problems;
- which systems are missing from the current mod ecosystem;
- which mistakes should be avoided;
- how to design a better standalone simulation core.

The project does not claim ownership over other mods and does not embed other mods.

## What keeps the project safe

The project remains clean if contributors do not copy:

- source code;
- compiled assemblies / DLLs;
- XML definitions;
- formulas;
- balancing tables;
- UI graphics;
- icons;
- textures;
- preview images;
- Steam Workshop descriptions;
- README text;
- unique names and naming schemes;
- direct class layouts;
- direct file/folder structures from another mod.

## What is allowed

The following is allowed:

- studying public behavior of another mod;
- reading public documentation;
- identifying high-level ideas;
- writing independent notes;
- designing a new architecture from scratch;
- implementing new code with original class names, APIs and data models;
- crediting projects as research inspiration.

Examples:

```text
Allowed: RimWar has moving world warbands. Living World may design its own persistent army system.
Not allowed: copying RimWar's WarObject implementation.

Allowed: Economics & Demography has population counters and daily economy simulation. Living World may design its own demographic ledger and economy scheduler.
Not allowed: copying formulas, code, data tables or text from Economics & Demography.

Allowed: Empire has colonies, taxes and edicts. Living World may design its own settlement governance system.
Not allowed: copying Empire GPL code, XML schemas or UI assets.
```

## Research sources status

### RimWar Threaded

Repository: https://github.com/TorannD/RimWar---Threaded

License found: MIT.

Project decision:

- use as research inspiration only by default;
- do not copy code unless there is an explicit future decision;
- if any MIT code is ever reused, preserve copyright and license notice.

### Economics & Demography

Repository: https://github.com/helldanpwnz/Economics-and-Demography

License found: none during research.

Project decision:

- treat as source-available but not reusable;
- do not copy code, formulas, text, XML or assets;
- only study high-level ideas and public behavior.

### Empire Refactored / Empire 1.6 Continued

Steam Workshop: https://steamcommunity.com/workshop/filedetails/?id=3701480464

Repository: https://github.com/matathias/Empire-1_6-Continued

License found: GPL-3.0.

Project decision:

- do not copy GPL code unless Living World intentionally adopts a GPL-compatible licensing strategy;
- do not copy assets, XML, UI or text;
- study high-level gameplay and architectural ideas only.

## Contributor rule

Before contributing a system inspired by another mod, the contributor should be able to say:

```text
I understand the idea, but I wrote the implementation myself.
```

## Clean implementation checklist

Before merging a change, check:

1. Was the code written from scratch?
2. Are class names and APIs original to Living World?
3. Are XML definitions original?
4. Are formulas and balancing values independently designed?
5. Are assets original or properly licensed?
6. Is text written by Living World contributors?
7. Is any third-party license triggered?
8. Is the inspiration credited only as research/inspiration, not as dependency?

If any answer is unclear, the change should not be merged until reviewed.

## Recommended wording

Use:

```text
Inspired by the high-level idea of persistent faction activity.
Designed after studying existing RimWorld world-simulation mods.
Independent implementation.
Original codebase.
Research source.
Prior art.
```

Avoid:

```text
Based on RimWar code.
Uses Economics & Demography system.
Ported from Empire.
Copied from.
Forked from.
Derived from.
```

## Final position

Living World is safe as long as it remains an original implementation.

The project may study ideas from existing mods, but the code, data, assets, text and architecture must be created independently for Living World.
