namespace LivingWorld.Core;

public enum MaterializationUseCase
{
    Raid,
    Caravan,
    Visitor,
    PeacefulVisitObserver,
    PeacefulNpcSchedule,
    SettlementAssault,
    AnimalMap
}

public enum MaterializationVisibility
{
    LedgerOnly,
    WorldMapUi,
    ActiveMap
}

public enum MaterializationPolicyDecision
{
    NoMaterialization,
    RecordIntelOnly,
    LeaseAndMaterialize,
    RequiresDedicatedScheduler
}

public sealed record MaterializationPolicyRequest(
    MaterializationUseCase UseCase,
    MaterializationVisibility Visibility,
    bool RequiresPawnIdentity,
    bool RequiresResourceLease,
    bool IsPlayerFacing);

public sealed record MaterializationPolicyResult(
    MaterializationPolicyDecision Decision,
    bool RequiresLedgerSource,
    string Reason);

public static class MaterializationPolicyService
{
    public static MaterializationPolicyResult Evaluate(MaterializationPolicyRequest request)
    {
        if (request.UseCase == MaterializationUseCase.PeacefulNpcSchedule)
        {
            return new MaterializationPolicyResult(
                MaterializationPolicyDecision.RequiresDedicatedScheduler,
                RequiresLedgerSource: true,
                "A peaceful living city needs a scheduler before map pawns are allowed to run free.");
        }

        if (request.UseCase == MaterializationUseCase.PeacefulVisitObserver
            && request.Visibility == MaterializationVisibility.WorldMapUi)
        {
            return new MaterializationPolicyResult(
                MaterializationPolicyDecision.RecordIntelOnly,
                RequiresLedgerSource: false,
                "Observer visits reveal ledger state without spawning a fake city.");
        }

        if (request.Visibility == MaterializationVisibility.ActiveMap
            && (request.RequiresPawnIdentity || request.RequiresResourceLease || request.IsPlayerFacing))
        {
            return new MaterializationPolicyResult(
                MaterializationPolicyDecision.LeaseAndMaterialize,
                RequiresLedgerSource: true,
                "Active-map gameplay must lease real ledger citizens, animals, or resources.");
        }

        return new MaterializationPolicyResult(
            MaterializationPolicyDecision.NoMaterialization,
            RequiresLedgerSource: false,
            "Ledger-only simulation remains lightweight.");
    }
}
