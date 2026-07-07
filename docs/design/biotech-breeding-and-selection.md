# Living World - BioTech, Breeding And Selection

Date: 2026-07-08

Status: design baseline. This document defines how incubators, crossbreeding,
selection and genetic technology fit into Living World without breaking the
ledger-first architecture.

## 1. Core Principle

BioTech systems must not create free pawns, animals or food.

Everything starts as ledger state:

- owned animal cohorts;
- crop strains;
- breeding lines;
- embryos, eggs, samples or xenogerms;
- facilities and projects;
- input resources;
- specialists and technology;
- risks and failures.

Pawns are materialized only when an animal, child, clone, caravan load or
experiment becomes relevant on an active RimWorld map.

## 2. Scope

The feature should be split into four layers.

### Layer A: Animal Selection

Animal populations are stored as lightweight cohorts, not inactive pawns.

Example:

```text
AnimalCohort
  species: Muffalo
  owner: Settlement:18
  count: 42
  adults: 31
  juveniles: 11
  traits:
    milkYield: 0.62
    woolYield: 0.71
    fertility: 0.48
    coldAdaptation: 0.82
    aggression: 0.14
  diversity: 0.76
  diseasePressure: 0.11
```

Gameplay uses:

- better pack animals for caravans;
- more milk/wool/meat;
- war animals;
- biome-adapted herds;
- trade goods;
- disease and inbreeding risk.

### Layer B: Crop Selection

Crops are stored as strains. A settlement can improve or degrade local seed
quality over time.

Example:

```text
CropStrain
  crop: Corn
  owner: Settlement:18
  yield: 0.68
  growthSpeed: 0.51
  droughtResistance: 0.24
  coldResistance: 0.12
  diseaseResistance: 0.44
  soilDemand: 0.77
```

Gameplay uses:

- same workers produce different outputs by strain;
- crop failures can destroy or damage strains;
- traders can leak information about valuable seeds;
- raiders can target seed stores and greenhouses.

### Layer C: Incubation Projects

Incubators are projects that consume owned resources over time.

Example:

```text
IncubationProject
  owner: Settlement:18
  species: Wolf
  line: Combat Wolf Line A
  embryos: 6
  progressTicks: 180000
  requiredTicks: 360000
  nutrientNeedPerDay: 12
  powerNeed: 600W
  medicineNeed: IndustrialMedicine
  componentNeed: 2
  specialistNeed: AnimalHandler or Researcher
  expectedViableBirths: 4
  defectRisk: 0.18
```

The project can fail if the settlement loses power, food, medicine, staff,
temperature control or security.

### Layer D: Human And Xenotype Engineering

This is later-stage work. It must be gated behind Biotech compatibility and
ideological/faction rules.

Allowed early:

- record that a faction has gene-tech capability;
- record known xenotype bands through intel;
- let advanced factions have better medical/birth outcomes.

Deferred:

- full human cloning;
- artificial womb population factories;
- xenogerm editing;
- child growth vat integration;
- custom xenotype creation.

Reason: human gene systems touch Biotech, ideology, slavery, children, faction
ethics, pawn generation and save compatibility. That is too wide for the first
BioTech slice.

## 3. Proposed Core Entities

### `WorldAnimalCohort`

Represents many off-map animals as a compact record. It should not become one
RimWorld pawn per animal until materialization.

Fields:

- id;
- species key;
- owner id;
- count by age band;
- health band;
- fertility;
- trait vector;
- diversity;
- last simulated tick.

### `CropStrain`

Represents seed genetics and adaptation for a crop.

Fields:

- crop def name;
- owner id;
- trait vector;
- adaptation biome/temperature/rainfall hints;
- disease resistance;
- generation;
- source confidence for player-visible intel.

### `BreedingLine`

Represents deliberate selection history.

Fields:

- line id;
- target species/crop;
- owner;
- selected trait priorities;
- generation;
- diversity;
- quality band;
- known defects.

### `BioFacility`

Represents ledger-owned capacity.

Examples:

- ranch;
- stable;
- kennel;
- greenhouse;
- seed bank;
- laboratory;
- incubator;
- growth vat;
- gene lab.

