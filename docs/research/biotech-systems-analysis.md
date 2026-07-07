# Living World - BioTech Systems Analysis

Date: 2026-07-08

Status: research and architecture input. This document answers how incubators,
selection, crossbreeding, human reproduction, xenogenetics and mechtech are
connected to Living World, what must exist before implementation, and what risks
must be controlled so the feature does not become another set of virtual
numbers.

Related:

- [BioTech, Breeding And Selection](../design/biotech-breeding-and-selection.md)
- [Gap Analysis](../design/gap-analysis.md)
- [Roadmap](../roadmap.md)
- [Simulation](../simulation.md)
- [Save Format](../save_format.md)

## 1. Executive Summary

These systems are directly connected to Living World, but only if Living World
keeps ownership.

Incubators, breeding lines, gene labs and mech gestators must not be free spawn
buttons. They must be ledger projects that consume owned resources, facilities,
specialists, technology, time and information. The output must become owned
ledger state first, and only become RimWorld pawns/items/buildings when the
object is present on an active map, caravan, raid, trade, quest or visible world
object.

The most important split:

- animal and crop selection can be original Living World systems;
- human birth, embryos, growth vats and xenogerms need Biotech-aware adapters;
- mech production is not demography and should be a separate machine industry
  layer;
- pollution and waste are world ecology/economy inputs, not visual flavor;
- exact values must be hidden behind intel, not shown globally.

Near-term conclusion: before implementing incubators, Living World needs
settlement capabilities: infrastructure, specialists, power, medicine, storage,
animal/crop cohorts and compact save data. Otherwise incubators will create the
same problem the project is trying to remove: disconnected numbers that do not
exist in gameplay.

## 2. Evidence From Current Project

Current `WorldState` already owns the right foundation:

- citizens;
- settlements;
- armies and army movements;
- migration groups;
- intel reports and known settlement information;
- raid opportunities, pawn links and raid outcomes;
- faction behavior, relations and lifecycle records;
- production profiles;
- ownership records and resource stacks;
- event history.

This means the architecture already has a place to attach bio systems:

- `Ownership` can own embryos, samples, herds, seed banks, labs and machines;
- `Resources` can pay project costs;
- `ProductionProfiles` can describe base environmental output;
- `SettlementCapabilities` can describe housing, storage, power, lab, animal,
  crop, research, mechanical and pollution-handling capacity;
- `SpecialistPools` can describe farmer, handler, doctor, researcher, engineer,
  geneticist, mechanitor, soldier and diplomat labor limits;
- `KnownSettlementInfo` can expose only bands and confidence;
- `Events` can record births, failed projects, theft, disease, pollution and
  tech leaks;
- `WorldArmy` and caravans can carry real animals, samples, mechs and supplies.

Current limits:

- production is still mostly per-adult output multiplied by environment;
- daily birth is simple interval logic;
- infrastructure and specialists now have a first compact core slice, but are
  not yet bootstrapped from RimWorld world data or shown through intel bands;
- animals and crops are not yet ledger-owned cohorts;
- no settlement project queue exists;
- no pollution, disease, sanitation or lab-risk model exists.

So BioTech is connected to us, but it should not be the next core slice until
these prerequisites are at least represented in the ledger.

## 3. Evidence From Local RimWorld 1.6/Biotech Defs

Local game data confirms that Biotech is not one feature. It is several separate
systems that should map to different Living World modules.

Human reproduction and childcare:

- `C:\Games\RimWorld\Data\Biotech\Defs\WorkTypeDefs\WorkTypes.xml:5`
  defines `Childcare`;
- `C:\Games\RimWorld\Data\Biotech\Defs\Stats\Stats_Pawns_General.xml:5`
  defines `Fertility`;
- `C:\Games\RimWorld\Data\Biotech\Defs\RecipeDefs\Recipes_Surgery_Misc.xml:107`
  defines `ExtractOvum`;
- `C:\Games\RimWorld\Data\Biotech\Defs\RecipeDefs\Recipes_Surgery_Misc.xml:123`
  defines `ImplantEmbryo`;
