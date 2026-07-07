namespace LivingWorld.Core;

public sealed class WorldState
{
    private readonly Dictionary<EntityId, WorldCitizen> _citizens = new();
    private readonly Dictionary<EntityId, WorldSettlement> _settlements = new();
    private readonly Dictionary<EntityId, WorldArmy> _armies = new();
    private readonly Dictionary<EntityId, WorldIntelReport> _intelReports = new();
    private readonly Dictionary<EntityId, KnownSettlementInfo> _knownSettlementInfos = new();
    private readonly Dictionary<EntityId, RaidOpportunity> _raidOpportunities = new();
    private readonly Dictionary<int, RaidPawnLink> _raidPawnLinks = new();
    private readonly Dictionary<EntityId, WorldRaidOutcome> _raidOutcomes = new();
    private readonly Dictionary<EntityId, Drifter> _drifters = new();
    private readonly Dictionary<EntityId, EntityId> _owners = new();
    private readonly Dictionary<(EntityId OwnerId, string ResourceKey), int> _resources = new();
    private readonly List<WorldEvent> _events = new();
    private readonly Dictionary<EntityKind, long> _nextIds = new();
    private int eventSuppressionDepth;

    public WorldState(int worldSeed)
    {
        WorldSeed = worldSeed;
    }

    public int WorldSeed { get; }

    public int CurrentTick { get; private set; }

    public IReadOnlyCollection<WorldCitizen> Citizens => _citizens.Values;

    public IReadOnlyCollection<WorldSettlement> Settlements => _settlements.Values;

    public IReadOnlyCollection<WorldArmy> Armies => _armies.Values;

    public IReadOnlyCollection<WorldIntelReport> IntelReports => _intelReports.Values;

    public IReadOnlyCollection<KnownSettlementInfo> KnownSettlementInfos => _knownSettlementInfos.Values;

    public IReadOnlyCollection<RaidOpportunity> RaidOpportunities => _raidOpportunities.Values;

    public IReadOnlyCollection<RaidPawnLink> RaidPawnLinks => _raidPawnLinks.Values;

    public IReadOnlyCollection<WorldRaidOutcome> RaidOutcomes => _raidOutcomes.Values;

    public IReadOnlyCollection<Drifter> Drifters => _drifters.Values;

    public IReadOnlyList<WorldEvent> Events => _events;

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

    public WorldStateSnapshot CreateSnapshot()
    {
        return new WorldStateSnapshot(
            WorldSeed,
            CurrentTick,
            _settlements.Values.OrderBy(settlement => settlement.Id.Value).ToList(),
            _citizens.Values.OrderBy(citizen => citizen.Id.Value).ToList(),
            _armies.Values.OrderBy(army => army.Id.Value).ToList(),
            _intelReports.Values.OrderBy(report => report.Id.Value).ToList(),
            _knownSettlementInfos.Values.OrderBy(info => info.SettlementId.Value).ToList(),
            _raidOpportunities.Values.OrderBy(opportunity => opportunity.Id.Value).ToList(),
            _raidPawnLinks.Values.OrderBy(link => link.PawnThingId).ToList(),
            _raidOutcomes.Values.OrderBy(outcome => outcome.ArmyId.Value).ToList(),
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

    public Drifter CreateDrifter(string name, int age, Sex sex)
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
            CurrentTick);

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

    public EntityId? GetOwner(EntityId assetId)
    {
        return _owners.TryGetValue(assetId, out var ownerId)
            ? ownerId
            : null;
    }

    public void AddResource(EntityId ownerId, string resourceKey, int quantity)
    {
        ThrowIfNullOrWhiteSpace(resourceKey, nameof(resourceKey));

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Resource quantity must be positive.");
        }

        EnsureOwnerExists(ownerId);

        var key = (ownerId, resourceKey);
        _resources.TryGetValue(key, out var current);
        _resources[key] = current + quantity;

        AppendEvent(WorldEventKind.ResourceAdded, ownerId, $"{quantity} {resourceKey} added to {ownerId}.");
    }

