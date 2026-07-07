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
    int ArrivalTick,
    int CombatAptitude,
    int OrganizationAptitude)
{
    /// <summary>
    /// Ledger proxy for the leader's traits/skills: the higher of the two aptitudes gates
    /// whether this drifter is capable of founding a new settlement. Real RimWorld pawn
    /// skills/traits map onto these only at materialization time.
    /// </summary>
    public int LeadershipAptitude => Math.Max(CombatAptitude, OrganizationAptitude);

    /// <summary>A combat-minded leader founds a raider band; an organizer founds a settlement.</summary>
    public bool LeadsRaiderBand => CombatAptitude > OrganizationAptitude;

    public bool IsChild => Age < 18;

    public bool IsElderly => Age >= 65;

    public bool IsAdult => !IsChild && !IsElderly;
}