- `C:\Games\RimWorld\Data\Biotech\Defs\ThingDefs_Items\Items_Various.xml:163`
  defines `HumanOvum`;
- `C:\Games\RimWorld\Data\Biotech\Defs\ThingDefs_Items\Items_Various.xml:193`
  defines `HumanEmbryo`;
- `C:\Games\RimWorld\Data\Biotech\Defs\PawnKindDefs_Humanlikes\PawnKinds_Special.xml:213`
  defines `Villager_Child`;
- `C:\Games\RimWorld\Data\Biotech\Defs\PawnKindDefs_Humanlikes\PawnKinds_Special.xml:221`
  defines `Tribal_Child`.

Xenogenetics:

- `C:\Games\RimWorld\Data\Biotech\Defs\ThingDefs_Buildings\Buildings_Misc.xml:162`
  defines `GeneAssembler`;
- `C:\Games\RimWorld\Data\Biotech\Defs\ThingDefs_Buildings\Buildings_Misc.xml:218`
  defines `GeneBank`;
- `C:\Games\RimWorld\Data\Biotech\Defs\ThingDefs_Buildings\Buildings_Misc.xml:275`
  defines `GeneExtractor`;
- `C:\Games\RimWorld\Data\Biotech\Defs\ThingDefs_Buildings\Buildings_Misc.xml:798`
  defines `GeneProcessor`;
- `C:\Games\RimWorld\Data\Biotech\Defs\WorkGiverDefs\WorkGivers.xml:47`
  defines `CreateXenogerm`;
- `C:\Games\RimWorld\Data\Biotech\Defs\ResearchProjectDefs\ResearchProjects_Misc.xml:29`
  defines `Xenogermination`;
- `C:\Games\RimWorld\Data\Biotech\Defs\ResearchProjectDefs\ResearchProjects_Misc.xml:54`
  defines `GeneProcessor`.

Growth vats and incubation:

- `C:\Games\RimWorld\Data\Biotech\Defs\ThingDefs_Buildings\Buildings_Misc.xml:331`
  defines `GrowthVat`;
- `C:\Games\RimWorld\Data\Biotech\Defs\WorkGiverDefs\WorkGivers.xml:73`
  defines `EnterGrowthVat`;
- `C:\Games\RimWorld\Data\Biotech\Defs\WorkGiverDefs\WorkGivers.xml:198`
  defines `HaulToGrowthVat`.

Mechtech:

- `C:\Games\RimWorld\Data\Biotech\Defs\ThingDefs_Buildings\Buildings_Production.xml:43`
  defines `MechGestator`;
- `C:\Games\RimWorld\Data\Biotech\Defs\ThingDefs_Buildings\Buildings_Production.xml:402`
  defines `SubcoreSoftscanner`;
- `C:\Games\RimWorld\Data\Biotech\Defs\ThingDefs_Buildings\Buildings_Production.xml:431`
  defines `SubcoreRipscanner`;
- `C:\Games\RimWorld\Data\Biotech\Defs\ResearchProjectDefs\ResearchProjects_Mechanitor.xml:10`
  defines `BasicMechtech`;
- `C:\Games\RimWorld\Data\Biotech\Defs\ResearchProjectDefs\ResearchProjects_Mechanitor.xml:35`
  defines `StandardMechtech`;
- `C:\Games\RimWorld\Data\Biotech\Defs\ResearchProjectDefs\ResearchProjects_Mechanitor.xml:63`
  defines `HighMechtech`;
- `C:\Games\RimWorld\Data\Biotech\Defs\ResearchProjectDefs\ResearchProjects_Mechanitor.xml:96`
  defines `UltraMechtech`.

Pollution and waste:

- `C:\Games\RimWorld\Data\Biotech\Defs\WorldGeneration\WorldGenerator.xml:5`
  defines world `Pollution`;
- `C:\Games\RimWorld\Data\Biotech\Defs\ThingDefs_Items\Items_Resource_Manufactured.xml:280`
  defines `Wastepack`;
- `C:\Games\RimWorld\Data\Biotech\Defs\ThingDefs_Buildings\Buildings_Misc.xml:646`
  defines `WastepackAtomizer`.

