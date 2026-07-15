namespace LivingWorld.Core;

public enum RaidPopulationAllocationStatus
{
    Success,
    InvalidRequest,
    UnknownFaction,
    NoAvailableCombatants
}

public sealed record RaidPopulationAllocationRequest(
    string FactionId,
    string Name,
    int RequestedCombatants,
    string FoodResourceKey = "PackagedSurvivalMeal",
    int FoodPerCitizen = 1,
    bool TransferFoodToArmy = false,
    bool RequireExactCombatants = false,
    string EquipmentResourceKey = "Steel",
    int EquipmentPerCombatant = 0);

public sealed record RaidPopulationAllocationResult(
    RaidPopulationAllocationStatus Status,
    string Reason,
    WorldArmy? Army,
    WorldSettlement? SourceSettlement,
    int RequestedCombatants,
    int ReservedCombatants,
    int AvailableCombatants)
{
    public static RaidPopulationAllocationResult Failed(
        RaidPopulationAllocationStatus status,
        string reason,
        int requestedCombatants,
        int availableCombatants)
    {
        return new RaidPopulationAllocationResult(
            status,
            reason,
            null,
            null,
            requestedCombatants,
            0,
            availableCombatants);
    }

    public static RaidPopulationAllocationResult Completed(
        WorldArmy army,
        WorldSettlement sourceSettlement,
        int requestedCombatants,
        int reservedCombatants,
        int availableCombatants)
    {
        return new RaidPopulationAllocationResult(
            RaidPopulationAllocationStatus.Success,
            $"Reserved {reservedCombatants} of {requestedCombatants} requested combatants from {sourceSettlement.Id}.",
            army,
            sourceSettlement,
            requestedCombatants,
            reservedCombatants,
            availableCombatants);
    }
}

public static class RaidPopulationAllocator
{
    public static RaidPopulationAllocationResult ReserveForRaid(
        WorldState state,
        RaidPopulationAllocationRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(request.FactionId))
        {
            return RaidPopulationAllocationResult.Failed(
                RaidPopulationAllocationStatus.InvalidRequest,
                "Raid faction cannot be empty.",
                request.RequestedCombatants,
                0);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return RaidPopulationAllocationResult.Failed(
                RaidPopulationAllocationStatus.InvalidRequest,
                "Raid name cannot be empty.",
                request.RequestedCombatants,
                0);
        }

        if (request.RequestedCombatants <= 0)
        {
            return RaidPopulationAllocationResult.Failed(
                RaidPopulationAllocationStatus.InvalidRequest,
                "Raid must request at least one combatant.",
                request.RequestedCombatants,
                0);
        }

        var settlements = state.Settlements
            .Where(settlement => string.Equals(settlement.FactionId, request.FactionId, StringComparison.Ordinal))
            // Never raise an army from a settlement that is no longer active: WorldState.CreateArmy rejects
            // an inactive source and would throw an unhandled exception during the storyteller raid tick.
            .Where(settlement => settlement.IsActive)
            .OrderByDescending(settlement => GetRaidReadyAdults(state, settlement.Id, request))
            .ThenBy(settlement => settlement.Id.Value)
            .ToList();

        if (settlements.Count == 0)
        {
            return RaidPopulationAllocationResult.Failed(
                RaidPopulationAllocationStatus.UnknownFaction,
                $"No Living World settlement exists for faction {request.FactionId}.",
                request.RequestedCombatants,
                0);
        }

        var availableCombatants = settlements.Sum(settlement => GetRaidReadyAdults(state, settlement.Id, request));
        if (availableCombatants <= 0)
        {
            return RaidPopulationAllocationResult.Failed(
                RaidPopulationAllocationStatus.NoAvailableCombatants,
                $"Faction {request.FactionId} has no available adult combatants.",
                request.RequestedCombatants,
                0);
        }

