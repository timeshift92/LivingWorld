namespace LivingWorld.Core;

public enum PawnFateKind
{
    Dead,
    Prisoner,
    Returned,
    Missing
}

public enum PawnFateSyncStatus
{
    Success,
    InvalidRequest,
    UnknownLedgerId,
    AlreadyResolved
}

public sealed record PawnFateSyncRequest(
    EntityId LedgerId,
    PawnFateKind Fate,
    string Reason);

public sealed record PawnFateSyncResult(
    PawnFateSyncStatus Status,
    string Reason,
    EntityId? CitizenId);

public static class LivingWorldPawnSyncService
{
    public static PawnFateSyncResult Apply(WorldState state, PawnFateSyncRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.LedgerId.Kind != EntityKind.Citizen)
        {
            return new PawnFateSyncResult(
                PawnFateSyncStatus.InvalidRequest,
                $"Ledger id {request.LedgerId} is not a citizen.",
                null);
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return new PawnFateSyncResult(
                PawnFateSyncStatus.InvalidRequest,
                "Pawn fate reason cannot be empty.",
                request.LedgerId);
        }

        var link = state.RaidPawnLinks
            .Where(link => link.CitizenId == request.LedgerId)
            .OrderBy(link => link.Status == RaidPawnLinkStatus.Active ? 0 : 1)
            .ThenBy(link => link.PawnThingId)
            .FirstOrDefault();

        if (link == null)
        {
            return new PawnFateSyncResult(
                PawnFateSyncStatus.UnknownLedgerId,
                $"Citizen {request.LedgerId} is not linked to an active materialized pawn.",
                request.LedgerId);
        }

        if (link.Status != RaidPawnLinkStatus.Active)
        {
            return new PawnFateSyncResult(
                PawnFateSyncStatus.AlreadyResolved,
                $"Citizen {request.LedgerId} was already resolved as {link.Status}.",
                request.LedgerId);
        }

        var updated = request.Fate switch
        {
            PawnFateKind.Dead => state.MarkRaidPawnDead(link.PawnThingId, request.Reason),
            PawnFateKind.Prisoner => state.MarkRaidPawnPrisoner(link.PawnThingId, request.Reason),
            PawnFateKind.Returned => state.MarkRaidPawnReturned(link.PawnThingId, request.Reason),
            PawnFateKind.Missing => state.MarkRaidPawnMissing(link.PawnThingId, request.Reason),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Fate, "Unknown pawn fate kind.")
        };

        RaidOutcomeService.TryRecordResolvedRaidOutcome(state, updated.ArmyId);
        return new PawnFateSyncResult(
            PawnFateSyncStatus.Success,
            $"Citizen {request.LedgerId} synced as {request.Fate}.",
            request.LedgerId);
    }
}