Implication: one `BioTechService` would be the wrong architecture. We need
separate services with explicit boundaries.

## 4. How The Systems Connect To Living World

### 4.1 World Ownership

Everything must have an owner:

- animal herds owned by a settlement, caravan, army or trader;
- seed stock owned by farms, seed banks or caravans;
- embryos, ova and xenogerms owned by laboratories or traders;
- gene banks and processors owned as infrastructure;
- mech gestators, subcores and mech cohorts owned by mechanitor-capable groups;
- wastepacks and polluted zones owned as environmental liabilities.

If ownership is missing, the feature becomes fake. A destroyed caravan must not
only remove pawns; it should remove pack animals, embryos, rare seed stock,
genepacks, medicine, components and research samples that were really being
transported.

### 4.2 Economy And Production

Bio projects must be downstream of economy:

- power is needed for growth vats, gene equipment and mech gestation;
- nutrition is needed for vats, animal breeding and population growth;
- medicine controls survival and failure rates;
- components and advanced components gate advanced labs;
- steel and construction capacity gate buildings;
- food surplus gates births and large animal cohorts;
- toxic waste and pollution add future costs.

Current production profiles use biome, hilliness, growing days, rainfall,
temperature and tech. That is a good seed, but future production must also use:

- soil and water access;
- infrastructure level;
- tool/factory quality;
- specialist availability;
- storage capacity;
- logistics;
- pollution and disease pressure;
- local strain/herd quality.

### 4.3 Demography

Biotech touches demography, but it should not replace it.

Normal births should remain part of `Demography`. Growth vats should be a late
modifier that accelerates or bypasses parts of a lifecycle at high cost. A child
created from a vat still needs:

- identity;
- faction/settlement owner;
- age band;
- family or origin record;
- health and defect risk;
- education/skill penalty or modifier;
- ideology/diplomacy consequences.

Human embryos and growth vats should be disabled or abstracted without Biotech.
Animal/crop selection can run without Biotech because it is Living World's own
global ecology/economy layer.

### 4.4 Ecology

Animal breeding and crop selection belong to ecology first.

Animals should be cohorts, not hidden pawns:

- species;
- count by age band;
- health;
- fertility;
- adaptation;
- diversity;
- disease pressure;
- useful traits such as milk, wool, pack capacity, combat temperament.

Crops should be strains:

- crop def;
- yield;
- growth speed;
- drought/cold/heat resistance;
- disease resistance;
- soil demand;
- generation;
- owner and known intel level.

This prevents a situation where the UI says a settlement has 500 elite animals
but no animal can ever be bought, stolen, killed, diseased, migrated or used by a
raid.

### 4.5 Military

Military consequences must be real:

- war animals come from cohorts;
- mechanized raids come from machine capacity and mechtech;
- gene-enhanced troops come from faction capability and existing citizens;
- casualties damage breeding lines and trained specialist pools;
- destroyed labs delay future elite troop recovery;
- stolen samples can improve another faction later;
- toxic weapons and waste create ecological consequences.

This also answers the raid philosophy: raids should eventually happen because a
faction learned something, needed something, had logistics, had people/machines
available, and accepted the risk. They should not happen just because the
storyteller rolled a hostile event.

### 4.6 Diplomacy, Culture And Law

Gene work, slavery, children, pregnancy, animal sacrifice, pollution and
mechanitors are faction-cultural subjects.

Living World should track policy bands:

- accepts gene editing;
- uses growth vats;
- rejects child vat growth;
- treats embryos as contraband;
- tolerates human experimentation;
- relies on animal husbandry;
- uses mechtech;
- exports pollution;
- hides lab capability.

These bands should affect goodwill, trade, refugees, espionage, raids, quests
and civil unrest.

### 4.7 Intel And Visibility

The player should not know everything. Information should come from:

- direct visit or active map observation;
- trade records;
- scouts;
- prisoners;
- refugees;
- intercepted caravans;
- stolen samples;
- allied reports;
- visible world events;
- city public policy, if that city openly advertises it.

Hidden exact values should remain hidden. UI should show bands:

