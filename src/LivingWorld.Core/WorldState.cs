namespace LivingWorld.Core;

public sealed class WorldState
{
    private readonly Dictionary<EntityId, WorldCitizen> _citizens = new();
    private readonly Dictionary<EntityId, WorldSettlement> _settlements = new();
    private readonly Dictionary<EntityId, WorldArmy> _armies = new();
    private readonly Dictionary<EntityId, WorldMigrationGroup> _migrationGroups = new();
    private readonly Dictionary<EntityId, WorldIntelReport> _intelReports = new();
    private readonly Dictionary<EntityId, KnownSettlementInfo> _knownSettlementInfos = new();
    private readonly Dictionary<EntityId, RaidOpportunity> _raidOpportunities = new();
    private readonly Dictionary<int, RaidPawnLink> _raidPawnLinks = new();
    private readonly Dictionary<EntityId, WorldRaidOutcome> _raidOutcomes = new();
    private readonly Dictionary<EntityId, Drifter> _drifters = new();
    private readonly Dictionary<EntityId, WorldArmyMovement> _armyMovements = new();
    private readonly Dictionary<string, FactionBehavior> _factionBehaviors = new(StringComparer.Ordinal);
    private readonly Dictionary<(string, string), int> _factionRelations = new();
    private readonly HashSet<string> _irreconcilableFactions = new(StringComparer.Ordinal);
    private readonly Dictionary<EntityId, SettlementProductionProfile> _productionProfiles = new();
    private readonly Dictionary<string, WorldFactionRecord> _factionRecords = new(StringComparer.Ordinal);
    private readonly Dictionary<EntityId, EntityId> _owners = new();
    private readonly Dictionary<(EntityId OwnerId, string ResourceKey), int> _resources = new();
    private readonly List<WorldEvent> _events = new();
    private readonly Dictionary<EntityKind, long> _nextIds = new();
    private int eventSuppressionDepth;
    private int initialWorldSeedingDepth;

    public WorldState(int worldSeed)
    {
        WorldSeed = worldSeed;
    }

    public int WorldSeed { get; }

    public int CurrentTick { get; private set; }

    public IReadOnlyCollection<WorldCitizen> Citizens => _citizens.Values;

    public IReadOnlyCollection<WorldSettlement> Settlements => _settlements.Values;

    public IReadOnlyCollection<WorldArmy> Armies => _armies.Values;

    public IReadOnlyCollection<WorldArmyMovement> ArmyMovements => _armyMovements.Values;

    public IReadOnlyDictionary<string, FactionBehavior> FactionBehaviors => _factionBehaviors;

    public IReadOnlyDictionary<(string, string), int> FactionRelations => _factionRelations;

    public IReadOnlyCollection<string> IrreconcilableFactions => _irreconcilableFactions;

    public IReadOnlyCollection<WorldMigrationGroup> MigrationGroups => _migrationGroups.Values;

    public IReadOnlyCollection<WorldIntelReport> IntelReports => _intelReports.Values;

    public IReadOnlyCollection<KnownSettlementInfo> KnownSettlementInfos => _knownSettlementInfos.Values;

    public IReadOnlyCollection<RaidOpportunity> RaidOpportunities => _raidOpportunities.Values;

    public IReadOnlyCollection<RaidPawnLink> RaidPawnLinks => _raidPawnLinks.Values;

    public IReadOnlyCollection<WorldRaidOutcome> RaidOutcomes => _raidOutcomes.Values;

    public IReadOnlyCollection<Drifter> Drifters => _drifters.Values;

    public IReadOnlyCollection<SettlementProductionProfile> ProductionProfiles => _productionProfiles.Values;

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
            _drifters.Values.OrderBy(drifter => drifter.Id.Value).ToList());
    }

    public static WorldState FromSnapshot(WorldStateSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var state = new WorldState(snapshot.WorldSeed)
        {
            CurrentTick = snapshot.CurrentTick
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

        foreach (var opportunity in snapshot.RaidOpportunities)
        {
            state._raidOpportunities.Add(opportunity.Id, opportunity);
            state.ReserveExistingId(opportunity.Id);
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
        AppendEvent(WorldEventKind.CitizenImported, citizen.Id, $"Citizen {citizen.Id} imported.");
    }

    public WorldArmy CreateArmy(string name, string factionId, EntityId sourceSettlementId)
    {
        ThrowIfNullOrWhiteSpace(name, nameof(name));
        ThrowIfNullOrWhiteSpace(factionId, nameof(factionId));

        if (!_settlements.ContainsKey(sourceSettlementId))
        {
            throw new InvalidOperationException($"Settlement {sourceSettlementId} does not exist.");
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

    public WorldArmyMovement DispatchArmy(EntityId armyId, EntityId targetSettlementId, int arrivalTick)
    {
        if (!_armies.ContainsKey(armyId))
        {
            throw new InvalidOperationException($"Army {armyId} does not exist.");
        }

        if (!_settlements.ContainsKey(targetSettlementId))
        {
            throw new InvalidOperationException($"Settlement {targetSettlementId} does not exist.");
        }

        var movement = new WorldArmyMovement(
            armyId,
            targetSettlementId,
            CurrentTick,
            Math.Max(CurrentTick, arrivalTick),
            ArmyMovementStatus.Traveling);

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

        var updated = movement with { Status = status };
        _armyMovements[armyId] = updated;
        return updated;
    }

    internal void RestoreArmyMovementForLedger(WorldArmyMovement movement)
    {
        _armyMovements[movement.ArmyId] = movement;
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

        // Surviving residents keep their SettlementId and ownership, so they simply belong to
        // the capturing faction now.
        var previousFaction = settlement.FactionId;
        var captured = settlement with { FactionId = newFactionId };
        _settlements[settlementId] = captured;
        AppendEvent(
            WorldEventKind.SettlementCaptured,
            settlementId,
            $"Settlement {settlementId} captured by {newFactionId} from {previousFaction}.");
        return captured;
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

        AppendEvent(
            WorldEventKind.SettlementFounded,
            colony.Id,
            $"Settlement {colony.Id} founded by {source.FactionId} with {settlers.Count} settlers from {sourceSettlementId}.");
        return colony;
    }

    public void AssignFactionBehavior(string factionId, FactionBehavior behavior)
    {
        ThrowIfNullOrWhiteSpace(factionId, nameof(factionId));
        _factionBehaviors[factionId] = behavior;
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
        return SettlementQueryService.GetPopulation(this, settlementId);
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
    }

    internal void ReplaceCitizenForSimulation(WorldCitizen citizen)
    {
        if (!_citizens.ContainsKey(citizen.Id))
        {
            throw new InvalidOperationException($"Citizen {citizen.Id} does not exist.");
        }

        _citizens[citizen.Id] = citizen;
    }

    private bool OwnerExists(EntityId ownerId)
    {
        return ownerId.Kind switch
        {
            EntityKind.Citizen => _citizens.ContainsKey(ownerId),
            EntityKind.Settlement => _settlements.ContainsKey(ownerId),
            EntityKind.Army => _armies.ContainsKey(ownerId),
            EntityKind.MigrationGroup => _migrationGroups.ContainsKey(ownerId),
            EntityKind.IntelReport => _intelReports.ContainsKey(ownerId),
            EntityKind.RaidOpportunity => _raidOpportunities.ContainsKey(ownerId),
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
            EntityKind.MigrationGroup => _migrationGroups.ContainsKey(assetId),
            EntityKind.IntelReport => _intelReports.ContainsKey(assetId),
            EntityKind.RaidOpportunity => _raidOpportunities.ContainsKey(assetId),
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
}