Fields:

- owner settlement;
- facility kind;
- capacity;
- tech tier;
- power requirement;
- sterile quality;
- staff requirement;
- active project slots.

### `BioProject`

Represents work in progress.

Kinds:

- selective breeding;
- crop strain selection;
- incubation;
- embryo preservation;
- disease resistance program;
- gene research.

Fields:

- project id;
- owner;
- kind;
- target species/crop/xenotype;
- input assets;
- resource costs;
- progress;
- risk;
- expected outputs.

## 4. Services

### `SelectionService`

Runs long-term trait drift for animal cohorts and crop strains.

Inputs:

- parent cohorts/strains;
- target trait priorities;
- handler/farmer/researcher capacity;
- technology;
- nutrition and medicine;
- biome and climate.

Outputs:

- updated trait bands;
- new breeding line generation;
- failed selection events;
- inbreeding/disease pressure.

### `IncubationService`

Runs incubation projects.

Inputs:

- facility capacity;
- embryo/egg/sample count;
- power;
- nutrients;
- medicine;
- components;
- sterile quality;
- specialist capacity.

Outputs:

- viable offspring added to `WorldAnimalCohort`;
- failed embryos;
- defect risk;
- disease outbreak risk;
- event history.

### `CropStrainService`

Runs crop line development and seed bank survival.

Inputs:

- crop strain;
- biome;
- soil quality;
- rainfall;
- temperature;
- greenhouse facility;
- farmer skill/profession aggregate;
- pests/disease events.

Outputs:

- yield/growth/resistance changes;
- seed loss;
- crop failure events;
- trade surplus.

### `BioIntelService`

Controls what the player knows.

The UI must not show exact hidden genetic values unless the player has a direct
source. It should show bands:

- common / improved / specialized / unstable / elite;
- low / medium / high disease risk;
- rumored / trader report / direct visit / captured sample.

## 5. Daily Simulation Flow

Suggested order in the daily world tick:

1. production consumes and creates normal resources;
2. animals consume feed and update health/fertility;
3. crop strains update from climate and disease pressure;
4. breeding projects progress;
5. incubation projects consume resources and progress;
6. failures create disease, lost embryos, wasted medicine or damaged facilities;
7. outputs become cohorts/strains/resources;
8. intel reports are created only through scouts, traders, prisoners, direct visit
   or visible events.

This keeps BioTech downstream of economy and ecology, not a separate generator.

## 6. Gameplay Consequences

BioTech should create real strategic consequences:

- destroying a gene lab removes future elite animals, not just abstract value;
- raiding a seed bank can reduce food production next year;
- killing breeding adults damages a line's diversity;
- stealing embryos gives another faction a military or economic advantage;
- trading rare animals or seeds leaks intel;
- famine slows or cancels incubation;
- disease can wipe out inbred herds;
- advanced factions can recover population and animal losses faster;
- low-tech factions rely on natural selection and animal husbandry.

## 7. Balance Rules

Avoid snowball factories.

Rules:

- every project consumes scarce inputs;
- time cost is mandatory;
- high-quality traits increase defect/disease risk;
- low diversity increases inbreeding risk;
- incubators require power and stable infrastructure;
- advanced outputs need specialists;
- hidden exact values require intel;
- human gene work is late, expensive and DLC-gated.

## 8. DLC And Compatibility Rules

### Without Biotech

Enable:

- animal cohort selection;
- crop strain selection;
- simple incubation for eggs/animals if backed by vanilla-compatible defs.

Disable or degrade:

- xenotype editing;
- growth vats;
- gene packs/xenogerms;
- child vat growth.

### With Biotech

Allow an adapter layer that maps Living World project outputs into Biotech
concepts when materialized, but Living World still owns the off-map source of
truth.

### With Ideology

Ideology can change acceptance and consequences:

- gene editing approved / taboo;
- animal sacrifice / sacred animals;
- slavery and child growth ethics;
- goodwill effects for illegal experiments.

### With Royalty

High-tech labs and gene programs may require noble permits, imperial tech or
special trade relationships.

### With Anomaly

