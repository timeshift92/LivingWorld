namespace LivingWorld.Core;

public sealed record WorldSettlement(
    EntityId Id,
    string Slug,
    string Name,
    string FactionId);

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
