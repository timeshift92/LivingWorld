namespace LivingWorld.Core;

public static class SettlementLifecycleService
{
    public static SettlementDestructionResult DestroySettlement(
        WorldState state,
        EntityId settlementId,
        int tick,
        string reason)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        ThrowIfNullOrWhiteSpace(reason, nameof(reason));
        state.AdvanceToTick(Math.Max(0, tick));

        var settlement = state.GetSettlement(settlementId)
            ?? throw new InvalidOperationException($"Settlement {settlementId} does not exist.");
        if (settlement.Status == SettlementLifecycleStatus.Destroyed)
        {
            var existing = state.Ruins
                .Where(ruin => ruin.OriginalSettlementId == settlementId)
                .OrderByDescending(ruin => ruin.CreatedTick)
                .First();
            return new SettlementDestructionResult(existing, 0);
        }

        var ruin = state.CreateRuinForLedger(
            settlement,
            claimFactionId: settlement.FactionId,
            salvageBand: EstimateSalvageBand(state, settlementId),
            dangerBand: RuinDangerBand.Medium,
            tick: state.CurrentTick);
        MoveAllResources(state, settlementId, ruin.Id, "settlement destroyed");
        foreach (var cohort in state.GetAnimalCohorts(settlementId).ToList())
        {
            var transfer = state.TransferAsset(cohort.Id, settlementId, ruin.Id, "settlement animals stranded in ruins");
            if (transfer.Status != OwnershipTransferStatus.Success)
            {
                throw new InvalidOperationException(transfer.Reason);
            }

            state.RecordAnimalCohortForSimulation(cohort with { OwnerId = ruin.Id, LastUpdatedTick = state.CurrentTick });
        }

        foreach (var project in state.AnimalBreedingProjects
            .Where(project => project.SettlementId == settlementId && project.Status == AnimalBreedingProjectStatus.Active)
            .ToList())
        {
            state.RecordAnimalBreedingProjectForSimulation(project with { Status = AnimalBreedingProjectStatus.Cancelled });
        }

        var refugees = 0;
        foreach (var citizen in state.GetCitizensBySettlement(settlementId)
            .Where(citizen =>
                citizen.Status == CitizenStatus.Alive
                && state.GetOwner(citizen.Id) == settlementId)
            .OrderBy(citizen => citizen.Id.Value)
            .ToList())
        {
            state.MarkCitizenRefugee(citizen.Id, reason);
            refugees++;
        }

        state.SetSettlementLifecycleStatusForLedger(settlementId, SettlementLifecycleStatus.Destroyed);
        state.RecordEvent(
            WorldEventKind.SettlementDestroyed,
            settlementId,
            $"Settlement {settlementId} destroyed: {reason}.");

