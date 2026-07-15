namespace LivingWorld.Core;

public sealed class WorldState
{
    private readonly Dictionary<EntityId, WorldCitizen> _citizens = new();
    private readonly Dictionary<EntityId, WorldSettlement> _settlements = new();
    private readonly Dictionary<EntityId, WorldArmy> _armies = new();
    private readonly Dictionary<EntityId, WorldCaravan> _caravans = new();
    private readonly Dictionary<EntityId, WorldMission> _missions = new();
    private readonly Dictionary<EntityId, WorldRuin> _ruins = new();
    private readonly Dictionary<EntityId, WorldConflict> _conflicts = new();
    private readonly Dictionary<(EntityId ConflictId, EntityId SettlementId), ConflictClaim> _conflictClaims = new();
    private readonly Dictionary<EntityId, WorldAnimalCohort> _animalCohorts = new();
    private readonly Dictionary<EntityId, AnimalBreedingProject> _animalBreedingProjects = new();
    private readonly Dictionary<(EntityId SettlementId, string CropKind), CropStrain> _cropStrains = new();
    private readonly Dictionary<EntityId, CropStrainProject> _cropStrainProjects = new();
    private readonly Dictionary<(EntityId SettlementId, TechnologyDomain Domain), SettlementTechnology> _settlementTechnologies = new();
    private readonly Dictionary<EntityId, WorldMigrationGroup> _migrationGroups = new();
    private readonly Dictionary<EntityId, WorldIntelReport> _intelReports = new();
    private readonly Dictionary<EntityId, KnownSettlementInfo> _knownSettlementInfos = new();
    private readonly Dictionary<(string FactionId, EntityId SettlementId), FactionSettlementIntel> _factionSettlementIntel = new();
    private readonly Dictionary<EntityId, RaidOpportunity> _raidOpportunities = new();
    private readonly Dictionary<EntityId, RaidIntelFact> _raidIntelFacts = new();
    private readonly Dictionary<EntityId, RaidPreparation> _raidPreparations = new();
    private readonly Dictionary<EntityId, MaterializationLease> _materializationLeases = new();
    private readonly Dictionary<int, RaidPawnLink> _raidPawnLinks = new();
    private readonly Dictionary<EntityId, WorldRaidOutcome> _raidOutcomes = new();
    private readonly Dictionary<EntityId, Drifter> _drifters = new();
    private readonly Dictionary<EntityId, WorldArmyMovement> _armyMovements = new();
    private readonly Dictionary<string, FactionBehavior> _factionBehaviors = new(StringComparer.Ordinal);
    private readonly Dictionary<(string, string), int> _factionRelations = new();
    private readonly HashSet<string> _irreconcilableFactions = new(StringComparer.Ordinal);
    private readonly Dictionary<EntityId, SettlementProductionProfile> _productionProfiles = new();
    private readonly Dictionary<EntityId, SettlementFacility> _settlementFacilities = new();
    private readonly Dictionary<EntityId, SettlementProject> _settlementProjects = new();
    private readonly Dictionary<EntityId, SettlementCapability> _settlementCapabilities = new();
    private readonly Dictionary<EntityId, SpecialistPool> _specialistPools = new();
    private readonly Dictionary<EntityId, SettlementWealthSnapshot> _settlementWealth = new();
    private readonly Dictionary<string, FactionWealthSnapshot> _factionWealth = new(StringComparer.Ordinal);
    private readonly Dictionary<string, WorldFactionRecord> _factionRecords = new(StringComparer.Ordinal);
    private readonly Dictionary<EntityId, SettlementDerivedAggregate> _settlementAggregates = new();
    private readonly Dictionary<string, FactionDerivedAggregate> _factionAggregates = new(StringComparer.Ordinal);
    private readonly Dictionary<EntityId, EntityId> _owners = new();
    private readonly Dictionary<(EntityId OwnerId, string ResourceKey), int> _resources = new();
    private readonly List<WorldEvent> _events = new();
    private readonly Dictionary<EntityKind, long> _nextIds = new();
    private int eventSuppressionDepth;
    private int initialWorldSeedingDepth;
    private int drifterArrivalReservoir;
    private bool derivedAggregatesDirty = true;
    private string? playerFactionId;

    public WorldState(int worldSeed)
    {
        WorldSeed = worldSeed;
    }

    public int WorldSeed { get; }

    public int CurrentTick { get; private set; }

    public string? PlayerFactionId => playerFactionId;

    public IReadOnlyCollection<WorldCitizen> Citizens => _citizens.Values;

    public IReadOnlyCollection<WorldSettlement> Settlements => _settlements.Values;

    public IReadOnlyCollection<WorldArmy> Armies => _armies.Values;

    public IReadOnlyCollection<WorldCaravan> Caravans => _caravans.Values;
    public IReadOnlyCollection<WorldMission> Missions => _missions.Values;

    public IReadOnlyCollection<WorldRuin> Ruins => _ruins.Values;

    public IReadOnlyCollection<WorldConflict> Conflicts => _conflicts.Values;

    public IReadOnlyCollection<ConflictClaim> ConflictClaims => _conflictClaims.Values;

    public IReadOnlyCollection<WorldAnimalCohort> AnimalCohorts => _animalCohorts.Values;

    public IReadOnlyCollection<AnimalBreedingProject> AnimalBreedingProjects => _animalBreedingProjects.Values;

    public IReadOnlyCollection<CropStrain> CropStrains => _cropStrains.Values;

    public IReadOnlyCollection<CropStrainProject> CropStrainProjects => _cropStrainProjects.Values;

    public IReadOnlyCollection<SettlementTechnology> SettlementTechnologies => _settlementTechnologies.Values;

    public IReadOnlyCollection<WorldArmyMovement> ArmyMovements => _armyMovements.Values;

    public IReadOnlyDictionary<string, FactionBehavior> FactionBehaviors => _factionBehaviors;

    public IReadOnlyDictionary<(string, string), int> FactionRelations => _factionRelations;

    public IReadOnlyCollection<string> IrreconcilableFactions => _irreconcilableFactions;

    public IReadOnlyCollection<WorldMigrationGroup> MigrationGroups => _migrationGroups.Values;

    public IReadOnlyCollection<WorldIntelReport> IntelReports => _intelReports.Values;

    public IReadOnlyCollection<KnownSettlementInfo> KnownSettlementInfos => _knownSettlementInfos.Values;

    public IReadOnlyCollection<FactionSettlementIntel> FactionSettlementIntel => _factionSettlementIntel.Values;

    public IReadOnlyCollection<RaidOpportunity> RaidOpportunities => _raidOpportunities.Values;

    public IReadOnlyCollection<RaidIntelFact> RaidIntelFacts => _raidIntelFacts.Values;

    public IReadOnlyCollection<RaidPreparation> RaidPreparations => _raidPreparations.Values;

    public IReadOnlyCollection<MaterializationLease> MaterializationLeases => _materializationLeases.Values;

    public IReadOnlyCollection<RaidPawnLink> RaidPawnLinks => _raidPawnLinks.Values;

    public IReadOnlyCollection<WorldRaidOutcome> RaidOutcomes => _raidOutcomes.Values;

    public IReadOnlyCollection<Drifter> Drifters => _drifters.Values;

    public int DrifterArrivalReservoir => drifterArrivalReservoir;

    public IReadOnlyCollection<SettlementProductionProfile> ProductionProfiles => _productionProfiles.Values;

    public IReadOnlyCollection<SettlementFacility> SettlementFacilities => _settlementFacilities.Values;

    public IReadOnlyCollection<SettlementProject> SettlementProjects => _settlementProjects.Values;

    public IReadOnlyCollection<SettlementCapability> SettlementCapabilities => _settlementCapabilities.Values;

    public IReadOnlyCollection<SpecialistPool> SpecialistPools => _specialistPools.Values;

    public IReadOnlyCollection<SettlementWealthSnapshot> SettlementWealth => _settlementWealth.Values;

    public IReadOnlyCollection<FactionWealthSnapshot> FactionWealth => _factionWealth.Values;

    public IReadOnlyCollection<WorldFactionRecord> FactionRecords => _factionRecords.Values;

    public IReadOnlyList<WorldEvent> Events => _events;

    public bool IsInitialWorldSeedingActive => initialWorldSeedingDepth > 0;

    public void AdvanceToTick(int tick)
    {
        if (tick < CurrentTick)
        {
            return;
        }

        CurrentTick = tick;
    }

    public void RunWithoutEvents(Action action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        eventSuppressionDepth++;
        try
        {
            action();
        }
        finally
        {
            eventSuppressionDepth--;
        }
    }

    public void RunInitialWorldSeeding(Action action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        initialWorldSeedingDepth++;
        try
        {
            RunWithoutEvents(action);
        }
        finally
        {
            initialWorldSeedingDepth--;
        }
    }

    public WorldStateSnapshot CreateSnapshot()
    {
        return new WorldStateSnapshot(
            WorldSeed,
            CurrentTick,
            _settlements.Values.OrderBy(settlement => settlement.Id.Value).ToList(),
            _citizens.Values.OrderBy(citizen => citizen.Id.Value).ToList(),
            _armies.Values.OrderBy(army => army.Id.Value).ToList(),
            _migrationGroups.Values.OrderBy(group => group.Id.Value).ToList(),
            _intelReports.Values.OrderBy(report => report.Id.Value).ToList(),
            _knownSettlementInfos.Values.OrderBy(info => info.SettlementId.Value).ToList(),
            _raidOpportunities.Values.OrderBy(opportunity => opportunity.Id.Value).ToList(),
            _raidPawnLinks.Values.OrderBy(link => link.PawnThingId).ToList(),
            _raidOutcomes.Values.OrderBy(outcome => outcome.ArmyId.Value).ToList(),
            _productionProfiles.Values.OrderBy(profile => profile.SettlementId.Value).ToList(),
            _factionRecords.Values.OrderBy(record => record.FactionId, StringComparer.Ordinal).ToList(),
            _owners
                .OrderBy(pair => pair.Key.Kind)
                .ThenBy(pair => pair.Key.Value)
                .Select(pair => new OwnershipRecord(pair.Key, pair.Value))
                .ToList(),
            _resources
                .OrderBy(pair => pair.Key.OwnerId.Kind)
                .ThenBy(pair => pair.Key.OwnerId.Value)
                .ThenBy(pair => pair.Key.ResourceKey, StringComparer.Ordinal)
                .Select(pair => new ResourceStack(pair.Key.OwnerId, pair.Key.ResourceKey, pair.Value))
                .ToList(),
            _events.OrderBy(worldEvent => worldEvent.Id.Value).ToList(),
            _drifters.Values.OrderBy(drifter => drifter.Id.Value).ToList())
        {
            PlayerFactionId = playerFactionId,
            DrifterArrivalReservoir = drifterArrivalReservoir,
            FactionSettlementIntel = _factionSettlementIntel.Values
                .OrderBy(intel => intel.FactionId, StringComparer.Ordinal)
                .ThenBy(intel => intel.SettlementId.Kind)
                .ThenBy(intel => intel.SettlementId.Value)
                .ToList(),
            SettlementCapabilities = _settlementCapabilities.Values
                .OrderBy(capability => capability.SettlementId.Kind)
                .ThenBy(capability => capability.SettlementId.Value)
                .ToList(),
            SpecialistPools = _specialistPools.Values
                .OrderBy(specialists => specialists.SettlementId.Kind)
                .ThenBy(specialists => specialists.SettlementId.Value)
                .ToList(),
            SettlementWealth = _settlementWealth.Values
                .OrderBy(wealth => wealth.SettlementId.Kind)
                .ThenBy(wealth => wealth.SettlementId.Value)
                .ToList(),
            FactionWealth = _factionWealth.Values
                .OrderBy(wealth => wealth.FactionId, StringComparer.Ordinal)
                .ToList(),
            Caravans = _caravans.Values
                .OrderBy(caravan => caravan.Id.Value)
                .ToList(),
            Missions = _missions.Values
                .OrderBy(mission => mission.Id.Value)
                .ToList(),
            Ruins = _ruins.Values
                .OrderBy(ruin => ruin.Id.Value)
                .ToList(),
            Conflicts = _conflicts.Values
                .OrderBy(conflict => conflict.Id.Value)
                .ToList(),
            ConflictClaims = _conflictClaims.Values
                .OrderBy(claim => claim.ConflictId.Value)
                .ThenBy(claim => claim.SettlementId.Value)
                .ToList(),
            RaidIntelFacts = _raidIntelFacts.Values
                .OrderBy(fact => fact.Id.Value)
                .ToList(),
            RaidPreparations = _raidPreparations.Values
                .OrderBy(preparation => preparation.Id.Value)
                .ToList(),
            MaterializationLeases = _materializationLeases.Values
                .OrderBy(lease => lease.Id.Value)
                .ToList(),
            SettlementFacilities = _settlementFacilities.Values
                .OrderBy(facility => facility.Id.Value)
                .ToList(),
            SettlementProjects = _settlementProjects.Values
                .OrderBy(project => project.Id.Value)
                .ToList(),
            AnimalCohorts = _animalCohorts.Values
                .OrderBy(cohort => cohort.Id.Value)
                .ToList(),
            AnimalBreedingProjects = _animalBreedingProjects.Values
                .OrderBy(project => project.Id.Value)
                .ToList(),
            CropStrains = _cropStrains.Values
                .OrderBy(strain => strain.SettlementId.Value)
                .ThenBy(strain => strain.CropKind, StringComparer.Ordinal)
                .ToList(),
            CropStrainProjects = _cropStrainProjects.Values
                .OrderBy(project => project.Id.Value)
                .ToList(),
            SettlementTechnologies = _settlementTechnologies.Values
                .OrderBy(technology => technology.SettlementId.Value)
                .ThenBy(technology => technology.Domain)
                .ToList()
        };
    }

