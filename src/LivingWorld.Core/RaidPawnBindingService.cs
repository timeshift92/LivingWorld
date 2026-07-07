namespace LivingWorld.Core;

public enum RaidPawnBindingStatus
{
    Success,
    UnknownArmy,
    NoAvailableCitizens,
    InvalidRequest
}

public enum RaidPawnCasualtyStatus
{
    Success,
    UnknownPawn,
    AlreadyResolved
}

public enum RaidPawnReturnStatus
{
    Success,
    UnknownPawn,
    AlreadyResolved,
    MissingArmy
}

public enum RaidPawnPrisonerStatus
{
    Success,
    UnknownPawn,
    AlreadyResolved
}

public sealed record RaidPawnBindingResult(
    RaidPawnBindingStatus Status,
    string Reason,
    int BoundCount);

public sealed record RaidPawnCasualtyResult(
    RaidPawnCasualtyStatus Status,
    string Reason,
    EntityId? CitizenId);

public sealed record RaidPawnReturnResult(
    RaidPawnReturnStatus Status,
    string Reason,
    EntityId? CitizenId);

public sealed record RaidPawnPrisonerResult(
    RaidPawnPrisonerStatus Status,
    string Reason,
    EntityId? CitizenId);

public static class RaidPawnBindingService
{
    public static RaidPawnBindingResult BindRaidPawns(
        WorldState state,
        EntityId armyId,
        IEnumerable<int> pawnThingIds)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (pawnThingIds == null)
        {
            throw new ArgumentNullException(nameof(pawnThingIds));
        }

        if (state.GetArmy(armyId) == null)
        {
            return new RaidPawnBindingResult(
                RaidPawnBindingStatus.UnknownArmy,
                $"Army {armyId} does not exist.",
                0);
        }

        var unboundPawnIds = pawnThingIds
            .Where(id => id > 0 && state.GetRaidPawnLink(id) == null)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        if (unboundPawnIds.Count == 0)
        {
            return new RaidPawnBindingResult(
                RaidPawnBindingStatus.InvalidRequest,
                "No unbound pawn IDs were provided.",
                0);
        }

        var availableCitizens = state.Citizens
            .Where(citizen =>
                citizen.Status == CitizenStatus.Alive
                && state.GetOwner(citizen.Id) == armyId
                && !state.RaidPawnLinks.Any(link => link.CitizenId == citizen.Id))
            .OrderBy(citizen => citizen.Id.Value)
            .Take(unboundPawnIds.Count)
            .ToList();

        if (availableCitizens.Count == 0)
        {
            return new RaidPawnBindingResult(
                RaidPawnBindingStatus.NoAvailableCitizens,
                $"Army {armyId} has no unbound Living World citizens.",
                0);
        }

        var boundCount = Math.Min(unboundPawnIds.Count, availableCitizens.Count);
        for (var index = 0; index < boundCount; index++)
        {
            state.LinkRaidPawn(unboundPawnIds[index], availableCitizens[index].Id, armyId);
        }

