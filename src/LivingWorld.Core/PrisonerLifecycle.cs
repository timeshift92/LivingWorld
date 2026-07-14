namespace LivingWorld.Core;

public enum PrisonerDisposition
{
    Captive,
    Recruited,
    Enslaved,
    Released,
    Escaped,
    Dead
}

public enum PrisonerLifecycleAction
{
    Capture,
    Recruit,
    Enslave,
    Release,
    Escape,
    Death
}

public enum PrisonerTransitionStatus
{
    Success,
    AlreadyApplied,
    UnknownCitizen,
    InvalidRequest,
    InvalidTransition,
    PawnMismatch
}

public sealed record PrisonerRecord(
    EntityId CitizenId,
    int PawnThingId,
    EntityId SourceOwnerId,
    EntityId ReturnOwnerId,
    string CaptorFactionId,
    int CapturedTick,
    PrisonerDisposition Disposition,
    int LastTransitionTick);

public sealed record PrisonerTransitionRequest(
    EntityId CitizenId,
    int PawnThingId,
    PrisonerLifecycleAction Action,
    string ActorFactionId,
    string Reason);

public sealed record PrisonerTransitionResult(
    PrisonerTransitionStatus Status,
    string Reason,
    PrisonerRecord? Record);

public static class PrisonerLifecycleService
{
    public static PrisonerTransitionResult Apply(WorldState state, PrisonerTransitionRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.CitizenId.Kind != EntityKind.Citizen
            || request.PawnThingId <= 0
            || string.IsNullOrWhiteSpace(request.Reason)
            || (request.Action == PrisonerLifecycleAction.Capture
                && string.IsNullOrWhiteSpace(request.ActorFactionId)))
        {
            return new PrisonerTransitionResult(
                PrisonerTransitionStatus.InvalidRequest,
                "A prisoner transition requires a citizen, pawn, reason and capture faction.",
                null);
        }

        var citizen = state.GetCitizen(request.CitizenId);
        if (citizen == null)
        {
            return new PrisonerTransitionResult(
                PrisonerTransitionStatus.UnknownCitizen,
                $"Citizen {request.CitizenId} does not exist.",
                null);
        }

        var existing = state.GetPrisonerRecord(request.CitizenId);
        if (request.Action == PrisonerLifecycleAction.Capture)
        {
            return Capture(state, citizen, existing, request);
        }

        if (existing == null)
        {
            existing = TryRecoverLegacyCapture(state, citizen, request);
            if (existing == null)
            {
                if (request.Action == PrisonerLifecycleAction.Death
                    && citizen.Status == CitizenStatus.Dead)
                {
                    return new PrisonerTransitionResult(
                        PrisonerTransitionStatus.AlreadyApplied,
                        $"Citizen {citizen.Id} is already dead.",
                        null);
                }

                return new PrisonerTransitionResult(
                    PrisonerTransitionStatus.InvalidTransition,
                    $"Citizen {citizen.Id} has no prisoner lifecycle to resolve.",
                    null);
            }
        }

        if (existing.PawnThingId != request.PawnThingId)
        {
            return new PrisonerTransitionResult(
                PrisonerTransitionStatus.PawnMismatch,
                $"Citizen {citizen.Id} is bound to pawn {existing.PawnThingId}, not {request.PawnThingId}.",
                existing);
        }

        var target = ToDisposition(request.Action);
        if (existing.Disposition == target)
        {
            state.ReconcilePrisonerStateForLedger(existing);
            return new PrisonerTransitionResult(
                PrisonerTransitionStatus.AlreadyApplied,
                $"Citizen {citizen.Id} is already {target}.",
                existing);
        }

        if (existing.Disposition == PrisonerDisposition.Dead
            || (request.Action != PrisonerLifecycleAction.Death
                && existing.Disposition != PrisonerDisposition.Captive))
        {
            return new PrisonerTransitionResult(
                PrisonerTransitionStatus.InvalidTransition,
                $"Citizen {citizen.Id} cannot transition from {existing.Disposition} to {target}.",
                existing);
        }

