namespace LivingWorld.Core;

public sealed record SettlementCapability(
    EntityId SettlementId,
    int HousingCapacity,
    int FoodStorageCapacity,
    int MedicineStorageCapacity,
    int PowerCapacity,
    int LaboratoryCapacity,
    int AnimalCapacity,
    int CropCapacity,
    int ResearchCapacity,
    int MechanicalCapacity,
    int PollutionHandling);

public sealed record SpecialistPool(
    EntityId SettlementId,
    int Farmers,
    int Handlers,
    int Doctors,
    int Researchers,
    int Engineers,
    int Geneticists,
    int Mechanitors,
    int Soldiers,
    int Diplomats);

public sealed record SettlementCapabilityStatus(
    EntityId SettlementId,
    int Population,
    int HousingCapacity,
    int FreeHousing,
    int FoodStorageCapacity,
    int MedicineStorageCapacity,
    int PowerCapacity,
    int LaboratoryCapacity,
    int AnimalCapacity,
    int CropCapacity,
    int ResearchCapacity,
    int MechanicalCapacity,
    int PollutionHandling,
    bool HasHousingForPopulation,
    bool CanSupportCropProgram,
    bool CanSupportAnimalProgram,
    bool CanRunBasicLab,
    bool CanRunMechanicalProduction);

public static class SettlementCapabilityService
{
    public static SettlementCapabilityStatus GetStatus(WorldState state, EntityId settlementId)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var population = state.GetSettlementPopulation(settlementId).Total;
        var capability = state.GetSettlementCapability(settlementId) ?? EmptyCapability(settlementId);
        var specialists = state.GetSpecialistPool(settlementId) ?? EmptySpecialists(settlementId);
        var freeHousing = capability.HousingCapacity - population;

        return new SettlementCapabilityStatus(
            settlementId,
            population,
            capability.HousingCapacity,
            freeHousing,
            capability.FoodStorageCapacity,
            capability.MedicineStorageCapacity,
            capability.PowerCapacity,
            capability.LaboratoryCapacity,
            capability.AnimalCapacity,
            capability.CropCapacity,
            capability.ResearchCapacity,
            capability.MechanicalCapacity,
            capability.PollutionHandling,
            freeHousing >= 0,
            capability.CropCapacity > 0 && specialists.Farmers > 0,
            capability.AnimalCapacity > 0 && specialists.Handlers > 0,
            capability.PowerCapacity > 0
                && capability.LaboratoryCapacity > 0
                && capability.ResearchCapacity > 0
                && (specialists.Researchers > 0 || specialists.Geneticists > 0),
            capability.PowerCapacity > 0
                && capability.MechanicalCapacity > 0
                && (specialists.Engineers > 0 || specialists.Mechanitors > 0));
    }

    private static SettlementCapability EmptyCapability(EntityId settlementId)
    {
        return new SettlementCapability(
            settlementId,
            HousingCapacity: 0,
            FoodStorageCapacity: 0,
            MedicineStorageCapacity: 0,
            PowerCapacity: 0,
            LaboratoryCapacity: 0,
            AnimalCapacity: 0,
            CropCapacity: 0,
            ResearchCapacity: 0,
            MechanicalCapacity: 0,
            PollutionHandling: 0);
    }

    private static SpecialistPool EmptySpecialists(EntityId settlementId)
    {
        return new SpecialistPool(
            settlementId,
            Farmers: 0,
            Handlers: 0,
            Doctors: 0,
            Researchers: 0,
            Engineers: 0,
            Geneticists: 0,
            Mechanitors: 0,
            Soldiers: 0,
            Diplomats: 0);
    }
}
