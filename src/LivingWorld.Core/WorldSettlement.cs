namespace LivingWorld.Core;

public enum SettlementLifecycleStatus
{
    Active,
    Abandoned,
    Destroyed,
}

public sealed record WorldSettlement(
    EntityId Id,
    string Slug,
    string Name,
    string FactionId,
    SettlementLifecycleStatus Status = SettlementLifecycleStatus.Active)
{
    public bool IsActive => Status == SettlementLifecycleStatus.Active;
}

public readonly record struct SettlementPopulation(
    int Total,
    int Children,
    int Adults,
    int Elderly);

public readonly record struct SettlementFoodStatus(
    int Population,
    int DailyNeed,
    int Food,
    int FoodDays,
    bool IsShortage);

public readonly record struct SettlementMigrationStatus(
    int Pressure,
    int Refugees,
    string PrimaryReason,
    bool ShouldCreateRefugees);