        var transitioned = state.TransitionPrisonerForLedger(
            existing,
            target,
            request.ActorFactionId,
            request.Reason);
        return new PrisonerTransitionResult(
            PrisonerTransitionStatus.Success,
            $"Citizen {citizen.Id} transitioned from {existing.Disposition} to {target}.",
            transitioned);
    }

    private static PrisonerTransitionResult Capture(
        WorldState state,
        WorldCitizen citizen,
        PrisonerRecord? existing,
        PrisonerTransitionRequest request)
    {
        if (citizen.Status == CitizenStatus.Dead || existing?.Disposition == PrisonerDisposition.Dead)
        {
            return new PrisonerTransitionResult(
                PrisonerTransitionStatus.InvalidTransition,
                $"Dead citizen {citizen.Id} cannot be captured.",
                existing);
        }

        if (existing != null && existing.PawnThingId != request.PawnThingId)
        {
            return new PrisonerTransitionResult(
                PrisonerTransitionStatus.PawnMismatch,
                $"Citizen {citizen.Id} is already bound to pawn {existing.PawnThingId}, not {request.PawnThingId}.",
                existing);
        }

        if (existing is
            {
                Disposition: PrisonerDisposition.Captive
            }
            && existing.PawnThingId == request.PawnThingId
            && string.Equals(existing.CaptorFactionId, request.ActorFactionId, StringComparison.Ordinal))
        {
            state.ReconcilePrisonerStateForLedger(existing);
            return new PrisonerTransitionResult(
                PrisonerTransitionStatus.AlreadyApplied,
                $"Citizen {citizen.Id} is already held by {request.ActorFactionId}.",
                existing);
        }

        var currentOwnerId = state.GetOwner(citizen.Id);
        var sourceOwnerId = currentOwnerId == citizen.Id
            && existing != null
            && state.OwnerExistsForLedger(existing.SourceOwnerId)
                ? existing.SourceOwnerId
                : currentOwnerId.HasValue && state.OwnerExistsForLedger(currentOwnerId.Value)
                    ? currentOwnerId.Value
                    : existing != null && state.OwnerExistsForLedger(existing.SourceOwnerId)
                        ? existing.SourceOwnerId
                        : citizen.SettlementId;
        var returnOwnerId = ResolveReturnOwner(state, citizen, request.PawnThingId, existing);

        // Finish the materialization/raid path when it is still active. Legacy captures and
        // recaptures can already have a terminal link; the custody record remains authoritative.
        LivingWorldPawnSyncService.Apply(
            state,
            new PawnFateSyncRequest(citizen.Id, PawnFateKind.Prisoner, request.Reason));

        var captured = state.CapturePrisonerForLedger(
            citizen.Id,
            request.PawnThingId,
            sourceOwnerId,
            returnOwnerId,
            request.ActorFactionId,
            request.Reason);
        return new PrisonerTransitionResult(
            PrisonerTransitionStatus.Success,
            $"Citizen {citizen.Id} captured by {request.ActorFactionId}.",
            captured);
    }

    private static PrisonerRecord? TryRecoverLegacyCapture(
        WorldState state,
        WorldCitizen citizen,
        PrisonerTransitionRequest request)
    {
        if (citizen.Status != CitizenStatus.Prisoner)
        {
            return null;
        }

        var sourceOwnerId = state.GetOwner(citizen.Id) ?? citizen.SettlementId;
        var returnOwnerId = ResolveReturnOwner(state, citizen, request.PawnThingId, null);
        return state.CapturePrisonerForLedger(
            citizen.Id,
            request.PawnThingId,
            sourceOwnerId,
            returnOwnerId,
            string.IsNullOrWhiteSpace(request.ActorFactionId) ? "Unknown" : request.ActorFactionId,
            "recovered legacy prisoner lifecycle");
    }

    private static EntityId ResolveReturnOwner(
        WorldState state,
        WorldCitizen citizen,
        int pawnThingId,
        PrisonerRecord? existing)
    {
        var lease = state.MaterializationLeases
            .Where(candidate => candidate.CitizenId == citizen.Id)
            .OrderBy(candidate => candidate.PawnThingId == pawnThingId ? 0 : 1)
            .ThenBy(candidate => candidate.IsActive ? 0 : 1)
            .ThenByDescending(candidate => candidate.Id.Value)
            .FirstOrDefault();
        if (lease != null && state.OwnerExistsForLedger(lease.ReturnOwnerId))
        {
            return lease.ReturnOwnerId;
        }

        var link = state.RaidPawnLinks
            .Where(candidate => candidate.CitizenId == citizen.Id)
            .OrderBy(candidate => candidate.PawnThingId == pawnThingId ? 0 : 1)
            .ThenByDescending(candidate => candidate.PawnThingId)
            .FirstOrDefault();
        var army = link == null ? null : state.GetArmy(link.ArmyId);
        if (army != null && state.OwnerExistsForLedger(army.SourceSettlementId))
        {
            return army.SourceSettlementId;
        }

        if (existing != null && state.OwnerExistsForLedger(existing.ReturnOwnerId))
        {
            return existing.ReturnOwnerId;
        }

        return state.OwnerExistsForLedger(citizen.SettlementId)
            ? citizen.SettlementId
            : citizen.Id;
    }

    private static PrisonerDisposition ToDisposition(PrisonerLifecycleAction action)
    {
        return action switch
        {
            PrisonerLifecycleAction.Recruit => PrisonerDisposition.Recruited,
            PrisonerLifecycleAction.Enslave => PrisonerDisposition.Enslaved,
            PrisonerLifecycleAction.Release => PrisonerDisposition.Released,
            PrisonerLifecycleAction.Escape => PrisonerDisposition.Escaped,
            PrisonerLifecycleAction.Death => PrisonerDisposition.Dead,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Capture is handled separately.")
        };
    }
}
