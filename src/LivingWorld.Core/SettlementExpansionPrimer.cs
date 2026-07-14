using System;
using System.Linq;

namespace LivingWorld.Core;

/// <summary>
/// Turns an arrived expedition into a simulation-ready settlement. Population and supplies are
/// moved by <see cref="WorldState"/>; this service creates the production, infrastructure,
/// specialist, technology and ecology records that let the colony participate on the next day.
/// </summary>
internal static class SettlementExpansionPrimer
{
    public static void Prime(
        WorldState state,
        WorldSettlement source,
        WorldSettlement colony,
        WorldMigrationGroup expedition,
        int tick)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var profile = CreateProductionProfile(state, source, colony, expedition);
        state.RecordSettlementProductionProfile(profile);

        var population = state.GetSettlementPopulation(colony.Id).Total;
        var capability = CreateCapability(state, source.Id, colony.Id, population, profile);
        state.RecordSettlementCapability(capability);
        state.RecordSpecialistPool(CreateSpecialists(colony.Id, population, profile, capability));
        InheritTechnology(state, source.Id, colony.Id, tick);
        EnsureMilitaryTechnology(state, colony.Id, profile.TechLevel, tick);
        InheritCropStrains(state, source.Id, colony.Id, tick);
        MoveStarterAnimals(state, source.Id, colony.Id, capability.AnimalCapacity, population, tick);

