namespace LivingWorld.Core;

public enum MaterializationPurpose
{
    Raid,
    SettlementDefense,
    SettlementVisit,
    TradeCaravan,
    ScoutingParty,
    DiplomaticMission
}

public enum MaterializationLeaseLifecycle
{
    Reserved,
    Materialized,
    Returned,
    Dead,
    Prisoner,
    Missing,
    Released
}

public sealed record MaterializationLease(
    EntityId Id,
    EntityId CitizenId,
    EntityId SourceOwnerId,
    EntityId ReturnOwnerId,
    MaterializationPurpose Purpose,
    string PurposeKey,
    int CreatedTick,
    int ExpiresTick,
    MaterializationLeaseLifecycle Lifecycle,
    int? PawnThingId)
{
    public bool IsActive => Lifecycle is MaterializationLeaseLifecycle.Reserved or MaterializationLeaseLifecycle.Materialized;

    public MaterializationLease BindPawn(int pawnThingId)
    {
        if (pawnThingId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pawnThingId), "Pawn thing id must be positive.");
        }

        return this with
        {
            Lifecycle = MaterializationLeaseLifecycle.Materialized,
            PawnThingId = pawnThingId
        };
    }

    public MaterializationLease Resolve(PawnFateKind fate)
    {
        return this with
        {
            Lifecycle = fate switch
            {
                PawnFateKind.Dead => MaterializationLeaseLifecycle.Dead,
                PawnFateKind.Prisoner => MaterializationLeaseLifecycle.Prisoner,
                PawnFateKind.Returned => MaterializationLeaseLifecycle.Returned,
                PawnFateKind.Missing => MaterializationLeaseLifecycle.Missing,
                _ => throw new ArgumentOutOfRangeException(nameof(fate), fate, "Unknown pawn fate kind.")
            }
        };
    }

    public MaterializationLease Release()
    {
        return this with { Lifecycle = MaterializationLeaseLifecycle.Released };
    }
}
