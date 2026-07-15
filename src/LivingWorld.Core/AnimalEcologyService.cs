namespace LivingWorld.Core;

public sealed record AnimalEcologyRequest(
    int Tick,
    string FeedResourceKey,
    int FeedPerAnimal);

public sealed record AnimalEcologyResult(
    int Births,
    int Deaths);

public sealed record AnimalMigrationRequest(
    int Tick,
    EntityId SourceOwnerId,
    EntityId TargetOwnerId,
    string AnimalKind,
    int MaxCount);

public sealed record AnimalMigrationResult(int Migrated);

public static class AnimalEcologyService
{
    public static AnimalEcologyResult SimulateDay(WorldState state, AnimalEcologyRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.Tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Animal ecology tick cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(request.FeedResourceKey))
        {
            throw new ArgumentException("Feed resource key cannot be empty.", nameof(request));
        }

        state.AdvanceToTick(request.Tick);
        var births = 0;
        var deaths = 0;

        foreach (var cohort in state.AnimalCohorts
            .OrderBy(cohort => cohort.OwnerId.Kind)
            .ThenBy(cohort => cohort.OwnerId.Value)
            .ThenBy(cohort => cohort.AnimalKind, StringComparer.Ordinal)
            .ThenBy(cohort => cohort.Id.Value)
            .ToList())
        {
            if (cohort.Count <= 0 || !IsActiveSettlementOwner(state, cohort.OwnerId))
            {
                continue;
            }

            var updated = cohort;
            var cohortDeaths = 0;
            if (cohort.Type == AnimalCohortType.Domesticated)
            {
                var need = cohort.Count * Math.Max(0, request.FeedPerAnimal);
                if (need > 0)
                {
                    var consumed = state.ConsumeResource(
                        cohort.OwnerId,
                        request.FeedResourceKey,
                        need,
                        "animal feed");
                    var shortage = need - consumed;
                    if (shortage > 0)
                    {
                        cohortDeaths += Math.Max(1, shortage / Math.Max(1, request.FeedPerAnimal * 2));
                    }
                }
            }

            if (cohort.Count > cohort.CarryingCapacity)
            {
                cohortDeaths += Math.Max(1, (cohort.Count - cohort.CarryingCapacity) / 5);
            }

            if (cohortDeaths > 0)
            {
                cohortDeaths = Math.Min(cohortDeaths, cohort.Count);
                updated = updated with
                {
                    Count = cohort.Count - cohortDeaths,
                    HealthPercent = Math.Max(0, cohort.HealthPercent - 5),
                    LastUpdatedTick = request.Tick
                };
                deaths += cohortDeaths;
                state.RecordAnimalCohortForSimulation(updated);
                state.RecordEvent(
                    WorldEventKind.AnimalCohortDeclined,
                    updated.Id,
                    $"Animal cohort {updated.Id} declined by {cohortDeaths} {updated.AnimalKind}.");
                continue;
            }

            var cohortBirths = CalculateBirths(cohort);
            if (cohortBirths > 0)
            {
                updated = updated with
                {
                    Count = cohort.Count + cohortBirths,
                    LastUpdatedTick = request.Tick
                };
                births += cohortBirths;
                state.RecordAnimalCohortForSimulation(updated);
                state.RecordEvent(
                    WorldEventKind.AnimalCohortGrew,
                    updated.Id,
                    $"Animal cohort {updated.Id} grew by {cohortBirths} {updated.AnimalKind}.");
            }
            else
            {
                state.RecordAnimalCohortForSimulation(updated with { LastUpdatedTick = request.Tick });
            }
        }

        return new AnimalEcologyResult(births, deaths);
    }

    private static bool IsActiveSettlementOwner(WorldState state, EntityId ownerId)
    {
        return ownerId.Kind == EntityKind.Settlement && state.GetSettlement(ownerId)?.IsActive == true;
    }

    public static AnimalMigrationResult MigratePressure(WorldState state, AnimalMigrationRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.Tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Animal migration tick cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(request.AnimalKind))
        {
            throw new ArgumentException("Animal kind cannot be empty.", nameof(request));
        }

        state.AdvanceToTick(request.Tick);
        state.EnsureOwnerExistsForLedger(request.SourceOwnerId);
        state.EnsureOwnerExistsForLedger(request.TargetOwnerId);

        var source = state.GetAnimalCohorts(request.SourceOwnerId)
            .Where(cohort => string.Equals(cohort.AnimalKind, request.AnimalKind.Trim(), StringComparison.Ordinal))
            .OrderByDescending(cohort => cohort.Count - cohort.CarryingCapacity)
            .ThenBy(cohort => cohort.Id.Value)
            .FirstOrDefault();
        if (source == null || source.Count <= source.CarryingCapacity || request.MaxCount <= 0)
        {
            return new AnimalMigrationResult(0);
        }

        var target = state.GetAnimalCohorts(request.TargetOwnerId)
            .Where(cohort => string.Equals(cohort.AnimalKind, source.AnimalKind, StringComparison.Ordinal))
            .OrderBy(cohort => cohort.Id.Value)
            .FirstOrDefault();
        var targetHeadroom = target == null
            ? source.CarryingCapacity
            : Math.Max(0, target.CarryingCapacity - target.Count);
        var migrated = Math.Min(Math.Max(0, request.MaxCount), Math.Min(source.Count - source.CarryingCapacity, targetHeadroom));
        if (migrated <= 0)
        {
            return new AnimalMigrationResult(0);
        }

        state.RecordAnimalCohortForSimulation(source with
        {
            Count = source.Count - migrated,
            LastUpdatedTick = request.Tick
        });

        if (target == null)
        {
            state.CreateAnimalCohort(
                request.TargetOwnerId,
                source.AnimalKind,
                source.Type,
                migrated,
                source.HealthPercent,
                source.FertilityPercent,
                source.CarryingCapacity,
                request.Tick);
        }
        else
        {
            state.RecordAnimalCohortForSimulation(target with
            {
                Count = target.Count + migrated,
                HealthPercent = (target.HealthPercent + source.HealthPercent) / 2,
                FertilityPercent = (target.FertilityPercent + source.FertilityPercent) / 2,
                LastUpdatedTick = request.Tick
            });
        }

        state.RecordEvent(
            WorldEventKind.AnimalCohortMigrated,
            source.Id,
            $"Animal cohort {source.Id} moved {migrated} {source.AnimalKind} from {request.SourceOwnerId} to {request.TargetOwnerId}.");
        return new AnimalMigrationResult(migrated);
    }

    private static int CalculateBirths(WorldAnimalCohort cohort)
    {
        if (cohort.Count >= cohort.CarryingCapacity)
        {
            return 0;
        }

        var raw = cohort.Count
            * Math.Max(0, cohort.HealthPercent)
            * Math.Max(0, cohort.FertilityPercent)
            / 50_000;
        return Math.Min(Math.Max(0, raw), cohort.CarryingCapacity - cohort.Count);
    }
}