        var sourceSettlement = settlements[0];
        var sourceCapacity = GetRaidReadyAdults(state, sourceSettlement.Id, request);
        if (request.RequireExactCombatants && sourceCapacity < request.RequestedCombatants)
        {
            return RaidPopulationAllocationResult.Failed(
                RaidPopulationAllocationStatus.NoAvailableCombatants,
                $"Settlement {sourceSettlement.Id} can supply only {sourceCapacity} of {request.RequestedCombatants} required combatants.",
                request.RequestedCombatants,
                availableCombatants);
        }

        var combatants = GetAvailableCombatants(state, sourceSettlement.Id)
            .Take(Math.Min(request.RequestedCombatants, sourceCapacity))
            .ToList();

        var army = state.CreateArmy(request.Name, sourceSettlement.FactionId, sourceSettlement.Id);
        foreach (var combatant in combatants)
        {
            state.TransferAsset(combatant.Id, sourceSettlement.Id, army.Id, "vanilla raid launched");
        }

        var supplies = request.TransferFoodToArmy
            ? combatants.Count * Math.Max(0, request.FoodPerCitizen)
            : 0;
        if (supplies > 0)
        {
            var transfer = state.TransferResource(
                sourceSettlement.Id,
                army.Id,
                request.FoodResourceKey,
                supplies,
                "raid travel supplies");
            if (transfer.Status != OwnershipTransferStatus.Success)
            {
                foreach (var combatant in combatants)
                {
                    state.TransferAsset(combatant.Id, army.Id, sourceSettlement.Id, "raid supply reservation rollback");
                }

                return RaidPopulationAllocationResult.Failed(
                    RaidPopulationAllocationStatus.NoAvailableCombatants,
                    transfer.Reason,
                    request.RequestedCombatants,
                    0);
            }
        }

        var equipment = combatants.Count * Math.Max(0, request.EquipmentPerCombatant);
        if (equipment > 0)
        {
            var transfer = state.TransferResource(
                sourceSettlement.Id,
                army.Id,
                request.EquipmentResourceKey,
                equipment,
                "raid equipment reserved");
            if (transfer.Status != OwnershipTransferStatus.Success)
            {
                RaidReconciliationService.ReleaseUndeployedReserves(state, army.Id);
                return RaidPopulationAllocationResult.Failed(
                    RaidPopulationAllocationStatus.NoAvailableCombatants,
                    transfer.Reason,
                    request.RequestedCombatants,
                    0);
            }
        }

        state.RecordEvent(
            WorldEventKind.RaidLaunched,
            army.Id,
            $"Raid {army.Id} launched from {sourceSettlement.Id} with {combatants.Count} combatants.");

        return RaidPopulationAllocationResult.Completed(
            army,
            sourceSettlement,
            request.RequestedCombatants,
            combatants.Count,
            availableCombatants);
    }

    private static IEnumerable<WorldCitizen> GetAvailableCombatants(WorldState state, EntityId settlementId)
    {
        return state.Citizens
            .Where(citizen =>
                citizen.SettlementId == settlementId
                && citizen.Status == CitizenStatus.Alive
                && citizen.IsAdult
                && state.GetOwner(citizen.Id) == settlementId
                && !state.HasActiveMaterializationLease(citizen.Id))
            .OrderBy(citizen => citizen.Id.Value);
    }

    private static int GetRaidReadyAdults(
        WorldState state,
        EntityId settlementId,
        RaidPopulationAllocationRequest request)
    {
        var adults = GetAvailableCombatants(state, settlementId).Count();
        if (adults == 0)
        {
            return 0;
        }

        if (request.FoodPerCitizen > 0)
        {
            var supported = state.GetOwnedResourceQuantity(settlementId, request.FoodResourceKey)
                / request.FoodPerCitizen;
            adults = Math.Min(adults, supported);
        }

        if (request.EquipmentPerCombatant > 0)
        {
            var supported = state.GetOwnedResourceQuantity(settlementId, request.EquipmentResourceKey)
                / request.EquipmentPerCombatant;
            adults = Math.Min(adults, supported);
        }

        return adults;
    }
}