- rumored, reported, confirmed;
- poor, adequate, strong, elite;
- stable, unstable, contaminated;
- no known lab, basic lab, advanced lab;
- no known line, improved line, specialized line.

This keeps the world interesting and prevents the player from having omniscient
spreadsheets.

## 5. The Five Required Angles

### 5.1 Gameplay Angle

The system is worth adding only if it changes decisions.

Good outcomes:

- the player can starve a faction's animal program by raiding feed stores;
- a faction with good seed stock recovers from famine faster;
- a war kills breeding adults and weakens future caravans;
- a lab raid delays elite xenotype soldiers;
- a trader leaking rare goods increases future raid/scout interest;
- a toxic settlement becomes rich but causes refugees and disease;
- a low-tech tribe cannot suddenly produce growth-vat soldiers.

Bad outcomes:

- numbers only appear in debug UI;
- 57 listed citizens never exist in any materialized situation;
- a growth vat creates people without food, power, parents, risk or social cost;
- every faction gets the same lab because the code needs test data.

### 5.2 Simulation Angle

The order of simulation should be:

1. environment and climate update;
2. normal production;
3. consumption and health;
4. animal/crop cohort update;
5. facility capacity update;
6. bio/mech projects consume inputs;
7. failures and events are recorded;
8. outputs become ledger assets;
9. intel is generated only through plausible sources;
10. active map materialization uses the ledger state.

BioTech must be downstream of economy/ecology, not a parallel generator.

### 5.3 Data Ownership Angle

Source of truth:

- Living World owns off-map state;
- RimWorld owns active-map pawns, items and buildings while they exist;
- adapters reconcile active-map outcomes back into Living World;
- compact ledger IDs survive materialization;
- `thingIDNumber` is not enough as permanent identity because active pawns can be
  despawned, removed, garbage-collected or regenerated.

Save data should store:

- compact cohorts;
- project progress;
- facility capability;
- owned samples and stock;
- intel confidence;
- event history.

It should not store one hidden pawn per off-map animal, child, embryo, gene or
mech.

### 5.4 Performance Angle

Main risks:

- per-citizen scans across 20k-100k citizens;
- per-animal pawn storage;
- per-gene records for every hidden citizen;
- UI listing thousands of rows;
- large XML save sections;
- ticking every settlement every game tick;
- resolving too much exact information for hidden settlements.

Rules:

- daily or monthly schedulers for global cohorts;
- caches for settlement/faction aggregates;
- compact bands where exact detail is not visible or needed;
- materialize only active subsets;
- cap UI rows and use drill-down;
- stress tests must include 20k, 50k and 100k citizens plus animal/crop cohorts.

### 5.5 Compatibility Angle

DLC gates:

- no Biotech: animal/crop selection works; human embryos, growth vats, xenogerms
  and mechtech are disabled or abstracted;
- Biotech: adapters can map outputs into vats, embryos, xenotypes and mechtech;
- Ideology: ethics, slavery, child growth, rituals and taboos influence policy;
- Royalty: imperial tech, permits, noble labs and diplomatic restrictions can
  affect advanced facilities;
- Anomaly: mutation/containment should be feature-gated, not default BioTech.

Mod compatibility:

- do not copy external mods;
- do not make RimWar the foundation;
- expose Living World API when core state is stable;
- adapters come after the core simulation is coherent;
- if another mod materializes pawns/animals, reconcile through public hooks where
  possible instead of patching everything.

## 6. Proposed Domain Model

### SettlementCapability

A compact profile of what a settlement can support:

- housing capacity;
- food storage;
- medicine storage;
- power capacity;
- sterile lab capacity;
- animal capacity;
- crop/greenhouse capacity;
- research capacity;
- mechanical production capacity;
- pollution handling.

First core slice exists. Remaining work is to seed these values from RimWorld
world data, expose them as intel bands, and make later projects consume them.

### SpecialistPool

A settlement aggregate, not individual pawns:

- farmers;
- handlers;
- doctors;
- researchers;
- engineers;
- geneticists;
- mechanitors;
- soldiers;
- diplomats.

First core slice exists. Remaining work is to derive and update the pools from
demography, education, migration, deaths, raids and active-map sync.

### WorldAnimalCohort