Anomaly should be a separate contamination/risk layer, not the default BioTech
model. BioTech projects can later interact with mutation or containment events,
but only behind explicit feature flags.

## 9. Save And Performance

Do not save every off-map animal as a pawn.

Save compact records:

- cohort count;
- age bands;
- trait vector;
- diversity;
- project progress;
- facility capacity;
- last simulated tick.

Hot-path rules:

- no per-tick scan of all animals;
- no per-tick scan of all crop strains;
- update by daily/monthly scheduler;
- use cached settlement aggregates;
- cap visible UI rows;
- materialize only the small active subset.

## 10. Implementation Slices

### B1: Animal Cohort Ledger

Add `WorldAnimalCohort` and ownership/resource integration.

Acceptance:

- cohorts save/load;
- killing/materializing an animal can reduce a cohort;
- no full pawn storage.

### B2: Crop Strain Ledger

Add `CropStrain` and connect production formulas to strain quality.

Acceptance:

- same settlement produces different food from different strains;
- seed loss affects future production;
- save/load works.

### B3: Selection Projects

Add `BreedingLine` and `SelectionService`.

Acceptance:

- selection changes trait bands over generations;
- inbreeding/diversity is tracked;
- specialists and technology matter.

### B4: Incubation Projects

Add `BioFacility` and `BioProject` for animal incubation.

Acceptance:

- consumes nutrition/power/medicine/components;
- outputs animal cohort changes only after time passes;
- failures are recorded as events.

### B5: Materialization And Trade

Connect cohorts/projects to active-map animals, caravans, raids and trade.

Acceptance:

- buying/selling animals moves real cohort counts;
- war animals in raids are drawn from real cohorts;
- destroyed caravans remove animals and embryos.

### B6: Human/Xenotype Adapter

Add Biotech-aware integration only after animal/crop systems are stable.

Acceptance:

- disabled cleanly without Biotech;
- no uncontrolled pawn factories;
- ideology/diplomacy consequences exist.

## 11. What We Were Still Missing

BioTech revealed broader missing systems that should be documented before the
simulation becomes too narrow.

| System | Why It Matters | Suggested Stage |
|---|---|---|
| Infrastructure and buildings | production, labs, hospitals, roads and defenses need capacity, not just population | Economy before BioTech |
| Housing and carrying capacity | population growth should require space, beds, sanitation and safety | Demography/Economy |
| Specialists, education and skills | labs, medicine, armies and industry need trained people, not generic adults | Demography/Economy |
| Disease and medicine | dense settlements, inbreeding, famine and travel should create epidemics | Economy/BioTech |
| Sanitation and pollution | large farms/labs/industry should create waste, tox risk and disease pressure | Ecology/Economy |
| Technology diffusion | factions should learn, steal, trade or lose technology over time | Diplomacy/Economy |
| Infrastructure damage and repair | raids should damage bridges, roads, power and labs, not only people | War/Economy |
| Logistics and transport capacity | caravans need animals, vehicles, roads, fuel and port access | Economy/World |
| Climate and seasonal shocks | droughts, cold snaps and toxic fallout should affect crops/herds/migration | Ecology |
| Governance and leadership | settlements need leaders, succession, stability and policy decisions | Diplomacy |
| Law, crime and unrest | famine, slavery, gene work and inequality should cause rebellion or collapse | Diplomacy/Demography |
| Religion/ideology/culture | what is acceptable depends on faction culture and DLC ideology | Diplomacy/BioTech |
| Intelligence networks | scouts/traders/prisoners/sensors need a fuller information model | Intel |
| Markets and prices | rare seeds, embryos and animals need trade value and scarcity | Economy |
| Ruins and rebuilding | destroyed settlements should leave recoverable assets, refugees and claims | World/Diplomacy |
| Quests and hospitality | refugees, prisoners, deserters, visitors and rescue quests must sync with ledger | RimWorld integration |
| Naval/air/space routes | later transport should not be road-only on every planet | World/Logistics |

The near-term priority remains core ownership, economy, ecology and world-war
visibility. BioTech should start only after animal/crop ecology has a compact
cohort model, otherwise it will become another virtual number with no gameplay
cause.

