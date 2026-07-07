using System;
using System.Linq;

namespace LivingWorld.Core;

public sealed record SettlementDevelopmentRequest(
    int Tick,
    string FoodResourceKey,
    int HousingHeadroom,
    int DevelopmentStep,
    int MaxHousing);

public sealed record SettlementDevelopmentResult(int SettlementsDeveloped);

/// <summary>
/// Lets prospering settlements build up their infrastructure over time: a settlement that is fed
/// (holds at least a day of food) and outgrowing its housing grows its housing/food-storage
/// capacity a step per day toward its population plus some headroom, up to a hard cap. This is the
/// "bases develop" loop — thriving villages slowly become towns. Pure and deterministic; capacity
/// is infrastructure, not people, so nothing is created from thin air.
/// </summary>
public static class SettlementDevelopmentService
{
    public static SettlementDevelopmentResult SimulateDay(WorldState state, SettlementDevelopmentRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        state.AdvanceToTick(request.Tick);

        var step = Math.Max(1, request.DevelopmentStep);
        var headroom = Math.Max(0, request.HousingHeadroom);
        var maxHousing = Math.Max(0, request.MaxHousing);
        var developed = 0;

        foreach (var settlement in state.Settlements
            .OrderBy(candidate => candidate.Id.Value)
            .ToList())
        {
            var population = state.GetSettlementPopulation(settlement.Id).Total;
            if (population <= 0)
            {
                continue;
            }

            // Only a fed settlement invests in growth; a starving one has nothing to spare.
            if (state.GetOwnedResourceQuantity(settlement.Id, request.FoodResourceKey) < population)
            {
                continue;
            }

            var capability = state.GetSettlementCapability(settlement.Id) ?? EmptyCapability(settlement.Id);
            var target = Math.Min(maxHousing, population + headroom);
            if (capability.HousingCapacity >= target)
            {
                continue;
            }

            var newHousing = Math.Min(target, capability.HousingCapacity + step);
            var developedCapability = capability with
            {
                HousingCapacity = newHousing,
                FoodStorageCapacity = Math.Max(capability.FoodStorageCapacity, newHousing),
            };
            state.RecordSettlementCapability(developedCapability);
            state.RecordEvent(
                WorldEventKind.SettlementDeveloped,
                settlement.Id,
                $"Settlement {settlement.Id} developed housing to {newHousing}.");
            developed++;
        }

        return new SettlementDevelopmentResult(developed);
    }

    private static SettlementCapability EmptyCapability(EntityId settlementId)
    {
        return new SettlementCapability(settlementId, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
    }
}