    public static WorldState FromSnapshot(WorldStateSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var state = new WorldState(snapshot.WorldSeed)
        {
            CurrentTick = snapshot.CurrentTick,
            drifterArrivalReservoir = Math.Max(0, snapshot.DrifterArrivalReservoir),
            playerFactionId = snapshot.PlayerFactionId
        };

        foreach (var settlement in snapshot.Settlements)
        {
            state._settlements.Add(settlement.Id, settlement);
            state.ReserveExistingId(settlement.Id);
        }

        foreach (var citizen in snapshot.Citizens)
        {
            state._citizens.Add(citizen.Id, citizen);
            state.ReserveExistingId(citizen.Id);
        }

        foreach (var army in snapshot.Armies)
        {
            state._armies.Add(army.Id, army);
            state.ReserveExistingId(army.Id);
        }

        foreach (var caravan in snapshot.Caravans)
        {
            state._caravans.Add(caravan.Id, caravan);
            state.ReserveExistingId(caravan.Id);
        }

        foreach (var mission in snapshot.Missions)
        {
            state._missions.Add(mission.Id, mission);
            state.ReserveExistingId(mission.Id);
        }

        foreach (var ruin in snapshot.Ruins)
        {
            state._ruins.Add(ruin.Id, Normalize(ruin));
            state.ReserveExistingId(ruin.Id);
        }

        foreach (var conflict in snapshot.Conflicts)
        {
            state._conflicts.Add(conflict.Id, Normalize(conflict));
            state.ReserveExistingId(conflict.Id);
        }

        foreach (var claim in snapshot.ConflictClaims)
        {
            state._conflictClaims[(claim.ConflictId, claim.SettlementId)] = Normalize(claim);
        }

        foreach (var group in snapshot.MigrationGroups)
        {
            state._migrationGroups.Add(group.Id, group);
            state.ReserveExistingId(group.Id);
        }

        foreach (var report in snapshot.IntelReports)
        {
            state._intelReports.Add(report.Id, report);
            state.ReserveExistingId(report.Id);
        }

        foreach (var info in snapshot.KnownSettlementInfos)
        {
            state._knownSettlementInfos.Add(info.SettlementId, info);
        }

        foreach (var intel in snapshot.FactionSettlementIntel)
        {
            var normalized = Normalize(intel);
            state._factionSettlementIntel[(normalized.FactionId, normalized.SettlementId)] = normalized;
        }

        foreach (var opportunity in snapshot.RaidOpportunities)
        {
            state._raidOpportunities.Add(opportunity.Id, opportunity);
            state.ReserveExistingId(opportunity.Id);
        }

        foreach (var fact in snapshot.RaidIntelFacts)
        {
            state._raidIntelFacts.Add(fact.Id, fact);
            state.ReserveExistingId(fact.Id);
        }

        foreach (var preparation in snapshot.RaidPreparations)
        {
            state._raidPreparations.Add(preparation.Id, preparation);
            state.ReserveExistingId(preparation.Id);
        }

        foreach (var lease in snapshot.MaterializationLeases)
        {
            state._materializationLeases.Add(lease.Id, lease);
            state.ReserveExistingId(lease.Id);
        }

        foreach (var link in snapshot.RaidPawnLinks)
        {
            state._raidPawnLinks.Add(link.PawnThingId, link);
        }

        foreach (var outcome in snapshot.RaidOutcomes)
        {
            state._raidOutcomes.Add(outcome.ArmyId, outcome);
        }

        foreach (var profile in snapshot.ProductionProfiles)
        {
            state._productionProfiles.Add(profile.SettlementId, profile);
            state.SeedTechnologyFromProductionProfile(profile);
        }

        foreach (var facility in snapshot.SettlementFacilities)
        {
            state._settlementFacilities.Add(facility.Id, Normalize(facility));
            state.ReserveExistingId(facility.Id);
        }

        foreach (var project in snapshot.SettlementProjects)
        {
            state._settlementProjects.Add(project.Id, Normalize(project));
            state.ReserveExistingId(project.Id);
        }

        foreach (var cohort in snapshot.AnimalCohorts)
        {
            state._animalCohorts.Add(cohort.Id, Normalize(cohort));
            state.ReserveExistingId(cohort.Id);
        }

        foreach (var project in snapshot.AnimalBreedingProjects)
        {
            state._animalBreedingProjects.Add(project.Id, Normalize(project));
            state.ReserveExistingId(project.Id);
        }

        foreach (var strain in snapshot.CropStrains)
        {
            var normalized = Normalize(strain);
            state._cropStrains[(normalized.SettlementId, normalized.CropKind)] = normalized;
        }

        foreach (var project in snapshot.CropStrainProjects)
        {
            var normalized = Normalize(project);
            state._cropStrainProjects.Add(normalized.Id, normalized);
            state.ReserveExistingId(normalized.Id);
        }

        foreach (var technology in snapshot.SettlementTechnologies)
        {
            var normalized = Normalize(technology);
            state._settlementTechnologies[(normalized.SettlementId, normalized.Domain)] = normalized;
        }

        foreach (var capability in snapshot.SettlementCapabilities)
        {
            state._settlementCapabilities[capability.SettlementId] = Normalize(capability);
        }

        foreach (var specialists in snapshot.SpecialistPools)
        {
            state._specialistPools[specialists.SettlementId] = Normalize(specialists);
        }

        foreach (var wealth in snapshot.SettlementWealth)
        {
            state._settlementWealth[wealth.SettlementId] = wealth;
        }

        foreach (var wealth in snapshot.FactionWealth)
        {
            state._factionWealth[wealth.FactionId] = wealth;
        }

        foreach (var factionRecord in snapshot.FactionRecords)
        {
            state._factionRecords[factionRecord.FactionId] = factionRecord;
        }

        foreach (var ownership in snapshot.Ownership)
        {
            state._owners[ownership.AssetId] = ownership.OwnerId;
        }

        foreach (var resource in snapshot.Resources)
        {
            state._resources[(resource.OwnerId, resource.ResourceKey)] = resource.Quantity;
        }

        foreach (var worldEvent in snapshot.Events)
        {
            state._events.Add(worldEvent);
            state.ReserveExistingId(worldEvent.Id);
        }

        foreach (var drifter in snapshot.Drifters)
        {
            state._drifters.Add(drifter.Id, drifter);
            state.ReserveExistingId(drifter.Id);
        }

        return state;
    }

    public WorldSettlement CreateSettlement(string slug, string name, string factionId)
    {
        ThrowIfNullOrWhiteSpace(slug, nameof(slug));
        ThrowIfNullOrWhiteSpace(name, nameof(name));
        ThrowIfNullOrWhiteSpace(factionId, nameof(factionId));

        var settlement = new WorldSettlement(
            NextId(EntityKind.Settlement),
            slug,
            name,
            factionId);

        _settlements.Add(settlement.Id, settlement);
        MarkDerivedAggregatesDirty();
        AppendEvent(WorldEventKind.SettlementCreated, settlement.Id, $"Settlement {settlement.Id} created.");

        return settlement;
    }

    public WorldCitizen CreateCitizen(
        string name,
        int age,
        Sex sex,
        string profession,
        EntityId settlementId)
    {
        ThrowIfNullOrWhiteSpace(name, nameof(name));
        ThrowIfNullOrWhiteSpace(profession, nameof(profession));

        if (age < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(age), "Citizen age cannot be negative.");
        }

        if (!_settlements.ContainsKey(settlementId))
        {
            throw new InvalidOperationException($"Settlement {settlementId} does not exist.");
        }

        var citizen = new WorldCitizen(
            NextId(EntityKind.Citizen),
            name,
            age,
            sex,
            profession,
            settlementId,
            CitizenStatus.Alive);

        _citizens.Add(citizen.Id, citizen);
        _owners[citizen.Id] = settlementId;
        MarkDerivedAggregatesDirty();
        AppendEvent(WorldEventKind.CitizenCreated, citizen.Id, $"Citizen {citizen.Id} created.");
        AppendEvent(WorldEventKind.OwnershipAssigned, citizen.Id, $"Citizen {citizen.Id} assigned to {settlementId}.");