        return new RaidPawnBindingResult(
            RaidPawnBindingStatus.Success,
            $"Bound {boundCount} raid pawns to Living World citizens.",
            boundCount);
    }

    public static RaidPawnCasualtyResult MarkPawnDead(
        WorldState state,
        int pawnThingId,
        string reason)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Casualty reason cannot be empty.", nameof(reason));
        }

        var link = state.GetRaidPawnLink(pawnThingId);
        if (link == null)
        {
            return new RaidPawnCasualtyResult(
                RaidPawnCasualtyStatus.UnknownPawn,
                $"Pawn {pawnThingId} is not linked to a Living World citizen.",
                null);
        }

        if (link.Status != RaidPawnLinkStatus.Active)
        {
            return new RaidPawnCasualtyResult(
                RaidPawnCasualtyStatus.AlreadyResolved,
                $"Pawn {pawnThingId} was already resolved.",
                link.CitizenId);
        }

        var updated = state.MarkRaidPawnDead(pawnThingId, reason);
        RaidOutcomeService.TryRecordResolvedRaidOutcome(state, updated.ArmyId);
        return new RaidPawnCasualtyResult(
            RaidPawnCasualtyStatus.Success,
            $"Pawn {pawnThingId} marked dead.",
            link.CitizenId);
    }

    public static RaidPawnCasualtyResult MarkPawnMissing(
        WorldState state,
        int pawnThingId,
        string reason)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Missing reason cannot be empty.", nameof(reason));
        }

        var link = state.GetRaidPawnLink(pawnThingId);
        if (link == null)
        {
            return new RaidPawnCasualtyResult(
                RaidPawnCasualtyStatus.UnknownPawn,
                $"Pawn {pawnThingId} is not linked to a Living World citizen.",
                null);
        }

        if (link.Status != RaidPawnLinkStatus.Active)
        {
            return new RaidPawnCasualtyResult(
                RaidPawnCasualtyStatus.AlreadyResolved,
                $"Pawn {pawnThingId} was already resolved.",
                link.CitizenId);
        }

        var updated = state.MarkRaidPawnMissing(pawnThingId, reason);
        RaidOutcomeService.TryRecordResolvedRaidOutcome(state, updated.ArmyId);
        return new RaidPawnCasualtyResult(
            RaidPawnCasualtyStatus.Success,
            $"Pawn {pawnThingId} marked missing.",
            link.CitizenId);
    }

    public static RaidPawnReturnResult MarkPawnReturned(
        WorldState state,
        int pawnThingId,
        string reason)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Return reason cannot be empty.", nameof(reason));
        }

        var link = state.GetRaidPawnLink(pawnThingId);
        if (link == null)
        {
            return new RaidPawnReturnResult(
                RaidPawnReturnStatus.UnknownPawn,
                $"Pawn {pawnThingId} is not linked to a Living World citizen.",
                null);
        }

        if (link.Status != RaidPawnLinkStatus.Active)
        {
            return new RaidPawnReturnResult(
                RaidPawnReturnStatus.AlreadyResolved,
                $"Pawn {pawnThingId} was already resolved.",
                link.CitizenId);
        }

        if (state.GetArmy(link.ArmyId) == null)
        {
            return new RaidPawnReturnResult(
                RaidPawnReturnStatus.MissingArmy,
                $"Army {link.ArmyId} does not exist.",
                link.CitizenId);
        }

        var updated = state.MarkRaidPawnReturned(pawnThingId, reason);
        RaidOutcomeService.TryRecordResolvedRaidOutcome(state, updated.ArmyId);
        return new RaidPawnReturnResult(
            RaidPawnReturnStatus.Success,
            $"Pawn {pawnThingId} returned.",
            link.CitizenId);
    }

    public static RaidPawnPrisonerResult MarkPawnPrisoner(
        WorldState state,
        int pawnThingId,
        string reason)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Prisoner reason cannot be empty.", nameof(reason));
        }

        var link = state.GetRaidPawnLink(pawnThingId);
        if (link == null)
        {
            return new RaidPawnPrisonerResult(
                RaidPawnPrisonerStatus.UnknownPawn,
                $"Pawn {pawnThingId} is not linked to a Living World citizen.",
                null);
        }

        if (link.Status != RaidPawnLinkStatus.Active)
        {
            return new RaidPawnPrisonerResult(
                RaidPawnPrisonerStatus.AlreadyResolved,
                $"Pawn {pawnThingId} was already resolved.",
                link.CitizenId);
        }

        var updated = state.MarkRaidPawnPrisoner(pawnThingId, reason);
        RaidOutcomeService.TryRecordResolvedRaidOutcome(state, updated.ArmyId);
        return new RaidPawnPrisonerResult(
            RaidPawnPrisonerStatus.Success,
            $"Pawn {pawnThingId} captured.",
            link.CitizenId);
    }
}
