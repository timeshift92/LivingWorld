namespace LivingWorld.Core;

public enum Sex
{
    Male,
    Female
}

public enum CitizenStatus
{
    Alive,
    Dead,
    Missing,
    Prisoner,
    Refugee,
    Migrating
}

public sealed record WorldCitizen(
    EntityId Id,
    string Name,
    int Age,
    Sex Sex,
    string Profession,
    EntityId SettlementId,
    CitizenStatus Status)
{
    public bool IsChild => Age < 18;

    public bool IsElderly => Age >= 65;

    public bool IsAdult => !IsChild && !IsElderly;
}