    public int GetOwnedResourceQuantity(EntityId ownerId, string resourceKey)
    {
        ThrowIfNullOrWhiteSpace(resourceKey, nameof(resourceKey));

        return _resources.TryGetValue((ownerId, resourceKey), out var quantity)
            ? quantity
            : 0;
    }

    public int ConsumeResource(EntityId ownerId, string resourceKey, int requestedQuantity, string reason)
    {
        ThrowIfNullOrWhiteSpace(resourceKey, nameof(resourceKey));
        ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (requestedQuantity <= 0)
        {
            return 0;
        }

        EnsureOwnerExists(ownerId);

        var key = (ownerId, resourceKey);
        var available = GetOwnedResourceQuantity(ownerId, resourceKey);
        var consumed = Math.Min(available, requestedQuantity);
        if (consumed == 0)
        {
            return 0;
        }

        var remaining = available - consumed;
        if (remaining == 0)
        {
            _resources.Remove(key);
        }
        else
        {
            _resources[key] = remaining;
        }

        AppendEvent(
            WorldEventKind.ResourceConsumed,
            ownerId,
            $"{consumed} {resourceKey} consumed by {ownerId}: {reason}.");

        return consumed;
    }

    public OwnershipTransferResult TransferResource(
        EntityId fromOwnerId,
        EntityId toOwnerId,
        string resourceKey,
        int quantity,
        string reason)
    {
        ThrowIfNullOrWhiteSpace(resourceKey, nameof(resourceKey));
        ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (quantity <= 0)
        {
            return new OwnershipTransferResult(
                OwnershipTransferStatus.InvalidQuantity,
                "Resource transfer quantity must be positive.");
        }

        if (!OwnerExists(fromOwnerId))
        {
            return new OwnershipTransferResult(
                OwnershipTransferStatus.UnknownOwner,
                $"Source owner {fromOwnerId} does not exist.");
        }

        if (!OwnerExists(toOwnerId))
        {
            return new OwnershipTransferResult(
                OwnershipTransferStatus.UnknownOwner,
                $"Target owner {toOwnerId} does not exist.");
        }

        var fromKey = (fromOwnerId, resourceKey);
        var toKey = (toOwnerId, resourceKey);
        var available = GetOwnedResourceQuantity(fromOwnerId, resourceKey);

        if (available < quantity)
        {
            return new OwnershipTransferResult(
                OwnershipTransferStatus.InsufficientOwnedAssets,
                $"Owner {fromOwnerId} has {available} {resourceKey} but transfer requested {quantity}.");
        }

        var remaining = available - quantity;
        if (remaining == 0)
        {
            _resources.Remove(fromKey);
        }
        else
        {
            _resources[fromKey] = remaining;
        }

        _resources.TryGetValue(toKey, out var targetCurrent);
        _resources[toKey] = targetCurrent + quantity;

        AppendEvent(
            WorldEventKind.OwnershipTransferred,
            fromOwnerId,
            $"{quantity} {resourceKey} transferred from {fromOwnerId} to {toOwnerId}: {reason}.");

        return OwnershipTransferResult.Completed(
            $"Transferred {quantity} {resourceKey} from {fromOwnerId} to {toOwnerId}.");
    }