        return new SettlementDestructionResult(ruin, refugees);
    }

    public static SettlementRelocationResult StartRelocation(
        WorldState state,
        EntityId sourceSettlementId,
        EntityId targetSettlementId,
        int tick,
        int arrivalTick,
        string reason)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        ThrowIfNullOrWhiteSpace(reason, nameof(reason));
        state.AdvanceToTick(Math.Max(0, tick));

        var source = state.GetSettlement(sourceSettlementId)
            ?? throw new InvalidOperationException($"Settlement {sourceSettlementId} does not exist.");
        if (state.GetSettlement(targetSettlementId) == null)
        {
            throw new InvalidOperationException($"Settlement {targetSettlementId} does not exist.");
        }

        var group = state.CreateMigrationGroup(
            sourceSettlementId,
            targetSettlementId,
            source.FactionId,
            state.CurrentTick,
            Math.Max(state.CurrentTick, arrivalTick),
            reason);
        var movedCitizens = 0;
        foreach (var citizen in state.GetCitizensBySettlement(sourceSettlementId)
            .Where(citizen =>
                citizen.Status == CitizenStatus.Alive
                && state.GetOwner(citizen.Id) == sourceSettlementId)
            .OrderBy(citizen => citizen.Id.Value)
            .ToList())
        {
            state.ReplaceCitizenForSimulation(citizen with { Status = CitizenStatus.Migrating });
            var transfer = state.TransferAsset(citizen.Id, sourceSettlementId, group.Id, "settlement relocation");
            if (transfer.Status != OwnershipTransferStatus.Success)
            {
                throw new InvalidOperationException(transfer.Reason);
            }

            movedCitizens++;
        }

        var movedResources = MoveAllResources(state, sourceSettlementId, group.Id, "settlement relocation");
        var ruin = state.CreateRuinForLedger(
            source,
            claimFactionId: source.FactionId,
            salvageBand: RuinSalvageBand.Low,
            dangerBand: RuinDangerBand.Low,
            tick: state.CurrentTick);
        state.SetSettlementLifecycleStatusForLedger(sourceSettlementId, SettlementLifecycleStatus.Abandoned);
        state.RecordEvent(
            WorldEventKind.SettlementAbandoned,
            sourceSettlementId,
            $"Settlement {sourceSettlementId} abandoned: {reason}.");
        state.RecordEvent(
            WorldEventKind.SettlementRelocationStarted,
            group.Id,
            $"Settlement {sourceSettlementId} relocated toward {targetSettlementId}: {reason}.");

        return new SettlementRelocationResult(ruin, group, movedCitizens, movedResources);
    }

    public static RuinReclaimResult ReclaimRuin(
        WorldState state,
        EntityId ruinId,
        string claimantFactionId,
        int tick)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        ThrowIfNullOrWhiteSpace(claimantFactionId, nameof(claimantFactionId));
        state.AdvanceToTick(Math.Max(0, tick));

        var ruin = state.GetRuin(ruinId)
            ?? throw new InvalidOperationException($"Ruin {ruinId} does not exist.");
        if (ruin.Status == RuinStatus.Reclaimed && ruin.ReclaimedSettlementId.HasValue)
        {
            return new RuinReclaimResult(ruin, state.GetSettlement(ruin.ReclaimedSettlementId.Value)!);
        }

        var settlement = state.GetSettlement(ruin.OriginalSettlementId)
            ?? throw new InvalidOperationException($"Original settlement {ruin.OriginalSettlementId} does not exist.");
        var reclaimedSettlement = state.SetSettlementFactionAndStatusForLedger(
            settlement.Id,
            claimantFactionId,
            SettlementLifecycleStatus.Active);
        MoveAllResources(state, ruin.Id, settlement.Id, "ruin reclaimed");
        var reclaimedRuin = state.RecordRuinForLedger(ruin with
        {
            ClaimFactionId = claimantFactionId,
            Status = RuinStatus.Reclaimed,
            ReclaimedSettlementId = settlement.Id,
            StatusTick = state.CurrentTick
        });
        state.RecordEvent(
            WorldEventKind.RuinReclaimed,
            ruin.Id,
            $"Ruin {ruin.Id} reclaimed by {claimantFactionId}.");

        return new RuinReclaimResult(reclaimedRuin, reclaimedSettlement);
    }

    public static RuinPruneResult PruneInactiveRuins(WorldState state, int currentTick, int retentionDays)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (currentTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentTick), "Prune tick cannot be negative.");
        }

        state.AdvanceToTick(currentTick);
        var cutoff = currentTick - Math.Max(0, retentionDays) * 60_000;
        var toPrune = state.Ruins
            .Where(ruin => ruin.Status != RuinStatus.Active && ruin.StatusTick <= cutoff)
            .OrderBy(ruin => ruin.StatusTick)
            .ThenBy(ruin => ruin.Id.Value)
            .Select(ruin => ruin.Id)
            .ToList();

        foreach (var ruinId in toPrune)
        {
            foreach (var resource in state.ResourcesForOwner(ruinId))
            {
                state.SetResourceQuantityForLedger(ruinId, resource.ResourceKey, 0);
            }

            state.RemoveRuinForLedger(ruinId);
            state.RecordEvent(WorldEventKind.RuinPruned, ruinId, $"Ruin {ruinId} pruned after retention.");
        }

        return new RuinPruneResult(toPrune.Count);
    }

    public static WorldSettlement ChangeSettlementFaction(
        WorldState state,
        EntityId settlementId,
        string newFactionId,
        int tick,
        string reason)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        ThrowIfNullOrWhiteSpace(newFactionId, nameof(newFactionId));
        ThrowIfNullOrWhiteSpace(reason, nameof(reason));
        state.AdvanceToTick(Math.Max(0, tick));

        var settlement = state.GetSettlement(settlementId)
            ?? throw new InvalidOperationException($"Settlement {settlementId} does not exist.");
        if (settlement.Status != SettlementLifecycleStatus.Active)
        {
            // Destroyed/abandoned settlements must not be resurrected by a faction change; only
            // ReclaimRuin (an explicit action) may bring them back to Active.
            return settlement;
        }

        if (string.Equals(settlement.FactionId, newFactionId, StringComparison.Ordinal))
        {
            return settlement;
        }

        var updated = state.SetSettlementFactionAndStatusForLedger(
            settlementId,
            newFactionId,
            SettlementLifecycleStatus.Active);
        state.RecordEvent(
            WorldEventKind.SettlementCaptured,
            settlementId,
            $"Settlement {settlementId} captured by {newFactionId}: {reason}.");

        return updated;
    }

    public static WorldSettlement BindPhysicalLocation(
        WorldState state,
        EntityId settlementId,
        string stableKey,
        int tick,
        string reason)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        ThrowIfNullOrWhiteSpace(stableKey, nameof(stableKey));
        ThrowIfNullOrWhiteSpace(reason, nameof(reason));
        if (SettlementSlug.ParseTile(stableKey) < 0)
        {
            throw new ArgumentException("A physical settlement location must contain a valid tile.", nameof(stableKey));
        }

        state.AdvanceToTick(Math.Max(0, tick));
        var before = state.GetSettlement(settlementId)
            ?? throw new InvalidOperationException($"Settlement {settlementId} does not exist.");
        var updated = state.SetSettlementSlugForLedger(settlementId, stableKey);
        if (!string.Equals(before.Slug, updated.Slug, StringComparison.Ordinal))
        {
            state.RecordEvent(
                WorldEventKind.SettlementLocationBound,
                settlementId,
                $"Settlement {settlementId} bound to {stableKey}: {reason}.");
        }

        return updated;
    }

    public static WorldSettlement AbandonSettlement(
        WorldState state,
        EntityId settlementId,
        int tick,
        string reason)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        ThrowIfNullOrWhiteSpace(reason, nameof(reason));
        state.AdvanceToTick(Math.Max(0, tick));

        var settlement = state.GetSettlement(settlementId)
            ?? throw new InvalidOperationException($"Settlement {settlementId} does not exist.");
        if (settlement.Status != SettlementLifecycleStatus.Active)
        {
            return settlement;
        }

        var updated = state.SetSettlementLifecycleStatusForLedger(
            settlementId,
            SettlementLifecycleStatus.Abandoned);
        state.RecordEvent(
            WorldEventKind.SettlementAbandoned,
            settlementId,
            $"Settlement {settlementId} abandoned: {reason}.");

        return updated;
    }

    private static int MoveAllResources(WorldState state, EntityId fromOwnerId, EntityId toOwnerId, string reason)
    {
        var moved = 0;
        foreach (var resource in state.ResourcesForOwner(fromOwnerId).ToList())
        {
            if (resource.Quantity <= 0)
            {
                continue;
            }

            var transfer = state.TransferResource(fromOwnerId, toOwnerId, resource.ResourceKey, resource.Quantity, reason);
            if (transfer.Status != OwnershipTransferStatus.Success)
            {
                throw new InvalidOperationException(transfer.Reason);
            }

            moved++;
        }

        return moved;
    }

    private static RuinSalvageBand EstimateSalvageBand(WorldState state, EntityId settlementId)
    {
        var total = state.ResourcesForOwner(settlementId).Sum(resource => Math.Max(0, resource.Quantity));
        if (total <= 0)
        {
            return RuinSalvageBand.None;
        }

        if (total < 50)
        {
            return RuinSalvageBand.Low;
        }

        return total < 200 ? RuinSalvageBand.Medium : RuinSalvageBand.High;
    }

    private static void ThrowIfNullOrWhiteSpace(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", parameterName);
        }
    }
}