        return citizen;
    }

    public void ImportCitizen(WorldCitizen citizen)
    {
        if (_citizens.ContainsKey(citizen.Id))
        {
            throw new InvalidOperationException($"Citizen {citizen.Id} already exists.");
        }

        _citizens.Add(citizen.Id, citizen);
        _owners[citizen.Id] = citizen.SettlementId;
        ReserveExistingId(citizen.Id);
        MarkDerivedAggregatesDirty();
        AppendEvent(WorldEventKind.CitizenImported, citizen.Id, $"Citizen {citizen.Id} imported.");
    }

    public WorldArmy CreateArmy(string name, string factionId, EntityId sourceSettlementId)
    {
        ThrowIfNullOrWhiteSpace(name, nameof(name));
        ThrowIfNullOrWhiteSpace(factionId, nameof(factionId));

        if (!IsActiveSettlement(sourceSettlementId))
        {
            throw new InvalidOperationException($"Settlement {sourceSettlementId} is not active.");
        }

        var army = new WorldArmy(
            NextId(EntityKind.Army),
            name,
            factionId,
            sourceSettlementId);

        _armies.Add(army.Id, army);
        _owners[army.Id] = sourceSettlementId;
        AppendEvent(WorldEventKind.ArmyCreated, army.Id, $"Army {army.Id} created from {sourceSettlementId}.");
        AppendEvent(WorldEventKind.OwnershipAssigned, army.Id, $"Army {army.Id} assigned to {sourceSettlementId}.");

        return army;
    }

    public WorldCaravan CreateCaravan(
        string name,
        string factionId,
        EntityId sourceSettlementId,
        EntityId targetSettlementId,
        int departTick,
        int arrivalTick,
        EntityId? crewCitizenId = null)
    {
        ThrowIfNullOrWhiteSpace(name, nameof(name));
        ThrowIfNullOrWhiteSpace(factionId, nameof(factionId));

        if (!IsActiveSettlement(sourceSettlementId))
        {
            throw new InvalidOperationException($"Source settlement {sourceSettlementId} is not active.");
        }

        if (!IsActiveSettlement(targetSettlementId))
        {
            throw new InvalidOperationException($"Target settlement {targetSettlementId} is not active.");
        }

        var caravan = new WorldCaravan(
            NextId(EntityKind.Caravan),
            name,
            factionId,
            sourceSettlementId,
            targetSettlementId,
            Math.Max(0, departTick),
            Math.Max(departTick, arrivalTick),
            CaravanStatus.Traveling,
            crewCitizenId);

        _caravans.Add(caravan.Id, caravan);
        _owners[caravan.Id] = sourceSettlementId;
        AppendEvent(WorldEventKind.CaravanLaunched, caravan.Id, $"Caravan {caravan.Id} departed {sourceSettlementId} for {targetSettlementId}.");
        AppendEvent(WorldEventKind.OwnershipAssigned, caravan.Id, $"Caravan {caravan.Id} assigned to {sourceSettlementId}.");

        return caravan;
    }

    public WorldCaravan? GetCaravan(EntityId caravanId)
    {
        return _caravans.TryGetValue(caravanId, out var caravan)
            ? caravan
            : null;
    }

    public WorldCaravan MarkCaravanArrived(EntityId caravanId)
    {
        if (!_caravans.TryGetValue(caravanId, out var caravan))
        {
            throw new InvalidOperationException($"Caravan {caravanId} does not exist.");
        }

        if (caravan.Status == CaravanStatus.Arrived)
        {
            return caravan;
        }

        if (caravan.Status is CaravanStatus.Destroyed or CaravanStatus.Recalled)
        {
            throw new InvalidOperationException($"Terminal caravan {caravanId} cannot arrive.");
        }

        if (!IsActiveSettlement(caravan.TargetSettlementId))
        {
            return MarkCaravanRecalled(caravanId, "target settlement unavailable");
        }

        foreach (var resource in ResourcesForOwner(caravanId))
        {
            var transfer = TransferResource(
                caravanId,
                caravan.TargetSettlementId,
                resource.ResourceKey,
                resource.Quantity,
                "caravan arrived");
            if (transfer.Status != OwnershipTransferStatus.Success)
            {
                throw new InvalidOperationException(transfer.Reason);
            }
        }

        var arrived = caravan with { Status = CaravanStatus.Arrived };
        _caravans[caravanId] = arrived;
        TravelCrewService.ReturnCrew(this, caravanId, caravan.SourceSettlementId, caravan.CrewCitizenId, "caravan arrived");
        AppendEvent(WorldEventKind.CaravanArrived, caravanId, $"Caravan {caravanId} arrived at {caravan.TargetSettlementId}.");
        return arrived;
    }

    public WorldCaravan MarkCaravanRecalled(EntityId caravanId, string reason)
    {
        ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (!_caravans.TryGetValue(caravanId, out var caravan))
        {
            throw new InvalidOperationException($"Caravan {caravanId} does not exist.");
        }

        if (caravan.Status == CaravanStatus.Recalled)
        {
            return caravan;
        }

        if (caravan.Status is CaravanStatus.Arrived or CaravanStatus.Destroyed)
        {
            throw new InvalidOperationException($"Terminal caravan {caravanId} cannot be recalled.");
        }

        foreach (var resource in ResourcesForOwner(caravanId))
        {
            var transfer = TransferResource(
                caravanId,
                caravan.SourceSettlementId,
                resource.ResourceKey,
                resource.Quantity,
                reason);
            if (transfer.Status != OwnershipTransferStatus.Success)
            {
                throw new InvalidOperationException(transfer.Reason);
            }
        }

        var recalled = caravan with { Status = CaravanStatus.Recalled };
        _caravans[caravanId] = recalled;
        TravelCrewService.ReturnCrew(this, caravanId, caravan.SourceSettlementId, caravan.CrewCitizenId, reason);
        AppendEvent(WorldEventKind.CaravanDestroyed, caravanId, $"Caravan {caravanId} recalled: {reason}.");
        return recalled;
    }

    public WorldCaravan DestroyCaravan(EntityId caravanId, string reason)
    {
        ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (!_caravans.TryGetValue(caravanId, out var caravan))
        {
            throw new InvalidOperationException($"Caravan {caravanId} does not exist.");
        }

        if (caravan.Status == CaravanStatus.Destroyed)
        {
            return caravan;
        }

        foreach (var resource in ResourcesForOwner(caravanId))
        {
            SetResourceQuantityForLedger(caravanId, resource.ResourceKey, 0);
        }

        var destroyed = caravan with { Status = CaravanStatus.Destroyed };
        _caravans[caravanId] = destroyed;
        TravelCrewService.MarkCrewMissing(this, caravanId, caravan.SourceSettlementId, caravan.CrewCitizenId, reason);
        AppendEvent(WorldEventKind.CaravanDestroyed, caravanId, $"Caravan {caravanId} destroyed: {reason}.");
        return destroyed;
    }

    public WorldArmyMovement DispatchArmy(EntityId armyId, EntityId targetSettlementId, int arrivalTick)
    {
        if (!_armies.ContainsKey(armyId))
        {
            throw new InvalidOperationException($"Army {armyId} does not exist.");
        }

        if (!IsActiveSettlement(targetSettlementId))
        {
            throw new InvalidOperationException($"Settlement {targetSettlementId} is not active.");
        }

        var movement = new WorldArmyMovement(
            armyId,
            targetSettlementId,
            CurrentTick,
            Math.Max(CurrentTick, arrivalTick),
            ArmyMovementStatus.Traveling)
        {
            StatusTick = CurrentTick
        };

        _armyMovements[armyId] = movement;
        AppendEvent(WorldEventKind.WarbandLaunched, armyId, $"Army {armyId} set out for {targetSettlementId}.");
        return movement;
    }

    public WorldArmyMovement? GetArmyMovement(EntityId armyId)
    {
        return _armyMovements.TryGetValue(armyId, out var movement)
            ? movement
            : null;
    }

    public WorldArmyMovement SetArmyMovementStatus(EntityId armyId, ArmyMovementStatus status)
    {
        if (!_armyMovements.TryGetValue(armyId, out var movement))
        {
            throw new InvalidOperationException($"Army {armyId} has no movement.");
        }

        var updated = movement with { Status = status, StatusTick = CurrentTick };
        _armyMovements[armyId] = updated;
        return updated;
    }

    internal void RestoreArmyMovementForLedger(WorldArmyMovement movement)
    {
        _armyMovements[movement.ArmyId] = movement;
    }

    internal bool RemoveArmyMovementForLedger(EntityId armyId)
    {
        return _armyMovements.Remove(armyId);
    }

    // Removes a terminal (arrived/destroyed) caravan and its asset-ownership entry. A terminal
    // caravan owns no resources (delivered on arrival, zeroed on destroy), so this leaks nothing.
    internal bool RemoveCaravanForLedger(EntityId caravanId)
    {
        _owners.Remove(caravanId);
        return _caravans.Remove(caravanId);
    }

    public WorldMission DispatchMission(
        WorldMissionKind kind,
        string factionId,
        EntityId originSettlementId,
        EntityId targetSettlementId,
        int departTick,
        int arrivalTick,
        string targetFactionId = "",
        int amount = 0,
        EntityId? crewCitizenId = null)
    {
        ThrowIfNullOrWhiteSpace(factionId, nameof(factionId));

        if (!IsActiveSettlement(originSettlementId))
        {
            throw new InvalidOperationException($"Origin settlement {originSettlementId} is not active.");
        }

        if (!IsActiveSettlement(targetSettlementId))
        {
            throw new InvalidOperationException($"Target settlement {targetSettlementId} is not active.");
        }

        var mission = new WorldMission(
            NextId(EntityKind.Mission),
            kind,
            factionId,
            originSettlementId,
            targetSettlementId,
            Math.Max(0, departTick),
            Math.Max(departTick, arrivalTick),
            WorldMissionStatus.Traveling,
            crewCitizenId)
        {
            TargetFactionId = targetFactionId ?? string.Empty,
            Amount = amount,
        };

        _missions.Add(mission.Id, mission);
        return mission;
    }

    public WorldMission? GetMission(EntityId missionId)
    {
        return _missions.TryGetValue(missionId, out var mission) ? mission : null;
    }

    public WorldMission SetMissionStatus(EntityId missionId, WorldMissionStatus status)
    {
        if (!_missions.TryGetValue(missionId, out var mission))
        {
            throw new InvalidOperationException($"Mission {missionId} does not exist.");
        }

        var updated = mission with { Status = status };
        _missions[missionId] = updated;
        return updated;
    }

    public WorldMission FailMission(EntityId missionId, string reason)
    {
        ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (!_missions.TryGetValue(missionId, out var mission))
        {
            throw new InvalidOperationException($"Mission {missionId} does not exist.");
        }

        if (mission.Status == WorldMissionStatus.Failed)
        {
            return mission;
        }

        var failed = mission with { Status = WorldMissionStatus.Failed };
        _missions[missionId] = failed;
        TravelCrewService.ReturnCrew(this, missionId, mission.OriginSettlementId, mission.CrewCitizenId, reason);
        AppendEvent(WorldEventKind.WorldMissionDisrupted, missionId, $"Mission {missionId} failed: {reason}.");
        return failed;
    }

    internal bool RemoveMissionForLedger(EntityId missionId)
    {
        return _missions.Remove(missionId);
    }

    public WorldCitizen MarkCitizenDead(EntityId citizenId, string reason)
    {
        ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (!_citizens.TryGetValue(citizenId, out var citizen))
        {
            throw new InvalidOperationException($"Citizen {citizenId} does not exist.");
        }

        if (citizen.Status == CitizenStatus.Dead)
        {
            return citizen;
        }

        var dead = citizen with { Status = CitizenStatus.Dead };
        _citizens[citizenId] = dead;
        MarkDerivedAggregatesDirty();
        AppendEvent(WorldEventKind.CitizenDied, citizenId, $"Citizen {citizenId} died: {reason}.");
        return dead;
    }

    public WorldSettlement CaptureSettlement(EntityId settlementId, string newFactionId)
    {
        ThrowIfNullOrWhiteSpace(newFactionId, nameof(newFactionId));

        if (!_settlements.TryGetValue(settlementId, out var settlement))
        {
            throw new InvalidOperationException($"Settlement {settlementId} does not exist.");
        }

        // A destroyed/abandoned settlement is a ruin: it must not change hands via capture (only
        // ReclaimRuin may resurrect it), mirroring ChangeSettlementFaction. Capturing a ruin would flip
        // its faction while leaving it Destroyed and fire a false SettlementCaptured event. No-op instead.
        if (!settlement.IsActive)
        {
            return settlement;
        }

        // Surviving residents keep their SettlementId and ownership, so they simply belong to
        // the capturing faction now.
        var previousFaction = settlement.FactionId;
        var captured = settlement with { FactionId = newFactionId };
        _settlements[settlementId] = captured;
        MarkDerivedAggregatesDirty();
        AppendEvent(
            WorldEventKind.SettlementCaptured,
            settlementId,
            $"Settlement {settlementId} captured by {newFactionId} from {previousFaction}.");
        return captured;
    }

    internal WorldSettlement SetSettlementLifecycleStatusForLedger(
        EntityId settlementId,
        SettlementLifecycleStatus status)
    {
        if (!_settlements.TryGetValue(settlementId, out var settlement))
        {
            throw new InvalidOperationException($"Settlement {settlementId} does not exist.");
        }

        var updated = settlement with { Status = status };
        _settlements[settlementId] = updated;
        MarkDerivedAggregatesDirty();
        return updated;
    }

    internal WorldSettlement SetSettlementFactionAndStatusForLedger(
        EntityId settlementId,
        string factionId,
        SettlementLifecycleStatus status)
    {
        ThrowIfNullOrWhiteSpace(factionId, nameof(factionId));

        if (!_settlements.TryGetValue(settlementId, out var settlement))
        {
            throw new InvalidOperationException($"Settlement {settlementId} does not exist.");
        }

        var updated = settlement with { FactionId = factionId, Status = status };
        _settlements[settlementId] = updated;
        MarkDerivedAggregatesDirty();
        return updated;
    }

    internal WorldRuin CreateRuinForLedger(
        WorldSettlement settlement,
        string claimFactionId,
        RuinSalvageBand salvageBand,
        RuinDangerBand dangerBand,
        int tick)
    {
        if (settlement == null)
        {
            throw new ArgumentNullException(nameof(settlement));
        }

        ThrowIfNullOrWhiteSpace(claimFactionId, nameof(claimFactionId));

        var ruin = Normalize(new WorldRuin(
            NextId(EntityKind.Ruin),
            settlement.Id,
            settlement.Slug,
            settlement.Name,
            settlement.FactionId,
            claimFactionId,
            salvageBand,
            dangerBand,
            Math.Max(0, tick),
            RuinStatus.Active,
            null,
            Math.Max(0, tick)));
        _ruins.Add(ruin.Id, ruin);
        return ruin;
    }

    internal WorldRuin RecordRuinForLedger(WorldRuin ruin)
    {
        if (ruin == null)
        {
            throw new ArgumentNullException(nameof(ruin));
        }

        if (ruin.Id.Kind != EntityKind.Ruin)
        {
            throw new InvalidOperationException($"Ruin id {ruin.Id} is not a ruin id.");
        }

        var normalized = Normalize(ruin);
        _ruins[normalized.Id] = normalized;
        ReserveExistingId(normalized.Id);
        return normalized;
    }

    public WorldRuin? GetRuin(EntityId ruinId)
    {
        return _ruins.TryGetValue(ruinId, out var ruin)
            ? ruin
            : null;
    }

    internal bool RemoveRuinForLedger(EntityId ruinId)
    {
        return _ruins.Remove(ruinId);
    }

    internal WorldConflict CreateConflictForLedger(string factionA, string factionB, int tick)
    {
        ThrowIfNullOrWhiteSpace(factionA, nameof(factionA));
        ThrowIfNullOrWhiteSpace(factionB, nameof(factionB));

        var conflict = Normalize(new WorldConflict(
            NextId(EntityKind.Conflict),
            factionA,
            factionB,
            WorldConflictStatus.Active,
            Math.Max(0, tick),
            Math.Max(0, tick),
            WarExhaustionA: 0,
            WarExhaustionB: 0,
            TruceExpiresTick: 0,
            RefugeesCreated: 0));
        _conflicts.Add(conflict.Id, conflict);
        return conflict;
    }

    internal WorldConflict RecordConflictForLedger(WorldConflict conflict)
    {
        if (conflict == null)
        {
            throw new ArgumentNullException(nameof(conflict));
        }

        if (conflict.Id.Kind != EntityKind.Conflict)
        {
            throw new InvalidOperationException($"Conflict id {conflict.Id} is not a conflict id.");
        }

        var normalized = Normalize(conflict);
        _conflicts[normalized.Id] = normalized;
        ReserveExistingId(normalized.Id);
        return normalized;
    }

    public WorldConflict? GetConflict(EntityId conflictId)
    {
        return _conflicts.TryGetValue(conflictId, out var conflict)
            ? conflict
            : null;
    }

    internal ConflictClaim RecordConflictClaimForLedger(ConflictClaim claim)
    {
        if (claim == null)
        {
            throw new ArgumentNullException(nameof(claim));
        }

        if (!_conflicts.ContainsKey(claim.ConflictId))
        {
            throw new InvalidOperationException($"Conflict {claim.ConflictId} does not exist.");
        }

        if (!_settlements.ContainsKey(claim.SettlementId))
        {
            throw new InvalidOperationException($"Settlement {claim.SettlementId} does not exist.");
        }

        var normalized = Normalize(claim);
        _conflictClaims[(normalized.ConflictId, normalized.SettlementId)] = normalized;
        return normalized;
    }

    // A faction expands by relocating some of a settlement's living adults into a brand-new
    // settlement of the same faction — the people move, none are created (population is conserved).
    public WorldSettlement ExpandSettlement(EntityId sourceSettlementId, string slug, string name, int settlerCount)
    {
        ThrowIfNullOrWhiteSpace(slug, nameof(slug));
        ThrowIfNullOrWhiteSpace(name, nameof(name));

        if (settlerCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settlerCount), "Expansion requires at least one settler.");
        }

        if (!_settlements.TryGetValue(sourceSettlementId, out var source))
        {
            throw new InvalidOperationException($"Settlement {sourceSettlementId} does not exist.");
        }

        if (_settlements.Values.Any(settlement => string.Equals(settlement.Slug, slug, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Settlement slug {slug} already exists.");
        }

        var settlers = _citizens.Values
            .Where(citizen => citizen.SettlementId == sourceSettlementId
                && citizen.Status == CitizenStatus.Alive
                && citizen.IsAdult
                && GetOwner(citizen.Id) == sourceSettlementId)
            .OrderBy(citizen => citizen.Id.Value)
            .Take(settlerCount)
            .ToList();

        if (settlers.Count < settlerCount)
        {
            throw new InvalidOperationException(
                $"Settlement {sourceSettlementId} has {settlers.Count} available adult settlers but expansion requested {settlerCount}.");
        }

        var colony = CreateSettlement(slug, name, source.FactionId);
        foreach (var settler in settlers)
        {
            _citizens[settler.Id] = settler with { SettlementId = colony.Id };
            _owners[settler.Id] = colony.Id;
        }
        MarkDerivedAggregatesDirty();

        AppendEvent(
            WorldEventKind.SettlementFounded,
            colony.Id,
            $"Settlement {colony.Id} founded by {source.FactionId} with {settlers.Count} settlers from {sourceSettlementId}.");
        return colony;
    }

    public WorldMigrationGroup StartSettlementExpedition(
        EntityId sourceSettlementId,
        string slug,
        string name,
        int settlerCount,
        int createdTick,
        int arrivalTick)
    {
        ThrowIfNullOrWhiteSpace(slug, nameof(slug));
        ThrowIfNullOrWhiteSpace(name, nameof(name));

        if (settlerCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(settlerCount), "Expansion requires at least one settler.");
        }

        if (!_settlements.TryGetValue(sourceSettlementId, out var source))
        {
            throw new InvalidOperationException($"Settlement {sourceSettlementId} does not exist.");
        }

        if (_settlements.Values.Any(settlement => string.Equals(settlement.Slug, slug, StringComparison.Ordinal))
            || _migrationGroups.Values.Any(group =>
                group.Status == MigrationGroupStatus.Traveling
                && string.Equals(group.PlannedSettlementSlug, slug, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"Settlement slug {slug} already exists or is already planned.");
        }

        var settlers = _citizens.Values
            .Where(citizen => citizen.SettlementId == sourceSettlementId
                && citizen.Status == CitizenStatus.Alive
                && citizen.IsAdult
                && GetOwner(citizen.Id) == sourceSettlementId)
            .OrderBy(citizen => citizen.Id.Value)
            .Take(settlerCount)
            .ToList();

        if (settlers.Count < settlerCount)
        {
            throw new InvalidOperationException(
                $"Settlement {sourceSettlementId} has {settlers.Count} available adult settlers but expansion requested {settlerCount}.");
        }

        var group = CreateMigrationGroup(
            sourceSettlementId: sourceSettlementId,
            targetSettlementId: null,
            factionId: source.FactionId,
            createdTick: createdTick,
            arrivalTick: arrivalTick,
            reason: MigrationService.ReasonSettlementFounding) with
        {
            PlannedSettlementSlug = slug,
            PlannedSettlementName = name
        };
        _migrationGroups[group.Id] = group;

        foreach (var settler in settlers)
        {
            _citizens[settler.Id] = settler with { Status = CitizenStatus.Migrating };
            _owners[settler.Id] = group.Id;
        }

        MoveResourceIfAvailable(sourceSettlementId, group.Id, "PackagedSurvivalMeal", settlerCount * 3, "settler expedition supplies");
        MoveResourceIfAvailable(sourceSettlementId, group.Id, "Steel", settlerCount * 10, "settler expedition materials");
        MarkDerivedAggregatesDirty();

        return group;
    }

    public WorldSettlement CompleteSettlementExpedition(EntityId groupId)
    {
        if (!_migrationGroups.TryGetValue(groupId, out var group))
        {
            throw new InvalidOperationException($"Migration group {groupId} does not exist.");
        }

        if (group.Status == MigrationGroupStatus.Arrived)
        {
            var existing = _settlements.Values.FirstOrDefault(settlement =>
                string.Equals(settlement.Slug, group.PlannedSettlementSlug, StringComparison.Ordinal));
            if (existing != null)
            {
                return existing;
            }
        }

        if (!string.Equals(group.Reason, MigrationService.ReasonSettlementFounding, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(group.PlannedSettlementSlug)
            || string.IsNullOrWhiteSpace(group.PlannedSettlementName))
        {
            throw new InvalidOperationException($"Migration group {groupId} is not a settlement expedition.");
        }

        var slug = UniqueSettlementSlug(group.PlannedSettlementSlug);
        var colony = CreateSettlement(slug, group.PlannedSettlementName, group.FactionId);
        var settlers = _citizens.Values
            .Where(citizen => citizen.Status == CitizenStatus.Migrating && GetOwner(citizen.Id) == group.Id)
            .OrderBy(citizen => citizen.Id.Value)
            .ToList();

        foreach (var settler in settlers)
        {
            CompleteCitizenMigration(settler.Id, colony.Id, "settler expedition arrived");
        }

        foreach (var resource in ResourcesForOwner(group.Id).ToList())
        {
            var transfer = TransferResource(
                group.Id,
                colony.Id,
                resource.ResourceKey,
                resource.Quantity,
                "settler expedition arrived");
            if (transfer.Status != OwnershipTransferStatus.Success)
            {
                throw new InvalidOperationException(transfer.Reason);
            }
        }

        MarkMigrationGroupArrived(group.Id);
        AppendEvent(
            WorldEventKind.SettlementFounded,
            colony.Id,
            $"Settlement {colony.Id} founded by {group.FactionId} with {settlers.Count} settlers from expedition {group.Id}.");
        return colony;
    }

    private void MoveResourceIfAvailable(EntityId fromOwnerId, EntityId toOwnerId, string resourceKey, int requestedQuantity, string reason)
    {
        var available = GetOwnedResourceQuantity(fromOwnerId, resourceKey);
        var moved = Math.Min(available, Math.Max(0, requestedQuantity));
        if (moved <= 0)
        {
            return;
        }

        var transfer = TransferResource(fromOwnerId, toOwnerId, resourceKey, moved, reason);
        if (transfer.Status != OwnershipTransferStatus.Success)
        {
            throw new InvalidOperationException(transfer.Reason);
        }
    }

    private string UniqueSettlementSlug(string desiredSlug)
    {
        if (!_settlements.Values.Any(settlement => string.Equals(settlement.Slug, desiredSlug, StringComparison.Ordinal)))
        {
            return desiredSlug;
        }

        var suffix = 2;
        var candidate = $"{desiredSlug}-{suffix}";
        while (_settlements.Values.Any(settlement => string.Equals(settlement.Slug, candidate, StringComparison.Ordinal)))
        {
            suffix++;
            candidate = $"{desiredSlug}-{suffix}";
        }

        return candidate;
    }

    public void AssignFactionBehavior(string factionId, FactionBehavior behavior)
    {
        ThrowIfNullOrWhiteSpace(factionId, nameof(factionId));
        _factionBehaviors[factionId] = behavior;
    }

    public void SetPlayerFactionId(string factionId)
    {
        ThrowIfNullOrWhiteSpace(factionId, nameof(factionId));
        playerFactionId = factionId;
    }

    public bool IsPlayerFaction(string factionId)
    {
        ThrowIfNullOrWhiteSpace(factionId, nameof(factionId));
        return playerFactionId != null
            && string.Equals(playerFactionId, factionId, StringComparison.Ordinal);
    }

    public FactionBehavior GetFactionBehavior(string factionId)
    {
        ThrowIfNullOrWhiteSpace(factionId, nameof(factionId));
        return _factionBehaviors.TryGetValue(factionId, out var behavior)
            ? behavior
            : FactionBehavior.Undefined;
    }

    internal void RestoreFactionBehaviorForLedger(string factionId, FactionBehavior behavior)
    {
        _factionBehaviors[factionId] = behavior;
    }

    public int GetFactionGoodwill(string factionA, string factionB)
    {
        ThrowIfNullOrWhiteSpace(factionA, nameof(factionA));
        ThrowIfNullOrWhiteSpace(factionB, nameof(factionB));

        return _factionRelations.TryGetValue(RelationKey(factionA, factionB), out var goodwill)
            ? goodwill
            : 0;
    }

    internal void SetFactionGoodwillForLedger(string factionA, string factionB, int goodwill)
    {
        ThrowIfNullOrWhiteSpace(factionA, nameof(factionA));
        ThrowIfNullOrWhiteSpace(factionB, nameof(factionB));

        _factionRelations[RelationKey(factionA, factionB)] = goodwill;
    }

    public void MarkFactionIrreconcilable(string factionId)
    {
        ThrowIfNullOrWhiteSpace(factionId, nameof(factionId));
        _irreconcilableFactions.Add(factionId);
    }

    public bool IsFactionIrreconcilable(string factionId)
    {
        ThrowIfNullOrWhiteSpace(factionId, nameof(factionId));
        return _irreconcilableFactions.Contains(factionId);
    }

    internal void RestoreIrreconcilableFactionForLedger(string factionId)
    {
        _irreconcilableFactions.Add(factionId);
    }

    // Faction relations are symmetric; a stable ordered key keeps A↔B and B↔A the same entry.
    private static (string, string) RelationKey(string factionA, string factionB)
    {
        return string.CompareOrdinal(factionA, factionB) <= 0
            ? (factionA, factionB)
            : (factionB, factionA);
    }

    public Drifter CreateDrifter(string name, int age, Sex sex, int combatAptitude = 0, int organizationAptitude = 0)
    {
        ThrowIfNullOrWhiteSpace(name, nameof(name));

        if (age < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(age), "Drifter age cannot be negative.");
        }

        var drifter = new Drifter(
            NextId(EntityKind.Drifter),
            name,
            age,
            sex,
            CurrentTick,
            Math.Max(0, combatAptitude),
            Math.Max(0, organizationAptitude));

        _drifters.Add(drifter.Id, drifter);
        AppendEvent(WorldEventKind.DrifterArrived, drifter.Id, $"Drifter {drifter.Id} arrived from beyond the world.");

        return drifter;
    }

    public void AddDrifterArrivalReservoir(int quantity, string reason)
    {
        ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (quantity <= 0)
        {
            return;
        }

        drifterArrivalReservoir += quantity;
        AppendEvent(
            WorldEventKind.DrifterReservoirReplenished,
            null,
            $"Drifter arrival reservoir increased by {quantity}: {reason}.");
    }

    internal int ConsumeDrifterArrivalReservoir(int requestedQuantity)
    {
        if (requestedQuantity <= 0 || drifterArrivalReservoir <= 0)
        {
            return 0;
        }

        var consumed = Math.Min(requestedQuantity, drifterArrivalReservoir);
        drifterArrivalReservoir -= consumed;
        return consumed;
    }

    public Drifter? GetDrifter(EntityId id)
    {
        return _drifters.TryGetValue(id, out var drifter)
            ? drifter
            : null;
    }

    public Drifter MaterializeDrifter(EntityId drifterId, int pawnThingId, int tick)
    {
        AdvanceToTick(tick);

        if (!_drifters.TryGetValue(drifterId, out var drifter))
        {
            throw new InvalidOperationException($"Drifter {drifterId} does not exist.");
        }

        _drifters.Remove(drifterId);
        AppendEvent(
            WorldEventKind.DrifterMaterialized,
            drifterId,
            $"Drifter {drifterId} materialized as pawn {pawnThingId}.");

        return drifter;
    }

    public Drifter MaterializeNewArrival(int pawnThingId, int tick, string name, int age, Sex sex)
    {
        AdvanceToTick(tick);

        Drifter drifter = null!;
        RunWithoutEvents(() => drifter = CreateDrifter(name, age, sex));
        _drifters.Remove(drifter.Id);
        AppendEvent(
            WorldEventKind.DrifterMaterialized,
            drifter.Id,
            $"Drifter {drifter.Id} materialized as pawn {pawnThingId} (unpooled arrival).");

        return drifter;
    }

    public WorldCitizen AssimilateDrifter(EntityId drifterId, EntityId settlementId)
    {
        if (!_settlements.ContainsKey(settlementId))
        {
            throw new InvalidOperationException($"Settlement {settlementId} does not exist.");
        }

        var citizen = ConvertDrifterToCitizen(drifterId, settlementId, "settler");
        AppendEvent(
            WorldEventKind.DrifterAssimilated,
            citizen.Id,
            $"Drifter {drifterId} assimilated into {settlementId} as {citizen.Id}.");

        return citizen;
    }

    public WorldSettlement FoundSettlement(
        string slug,
        string name,
        string factionId,
        EntityId leaderDrifterId,
        IEnumerable<EntityId> memberDrifterIds)
    {
        if (memberDrifterIds == null)
        {
            throw new ArgumentNullException(nameof(memberDrifterIds));
        }

        if (!_drifters.ContainsKey(leaderDrifterId))
        {
            throw new InvalidOperationException($"Drifter {leaderDrifterId} does not exist.");
        }

        var members = memberDrifterIds.Where(id => id != leaderDrifterId).Distinct().ToList();
        var settlement = CreateSettlement(slug, name, factionId);

        var leader = ConvertDrifterToCitizen(leaderDrifterId, settlement.Id, "leader");
        foreach (var memberId in members)
        {
            ConvertDrifterToCitizen(memberId, settlement.Id, "settler");
        }

        AppendEvent(
            WorldEventKind.SettlementFounded,
            settlement.Id,
            $"Settlement {settlement.Id} ({factionId}) founded by {members.Count + 1} drifters led by {leader.Id}.");

        return settlement;
    }

    private WorldCitizen ConvertDrifterToCitizen(EntityId drifterId, EntityId settlementId, string profession)
    {
        if (!_drifters.TryGetValue(drifterId, out var drifter))
        {
            throw new InvalidOperationException($"Drifter {drifterId} does not exist.");
        }

        _drifters.Remove(drifterId);
        return CreateCitizen(drifter.Name, drifter.Age, drifter.Sex, profession, settlementId);
    }

    public WorldMigrationGroup CreateMigrationGroup(
        EntityId sourceSettlementId,
        EntityId? targetSettlementId,
        string factionId,
        int createdTick,
        int arrivalTick,
        string reason)
    {
        ThrowIfNullOrWhiteSpace(factionId, nameof(factionId));
        ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (!_settlements.ContainsKey(sourceSettlementId))
        {
            throw new InvalidOperationException($"Settlement {sourceSettlementId} does not exist.");
        }

        if (targetSettlementId.HasValue && !_settlements.ContainsKey(targetSettlementId.Value))
        {
            throw new InvalidOperationException($"Settlement {targetSettlementId.Value} does not exist.");
        }

        var group = new WorldMigrationGroup(
            NextId(EntityKind.MigrationGroup),
            sourceSettlementId,
            targetSettlementId,
            factionId,
            createdTick,
            Math.Max(createdTick, arrivalTick),
            MigrationGroupStatus.Traveling,
            reason);

        _migrationGroups.Add(group.Id, group);
        AppendEvent(WorldEventKind.MigrationStarted, group.Id, $"Migration group {group.Id} started from {sourceSettlementId}: {reason}.");

        return group;
    }

    public WorldMigrationGroup MarkMigrationGroupArrived(EntityId groupId)
    {
        if (!_migrationGroups.TryGetValue(groupId, out var group))
        {
            throw new InvalidOperationException($"Migration group {groupId} does not exist.");
        }

        if (group.Status == MigrationGroupStatus.Arrived)
        {
            return group;
        }

        var arrived = group with { Status = MigrationGroupStatus.Arrived };
        _migrationGroups[groupId] = arrived;
        return arrived;
    }

    public WorldCitizen? GetCitizen(EntityId id)
    {
        return _citizens.TryGetValue(id, out var citizen)
            ? citizen
            : null;
    }

    public WorldSettlement? GetSettlement(EntityId id)
    {
        return _settlements.TryGetValue(id, out var settlement)
            ? settlement
            : null;
    }

    public WorldSettlement? FindActiveSettlementByTile(int tile)
    {
        if (tile < 0)
        {
            return null;
        }

        return _settlements.Values.FirstOrDefault(settlement =>
            settlement.IsActive && SettlementSlug.ParseTile(settlement.Slug) == tile);
    }

    public bool IsActiveSettlement(EntityId id)
    {
        return _settlements.TryGetValue(id, out var settlement) && settlement.IsActive;
    }

    public WorldArmy? GetArmy(EntityId id)
    {
        return _armies.TryGetValue(id, out var army)
            ? army
            : null;
    }

    public WorldMigrationGroup? GetMigrationGroup(EntityId id)
    {
        return _migrationGroups.TryGetValue(id, out var group)
            ? group
            : null;
    }

    public WorldIntelReport? GetIntelReport(EntityId id)
    {
        return _intelReports.TryGetValue(id, out var report)
            ? report
            : null;
    }

    public KnownSettlementInfo? GetKnownSettlementInfo(EntityId settlementId)
    {
        return _knownSettlementInfos.TryGetValue(settlementId, out var info)
            ? info
            : null;
    }

    public RaidOpportunity? GetRaidOpportunity(EntityId id)
    {
        return _raidOpportunities.TryGetValue(id, out var opportunity)
            ? opportunity
            : null;
    }

    public RaidIntelFact? GetRaidIntelFact(EntityId id)
    {
        return _raidIntelFacts.TryGetValue(id, out var fact)
            ? fact
            : null;
    }

    public RaidPreparation? GetRaidPreparation(EntityId id)
    {
        return _raidPreparations.TryGetValue(id, out var preparation)
            ? preparation
            : null;
    }

    public MaterializationLease? GetMaterializationLease(EntityId id)
    {
        return _materializationLeases.TryGetValue(id, out var lease)
            ? lease
            : null;
    }

    public RaidPawnLink? GetRaidPawnLink(int pawnThingId)
    {
        return _raidPawnLinks.TryGetValue(pawnThingId, out var link)
            ? link
            : null;
    }

    public WorldRaidOutcome? GetRaidOutcome(EntityId armyId)
    {
        return _raidOutcomes.TryGetValue(armyId, out var outcome)
            ? outcome
            : null;
    }

    public SettlementProductionProfile? GetSettlementProductionProfile(EntityId settlementId)
    {
        return _productionProfiles.TryGetValue(settlementId, out var profile)
            ? profile
            : null;
    }

    public SettlementFacility? GetSettlementFacility(EntityId facilityId)
    {
        return _settlementFacilities.TryGetValue(facilityId, out var facility)
            ? facility
            : null;
    }

    public IReadOnlyList<SettlementFacility> GetSettlementFacilities(EntityId settlementId)
    {
        return _settlementFacilities.Values
            .Where(facility => facility.SettlementId == settlementId)
            .OrderBy(facility => facility.Id.Value)
            .ToList();
    }

    public SettlementProject? GetSettlementProject(EntityId projectId)
    {
        return _settlementProjects.TryGetValue(projectId, out var project)
            ? project
            : null;
    }

    public SettlementCapability? GetSettlementCapability(EntityId settlementId)
    {
        return _settlementCapabilities.TryGetValue(settlementId, out var capability)
            ? capability
            : null;
    }

    public SpecialistPool? GetSpecialistPool(EntityId settlementId)
    {
        return _specialistPools.TryGetValue(settlementId, out var specialists)
            ? specialists
            : null;
    }

    public SettlementWealthSnapshot? GetSettlementWealth(EntityId settlementId)
    {
        return _settlementWealth.TryGetValue(settlementId, out var wealth)
            ? wealth
            : null;
    }

    public FactionWealthSnapshot? GetFactionWealth(string factionId)
    {
        ThrowIfNullOrWhiteSpace(factionId, nameof(factionId));

        return _factionWealth.TryGetValue(factionId, out var wealth)
            ? wealth
            : null;
    }

    public WorldFactionRecord? GetFactionRecord(string factionId)
    {
        ThrowIfNullOrWhiteSpace(factionId, nameof(factionId));

        return _factionRecords.TryGetValue(factionId, out var record)
            ? record
            : null;
    }

    public bool IsFactionCollapsed(string factionId)
    {
        return GetFactionRecord(factionId)?.Status == WorldFactionStatus.Collapsed;
    }

    public EntityId? GetOwner(EntityId assetId)
    {
        return _owners.TryGetValue(assetId, out var ownerId)
            ? ownerId
            : null;
    }

    public void AddResource(EntityId ownerId, string resourceKey, int quantity)
    {
        ResourceLedgerService.AddResource(this, ownerId, resourceKey, quantity);
    }

    public int GetOwnedResourceQuantity(EntityId ownerId, string resourceKey)
    {
        return ResourceLedgerService.GetQuantity(this, ownerId, resourceKey);
    }

    public IReadOnlyList<ResourceStack> ResourcesForOwner(EntityId ownerId)
    {
        return _resources
            .Where(pair => pair.Key.OwnerId == ownerId)
            .OrderBy(pair => pair.Key.ResourceKey, StringComparer.Ordinal)
            .Select(pair => new ResourceStack(pair.Key.OwnerId, pair.Key.ResourceKey, pair.Value))
            .ToList();
    }

    public int ConsumeResource(EntityId ownerId, string resourceKey, int requestedQuantity, string reason)
    {
        return ResourceLedgerService.ConsumeResource(this, ownerId, resourceKey, requestedQuantity, reason);
    }

    public OwnershipTransferResult TransferResource(
        EntityId fromOwnerId,
        EntityId toOwnerId,
        string resourceKey,
        int quantity,
        string reason)
    {
        return OwnershipService.TransferResource(this, fromOwnerId, toOwnerId, resourceKey, quantity, reason);
    }

    public OwnershipTransferResult TransferAsset(
        EntityId assetId,
        EntityId fromOwnerId,
        EntityId toOwnerId,
        string reason)
    {
        return OwnershipService.TransferAsset(this, assetId, fromOwnerId, toOwnerId, reason);
    }

    public void RecordEvent(WorldEventKind kind, EntityId? subjectId, string summary)
    {
        ThrowIfNullOrWhiteSpace(summary, nameof(summary));

        AppendEvent(kind, subjectId, summary);
    }

    public WorldAnimalCohort CreateAnimalCohort(
        EntityId ownerId,
        string animalKind,
        AnimalCohortType type,
        int count,
        int healthPercent,
        int fertilityPercent,
        int carryingCapacity,
        int tick)
    {
        EnsureOwnerExists(ownerId);
        ThrowIfNullOrWhiteSpace(animalKind, nameof(animalKind));

        var cohort = Normalize(new WorldAnimalCohort(
            NextId(EntityKind.Animal),
            ownerId,
            animalKind,
            type,
            count,
            healthPercent,
            fertilityPercent,
            carryingCapacity,
            tick));
        _animalCohorts[cohort.Id] = cohort;
        _owners[cohort.Id] = ownerId;
        AppendEvent(
            WorldEventKind.AnimalCohortCreated,
            cohort.Id,
            $"Animal cohort {cohort.Id} created: {cohort.Count} {cohort.AnimalKind}.");

        return cohort;
    }

    public WorldAnimalCohort? GetAnimalCohort(EntityId cohortId)
    {
        return _animalCohorts.TryGetValue(cohortId, out var cohort)
            ? cohort
            : null;
    }

    public IReadOnlyList<WorldAnimalCohort> GetAnimalCohorts(EntityId ownerId)
    {
        return _animalCohorts.Values
            .Where(cohort => cohort.OwnerId == ownerId)
            .OrderBy(cohort => cohort.AnimalKind, StringComparer.Ordinal)
            .ThenBy(cohort => cohort.Id.Value)
            .ToList();
    }

    internal WorldAnimalCohort RecordAnimalCohortForSimulation(WorldAnimalCohort cohort)
    {
        if (cohort == null)
        {
            throw new ArgumentNullException(nameof(cohort));
        }

        if (!_animalCohorts.ContainsKey(cohort.Id))
        {
            throw new InvalidOperationException($"Animal cohort {cohort.Id} does not exist.");
        }

        EnsureOwnerExists(cohort.OwnerId);
        var normalized = Normalize(cohort);
        _animalCohorts[normalized.Id] = normalized;
        _owners[normalized.Id] = normalized.OwnerId;

        return normalized;
    }

    public AnimalBreedingProject? GetAnimalBreedingProject(EntityId projectId)
    {
        return _animalBreedingProjects.TryGetValue(projectId, out var project)
            ? project
            : null;
    }

    internal AnimalBreedingProject CreateAnimalBreedingProject(
        EntityId settlementId,
        EntityId sourceCohortId,
        AnimalBreedingProjectKind kind,
        AnimalBreedingTrait trait,
        int startedTick,
        int completionTick,
        string feedResourceKey,
        int feedCost,
        string medicineResourceKey,
        int medicineCost,
        string componentResourceKey,
        int componentCost)
    {
        if (!_settlements.ContainsKey(settlementId))
        {
            throw new InvalidOperationException($"Settlement {settlementId} does not exist.");
        }

        if (!_animalCohorts.ContainsKey(sourceCohortId))
        {
            throw new InvalidOperationException($"Animal cohort {sourceCohortId} does not exist.");
        }

        var project = Normalize(new AnimalBreedingProject(
            NextId(EntityKind.AnimalBreedingProject),
            settlementId,
            sourceCohortId,
            kind,
            trait,
            AnimalBreedingProjectStatus.Active,
            startedTick,
            completionTick,
            feedResourceKey,
            feedCost,
            medicineResourceKey,
            medicineCost,
            componentResourceKey,
            componentCost));
        _animalBreedingProjects[project.Id] = project;
        AppendEvent(
            WorldEventKind.AnimalBreedingProjectStarted,
            project.Id,
            $"Animal breeding project {project.Id} started for {sourceCohortId}.");

        return project;
    }

    internal AnimalBreedingProject RecordAnimalBreedingProjectForSimulation(AnimalBreedingProject project)
    {
        if (project == null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        if (!_animalBreedingProjects.ContainsKey(project.Id))
        {
            throw new InvalidOperationException($"Animal breeding project {project.Id} does not exist.");
        }

        var normalized = Normalize(project);
        _animalBreedingProjects[normalized.Id] = normalized;
        return normalized;
    }

    public CropStrain? GetCropStrain(EntityId settlementId, string cropKind)
    {
        ThrowIfNullOrWhiteSpace(cropKind, nameof(cropKind));
        return _cropStrains.TryGetValue((settlementId, cropKind.Trim()), out var strain)
            ? strain
            : null;
    }

    public IReadOnlyList<CropStrain> GetCropStrains(EntityId settlementId)
    {
        return _cropStrains.Values
            .Where(strain => strain.SettlementId == settlementId)
            .OrderBy(strain => strain.CropKind, StringComparer.Ordinal)
            .ToList();
    }

    public CropStrain RecordCropStrain(CropStrain strain)
    {
        if (strain == null)
        {
            throw new ArgumentNullException(nameof(strain));
        }

        if (!_settlements.ContainsKey(strain.SettlementId))
        {
            throw new InvalidOperationException($"Settlement {strain.SettlementId} does not exist.");
        }

        var normalized = Normalize(strain);
        _cropStrains[(normalized.SettlementId, normalized.CropKind)] = normalized;
        return normalized;
    }

    public CropStrainProject? GetCropStrainProject(EntityId projectId)
    {
        return _cropStrainProjects.TryGetValue(projectId, out var project)
            ? project
            : null;
    }

    internal CropStrainProject CreateCropStrainProject(
        EntityId settlementId,
        string cropKind,
        CropStrainTrait trait,
        int startedTick,
        int completionTick,
        string foodResourceKey,
        int foodCost,
        string medicineResourceKey,
        int medicineCost)
    {
        if (!_settlements.ContainsKey(settlementId))
        {
            throw new InvalidOperationException($"Settlement {settlementId} does not exist.");
        }

        var project = Normalize(new CropStrainProject(
            NextId(EntityKind.CropStrainProject),
            settlementId,
            cropKind,
            trait,
            CropStrainProjectStatus.Active,
            startedTick,
            completionTick,
            foodResourceKey,
            foodCost,
            medicineResourceKey,
            medicineCost));
        _cropStrainProjects[project.Id] = project;
        AppendEvent(
            WorldEventKind.CropStrainProjectStarted,
            project.Id,
            $"Crop strain project {project.Id} started for {project.CropKind}.");

        return project;
    }

    public CropStrainProject RecordCropStrainProjectForSimulation(CropStrainProject project)
    {
        if (project == null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        var normalized = Normalize(project);
        if (!_cropStrainProjects.ContainsKey(normalized.Id))
        {
            ReserveExistingId(normalized.Id);
        }

        _cropStrainProjects[normalized.Id] = normalized;
        return normalized;
    }

    public SettlementTechnology? GetSettlementTechnology(EntityId settlementId, TechnologyDomain domain)
    {
        return _settlementTechnologies.TryGetValue((settlementId, domain), out var technology)
            ? technology
            : null;
    }

    public SettlementTechnology RecordSettlementTechnology(SettlementTechnology technology)
    {
        if (technology == null)
        {
            throw new ArgumentNullException(nameof(technology));
        }

        if (!_settlements.ContainsKey(technology.SettlementId))
        {
            throw new InvalidOperationException($"Settlement {technology.SettlementId} does not exist.");
        }

        var normalized = Normalize(technology);
        _settlementTechnologies[(normalized.SettlementId, normalized.Domain)] = normalized;
        return normalized;
    }

    public void RecordKnownSettlementInfo(KnownSettlementInfo info)
    {
        if (!_settlements.ContainsKey(info.SettlementId))
        {
            throw new InvalidOperationException($"Settlement {info.SettlementId} does not exist.");
        }

        if (_knownSettlementInfos.TryGetValue(info.SettlementId, out var existing)
            && ShouldKeepExistingKnowledge(existing, info))
        {
            return;
        }

        _knownSettlementInfos[info.SettlementId] = info;
        AppendEvent(
            WorldEventKind.SettlementIntelUpdated,
            info.SettlementId,
            $"Known settlement info updated from {info.SourceKind}: {info.Summary}.");
    }

    public FactionSettlementIntel RecordFactionSettlementIntel(
        string factionId,
        EntityId settlementId,
        IntelSourceKind sourceKind,
        int tick,
        int confidence)
    {
        ThrowIfNullOrWhiteSpace(factionId, nameof(factionId));
        if (!_settlements.ContainsKey(settlementId))
        {
            throw new InvalidOperationException($"Settlement {settlementId} does not exist.");
        }

        var intel = Normalize(new FactionSettlementIntel(
            factionId,
            settlementId,
            sourceKind,
            tick,
            confidence));
        var key = (intel.FactionId, intel.SettlementId);
        if (_factionSettlementIntel.TryGetValue(key, out var existing)
            && existing.Confidence >= intel.Confidence
            && existing.Tick >= intel.Tick)
        {
            return existing;
        }

        _factionSettlementIntel[key] = intel;
        AppendEvent(
            WorldEventKind.SettlementIntelUpdated,
            settlementId,
            $"Faction {intel.FactionId} learned about settlement {settlementId} from {sourceKind}.");
        return intel;
    }

    public bool HasFactionSettlementIntel(string factionId, EntityId settlementId)
    {
        if (string.IsNullOrWhiteSpace(factionId))
        {
            return false;
        }

        return _factionSettlementIntel.ContainsKey((factionId.Trim(), settlementId));
    }

    public FactionSettlementIntel? GetFactionSettlementIntel(string factionId, EntityId settlementId)
    {
        return string.IsNullOrWhiteSpace(factionId)
            ? null
            : _factionSettlementIntel.TryGetValue((factionId.Trim(), settlementId), out var intel)
                ? intel
                : null;
    }

    public void RecordSettlementProductionProfile(SettlementProductionProfile profile)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        if (!_settlements.ContainsKey(profile.SettlementId))
        {
            throw new InvalidOperationException($"Settlement {profile.SettlementId} does not exist.");
        }

        _productionProfiles[profile.SettlementId] = profile;
        SeedTechnologyFromProductionProfile(profile);
    }

    public SettlementFacility RecordSettlementFacility(SettlementFacility facility)
    {
        if (facility == null)
        {
            throw new ArgumentNullException(nameof(facility));
        }

        if (facility.Id.Kind != EntityKind.SettlementFacility)
        {
            throw new InvalidOperationException($"Facility id {facility.Id} is not a settlement facility id.");
        }

        if (!_settlements.ContainsKey(facility.SettlementId))
        {
            throw new InvalidOperationException($"Settlement {facility.SettlementId} does not exist.");
        }

        var normalized = Normalize(facility);
        _settlementFacilities[normalized.Id] = normalized;
        ReserveExistingId(normalized.Id);
        return normalized;
    }

    public SettlementProject CreateSettlementProject(
        EntityId settlementId,
        SettlementProjectKind kind,
        SettlementFacilityKind facilityKind,
        EntityId? targetFacilityId,
        int facilityLevel,
        int startedTick,
        int completionTick,
        int steelCost,
        int componentCost)
    {
        if (!_settlements.ContainsKey(settlementId))
        {
            throw new InvalidOperationException($"Settlement {settlementId} does not exist.");
        }

        if (targetFacilityId.HasValue
            && !_settlementFacilities.ContainsKey(targetFacilityId.Value))
        {
            throw new InvalidOperationException($"Facility {targetFacilityId.Value} does not exist.");
        }

        var project = Normalize(new SettlementProject(
            NextId(EntityKind.SettlementProject),
            settlementId,
            kind,
            SettlementProjectStatus.Active,
            facilityKind,
            targetFacilityId,
            facilityLevel,
            startedTick,
            completionTick,
            steelCost,
            componentCost));
        _settlementProjects[project.Id] = project;
        AppendEvent(
            WorldEventKind.SettlementProjectStarted,
            settlementId,
            $"Settlement {settlementId} started {kind} project {project.Id}.");

        return project;
    }

    public void RecordSettlementProject(SettlementProject project)
    {
        if (project == null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        if (project.Id.Kind != EntityKind.SettlementProject)
        {
            throw new InvalidOperationException($"Project id {project.Id} is not a settlement project id.");
        }

        if (!_settlements.ContainsKey(project.SettlementId))
        {
            throw new InvalidOperationException($"Settlement {project.SettlementId} does not exist.");
        }

        var normalized = Normalize(project);
        _settlementProjects[normalized.Id] = normalized;
        ReserveExistingId(normalized.Id);
    }

    internal EntityId NextFacilityIdForLedger()
    {
        return NextId(EntityKind.SettlementFacility);
    }

    public void RecordSettlementCapability(SettlementCapability capability)
    {
        if (capability == null)
        {
            throw new ArgumentNullException(nameof(capability));
        }

        if (!_settlements.ContainsKey(capability.SettlementId))
        {
            throw new InvalidOperationException($"Settlement {capability.SettlementId} does not exist.");
        }

        _settlementCapabilities[capability.SettlementId] = Normalize(capability);
    }

    public void RecordSpecialistPool(SpecialistPool specialists)
    {
        if (specialists == null)
        {
            throw new ArgumentNullException(nameof(specialists));
        }

        if (!_settlements.ContainsKey(specialists.SettlementId))
        {
            throw new InvalidOperationException($"Settlement {specialists.SettlementId} does not exist.");
        }

        _specialistPools[specialists.SettlementId] = Normalize(specialists);
    }

    public void RecordSettlementWealth(SettlementWealthSnapshot wealth)
    {
        if (wealth == null)
        {
            throw new ArgumentNullException(nameof(wealth));
        }

        if (!_settlements.ContainsKey(wealth.SettlementId))
        {
            throw new InvalidOperationException($"Settlement {wealth.SettlementId} does not exist.");
        }

        _settlementWealth[wealth.SettlementId] = wealth with
        {
            Silver = Math.Max(0, wealth.Silver),
            MaterialWealth = Math.Max(0, wealth.MaterialWealth),
            TotalWealth = Math.Max(0, wealth.TotalWealth)
        };
    }

    public void RecordFactionWealth(FactionWealthSnapshot wealth)
    {
        if (wealth == null)
        {
            throw new ArgumentNullException(nameof(wealth));
        }

        ThrowIfNullOrWhiteSpace(wealth.FactionId, nameof(wealth));
        _factionWealth[wealth.FactionId] = wealth with
        {
            Silver = Math.Max(0, wealth.Silver),
            MaterialWealth = Math.Max(0, wealth.MaterialWealth),
            TotalWealth = Math.Max(0, wealth.TotalWealth)
        };
    }

    internal WorldFactionRecord MarkFactionCollapsedForLifecycle(string factionId, int tick, string reason)
    {
        ThrowIfNullOrWhiteSpace(factionId, nameof(factionId));
        ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (_factionRecords.TryGetValue(factionId, out var existing)
            && existing.Status == WorldFactionStatus.Collapsed)
        {
            return existing;
        }

        var record = new WorldFactionRecord(
            factionId,
            WorldFactionStatus.Collapsed,
            tick,
            reason);
        _factionRecords[factionId] = record;
        AppendEvent(WorldEventKind.FactionCollapsed, null, $"Faction {factionId} collapsed: {reason}.");

        return record;
    }

    public WorldCitizen MarkCitizenRefugee(EntityId citizenId, string reason)
    {
        ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (!_citizens.TryGetValue(citizenId, out var citizen))
        {
            throw new InvalidOperationException($"Citizen {citizenId} does not exist.");
        }

        if (citizen.Status == CitizenStatus.Refugee)
        {
            return citizen;
        }

        if (citizen.Status != CitizenStatus.Alive)
        {
            throw new InvalidOperationException($"Citizen {citizenId} cannot become refugee from status {citizen.Status}.");
        }

        if (!_owners.TryGetValue(citizenId, out var ownerId) || ownerId != citizen.SettlementId)
        {
            throw new InvalidOperationException($"Citizen {citizenId} is not currently owned by their settlement.");
        }

        var refugee = citizen with { Status = CitizenStatus.Refugee };
        _citizens[citizenId] = refugee;
        _owners[citizenId] = citizenId;
        MarkDerivedAggregatesDirty();
        AppendEvent(WorldEventKind.RefugeeCreated, citizenId, $"Citizen {citizenId} became refugee: {reason}.");

        return refugee;
    }

    public WorldCitizen CompleteCitizenMigration(EntityId citizenId, EntityId targetSettlementId, string reason)
    {
        ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (!_citizens.TryGetValue(citizenId, out var citizen))
        {
            throw new InvalidOperationException($"Citizen {citizenId} does not exist.");
        }

        if (!_settlements.ContainsKey(targetSettlementId))
        {
            throw new InvalidOperationException($"Settlement {targetSettlementId} does not exist.");
        }

        if (citizen.Status != CitizenStatus.Refugee && citizen.Status != CitizenStatus.Migrating)
        {
            throw new InvalidOperationException($"Citizen {citizenId} cannot complete migration from status {citizen.Status}.");
        }

        var migrated = citizen with
        {
            SettlementId = targetSettlementId,
            Status = CitizenStatus.Alive
        };
        _citizens[citizenId] = migrated;
        _owners[citizenId] = targetSettlementId;
        MarkDerivedAggregatesDirty();
        AppendEvent(WorldEventKind.MigrationCompleted, citizenId, $"Citizen {citizenId} migrated to {targetSettlementId}: {reason}.");

        return migrated;
    }

    public WorldIntelReport RecordIntelReport(
        IntelSourceKind sourceKind,
        string factionId,
        int valueScore,
        string summary)
    {
        ThrowIfNullOrWhiteSpace(factionId, nameof(factionId));
        ThrowIfNullOrWhiteSpace(summary, nameof(summary));

        if (valueScore < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valueScore), "Intel value score cannot be negative.");
        }

        var report = new WorldIntelReport(
            NextId(EntityKind.IntelReport),
            sourceKind,
            factionId,
            CurrentTick,
            valueScore,
            summary);

        _intelReports.Add(report.Id, report);
        AppendEvent(WorldEventKind.IntelReported, report.Id, $"Intel {report.Id} reported for {factionId}: {summary}.");

        return report;
    }

    public RaidOpportunity CreateRaidOpportunity(
        string factionId,
        EntityId intelReportId,
        int combatantDemand,
        string reason)
    {
        ThrowIfNullOrWhiteSpace(factionId, nameof(factionId));
        ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (!_intelReports.ContainsKey(intelReportId))
        {
            throw new InvalidOperationException($"Intel report {intelReportId} does not exist.");
        }

        if (combatantDemand <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(combatantDemand), "Raid opportunity combatant demand must be positive.");
        }

        var opportunity = new RaidOpportunity(
            NextId(EntityKind.RaidOpportunity),
            factionId,
            intelReportId,
            RaidOpportunityStatus.Active,
            combatantDemand,
            reason);

        _raidOpportunities.Add(opportunity.Id, opportunity);
        AppendEvent(WorldEventKind.RaidOpportunityCreated, opportunity.Id, $"Raid opportunity {opportunity.Id} created for {factionId}: {reason}.");

        return opportunity;
    }

    public RaidOpportunity ConsumeRaidOpportunity(EntityId opportunityId)
    {
        if (!_raidOpportunities.TryGetValue(opportunityId, out var opportunity))
        {
            throw new InvalidOperationException($"Raid opportunity {opportunityId} does not exist.");
        }

        if (opportunity.Status == RaidOpportunityStatus.Consumed)
        {
            return opportunity;
        }

        var consumed = opportunity.Consume();
        _raidOpportunities[opportunityId] = consumed;
        AppendEvent(WorldEventKind.RaidOpportunityConsumed, consumed.Id, $"Raid opportunity {consumed.Id} consumed.");

        return consumed;
    }

    public RaidIntelFact RecordRaidIntelFact(
        IntelSourceKind sourceKind,
        string factionId,
        RaidIntelTargetKind targetKind,
        string targetKey,
        RaidIntelValueBand valueBand,
        int confidence,
        int lifetimeTicks,
        int combatantDemand,
        string summary)
    {
        ThrowIfNullOrWhiteSpace(factionId, nameof(factionId));
        ThrowIfNullOrWhiteSpace(targetKey, nameof(targetKey));
        ThrowIfNullOrWhiteSpace(summary, nameof(summary));

        if (confidence < 0 || confidence > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), "Raid intel confidence must be between 0 and 100.");
        }

        if (combatantDemand <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(combatantDemand), "Raid intel combatant demand must be positive.");
        }

        var fact = new RaidIntelFact(
            NextId(EntityKind.RaidIntelFact),
            sourceKind,
            factionId,
            targetKind,
            targetKey,
            valueBand,
            confidence,
            CurrentTick,
            CurrentTick + Math.Max(0, lifetimeTicks),
            combatantDemand,
            summary);

        _raidIntelFacts.Add(fact.Id, fact);
        AppendEvent(WorldEventKind.RaidIntelFactRecorded, fact.Id, $"Raid intel fact {fact.Id} recorded for {factionId}: {summary}.");

        return fact;
    }

    public RaidPreparation CreateRaidPreparation(
        string factionId,
        EntityId sourceSettlementId,
        EntityId armyId,
        EntityId? intelFactId,
        RaidIntentReason reason,
        RaidPreparationStatus status,
        int reservedCombatants,
        string supplyResourceKey,
        int reservedSupplies,
        int expiresTick,
        string summary)
    {
        ThrowIfNullOrWhiteSpace(factionId, nameof(factionId));
        ThrowIfNullOrWhiteSpace(supplyResourceKey, nameof(supplyResourceKey));
        ThrowIfNullOrWhiteSpace(summary, nameof(summary));

        if (reservedCombatants < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(reservedCombatants), "Reserved combatants cannot be negative.");
        }

        if (reservedSupplies < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(reservedSupplies), "Reserved supplies cannot be negative.");
        }

        var preparation = new RaidPreparation(
            NextId(EntityKind.RaidPreparation),
            factionId,
            sourceSettlementId,
            armyId,
            intelFactId,
            reason,
            status,
            reservedCombatants,
            supplyResourceKey,
            reservedSupplies,
            CurrentTick,
            Math.Max(CurrentTick, expiresTick),
            summary);

        _raidPreparations.Add(preparation.Id, preparation);
        AppendEvent(WorldEventKind.RaidPreparationCreated, preparation.Id, $"Raid preparation {preparation.Id} created for {factionId}: {summary}.");

        return preparation;
    }

    public RaidPreparation ReleaseRaidPreparation(EntityId preparationId)
    {
        if (!_raidPreparations.TryGetValue(preparationId, out var preparation))
        {
            throw new InvalidOperationException($"Raid preparation {preparationId} does not exist.");
        }

        if (preparation.Status != RaidPreparationStatus.Ready)
        {
            return preparation;
        }

        var released = preparation.Release();
        _raidPreparations[preparationId] = released;
        AppendEvent(WorldEventKind.RaidPreparationReleased, released.Id, $"Raid preparation {released.Id} released.");

        return released;
    }

    public RaidPreparation LaunchRaidPreparation(EntityId preparationId)
    {
        if (!_raidPreparations.TryGetValue(preparationId, out var preparation))
        {
            throw new InvalidOperationException($"Raid preparation {preparationId} does not exist.");
        }

        if (preparation.Status != RaidPreparationStatus.Ready)
        {
            return preparation;
        }

        var launched = preparation.Launch();
        _raidPreparations[preparationId] = launched;
        AppendEvent(WorldEventKind.RaidPreparationLaunched, launched.Id, $"Raid preparation {launched.Id} launched.");

        return launched;
    }

    public MaterializationLease CreateMaterializationLease(
        EntityId citizenId,
        EntityId sourceOwnerId,
        EntityId returnOwnerId,
        MaterializationPurpose purpose,
        string purposeKey,
        int lifetimeTicks)
    {
        ThrowIfNullOrWhiteSpace(purposeKey, nameof(purposeKey));
        if (!_citizens.TryGetValue(citizenId, out var citizen))
        {
            throw new InvalidOperationException($"Citizen {citizenId} does not exist.");
        }

        if (citizen.Status != CitizenStatus.Alive)
        {
            throw new InvalidOperationException($"Citizen {citizenId} is not alive.");
        }

        EnsureOwnerExists(sourceOwnerId);
        EnsureOwnerExists(returnOwnerId);
        if (GetOwner(citizenId) != sourceOwnerId)
        {
            throw new InvalidOperationException($"Citizen {citizenId} is not owned by {sourceOwnerId}.");
        }

        var lease = new MaterializationLease(
            NextId(EntityKind.MaterializationLease),
            citizenId,
            sourceOwnerId,
            returnOwnerId,
            purpose,
            purposeKey,
            CurrentTick,
            CurrentTick + Math.Max(0, lifetimeTicks),
            MaterializationLeaseLifecycle.Reserved,
            null);

        _materializationLeases.Add(lease.Id, lease);
        AppendEvent(WorldEventKind.MaterializationLeaseCreated, lease.Id, $"Materialization lease {lease.Id} created for {citizenId}: {purpose}.");

        return lease;
    }

    public MaterializationLease BindMaterializationLeasePawn(EntityId leaseId, int pawnThingId)
    {
        if (pawnThingId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pawnThingId), "Pawn thing ID must be positive.");
        }

        if (!_materializationLeases.TryGetValue(leaseId, out var lease))
        {
            throw new InvalidOperationException($"Materialization lease {leaseId} does not exist.");
        }

        if (!lease.IsActive)
        {
            throw new InvalidOperationException($"Materialization lease {leaseId} is already {lease.Lifecycle}.");
        }

        var bound = lease.BindPawn(pawnThingId);
        _materializationLeases[leaseId] = bound;
        AppendEvent(WorldEventKind.MaterializationLeasePawnBound, lease.Id, $"Pawn {pawnThingId} bound to materialization lease {lease.Id}.");

        return bound;
    }

    public MaterializationLease ResolveMaterializationLease(EntityId leaseId, PawnFateKind fate, string reason)
    {
        ThrowIfNullOrWhiteSpace(reason, nameof(reason));
        if (!_materializationLeases.TryGetValue(leaseId, out var lease))
        {
            throw new InvalidOperationException($"Materialization lease {leaseId} does not exist.");
        }

        if (!lease.IsActive)
        {
            return lease;
        }

        if (_citizens.TryGetValue(lease.CitizenId, out var citizen))
        {
            var status = fate switch
            {
                PawnFateKind.Dead => CitizenStatus.Dead,
                PawnFateKind.Prisoner => CitizenStatus.Prisoner,
                PawnFateKind.Missing => CitizenStatus.Missing,
                PawnFateKind.Returned => CitizenStatus.Alive,
                _ => throw new ArgumentOutOfRangeException(nameof(fate), fate, "Unknown pawn fate kind.")
            };

            _citizens[lease.CitizenId] = citizen with { Status = status };
            if (fate == PawnFateKind.Returned)
            {
                _owners[lease.CitizenId] = lease.ReturnOwnerId;
            }

            MarkDerivedAggregatesDirty();
        }

        var resolved = lease.Resolve(fate);
        _materializationLeases[leaseId] = resolved;
        AppendEvent(WorldEventKind.MaterializationLeaseResolved, lease.Id, $"Materialization lease {lease.Id} resolved as {fate}: {reason}.");

        return resolved;
    }

    public MaterializationLease ReleaseMaterializationLease(EntityId leaseId)
    {
        if (!_materializationLeases.TryGetValue(leaseId, out var lease))
        {
            throw new InvalidOperationException($"Materialization lease {leaseId} does not exist.");
        }

        if (!lease.IsActive)
        {
            return lease;
        }

        var released = lease.Release();
        _materializationLeases[leaseId] = released;
        if (_citizens.TryGetValue(lease.CitizenId, out var citizen) && citizen.Status == CitizenStatus.Alive)
        {
            _owners[lease.CitizenId] = lease.ReturnOwnerId;
            MarkDerivedAggregatesDirty();
        }

        AppendEvent(WorldEventKind.MaterializationLeaseResolved, lease.Id, $"Materialization lease {lease.Id} released.");
        return released;
    }

    public RaidPawnLink LinkRaidPawn(int pawnThingId, EntityId citizenId, EntityId armyId)
    {
        if (pawnThingId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pawnThingId), "Pawn thing ID must be positive.");
        }

        if (!_citizens.ContainsKey(citizenId))
        {
            throw new InvalidOperationException($"Citizen {citizenId} does not exist.");
        }

        if (!_armies.ContainsKey(armyId))
        {
            throw new InvalidOperationException($"Army {armyId} does not exist.");
        }

        if (_raidPawnLinks.ContainsKey(pawnThingId))
        {
            throw new InvalidOperationException($"Pawn {pawnThingId} is already linked.");
        }

        var link = new RaidPawnLink(
            pawnThingId,
            citizenId,
            armyId,
            RaidPawnLinkStatus.Active);

        _raidPawnLinks.Add(pawnThingId, link);
        AppendEvent(WorldEventKind.RaidPawnBound, citizenId, $"Pawn {pawnThingId} bound to citizen {citizenId} in {armyId}.");

        return link;
    }

    public RaidPawnLink MarkRaidPawnDead(int pawnThingId, string reason)
    {
        ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (!_raidPawnLinks.TryGetValue(pawnThingId, out var link))
        {
            throw new InvalidOperationException($"Pawn {pawnThingId} is not linked.");
        }

        if (link.Status == RaidPawnLinkStatus.Dead)
        {
            return link;
        }

        if (_citizens.TryGetValue(link.CitizenId, out var citizen) && citizen.Status != CitizenStatus.Dead)
        {
            _citizens[link.CitizenId] = citizen with { Status = CitizenStatus.Dead };
            MarkDerivedAggregatesDirty();
            AppendEvent(WorldEventKind.CitizenDied, link.CitizenId, $"Citizen {link.CitizenId} died: {reason}.");
        }

        var deadLink = link.MarkDead();
        _raidPawnLinks[pawnThingId] = deadLink;
        return deadLink;
    }

    public RaidPawnLink MarkRaidPawnReturned(int pawnThingId, string reason)
    {
        ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (!_raidPawnLinks.TryGetValue(pawnThingId, out var link))
        {
            throw new InvalidOperationException($"Pawn {pawnThingId} is not linked.");
        }

        if (link.Status != RaidPawnLinkStatus.Active)
        {
            return link;
        }

        var army = GetArmy(link.ArmyId)
            ?? throw new InvalidOperationException($"Army {link.ArmyId} does not exist.");
        var transfer = TransferAsset(link.CitizenId, link.ArmyId, army.SourceSettlementId, reason);
        if (transfer.Status != OwnershipTransferStatus.Success)
        {
            throw new InvalidOperationException(transfer.Reason);
        }

        var returnedLink = link.MarkReturned();
        _raidPawnLinks[pawnThingId] = returnedLink;
        AppendEvent(WorldEventKind.RaidPawnReturned, link.CitizenId, $"Pawn {pawnThingId} returned to {army.SourceSettlementId}: {reason}.");

        return returnedLink;
    }

    public RaidPawnLink MarkRaidPawnPrisoner(int pawnThingId, string reason)
    {
        ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (!_raidPawnLinks.TryGetValue(pawnThingId, out var link))
        {
            throw new InvalidOperationException($"Pawn {pawnThingId} is not linked.");
        }

        if (link.Status != RaidPawnLinkStatus.Active)
        {
            return link;
        }

        if (_citizens.TryGetValue(link.CitizenId, out var citizen) && citizen.Status != CitizenStatus.Prisoner)
        {
            _citizens[link.CitizenId] = citizen with { Status = CitizenStatus.Prisoner };
            MarkDerivedAggregatesDirty();
        }

        var prisonerLink = link.MarkPrisoner();
        _raidPawnLinks[pawnThingId] = prisonerLink;
        AppendEvent(WorldEventKind.RaidPawnCaptured, link.CitizenId, $"Pawn {pawnThingId} captured: {reason}.");

        return prisonerLink;
    }

    public RaidPawnLink MarkRaidPawnMissing(int pawnThingId, string reason)
    {
        ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (!_raidPawnLinks.TryGetValue(pawnThingId, out var link))
        {
            throw new InvalidOperationException($"Pawn {pawnThingId} is not linked.");
        }

        if (link.Status != RaidPawnLinkStatus.Active)
        {
            return link;
        }

        if (_citizens.TryGetValue(link.CitizenId, out var citizen)
            && citizen.Status != CitizenStatus.Dead
            && citizen.Status != CitizenStatus.Missing)
        {
            _citizens[link.CitizenId] = citizen with { Status = CitizenStatus.Missing };
            MarkDerivedAggregatesDirty();
        }

        var missingLink = link.MarkMissing();
        _raidPawnLinks[pawnThingId] = missingLink;
        AppendEvent(WorldEventKind.RaidPawnMissing, link.CitizenId, $"Pawn {pawnThingId} lost: {reason}.");

        return missingLink;
    }

    public WorldRaidOutcome RecordRaidOutcome(
        EntityId armyId,
        int sent,
        int active,
        int dead,
        int returned,
        int prisoner,
        int missing)
    {
        if (_raidOutcomes.TryGetValue(armyId, out var existing))
        {
            return existing;
        }

        var army = GetArmy(armyId)
            ?? throw new InvalidOperationException($"Army {armyId} does not exist.");
        var outcome = new WorldRaidOutcome(
            armyId,
            army.SourceSettlementId,
            army.FactionId,
            CurrentTick,
            sent,
            active,
            dead,
            returned,
            prisoner,
            missing);

        _raidOutcomes.Add(armyId, outcome);
        AppendEvent(
            WorldEventKind.RaidResolved,
            armyId,
            $"Raid {armyId} resolved: sent {sent}, dead {dead}, returned {returned}, prisoner {prisoner}, missing {missing}.");

        return outcome;
    }

    public SettlementPopulation GetSettlementPopulation(EntityId settlementId)
    {
        return GetSettlementDerivedAggregate(settlementId).Population;
    }

    public SettlementDerivedAggregate GetSettlementDerivedAggregate(EntityId settlementId)
    {
        EnsureDerivedAggregates();
        return _settlementAggregates.TryGetValue(settlementId, out var aggregate)
            ? aggregate
            : new SettlementDerivedAggregate(
                settlementId,
                string.Empty,
                new SettlementPopulation(0, 0, 0, 0),
                new SettlementPower(0, 0));
    }

    public FactionDerivedAggregate GetFactionDerivedAggregate(string factionId)
    {
        ThrowIfNullOrWhiteSpace(factionId, nameof(factionId));
        EnsureDerivedAggregates();
        return _factionAggregates.TryGetValue(factionId, out var aggregate)
            ? aggregate
            : new FactionDerivedAggregate(
                factionId,
                new SettlementPopulation(0, 0, 0, 0),
                new SettlementPower(0, 0));
    }

    public SettlementFoodStatus GetSettlementFoodStatus(
        EntityId settlementId,
        string foodResourceKey,
        int foodPerCitizen)
    {
        return SettlementQueryService.GetFoodStatus(this, settlementId, foodResourceKey, foodPerCitizen);
    }

    public SettlementMigrationStatus GetSettlementMigrationStatus(
        EntityId settlementId,
        string foodResourceKey,
        int foodPerCitizen)
    {
        return SettlementQueryService.GetMigrationStatus(this, settlementId, foodResourceKey, foodPerCitizen);
    }

    public SettlementProductionStatus GetSettlementProductionStatus(EntityId settlementId)
    {
        return SettlementQueryService.GetProductionStatus(this, settlementId);
    }

    public SettlementCapabilityStatus GetSettlementCapabilityStatus(EntityId settlementId)
    {
        return SettlementCapabilityService.GetStatus(this, settlementId);
    }

    public IEnumerable<string> Validate()
    {
        foreach (var slugGroup in _settlements.Values
            .GroupBy(settlement => settlement.Slug, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            yield return $"Duplicate settlement slug {slugGroup.Key}.";
        }

        foreach (var citizen in _citizens.Values.OrderBy(citizen => citizen.Id.Value))
        {
            if (!_settlements.ContainsKey(citizen.SettlementId))
            {
                yield return $"Citizen {citizen.Id} references missing settlement {citizen.SettlementId}.";
            }

            if (citizen.Status == CitizenStatus.Alive && !_owners.ContainsKey(citizen.Id))
            {
                yield return $"Alive citizen {citizen.Id} does not have an owner.";
            }
        }

        foreach (var army in _armies.Values.OrderBy(army => army.Id.Value))
        {
            if (!_settlements.ContainsKey(army.SourceSettlementId))
            {
                yield return $"Army {army.Id} references missing source settlement {army.SourceSettlementId}.";
            }
        }

        foreach (var caravan in _caravans.Values.OrderBy(caravan => caravan.Id.Value))
        {
            if (!_settlements.ContainsKey(caravan.SourceSettlementId))
            {
                yield return $"Caravan {caravan.Id} references missing source settlement {caravan.SourceSettlementId}.";
            }

            if (!_settlements.ContainsKey(caravan.TargetSettlementId))
            {
                yield return $"Caravan {caravan.Id} references missing target settlement {caravan.TargetSettlementId}.";
            }
        }

        foreach (var group in _migrationGroups.Values.OrderBy(group => group.Id.Value))
        {
            if (!_settlements.ContainsKey(group.SourceSettlementId))
            {
                yield return $"Migration group {group.Id} references missing source settlement {group.SourceSettlementId}.";
            }

            if (group.TargetSettlementId.HasValue && !_settlements.ContainsKey(group.TargetSettlementId.Value))
            {
                yield return $"Migration group {group.Id} references missing target settlement {group.TargetSettlementId.Value}.";
            }
        }

        foreach (var opportunity in _raidOpportunities.Values.OrderBy(opportunity => opportunity.Id.Value))
        {
            if (!_intelReports.ContainsKey(opportunity.IntelReportId))
            {
                yield return $"Raid opportunity {opportunity.Id} references missing intel report {opportunity.IntelReportId}.";
            }
        }

        foreach (var capability in _settlementCapabilities.Values.OrderBy(capability => capability.SettlementId.Value))
        {
            if (!_settlements.ContainsKey(capability.SettlementId))
            {
                yield return $"Settlement capability references missing settlement {capability.SettlementId}.";
            }
        }

        foreach (var specialists in _specialistPools.Values.OrderBy(specialists => specialists.SettlementId.Value))
        {
            if (!_settlements.ContainsKey(specialists.SettlementId))
            {
                yield return $"Specialist pool references missing settlement {specialists.SettlementId}.";
            }
        }

        foreach (var link in _raidPawnLinks.Values.OrderBy(link => link.PawnThingId))
        {
            if (!_citizens.ContainsKey(link.CitizenId))
            {
                yield return $"Raid pawn link {link.PawnThingId} references missing citizen {link.CitizenId}.";
            }

            if (!_armies.ContainsKey(link.ArmyId))
            {
                yield return $"Raid pawn link {link.PawnThingId} references missing army {link.ArmyId}.";
            }
        }

        foreach (var ownership in _owners.OrderBy(pair => pair.Key.Kind).ThenBy(pair => pair.Key.Value))
        {
            if (!AssetExists(ownership.Key))
            {
                yield return $"Ownership asset {ownership.Key} does not exist.";
            }

            if (!OwnerExists(ownership.Value))
            {
                yield return $"Ownership owner {ownership.Value} does not exist.";
            }

            if (ownership.Key.Kind == EntityKind.Army
                && _armies.TryGetValue(ownership.Key, out var ownedArmy)
                && ownership.Value != ownedArmy.SourceSettlementId)
            {
                yield return $"Army {ownedArmy.Id} is owned by {ownership.Value}, not source settlement {ownedArmy.SourceSettlementId}.";
            }
        }

        foreach (var cohort in _animalCohorts.Values.OrderBy(cohort => cohort.Id.Value))
        {
            if (!OwnerExists(cohort.OwnerId))
            {
                yield return $"Animal cohort {cohort.Id} references missing owner {cohort.OwnerId}.";
            }

            if (_owners.TryGetValue(cohort.Id, out var ownerId) && ownerId != cohort.OwnerId)
            {
                yield return $"Animal cohort {cohort.Id} owner mismatch: cohort {cohort.OwnerId}, ledger {ownerId}.";
            }
            else if (!_owners.ContainsKey(cohort.Id))
            {
                yield return $"Animal cohort {cohort.Id} has no ownership record.";
            }

            if (cohort.Count < 0)
            {
                yield return $"Animal cohort {cohort.Id} has negative count {cohort.Count}.";
            }
        }

        foreach (var project in _animalBreedingProjects.Values.OrderBy(project => project.Id.Value))
        {
            if (!_settlements.ContainsKey(project.SettlementId))
            {
                yield return $"Animal breeding project {project.Id} references missing settlement {project.SettlementId}.";
            }

            if (!_animalCohorts.ContainsKey(project.SourceCohortId))
            {
                yield return $"Animal breeding project {project.Id} references missing cohort {project.SourceCohortId}.";
            }

            if (project.CompletionTick < project.StartedTick)
            {
                yield return $"Animal breeding project {project.Id} completes before it starts.";
            }
        }

        foreach (var intel in _factionSettlementIntel.Values
            .OrderBy(intel => intel.FactionId, StringComparer.Ordinal)
            .ThenBy(intel => intel.SettlementId.Value))
        {
            if (!_settlements.ContainsKey(intel.SettlementId))
            {
                yield return $"Faction settlement intel for {intel.FactionId} references missing settlement {intel.SettlementId}.";
            }
        }

        foreach (var resource in _resources
            .OrderBy(pair => pair.Key.OwnerId.Kind)
            .ThenBy(pair => pair.Key.OwnerId.Value)
            .ThenBy(pair => pair.Key.ResourceKey, StringComparer.Ordinal))
        {
            if (!OwnerExists(resource.Key.OwnerId))
            {
                yield return $"Resource {resource.Key.ResourceKey} references missing owner {resource.Key.OwnerId}.";
            }

            if (resource.Value < 0)
            {
                yield return $"Resource {resource.Key.ResourceKey} owned by {resource.Key.OwnerId} has negative quantity {resource.Value}.";
            }
        }

        foreach (var outcome in _raidOutcomes.Values.OrderBy(outcome => outcome.ArmyId.Value))
        {
            if (!_armies.ContainsKey(outcome.ArmyId))
            {
                yield return $"Raid outcome {outcome.ArmyId} references missing army.";
            }

            var resolvedTotal = outcome.Active + outcome.Dead + outcome.Returned + outcome.Prisoner + outcome.Missing;
            if (outcome.Sent != resolvedTotal)
            {
                yield return $"Raid outcome {outcome.ArmyId} totals do not balance: sent {outcome.Sent}, active/dead/returned/prisoner/missing {resolvedTotal}.";
            }
        }

        foreach (var profile in _productionProfiles.Values.OrderBy(profile => profile.SettlementId.Value))
        {
            if (!_settlements.ContainsKey(profile.SettlementId))
            {
                yield return $"Production profile references missing settlement {profile.SettlementId}.";
            }
        }

        foreach (var ruin in _ruins.Values.OrderBy(ruin => ruin.Id.Value))
        {
            if (!_settlements.ContainsKey(ruin.OriginalSettlementId))
            {
                yield return $"Ruin {ruin.Id} references missing original settlement {ruin.OriginalSettlementId}.";
            }

            if (ruin.ReclaimedSettlementId.HasValue && !_settlements.ContainsKey(ruin.ReclaimedSettlementId.Value))
            {
                yield return $"Ruin {ruin.Id} references missing reclaimed settlement {ruin.ReclaimedSettlementId.Value}.";
            }
        }

        foreach (var claim in _conflictClaims.Values.OrderBy(claim => claim.ConflictId.Value).ThenBy(claim => claim.SettlementId.Value))
        {
            if (!_conflicts.ContainsKey(claim.ConflictId))
            {
                yield return $"Conflict claim references missing conflict {claim.ConflictId}.";
            }

            if (!_settlements.ContainsKey(claim.SettlementId))
            {
                yield return $"Conflict claim references missing settlement {claim.SettlementId}.";
            }
        }
    }

    private EntityId NextId(EntityKind kind)
    {
        _nextIds.TryGetValue(kind, out var current);
        var next = current + 1;
        _nextIds[kind] = next;

        return EntityId.Create(kind, next);
    }

    private void ReserveExistingId(EntityId id)
    {
        _nextIds.TryGetValue(id.Kind, out var current);

        if (id.Value > current)
        {
            _nextIds[id.Kind] = id.Value;
        }
    }

    private void EnsureOwnerExists(EntityId ownerId)
    {
        if (!OwnerExists(ownerId))
        {
            throw new InvalidOperationException($"Owner {ownerId} does not exist.");
        }
    }

    internal void EnsureOwnerExistsForLedger(EntityId ownerId)
    {
        EnsureOwnerExists(ownerId);
    }

    internal bool OwnerExistsForLedger(EntityId ownerId)
    {
        return OwnerExists(ownerId);
    }

    internal int GetResourceQuantityForLedger(EntityId ownerId, string resourceKey)
    {
        return _resources.TryGetValue((ownerId, resourceKey), out var quantity)
            ? quantity
            : 0;
    }

    internal void SetResourceQuantityForLedger(EntityId ownerId, string resourceKey, int quantity)
    {
        var key = (ownerId, resourceKey);
        if (quantity <= 0)
        {
            _resources.Remove(key);
        }
        else
        {
            _resources[key] = quantity;
        }
    }

    internal void SetOwnerForLedger(EntityId assetId, EntityId ownerId)
    {
        _owners[assetId] = ownerId;
        if (assetId.Kind == EntityKind.Citizen)
        {
            MarkDerivedAggregatesDirty();
        }
        else if (assetId.Kind == EntityKind.Animal && _animalCohorts.TryGetValue(assetId, out var cohort))
        {
            _animalCohorts[assetId] = cohort with { OwnerId = ownerId };
        }
    }

    internal void ReplaceCitizenForSimulation(WorldCitizen citizen)
    {
        if (!_citizens.ContainsKey(citizen.Id))
        {
            throw new InvalidOperationException($"Citizen {citizen.Id} does not exist.");
        }

        _citizens[citizen.Id] = citizen;
        MarkDerivedAggregatesDirty();
    }

    private bool OwnerExists(EntityId ownerId)
    {
        return ownerId.Kind switch
        {
            EntityKind.Citizen => _citizens.ContainsKey(ownerId),
            EntityKind.Settlement => _settlements.ContainsKey(ownerId),
            EntityKind.Army => _armies.ContainsKey(ownerId),
            EntityKind.Caravan => _caravans.ContainsKey(ownerId),
            EntityKind.Mission => _missions.ContainsKey(ownerId),
            EntityKind.MigrationGroup => _migrationGroups.ContainsKey(ownerId),
            EntityKind.IntelReport => _intelReports.ContainsKey(ownerId),
            EntityKind.RaidOpportunity => _raidOpportunities.ContainsKey(ownerId),
            EntityKind.RaidIntelFact => _raidIntelFacts.ContainsKey(ownerId),
            EntityKind.RaidPreparation => _raidPreparations.ContainsKey(ownerId),
            EntityKind.MaterializationLease => _materializationLeases.ContainsKey(ownerId),
            EntityKind.Ruin => _ruins.ContainsKey(ownerId),
            EntityKind.Conflict => _conflicts.ContainsKey(ownerId),
            _ => false
        };
    }

    private bool AssetExists(EntityId assetId)
    {
        return assetId.Kind switch
        {
            EntityKind.Citizen => _citizens.ContainsKey(assetId),
            EntityKind.Settlement => _settlements.ContainsKey(assetId),
            EntityKind.Army => _armies.ContainsKey(assetId),
            EntityKind.Caravan => _caravans.ContainsKey(assetId),
            EntityKind.Mission => _missions.ContainsKey(assetId),
            EntityKind.MigrationGroup => _migrationGroups.ContainsKey(assetId),
            EntityKind.IntelReport => _intelReports.ContainsKey(assetId),
            EntityKind.RaidOpportunity => _raidOpportunities.ContainsKey(assetId),
            EntityKind.RaidIntelFact => _raidIntelFacts.ContainsKey(assetId),
            EntityKind.RaidPreparation => _raidPreparations.ContainsKey(assetId),
            EntityKind.MaterializationLease => _materializationLeases.ContainsKey(assetId),
            EntityKind.Animal => _animalCohorts.ContainsKey(assetId),
            EntityKind.SettlementFacility => _settlementFacilities.ContainsKey(assetId),
            EntityKind.SettlementProject => _settlementProjects.ContainsKey(assetId),
            EntityKind.Ruin => _ruins.ContainsKey(assetId),
            EntityKind.Conflict => _conflicts.ContainsKey(assetId),
            EntityKind.AnimalBreedingProject => _animalBreedingProjects.ContainsKey(assetId),
            _ => false
        };
    }

    private static void ThrowIfNullOrWhiteSpace(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", parameterName);
        }
    }

    private static bool ShouldKeepExistingKnowledge(
        KnownSettlementInfo existing,
        KnownSettlementInfo incoming)
    {
        return existing.ExactValuesVisible
            && !incoming.ExactValuesVisible
            && existing.Confidence >= incoming.Confidence;
    }

    private static SettlementCapability Normalize(SettlementCapability capability)
    {
        return capability with
        {
            HousingCapacity = Math.Max(0, capability.HousingCapacity),
            FoodStorageCapacity = Math.Max(0, capability.FoodStorageCapacity),
            MedicineStorageCapacity = Math.Max(0, capability.MedicineStorageCapacity),
            PowerCapacity = Math.Max(0, capability.PowerCapacity),
            LaboratoryCapacity = Math.Max(0, capability.LaboratoryCapacity),
            AnimalCapacity = Math.Max(0, capability.AnimalCapacity),
            CropCapacity = Math.Max(0, capability.CropCapacity),
            ResearchCapacity = Math.Max(0, capability.ResearchCapacity),
            MechanicalCapacity = Math.Max(0, capability.MechanicalCapacity),
            PollutionHandling = Math.Max(0, capability.PollutionHandling)
        };
    }

    private static SettlementFacility Normalize(SettlementFacility facility)
    {
        return facility with
        {
            Level = Math.Max(1, facility.Level),
            ConditionPercent = Math.Max(0, Math.Min(100, facility.ConditionPercent)),
            BuiltTick = Math.Max(0, facility.BuiltTick)
        };
    }

    private static SettlementProject Normalize(SettlementProject project)
    {
        var started = Math.Max(0, project.StartedTick);
        return project with
        {
            FacilityLevel = Math.Max(1, project.FacilityLevel),
            StartedTick = started,
            CompletionTick = Math.Max(started, project.CompletionTick),
            SteelCost = Math.Max(0, project.SteelCost),
            ComponentCost = Math.Max(0, project.ComponentCost)
        };
    }

    private static WorldAnimalCohort Normalize(WorldAnimalCohort cohort)
    {
        return cohort with
        {
            AnimalKind = string.IsNullOrWhiteSpace(cohort.AnimalKind) ? "UnknownAnimal" : cohort.AnimalKind.Trim(),
            Count = Math.Max(0, cohort.Count),
            HealthPercent = Math.Max(0, Math.Min(100, cohort.HealthPercent)),
            FertilityPercent = Math.Max(0, Math.Min(100, cohort.FertilityPercent)),
            CarryingCapacity = Math.Max(0, cohort.CarryingCapacity),
            LastUpdatedTick = Math.Max(0, cohort.LastUpdatedTick)
        };
    }

    private static AnimalBreedingProject Normalize(AnimalBreedingProject project)
    {
        var started = Math.Max(0, project.StartedTick);
        return project with
        {
            StartedTick = started,
            CompletionTick = Math.Max(started, project.CompletionTick),
            FeedResourceKey = string.IsNullOrWhiteSpace(project.FeedResourceKey) ? "Hay" : project.FeedResourceKey.Trim(),
            FeedCost = Math.Max(0, project.FeedCost),
            MedicineResourceKey = string.IsNullOrWhiteSpace(project.MedicineResourceKey) ? "MedicineIndustrial" : project.MedicineResourceKey.Trim(),
            MedicineCost = Math.Max(0, project.MedicineCost),
            ComponentResourceKey = string.IsNullOrWhiteSpace(project.ComponentResourceKey) ? "ComponentIndustrial" : project.ComponentResourceKey.Trim(),
            ComponentCost = Math.Max(0, project.ComponentCost)
        };
    }

    private static CropStrain Normalize(CropStrain strain)
    {
        return strain with
        {
            CropKind = string.IsNullOrWhiteSpace(strain.CropKind) ? "UnknownCrop" : strain.CropKind.Trim(),
            YieldPercent = Math.Max(50, Math.Min(200, strain.YieldPercent)),
            HardinessPercent = Math.Max(50, Math.Min(200, strain.HardinessPercent)),
            GrowthSpeedPercent = Math.Max(50, Math.Min(200, strain.GrowthSpeedPercent)),
            LastUpdatedTick = Math.Max(0, strain.LastUpdatedTick)
        };
    }

    private static CropStrainProject Normalize(CropStrainProject project)
    {
        var started = Math.Max(0, project.StartedTick);
        return project with
        {
            CropKind = string.IsNullOrWhiteSpace(project.CropKind) ? "UnknownCrop" : project.CropKind.Trim(),
            StartedTick = started,
            CompletionTick = Math.Max(started, project.CompletionTick),
            FoodResourceKey = string.IsNullOrWhiteSpace(project.FoodResourceKey) ? "Food" : project.FoodResourceKey.Trim(),
            FoodCost = Math.Max(0, project.FoodCost),
            MedicineResourceKey = string.IsNullOrWhiteSpace(project.MedicineResourceKey) ? "MedicineIndustrial" : project.MedicineResourceKey.Trim(),
            MedicineCost = Math.Max(0, project.MedicineCost)
        };
    }

    private static SettlementTechnology Normalize(SettlementTechnology technology)
    {
        return technology with
        {
            LastUpdatedTick = Math.Max(0, technology.LastUpdatedTick),
            Source = string.IsNullOrWhiteSpace(technology.Source) ? "unknown" : technology.Source.Trim()
        };
    }

    private void SeedTechnologyFromProductionProfile(SettlementProductionProfile profile)
    {
        var tier = TechnologyTierFromProfile(profile.TechLevel);
        foreach (var domain in new[] { TechnologyDomain.Agriculture, TechnologyDomain.Medicine, TechnologyDomain.Industry })
        {
            var key = (profile.SettlementId, domain);
            if (!_settlementTechnologies.ContainsKey(key))
            {
                _settlementTechnologies[key] = new SettlementTechnology(
                    profile.SettlementId,
                    domain,
                    tier,
                    CurrentTick,
                    "production-profile");
            }
        }
    }

    private static TechnologyTier TechnologyTierFromProfile(string techLevel)
    {
        if (string.IsNullOrWhiteSpace(techLevel))
        {
            return TechnologyTier.Neolithic;
        }

        if (techLevel.IndexOf("Spacer", StringComparison.OrdinalIgnoreCase) >= 0
            || techLevel.IndexOf("Ultra", StringComparison.OrdinalIgnoreCase) >= 0
            || techLevel.IndexOf("Archotech", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return TechnologyTier.Spacer;
        }

        if (techLevel.IndexOf("Industrial", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return TechnologyTier.Industrial;
        }

        return techLevel.IndexOf("Medieval", StringComparison.OrdinalIgnoreCase) >= 0
            ? TechnologyTier.Medieval
            : TechnologyTier.Neolithic;
    }

    private static WorldRuin Normalize(WorldRuin ruin)
    {
        return ruin with
        {
            Slug = string.IsNullOrWhiteSpace(ruin.Slug) ? "unknown-ruin" : ruin.Slug.Trim(),
            Name = string.IsNullOrWhiteSpace(ruin.Name) ? "Unknown Ruin" : ruin.Name.Trim(),
            FormerFactionId = string.IsNullOrWhiteSpace(ruin.FormerFactionId) ? "Unknown" : ruin.FormerFactionId.Trim(),
            ClaimFactionId = string.IsNullOrWhiteSpace(ruin.ClaimFactionId) ? "Unknown" : ruin.ClaimFactionId.Trim(),
            CreatedTick = Math.Max(0, ruin.CreatedTick),
            StatusTick = Math.Max(0, ruin.StatusTick)
        };
    }

    private static WorldConflict Normalize(WorldConflict conflict)
    {
        return conflict with
        {
            FactionA = string.IsNullOrWhiteSpace(conflict.FactionA) ? "UnknownA" : conflict.FactionA.Trim(),
            FactionB = string.IsNullOrWhiteSpace(conflict.FactionB) ? "UnknownB" : conflict.FactionB.Trim(),
            StartedTick = Math.Max(0, conflict.StartedTick),
            StatusTick = Math.Max(0, conflict.StatusTick),
            WarExhaustionA = Math.Max(0, conflict.WarExhaustionA),
            WarExhaustionB = Math.Max(0, conflict.WarExhaustionB),
            TruceExpiresTick = Math.Max(0, conflict.TruceExpiresTick),
            RefugeesCreated = Math.Max(0, conflict.RefugeesCreated)
        };
    }

    private static ConflictClaim Normalize(ConflictClaim claim)
    {
        return claim with
        {
            ClaimantFactionId = string.IsNullOrWhiteSpace(claim.ClaimantFactionId) ? "Unknown" : claim.ClaimantFactionId.Trim(),
            Tick = Math.Max(0, claim.Tick)
        };
    }

    private static FactionSettlementIntel Normalize(FactionSettlementIntel intel)
    {
        return intel with
        {
            FactionId = string.IsNullOrWhiteSpace(intel.FactionId) ? "Unknown" : intel.FactionId.Trim(),
            Tick = Math.Max(0, intel.Tick),
            Confidence = Math.Max(0, Math.Min(100, intel.Confidence))
        };
    }

    private static SpecialistPool Normalize(SpecialistPool specialists)
    {
        return specialists with
        {
            Farmers = Math.Max(0, specialists.Farmers),
            Handlers = Math.Max(0, specialists.Handlers),
            Doctors = Math.Max(0, specialists.Doctors),
            Researchers = Math.Max(0, specialists.Researchers),
            Engineers = Math.Max(0, specialists.Engineers),
            Geneticists = Math.Max(0, specialists.Geneticists),
            Mechanitors = Math.Max(0, specialists.Mechanitors),
            Soldiers = Math.Max(0, specialists.Soldiers),
            Diplomats = Math.Max(0, specialists.Diplomats)
        };
    }

    private void MarkDerivedAggregatesDirty()
    {
        derivedAggregatesDirty = true;
    }

    private void EnsureDerivedAggregates()
    {
        if (!derivedAggregatesDirty)
        {
            return;
        }

        var settlementAccumulators = _settlements.Values.ToDictionary(
            settlement => settlement.Id,
            settlement => new SettlementAggregateAccumulator(settlement.Id, settlement.FactionId));

        foreach (var citizen in _citizens.Values)
        {
            if (citizen.Status != CitizenStatus.Alive
                || !_owners.TryGetValue(citizen.Id, out var ownerId)
                || ownerId != citizen.SettlementId
                || !settlementAccumulators.TryGetValue(citizen.SettlementId, out var accumulator))
            {
                continue;
            }

            accumulator.Add(citizen);
        }

        _settlementAggregates.Clear();
        foreach (var accumulator in settlementAccumulators.Values)
        {
            var population = accumulator.ToPopulation();
            _settlementAggregates[accumulator.SettlementId] = new SettlementDerivedAggregate(
                accumulator.SettlementId,
                accumulator.FactionId,
                population,
                new SettlementPower(
                    population.Adults,
                    SettlementPowerService.CombatPowerOf(population.Adults)));
        }

        _factionAggregates.Clear();
        foreach (var factionGroup in _settlementAggregates.Values
            .GroupBy(aggregate => aggregate.FactionId, StringComparer.Ordinal))
        {
            var population = SumPopulations(factionGroup.Select(aggregate => aggregate.Population));
            var combatants = factionGroup.Sum(aggregate => aggregate.Power.Combatants);
            var combatPower = factionGroup.Sum(aggregate => aggregate.Power.CombatPower);
            _factionAggregates[factionGroup.Key] = new FactionDerivedAggregate(
                factionGroup.Key,
                population,
                new SettlementPower(combatants, combatPower));
        }

        derivedAggregatesDirty = false;
    }

    private static SettlementPopulation SumPopulations(IEnumerable<SettlementPopulation> populations)
    {
        var total = 0;
        var children = 0;
        var adults = 0;
        var elderly = 0;

        foreach (var population in populations)
        {
            total += population.Total;
            children += population.Children;
            adults += population.Adults;
            elderly += population.Elderly;
        }

        return new SettlementPopulation(total, children, adults, elderly);
    }

    private void AppendEvent(WorldEventKind kind, EntityId? subjectId, string summary)
    {
        if (eventSuppressionDepth > 0)
        {
            return;
        }

        _events.Add(new WorldEvent(
            NextId(EntityKind.Event),
            kind,
            CurrentTick,
            subjectId,
            summary));
    }

    private sealed class SettlementAggregateAccumulator
    {
        private int total;
        private int children;
        private int adults;
        private int elderly;

        public SettlementAggregateAccumulator(EntityId settlementId, string factionId)
        {
            SettlementId = settlementId;
            FactionId = factionId;
        }

        public EntityId SettlementId { get; }

        public string FactionId { get; }

        public void Add(WorldCitizen citizen)
        {
            total++;
            if (citizen.IsChild)
            {
                children++;
            }
            else if (citizen.IsElderly)
            {
                elderly++;
            }
            else
            {
                adults++;
            }
        }

        public SettlementPopulation ToPopulation()
        {
            return new SettlementPopulation(total, children, adults, elderly);
        }
    }
}
