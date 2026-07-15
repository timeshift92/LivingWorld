namespace LivingWorld.Core;

public enum RaidPlanStatus
{
    Success,
    UnknownSettlement,
    InvalidRequest,
    InsufficientCombatants,
    InsufficientResources
}

public sealed record RaidPlanRequest(
    EntityId SourceSettlementId,
    string Name,
    int Combatants,
    int Meals,
    int Steel);

public sealed record RaidPlanResult(
    RaidPlanStatus Status,
    string Reason,
    WorldArmy? Army)
{
    public static RaidPlanResult Failed(RaidPlanStatus status, string reason)
    {
        return new RaidPlanResult(status, reason, null);
    }

    public static RaidPlanResult Completed(WorldArmy army)
    {
        return new RaidPlanResult(RaidPlanStatus.Success, $"Army {army.Id} planned.", army);
    }
}

public static class RaidPlanner
{
    private const string MealResourceKey = "PackagedSurvivalMeal";
    private const string SteelResourceKey = "Steel";

    public static RaidPlanResult PlanRaid(WorldState state, RaidPlanRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.Combatants <= 0)
        {
            return RaidPlanResult.Failed(RaidPlanStatus.InvalidRequest, "Raid must request at least one combatant.");
        }

        if (request.Meals < 0 || request.Steel < 0)
        {
            return RaidPlanResult.Failed(RaidPlanStatus.InvalidRequest, "Raid supplies cannot be negative.");
        }

        var settlement = state.GetSettlement(request.SourceSettlementId);
        if (settlement == null)
        {
            return RaidPlanResult.Failed(
                RaidPlanStatus.UnknownSettlement,
                $"Settlement {request.SourceSettlementId} does not exist.");
        }

        var combatants = state.Citizens
            .Where(citizen =>
                citizen.SettlementId == request.SourceSettlementId
                && citizen.Status == CitizenStatus.Alive
                && citizen.IsAdult
                && state.GetOwner(citizen.Id) == request.SourceSettlementId
                && !state.HasActiveMaterializationLease(citizen.Id))
            .OrderBy(citizen => citizen.Id.Value)
            .Take(request.Combatants)
            .ToList();

        if (combatants.Count < request.Combatants)
        {
            return RaidPlanResult.Failed(
                RaidPlanStatus.InsufficientCombatants,
                $"Settlement {request.SourceSettlementId} has {combatants.Count} available combatants but raid requested {request.Combatants}.");
        }

        if (state.GetOwnedResourceQuantity(request.SourceSettlementId, MealResourceKey) < request.Meals)
        {
            return RaidPlanResult.Failed(
                RaidPlanStatus.InsufficientResources,
                $"Settlement {request.SourceSettlementId} lacks {MealResourceKey} for raid.");
        }

        if (state.GetOwnedResourceQuantity(request.SourceSettlementId, SteelResourceKey) < request.Steel)
        {
            return RaidPlanResult.Failed(
                RaidPlanStatus.InsufficientResources,
                $"Settlement {request.SourceSettlementId} lacks {SteelResourceKey} for raid.");
        }

        var army = state.CreateArmy(request.Name, settlement.FactionId, request.SourceSettlementId);
        foreach (var combatant in combatants)
        {
            state.TransferAsset(combatant.Id, request.SourceSettlementId, army.Id, "raid planned");
        }

        if (request.Meals > 0)
        {
            state.TransferResource(request.SourceSettlementId, army.Id, MealResourceKey, request.Meals, "raid planned");
        }

        if (request.Steel > 0)
        {
            state.TransferResource(request.SourceSettlementId, army.Id, SteelResourceKey, request.Steel, "raid planned");
        }

        return RaidPlanResult.Completed(army);
    }
}
