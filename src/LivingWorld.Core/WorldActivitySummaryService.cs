namespace LivingWorld.Core;

public sealed record WorldActivitySummaryRequest(
    int CurrentTick,
    int LookbackTicks = 60_000,
    EntityId? SettlementId = null);

public sealed record WorldActivitySummary(
    int PopulationDelta,
    int EconomyEvents,
    int ConstructionEvents,
    int MilitaryEvents,
    int TradeEvents,
    int MigrationEvents,
    int DiplomacyEvents,
    int EcologyEvents,
    int TechnologyEvents,
    int TotalEvents);

public static class WorldActivitySummaryService
{
    public static WorldActivitySummary Summarize(WorldState state, WorldActivitySummaryRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var cutoff = Math.Max(0, request.CurrentTick - Math.Max(0, request.LookbackTicks));
        var populationDelta = 0;
        var economy = 0;
        var construction = 0;
        var military = 0;
        var trade = 0;
        var migration = 0;
        var diplomacy = 0;
        var ecology = 0;
        var technology = 0;
        var total = 0;

        var archive = state.EventArchive;
        var entireArchiveRequested = !request.SettlementId.HasValue
            && archive.ArchivedEventCount > 0
            && cutoff <= archive.FirstArchivedTick
            && request.CurrentTick >= archive.LastArchivedTick;
        if (entireArchiveRequested)
        {
            foreach (var entry in archive.LifetimeKindCounts)
            {
                Accumulate(
                    entry.Kind,
                    entry.Count,
                    ref populationDelta,
                    ref economy,
                    ref construction,
                    ref military,
                    ref trade,
                    ref migration,
                    ref diplomacy,
                    ref ecology,
                    ref technology,
                    ref total);
            }
        }
        else
        {
            foreach (var aggregate in archive.RecentAggregates)
            {
                if (aggregate.Tick < cutoff || aggregate.Tick > request.CurrentTick)
                {
                    continue;
                }

                if (request.SettlementId.HasValue)
                {
                    if (aggregate.SettlementId != request.SettlementId)
                    {
                        continue;
                    }
                }
                else if (aggregate.SettlementId.HasValue)
                {
                    continue;
                }

                Accumulate(
                    aggregate.Kind,
                    aggregate.Count,
                    ref populationDelta,
                    ref economy,
                    ref construction,
                    ref military,
                    ref trade,
                    ref migration,
                    ref diplomacy,
                    ref ecology,
                    ref technology,
                    ref total);
            }
        }

        foreach (var worldEvent in state.Events)
        {
            if (worldEvent.Tick < cutoff || worldEvent.Tick > request.CurrentTick)
            {
                continue;
            }

            if (request.SettlementId.HasValue && !TouchesSettlement(state, worldEvent, request.SettlementId.Value))
            {
                continue;
            }

            Accumulate(
                worldEvent.Kind,
                1,
                ref populationDelta,
                ref economy,
                ref construction,
                ref military,
                ref trade,
                ref migration,
                ref diplomacy,
                ref ecology,
                ref technology,
                ref total);
        }

        return new WorldActivitySummary(
            populationDelta,
            economy,
            construction,
            military,
            trade,
            migration,
            diplomacy,
            ecology,
            technology,
            total);
    }

    private static bool TouchesSettlement(WorldState state, WorldEvent worldEvent, EntityId settlementId)
    {
        if (worldEvent.SettlementId.HasValue)
        {
            return worldEvent.SettlementId.Value == settlementId;
        }

        if (!worldEvent.SubjectId.HasValue)
        {
            return false;
        }

        var subjectId = worldEvent.SubjectId.Value;
        if (subjectId == settlementId)
        {
            return true;
        }

        if (subjectId.Kind == EntityKind.Citizen)
        {
            var citizen = state.GetCitizen(subjectId);
            return citizen?.SettlementId == settlementId || state.GetOwner(subjectId) == settlementId;
        }

        if (subjectId.Kind == EntityKind.Settlement)
        {
            return subjectId == settlementId;
        }

        if (subjectId.Kind == EntityKind.SettlementFacility)
        {
            return state.GetSettlementFacility(subjectId)?.SettlementId == settlementId;
        }

        if (subjectId.Kind == EntityKind.SettlementProject)
        {
            return state.GetSettlementProject(subjectId)?.SettlementId == settlementId;
        }

        if (subjectId.Kind == EntityKind.Animal)
        {
            return state.GetAnimalCohort(subjectId)?.OwnerId == settlementId;
        }

        return state.GetOwner(subjectId) == settlementId;
    }