        SettlementBootstrapPrimer.PrimeSettlement(
            state,
            new SettlementBootstrapPrimerRequest(
                tick,
                colony.Id,
                "PackagedSurvivalMeal",
                "Steel",
                "ComponentIndustrial"));
    }

    private static SettlementProductionProfile CreateProductionProfile(
        WorldState state,
        WorldSettlement source,
        WorldSettlement colony,
        WorldMigrationGroup expedition)
    {
        var sourceProfile = state.GetSettlementProductionProfile(source.Id);
        var profile = sourceProfile == null
            ? SettlementProductionProfile.FromEnvironment(
                colony.Id,
                FallbackEnvironment(expedition.PlannedLocationToken))
            : sourceProfile with
            {
                SettlementId = colony.Id,
                LaborEfficiencyPercent = Math.Min(100, Math.Max(70, sourceProfile.LaborEfficiencyPercent - 5)),
                EconomyScalePercent = 100,
                ComplexityPenaltyPercent = 100,
            };

        return SettlementEconomicCharacterService.Apply(profile, state.WorldSeed);
    }

    private static SettlementProductionEnvironment FallbackEnvironment(string locationToken)
    {
        var climate = SettlementExpansionSiteSelector.StableBucket(locationToken + "|climate", 5);
        var hilliness = SettlementExpansionSiteSelector.StableBucket(locationToken + "|hills", 3) switch
        {
            0 => "Flat",
            1 => "SmallHills",
            _ => "LargeHills",
        };

        return climate switch
        {
            0 => new SettlementProductionEnvironment("TemperateForest", hilliness, "Industrial", 50, 700, 16),
            1 => new SettlementProductionEnvironment("AridShrubland", hilliness, "Industrial", 30, 300, 27),
            2 => new SettlementProductionEnvironment("BorealForest", hilliness, "Industrial", 30, 550, 2),
            3 => new SettlementProductionEnvironment("Desert", hilliness, "Industrial", 15, 120, 34),
            _ => new SettlementProductionEnvironment("Tundra", hilliness, "Industrial", 10, 300, -8),
        };
    }

    private static SettlementCapability CreateCapability(
        WorldState state,
        EntityId sourceId,
        EntityId colonyId,
        int population,
        SettlementProductionProfile profile)
    {
        var source = state.GetSettlementCapability(sourceId);
        var industrial = profile.TechLevel.IndexOf("Industrial", StringComparison.OrdinalIgnoreCase) >= 0
            || profile.TechLevel.IndexOf("Spacer", StringComparison.OrdinalIgnoreCase) >= 0;
        var housing = Math.Max(8, population + 4);
        var cropCapacity = profile.FoodPerAdult > 0 ? Math.Max(4, population) : 0;
        var animalCapacity = profile.FoodPerAdult > 0 ? Math.Max(4, population / 2) : 0;

        return new SettlementCapability(
            colonyId,
            HousingCapacity: Math.Max(housing, Math.Min(population + 12, source?.HousingCapacity ?? 0)),
            FoodStorageCapacity: Math.Max(population * 14, Math.Min(population * 30, source?.FoodStorageCapacity ?? 0)),
            MedicineStorageCapacity: Math.Max(4, Math.Min(Math.Max(4, population), source?.MedicineStorageCapacity ?? 0)),
            PowerCapacity: industrial ? Math.Max(5, Math.Min(20, (source?.PowerCapacity ?? 0) / 3)) : 0,
            LaboratoryCapacity: industrial ? Math.Max(1, Math.Min(4, (source?.LaboratoryCapacity ?? 0) / 3)) : 0,
            AnimalCapacity: Math.Max(animalCapacity, Math.Min(population, source?.AnimalCapacity ?? 0)),
            CropCapacity: Math.Max(cropCapacity, Math.Min(population * 2, source?.CropCapacity ?? 0)),
            ResearchCapacity: industrial ? Math.Max(1, Math.Min(4, (source?.ResearchCapacity ?? 0) / 3)) : 0,
            MechanicalCapacity: industrial ? Math.Max(1, Math.Min(4, (source?.MechanicalCapacity ?? 0) / 3)) : 0,
            PollutionHandling: Math.Max(0, Math.Min(3, (source?.PollutionHandling ?? 0) / 3)));
    }

    private static SpecialistPool CreateSpecialists(
        EntityId settlementId,
        int population,
        SettlementProductionProfile profile,
        SettlementCapability capability)
    {
        var workers = Math.Max(1, population);
        return new SpecialistPool(
            settlementId,
            Farmers: profile.FoodPerAdult > 0 ? Math.Max(1, workers / 4) : 0,
            Handlers: capability.AnimalCapacity > 0 ? Math.Max(1, workers / 8) : 0,
            Doctors: profile.MedicinePerAdult > 0 ? Math.Max(1, workers / 10) : 0,
            Researchers: capability.ResearchCapacity > 0 ? 1 : 0,
            Engineers: capability.MechanicalCapacity > 0 ? Math.Max(1, workers / 10) : 0,
            Geneticists: capability.LaboratoryCapacity > 1 && workers >= 12 ? 1 : 0,
            Mechanitors: 0,
            Soldiers: Math.Max(1, workers / 4),
            Diplomats: workers >= 4 ? 1 : 0);
    }

    private static void InheritTechnology(WorldState state, EntityId sourceId, EntityId colonyId, int tick)
    {
        foreach (var technology in state.SettlementTechnologies
            .Where(candidate => candidate.SettlementId == sourceId)
            .OrderBy(candidate => candidate.Domain))
        {
            state.RecordSettlementTechnology(technology with
            {
                SettlementId = colonyId,
                LastUpdatedTick = tick,
                Source = "settler-expedition",
            });
        }
    }

    private static void InheritCropStrains(WorldState state, EntityId sourceId, EntityId colonyId, int tick)
    {
        foreach (var strain in state.GetCropStrains(sourceId))
        {
            state.RecordCropStrain(strain with
            {
                SettlementId = colonyId,
                LastUpdatedTick = tick,
            });
        }
    }

    private static void EnsureMilitaryTechnology(
        WorldState state,
        EntityId colonyId,
        string techLevel,
        int tick)
    {
        if (state.GetSettlementTechnology(colonyId, TechnologyDomain.Military) != null)
        {
            return;
        }

        var tier = techLevel.IndexOf("Spacer", StringComparison.OrdinalIgnoreCase) >= 0
            || techLevel.IndexOf("Ultra", StringComparison.OrdinalIgnoreCase) >= 0
            || techLevel.IndexOf("Archotech", StringComparison.OrdinalIgnoreCase) >= 0
                ? TechnologyTier.Spacer
                : techLevel.IndexOf("Industrial", StringComparison.OrdinalIgnoreCase) >= 0
                    ? TechnologyTier.Industrial
                    : techLevel.IndexOf("Medieval", StringComparison.OrdinalIgnoreCase) >= 0
                        ? TechnologyTier.Medieval
                        : TechnologyTier.Neolithic;
        state.RecordSettlementTechnology(new SettlementTechnology(
            colonyId,
            TechnologyDomain.Military,
            tier,
            tick,
            "settler-expedition"));
    }

    private static void MoveStarterAnimals(
        WorldState state,
        EntityId sourceId,
        EntityId colonyId,
        int animalCapacity,
        int population,
        int tick)
    {
        if (animalCapacity <= 0)
        {
            return;
        }

        var source = state.GetAnimalCohorts(sourceId)
            .Where(cohort => cohort.Type == AnimalCohortType.Domesticated && cohort.Count >= 4)
            .OrderByDescending(cohort => cohort.Count)
            .ThenBy(cohort => cohort.Id.Value)
            .FirstOrDefault();
        if (source == null)
        {
            return;
        }

        var moved = Math.Min(Math.Max(2, population / 4), Math.Min(animalCapacity, source.Count / 3));
        if (moved <= 0)
        {
            return;
        }

        state.RecordAnimalCohortForSimulation(source with
        {
            Count = source.Count - moved,
            LastUpdatedTick = tick,
        });
        state.CreateAnimalCohort(
            colonyId,
            source.AnimalKind,
            AnimalCohortType.Domesticated,
            moved,
            source.HealthPercent,
            source.FertilityPercent,
            animalCapacity,
            tick);
    }
}
