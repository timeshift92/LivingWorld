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
        var diplomacy = 0;
        var ecology = 0;
        var technology = 0;
        var total = 0;

        foreach (var worldEvent in state.Events
            .Where(worldEvent => worldEvent.Tick >= cutoff && worldEvent.Tick <= request.CurrentTick)
            .OrderBy(worldEvent => worldEvent.Tick)
            .ThenBy(worldEvent => worldEvent.Id.Value))
        {
            if (request.SettlementId.HasValue && !TouchesSettlement(state, worldEvent, request.SettlementId.Value))
            {
                continue;
            }

            var counted = false;
            switch (worldEvent.Kind)
            {
                case WorldEventKind.CitizenBorn:
                case WorldEventKind.DrifterAssimilated:
                case WorldEventKind.MigrationCompleted:
                    populationDelta++;
                    counted = true;
                    break;
                case WorldEventKind.CitizenDied:
                case WorldEventKind.RefugeeCreated:
                case WorldEventKind.RaidPawnCaptured:
                case WorldEventKind.RaidPawnMissing:
                    populationDelta--;
                    counted = true;
                    break;
            }

            if (IsEconomy(worldEvent.Kind))
            {
                economy++;
                counted = true;
            }

            if (IsConstruction(worldEvent.Kind))
            {
                construction++;
                counted = true;
            }

            if (IsMilitary(worldEvent.Kind))
            {
                military++;
                counted = true;
            }

            if (IsTrade(worldEvent.Kind))
            {
                trade++;
                counted = true;
            }

            if (IsDiplomacy(worldEvent.Kind))
            {
                diplomacy++;
                counted = true;
            }

            if (IsEcology(worldEvent.Kind))
            {
                ecology++;
                counted = true;
            }

            if (IsTechnology(worldEvent.Kind))
            {
                technology++;
                counted = true;
            }

            if (counted)
            {
                total++;
            }
        }

        return new WorldActivitySummary(
            populationDelta,
            economy,
            construction,
            military,
            trade,
            diplomacy,
            ecology,
            technology,
            total);
    }

    private static bool TouchesSettlement(WorldState state, WorldEvent worldEvent, EntityId settlementId)
    {
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