    public OwnershipTransferResult TransferAsset(
        EntityId assetId,
        EntityId fromOwnerId,
        EntityId toOwnerId,
        string reason)
    {
        ThrowIfNullOrWhiteSpace(reason, nameof(reason));

        if (!_owners.TryGetValue(assetId, out var currentOwnerId))
        {
            return new OwnershipTransferResult(
                OwnershipTransferStatus.OwnerMismatch,
                $"Asset {assetId} does not have an owner.");
        }

        if (currentOwnerId != fromOwnerId)
        {
            return new OwnershipTransferResult(
                OwnershipTransferStatus.OwnerMismatch,
                $"Asset {assetId} is owned by {currentOwnerId}, not {fromOwnerId}.");
        }

        if (!OwnerExists(toOwnerId))
        {
            return new OwnershipTransferResult(
                OwnershipTransferStatus.UnknownOwner,
                $"Target owner {toOwnerId} does not exist.");
        }

        _owners[assetId] = toOwnerId;
        AppendEvent(
            WorldEventKind.OwnershipTransferred,
            assetId,
            $"Asset {assetId} transferred from {fromOwnerId} to {toOwnerId}: {reason}.");

        return OwnershipTransferResult.Completed(
            $"Transferred {assetId} from {fromOwnerId} to {toOwnerId}.");
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

        _knownSettlementInfos[info.SettlementId] = info;
        AppendEvent(
            WorldEventKind.SettlementIntelUpdated,
            info.SettlementId,
            $"Known settlement info updated from {info.SourceKind}: {info.Summary}.");
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
        var citizens = _citizens.Values
            .Where(citizen =>
                citizen.SettlementId == settlementId
                && citizen.Status != CitizenStatus.Dead
                && GetOwner(citizen.Id) == settlementId)
            .ToList();

        return new SettlementPopulation(
            citizens.Count,
            citizens.Count(citizen => citizen.IsChild),
            citizens.Count(citizen => citizen.IsAdult),
            citizens.Count(citizen => citizen.IsElderly));
    }

    public SettlementFoodStatus GetSettlementFoodStatus(
        EntityId settlementId,
        string foodResourceKey,
        int foodPerCitizen)
    {
        ThrowIfNullOrWhiteSpace(foodResourceKey, nameof(foodResourceKey));

        var population = GetSettlementPopulation(settlementId);
        var food = GetOwnedResourceQuantity(settlementId, foodResourceKey);
        var dailyNeed = population.Total * Math.Max(0, foodPerCitizen);
        var foodDays = dailyNeed > 0
            ? food / dailyNeed
            : 0;

        return new SettlementFoodStatus(
            population.Total,
            dailyNeed,
            food,
            foodDays,
            dailyNeed > 0 && food < dailyNeed);
    }

    public SettlementMigrationStatus GetSettlementMigrationStatus(
        EntityId settlementId,
        string foodResourceKey,
        int foodPerCitizen)
    {
        ThrowIfNullOrWhiteSpace(foodResourceKey, nameof(foodResourceKey));

        var food = GetSettlementFoodStatus(settlementId, foodResourceKey, foodPerCitizen);
        var refugees = _citizens.Values.Count(citizen =>
            citizen.SettlementId == settlementId
            && citizen.Status == CitizenStatus.Refugee);
        var pressure = 0;
        var reason = MigrationService.ReasonNone;

        if (food.DailyNeed > 0 && food.FoodDays <= 0)
        {
            pressure += 70;
            reason = MigrationService.ReasonStarvation;
        }
        else if (food.IsShortage)
        {
            pressure += 45;
            reason = MigrationService.ReasonStarvation;
        }

        var population = GetSettlementPopulation(settlementId);
        if (population.Adults <= 1 && population.Total > 0)
        {
            pressure += 15;
        }

        return new SettlementMigrationStatus(
            Math.Min(100, pressure),
            refugees,
            reason,
            pressure >= 50);
    }

    public IEnumerable<string> Validate()
    {
        foreach (var citizen in _citizens.Values.OrderBy(citizen => citizen.Id.Value))
        {
            if (!_settlements.ContainsKey(citizen.SettlementId))
            {
                yield return $"Citizen {citizen.Id} references missing settlement {citizen.SettlementId}.";
            }
        }

        foreach (var army in _armies.Values.OrderBy(army => army.Id.Value))
        {
            if (!_settlements.ContainsKey(army.SourceSettlementId))
            {
                yield return $"Army {army.Id} references missing source settlement {army.SourceSettlementId}.";
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

    private bool OwnerExists(EntityId ownerId)
    {
        return ownerId.Kind switch
        {
            EntityKind.Citizen => _citizens.ContainsKey(ownerId),
            EntityKind.Settlement => _settlements.ContainsKey(ownerId),
            EntityKind.Army => _armies.ContainsKey(ownerId),
            EntityKind.IntelReport => _intelReports.ContainsKey(ownerId),
            EntityKind.RaidOpportunity => _raidOpportunities.ContainsKey(ownerId),
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
