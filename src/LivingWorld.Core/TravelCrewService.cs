namespace LivingWorld.Core;

internal static class TravelCrewService
{
    public static WorldCitizen? FindAvailableCrew(WorldState state, EntityId sourceSettlementId)
    {
        return state.Citizens
            .Where(citizen =>
                citizen.Status == CitizenStatus.Alive
                && citizen.IsAdult
                && citizen.SettlementId == sourceSettlementId
                && state.GetOwner(citizen.Id) == sourceSettlementId
                && !state.HasActiveMaterializationLease(citizen.Id))
            .OrderBy(citizen => citizen.Id.Value)
            .FirstOrDefault();
    }

    public static bool ReserveCrew(
        WorldState state,
        EntityId sourceSettlementId,
        EntityId travelOwnerId,
        EntityId? crewCitizenId,
        string reason)
    {
        if (!crewCitizenId.HasValue)
        {
            return true;
        }

        var citizen = state.GetCitizen(crewCitizenId.Value);
        if (citizen == null
            || citizen.Status != CitizenStatus.Alive
            || !citizen.IsAdult
            || state.GetOwner(citizen.Id) != sourceSettlementId
            || state.HasActiveMaterializationLease(citizen.Id))
        {
            return false;
        }

        var transfer = state.TransferAsset(citizen.Id, sourceSettlementId, travelOwnerId, reason);
        return transfer.Status == OwnershipTransferStatus.Success;
    }

    public static void ReturnCrew(
        WorldState state,
        EntityId travelOwnerId,
        EntityId returnSettlementId,
        string factionId,
        EntityId? crewCitizenId,
        string reason)
    {
        if (!crewCitizenId.HasValue || state.GetOwner(crewCitizenId.Value) != travelOwnerId)
        {
            return;
        }

        var citizen = state.GetCitizen(crewCitizenId.Value);
        if (citizen == null)
        {
            return;
        }

        var destination = FactionReturnService.Resolve(state, factionId, returnSettlementId);
        if (destination == null)
        {
            state.SetOwnerForLedger(citizen.Id, citizen.Id);
            state.ReplaceCitizenForSimulation(citizen with { Status = CitizenStatus.Missing });
            state.RecordEvent(WorldEventKind.RaidPawnMissing, citizen.Id, $"Travel crew {citizen.Id} has no valid faction settlement: {reason}.");
            return;
        }

        var transfer = state.TransferAsset(citizen.Id, travelOwnerId, destination.Id, reason);
        if (transfer.Status != OwnershipTransferStatus.Success)
        {
            throw new InvalidOperationException(transfer.Reason);
        }

        if (citizen.SettlementId != destination.Id)
        {
            state.ReplaceCitizenForSimulation(citizen with { SettlementId = destination.Id });
        }
    }

    public static void MarkCrewMissing(
        WorldState state,
        EntityId travelOwnerId,
        EntityId returnSettlementId,
        string factionId,
        EntityId? crewCitizenId,
        string reason)
    {
        if (!crewCitizenId.HasValue || state.GetOwner(crewCitizenId.Value) != travelOwnerId)
        {
            return;
        }

        var citizen = state.GetCitizen(crewCitizenId.Value);
        if (citizen == null)
        {
            return;
        }

        var destination = FactionReturnService.Resolve(state, factionId, returnSettlementId);
        if (destination == null)
        {
            state.SetOwnerForLedger(citizen.Id, citizen.Id);
            state.ReplaceCitizenForSimulation(citizen with { Status = CitizenStatus.Missing });
        }
        else
        {
            var transfer = state.TransferAsset(citizen.Id, travelOwnerId, destination.Id, reason);
            if (transfer.Status != OwnershipTransferStatus.Success)
            {
                throw new InvalidOperationException(transfer.Reason);
            }

            state.ReplaceCitizenForSimulation(citizen with
            {
                SettlementId = destination.Id,
                Status = CitizenStatus.Missing
            });
        }
        state.RecordEvent(WorldEventKind.RaidPawnMissing, citizen.Id, $"Travel crew {citizen.Id} missing: {reason}.");
    }
}
