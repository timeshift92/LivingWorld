namespace LivingWorld.Core;

public enum PeacefulVisitMode
{
    Observer,
    ScheduledMap
}

public enum PeacefulSettlementVisitStatus
{
    Success,
    UnknownSettlement,
    RequiresScheduler
}

public sealed record PeacefulSettlementVisitRequest(
    EntityId SettlementId,
    PeacefulVisitMode Mode,
    int Tick,
    string Reason);

public sealed record PeacefulSettlementVisitResult(
    PeacefulSettlementVisitStatus Status,
    MaterializationPolicyResult Policy,
    string Reason);

public static class PeacefulSettlementVisitService
{
    public static PeacefulSettlementVisitResult PlanVisit(WorldState state, PeacefulSettlementVisitRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var settlement = state.GetSettlement(request.SettlementId);
        if (settlement == null)
        {
            return new PeacefulSettlementVisitResult(
                PeacefulSettlementVisitStatus.UnknownSettlement,
                MaterializationPolicyService.Evaluate(new MaterializationPolicyRequest(
                    MaterializationUseCase.PeacefulVisitObserver,
                    MaterializationVisibility.WorldMapUi,
                    false,
                    false,
                    true)),
                $"Settlement {request.SettlementId} does not exist.");
        }

        if (request.Mode == PeacefulVisitMode.ScheduledMap)
        {
            var policy = MaterializationPolicyService.Evaluate(new MaterializationPolicyRequest(
                MaterializationUseCase.PeacefulNpcSchedule,
                MaterializationVisibility.ActiveMap,
                RequiresPawnIdentity: true,
                RequiresResourceLease: true,
                IsPlayerFacing: true));
            return new PeacefulSettlementVisitResult(
                PeacefulSettlementVisitStatus.RequiresScheduler,
                policy,
                policy.Reason);
        }

        var observerPolicy = MaterializationPolicyService.Evaluate(new MaterializationPolicyRequest(
            MaterializationUseCase.PeacefulVisitObserver,
            MaterializationVisibility.WorldMapUi,
            RequiresPawnIdentity: false,
            RequiresResourceLease: false,
            IsPlayerFacing: true));

        state.AdvanceToTick(request.Tick);
        PlayerKnowledgeService.RecordDirectVisitSettlementInfo(
            state,
            request.SettlementId,
            string.IsNullOrWhiteSpace(request.Reason) ? observerPolicy.Reason : request.Reason);
        return new PeacefulSettlementVisitResult(
            PeacefulSettlementVisitStatus.Success,
            observerPolicy,
            string.IsNullOrWhiteSpace(request.Reason) ? observerPolicy.Reason : request.Reason);
    }
}