    private static void Accumulate(
        WorldEventKind kind,
        long count,
        ref int populationDelta,
        ref int economy,
        ref int construction,
        ref int military,
        ref int trade,
        ref int migration,
        ref int diplomacy,
        ref int ecology,
        ref int technology,
        ref int total)
    {
        var boundedCount = count > int.MaxValue ? int.MaxValue : (int)Math.Max(0, count);
        var counted = false;
        switch (kind)
        {
            case WorldEventKind.CitizenBorn:
            case WorldEventKind.DrifterAssimilated:
                populationDelta = SaturatingAdd(populationDelta, boundedCount);
                counted = true;
                break;
            case WorldEventKind.CitizenDied:
            case WorldEventKind.RefugeeCreated:
            case WorldEventKind.RaidPawnCaptured:
            case WorldEventKind.RaidPawnMissing:
                populationDelta = SaturatingAdd(populationDelta, -boundedCount);
                counted = true;
                break;
        }

        if (kind is WorldEventKind.MigrationStarted
            or WorldEventKind.MigrationCompleted)
        {
            migration = SaturatingAdd(migration, boundedCount);
            counted = true;
        }

        if (IsEconomy(kind))
        {
            economy = SaturatingAdd(economy, boundedCount);
            counted = true;
        }

        if (IsConstruction(kind))
        {
            construction = SaturatingAdd(construction, boundedCount);
            counted = true;
        }

        if (IsMilitary(kind))
        {
            military = SaturatingAdd(military, boundedCount);
            counted = true;
        }

        if (IsTrade(kind))
        {
            trade = SaturatingAdd(trade, boundedCount);
            counted = true;
        }

        if (IsDiplomacy(kind))
        {
            diplomacy = SaturatingAdd(diplomacy, boundedCount);
            counted = true;
        }

        if (IsEcology(kind))
        {
            ecology = SaturatingAdd(ecology, boundedCount);
            counted = true;
        }

        if (IsTechnology(kind))
        {
            technology = SaturatingAdd(technology, boundedCount);
            counted = true;
        }

        if (counted)
        {
            total = SaturatingAdd(total, boundedCount);
        }
    }

    private static int SaturatingAdd(int current, int delta)
    {
        var result = (long)current + delta;
        return result > int.MaxValue
            ? int.MaxValue
            : result < int.MinValue
                ? int.MinValue
                : (int)result;
    }

    private static bool IsEconomy(WorldEventKind kind)
    {
        return kind is WorldEventKind.ResourceAdded
            or WorldEventKind.ResourceConsumed
            or WorldEventKind.SettlementProductionUpdated;
    }

    private static bool IsConstruction(WorldEventKind kind)
    {
        return kind is WorldEventKind.SettlementProjectStarted
            or WorldEventKind.SettlementProjectCompleted
            or WorldEventKind.SettlementFacilityBuilt
            or WorldEventKind.SettlementFacilityDamaged
            or WorldEventKind.SettlementFacilityRepaired
            or WorldEventKind.SettlementDeveloped;
    }

    private static bool IsMilitary(WorldEventKind kind)
    {
        return kind is WorldEventKind.WarbandLaunched
            or WorldEventKind.RaidLaunched
            or WorldEventKind.SettlementCaptured
            or WorldEventKind.RaidResolved
            or WorldEventKind.ConflictStarted
            or WorldEventKind.ConflictUpdated
            or WorldEventKind.WorldMissionDisrupted;
    }

    private static bool IsTrade(WorldEventKind kind)
    {
        return kind is WorldEventKind.CaravanLaunched
            or WorldEventKind.CaravanArrived
            or WorldEventKind.CaravanDestroyed
            or WorldEventKind.SettlementTradeRecorded;
    }

    private static bool IsDiplomacy(WorldEventKind kind)
    {
        return kind is WorldEventKind.DiplomaticMissionSent
            or WorldEventKind.ConflictTruceStarted
            or WorldEventKind.ConflictClaimRecorded;
    }

    private static bool IsEcology(WorldEventKind kind)
    {
        return kind is WorldEventKind.AnimalCohortCreated
            or WorldEventKind.AnimalCohortGrew
            or WorldEventKind.AnimalCohortDeclined
            or WorldEventKind.AnimalCohortMigrated
            or WorldEventKind.AnimalProductsHarvested
            or WorldEventKind.AnimalHunted
            or WorldEventKind.AnimalBreedingProjectStarted
            or WorldEventKind.AnimalBreedingProjectCompleted
            or WorldEventKind.AnimalCohortIncubated;
    }

    private static bool IsTechnology(WorldEventKind kind)
    {
        return kind is WorldEventKind.CropStrainProjectStarted
            or WorldEventKind.CropStrainProjectCompleted
            or WorldEventKind.TechnologyDiffused;
    }
}
