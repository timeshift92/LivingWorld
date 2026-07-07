namespace LivingWorld.Core;

/// <summary>
/// An unaffiliated newcomer to the world (arrived "from space" seeking a new life).
/// Not yet a member of any faction or settlement: a drifter is a candidate for
/// assimilation, capture/recruitment, or founding a new settlement.
/// </summary>
public sealed record Drifter(
    EntityId Id,
    string Name,
    int Age,
    Sex Sex,
    int ArrivalTick)
{
    public bool IsChild => Age < 18;

    public bool IsElderly => Age >= 65;

    public bool IsAdult => !IsChild && !IsElderly;
}
