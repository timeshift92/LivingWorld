using System;
using System.Linq;

namespace LivingWorld.Core;

public sealed record SettlementDevelopmentRequest(
    int Tick,
    string FoodResourceKey,
    int HousingHeadroom,
    int DevelopmentStep,
    int MaxHousing)
{
    public string SilverResourceKey { get; init; } = "Silver";

    public int DevelopmentSilverCost { get; init; }

    public int SpecialistGrowthStep { get; init; }
}

public sealed record SettlementDevelopmentResult(int SettlementsDeveloped)
{
    public int TierUpgrades { get; init; }
}

public enum SettlementTier
{
    Camp,
    Village,
    Town,
    City,
}

/// <summary>
/// Lets prospering settlements build up their infrastructure over time: a settlement that is fed
/// (holds at least a day of food) and outgrowing its housing grows its housing/food-storage
/// capacity a step per day toward its population plus some headroom, up to a hard cap. This is the
/// "bases develop" loop — thriving villages slowly become towns. Pure and deterministic; capacity
/// is infrastructure, not people, so nothing is created from thin air.
/// </summary>
public static class SettlementDevelopmentService
{
    public static SettlementTier GetTier(WorldState state, EntityId settlementId)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var population = state.GetSettlementPopulation(settlementId).Total;
        var housing = state.GetSettlementCapability(settlementId)?.HousingCapacity ?? 0;
        if (population >= 50 && housing >= 70)
        {
            return SettlementTier.City;
        }

        if (population >= 10 && housing >= 30)
        {
            return SettlementTier.Town;
        }

        if (population >= 3 && housing >= 5)
        {
            return SettlementTier.Village;
        }

        return SettlementTier.Camp;
    }

    public static SettlementDevelopmentResult SimulateDay(WorldState state, SettlementDevelopmentRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        state.AdvanceToTick(request.Tick);

        var developed = 0;
        var tierUpgrades = 0;

        foreach (var settlement in state.Settlements
            .OrderBy(candidate => candidate.Id.Value)
            .ToList())
        {
            var result = DevelopSettlement(state, settlement.Id, request);
            developed += result.SettlementsDeveloped;
            tierUpgrades += result.TierUpgrades;
        }

        return new SettlementDevelopmentResult(developed) { TierUpgrades = tierUpgrades };
    }

    public static SettlementDevelopmentResult DevelopSettlement(
        WorldState state,
        EntityId settlementId,
        SettlementDevelopmentRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        state.AdvanceToTick(request.Tick);

        var population = state.GetSettlementPopulation(settlementId).Total;
        if (population <= 0)
        {
            return new SettlementDevelopmentResult(0);
        }

        if (state.GetOwnedResourceQuantity(settlementId, request.FoodResourceKey) < population)
        {
            return new SettlementDevelopmentResult(0);
        }

        var capability = state.GetSettlementCapability(settlementId) ?? EmptyCapability(settlementId);
        var step = Math.Max(1, request.DevelopmentStep);
        var target = Math.Min(Math.Max(0, request.MaxHousing), population + Math.Max(0, request.HousingHeadroom));
        if (capability.HousingCapacity >= target)
        {
            return new SettlementDevelopmentResult(0);
        }

        var cost = Math.Max(0, request.DevelopmentSilverCost);
        if (cost > 0 && state.GetOwnedResourceQuantity(settlementId, request.SilverResourceKey) < cost)
        {
            return new SettlementDevelopmentResult(0);
        }

        var beforeTier = GetTier(state, settlementId);
        var newHousing = Math.Min(target, capability.HousingCapacity + step);
        if (cost > 0)
        {
            state.ConsumeResource(settlementId, request.SilverResourceKey, cost, "settlement development");
        }

        state.RecordSettlementCapability(capability with
        {
            HousingCapacity = newHousing,
            FoodStorageCapacity = Math.Max(capability.FoodStorageCapacity, newHousing),
        });
        GrowSpecialists(state, settlementId, request.SpecialistGrowthStep);
        var afterTier = GetTier(state, settlementId);
        state.RecordEvent(
            WorldEventKind.SettlementDeveloped,
            settlementId,
            $"Settlement {settlementId} developed housing to {newHousing}.");

        return new SettlementDevelopmentResult(1)
        {
            TierUpgrades = afterTier > beforeTier ? 1 : 0
        };
    }

    private static SettlementCapability EmptyCapability(EntityId settlementId)
    {
        return new SettlementCapability(settlementId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    private static void GrowSpecialists(WorldState state, EntityId settlementId, int growthStep)
    {
        var step = Math.Max(0, growthStep);
        if (step == 0)
        {
            return;
        }

        var existing = state.GetSpecialistPool(settlementId)
            ?? new SpecialistPool(settlementId, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        state.RecordSpecialistPool(existing with
        {
            Farmers = existing.Farmers + step,
            Engineers = existing.Engineers + step,
            Soldiers = existing.Soldiers + step
        });
    }
}