Compact animal population:

- species;
- owner;
- adult/juvenile counts;
- health;
- fertility;
- diversity;
- adaptation;
- trait bands;
- materialization debt if active-map animals were spawned.

### CropStrain

Owned seed/genetic quality:

- crop;
- owner;
- yield band;
- growth speed band;
- disease resistance;
- temperature/rainfall/soil adaptation;
- generation;
- source confidence.

### BioFacility

Ledger facility:

- kind;
- owner;
- capacity;
- tech tier;
- power need;
- sterile quality;
- specialist need;
- active slots;
- damaged/disabled state.

### BioProject

Long-running project:

- kind: selection, incubation, embryo preservation, gene research, disease
  resistance, strain stabilization;
- owner;
- input assets;
- resource costs;
- facility slot;
- progress;
- risk;
- expected output;
- failure consequences.

### GeneticAsset

High-level asset for human/xenotype work:

- gene pack, xenogerm, ovum, embryo, sample or lineage;
- owner;
- source confidence;
- legality/policy flags;
- known value band;
- Biotech-only materialization data.

The first implementation should not model every gene for every citizen. Track
capability and notable assets first.

### MechProductionCapability

Machine industry, separate from demography:

- mechtech tier;
- gestator capacity;
- subcore supply;
- mechanitor/control capacity;
- power need;
- steel/component need;
- waste output;
- available mech cohort bands.

### PollutionFootprint

World ecological pressure:

- owner or source;
- tile/region;
- intensity;
- trend;
- health effect;
- crop/animal effect;
- migration pressure;
- cleanup capability.

## 7. Materialization Contract

A ledger asset becomes a RimWorld object only when needed.

Examples:

- caravan sells 3 muffalo: reduce `WorldAnimalCohort`, materialize 3 pawns in
  the trade context;
- raid uses 2 war animals: reserve from cohort, bind spawned animal pawns, return
  or kill them through reconciliation;
- trader carries embryos: create trade items only for the active caravan/trader;
- player raids lab: materialize relevant building/items for that map and sync
  stolen/destroyed outcomes;
- growth vat child appears: only when the project completes and an active map or
  quest needs the child;
- mech raid: reserve from mech capability/cohort, materialize mechs, reconcile
  destroyed or escaped machines.

Every materialization needs:

- ledger asset ID;
- owner before materialization;
- active object identifier;
- expected return/destruction path;
- timeout/reconciliation policy;
- event log entry.

## 8. AI Settlements Building And Relocation

AI settlements should be able to develop, decline and relocate, but not by
editing the active map in the background.

### Building Up

Off-map settlements should improve through ledger projects:

- build housing;
- improve farms;
- build roads/storage;
- add workshops;
- add clinics;
- build labs;
- fortify defenses;
- build greenhouses;
- expand animal facilities;
- add power and pollution handling.

These projects consume resources and specialists. When the player later visits,
the generated/updated map can use the ledger profile to choose buildings and
stockpiles.

### Relocation

Relocation should be possible through migration/founding projects:

- famine relocation;
- war evacuation;
- pollution evacuation;
- strategic colonization;
- nomad seasonal movement;
- refugee camp becoming a permanent settlement;
- destroyed settlement rebuilding nearby.

The move must transfer people, animals, resources, seed stock, specialists and
known technologies. It should leave ruins, claims, refugees or abandoned assets
behind where appropriate.

## 9. Failure Modes

Failures are essential because they make systems feel physical.

Possible failures:

- power loss kills embryos or pauses vats;
- medicine shortage increases defect/death risk;
- low diversity creates inbreeding;
- disease spreads through dense herds or settlements;
- pollution reduces fertility and crop yield;
- poor specialists create bad outcomes;
- raids destroy labs, seed banks or animal pens;
- stolen samples transfer advantage to another faction;
- bad storage loses seeds/embryos;
- taboo research causes unrest or faction hostility;
- mechanitor industry creates waste and requires subcores/control capacity.

Each failure should create a `WorldEvent` and should change future outcomes.

## 10. What We Still Missed Beyond BioTech

The following systems should be documented and eventually modeled because they
control whether BioTech is believable:

| Layer | Missing Piece | Why It Matters |
|---|---|---|
| Infrastructure | housing, storage, farms, labs, roads, power | gates population, production, labs and logistics |
| Labor | specialist pools and education | gates doctors, geneticists, handlers, engineers and officers |
| Health | sanitation, medicine, epidemics | prevents population from being a simple growth curve |
| Ecology | animal/crop cohorts, pests, disease | makes selection and food production real |
| Technology | diffusion, theft, loss, secrecy | explains why factions differ over time |
| Logistics | roads, pack animals, vehicles, fuel, seasons | explains trade, war reach and migration |
| Law/Culture | policy, taboo, crime, unrest | makes gene work and growth vats politically meaningful |
| Ruins | claims, salvage, refugees, rebuilding | makes destroyed settlements continue to matter |
| Markets | price, scarcity, contracts | makes rare seeds, embryos, animals and genes valuable |
| Espionage | scouts, prisoners, traders, spies | controls information and raid motivation |
| Pollution | waste, tox adaptation, cleanup | connects mechtech and industry to ecology |

## 11. Recommended Implementation Order

Compatibility adapters are secondary. The core should be completed in this order.

### P0: Settlement Capability Foundation

Add compact infrastructure and specialist profiles.

Acceptance:

- settlement can answer whether it has housing, food storage, power, labs,
  animal capacity and specialist capacity; **implemented in Core**;
- values save/load; **implemented in Core**;
- UI shows bands through intel, not exact hidden values.

### P1: Animal And Crop Cohorts

Add `WorldAnimalCohort` and `CropStrain`.

Acceptance:

- animal/crop assets are owned;
- production can use strain/herd quality;
- trade/raid/materialization can reduce real counts;
- no hidden animal pawns are saved.

### P2: Project Queue

Add generic settlement projects before BioTech-specific projects.

Acceptance:

- project consumes resources over days;
- project can fail;
- project output becomes owned ledger state;
- project creates events and visible intel bands.

### P3: Selection Projects

Implement animal/crop selection first.

Acceptance:

- traits drift over generations;
- diversity and disease pressure matter;
- specialists/tech/biome affect outcomes;
- player can see only learned bands.

### P4: Incubation Projects

Implement animal/crop incubation, not human growth vats yet.

Acceptance:

- consumes nutrition, power, medicine, components and facility slots;
- outputs cohort changes after time;
- failures are real and logged;
- materialization works for trade/raid/active maps.

### P5: Biotech Human/Xenotype Adapter

Add optional Biotech mapping.

Acceptance:

- disabled cleanly without Biotech;
- human embryos/growth vats/xenogerms are gated;
- no free pawn factories;
- ideology/diplomacy/intel consequences exist.

### P6: Mechtech And Pollution

Add machine industry separately.

Acceptance:

- mechs use mechtech, subcores, power, steel/components and control capacity;
- waste/pollution affects ecology and migration;
- mech raids do not draw from human population but still draw from real
  machine capacity.

## 12. Design Decisions

1. Living World remains source of truth for off-map systems.
2. BioTech is not a single module. It splits into demography, xenogenetics,
   ecology, economy, mechtech and pollution.
3. Animal/crop selection comes before human gene work.
4. Growth vats and human embryos are late and Biotech-gated.
5. Mechs are machine industry, not population.
6. Exact hidden values are not globally visible.
7. Every advanced output must consume resources, facilities, specialists and
   time.
8. Materialization must always reconcile back to the ledger.
9. AI settlements can build and relocate through ledger projects.
10. Save data must stay compact: cohorts, bands and projects, not hidden pawns.

## 13. Practical Next Step

The next useful implementation is not an incubator. It is
`SettlementCapability` plus `SpecialistPool` as compact, saved settlement
aggregates.

That gives every later system a real answer to:

- does this settlement have enough housing?
- can it power a lab?
- can it store medicine?
- does it have animal capacity?
- does it have a doctor, handler, researcher or engineer pool?
- can it hide or advertise its capability?

After that, animal/crop cohorts can be added without waiting on Biotech. Then
selection and incubation become meaningful instead of decorative.
