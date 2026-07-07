namespace LivingWorld.Core;

public enum RaidPawnLinkStatus
{
    Active,
    Dead,
    Returned,
    Prisoner,
    Missing
}

public sealed record RaidPawnLink(
    int PawnThingId,
    EntityId CitizenId,
    EntityId ArmyId,
    RaidPawnLinkStatus Status)
{
    public RaidPawnLink MarkDead()
    {
        return this with { Status = RaidPawnLinkStatus.Dead };
    }

    public RaidPawnLink MarkReturned()
    {
        return this with { Status = RaidPawnLinkStatus.Returned };
    }

    public RaidPawnLink MarkPrisoner()
    {
        return this with { Status = RaidPawnLinkStatus.Prisoner };
    }

    public RaidPawnLink MarkMissing()
    {
        return this with { Status = RaidPawnLinkStatus.Missing };
    }
}
