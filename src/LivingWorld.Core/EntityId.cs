namespace LivingWorld.Core;

public enum EntityKind
{
    Citizen,
    Settlement,
    Animal,
    Army,
    MigrationGroup,
    Event,
    IntelReport,
    RaidOpportunity,
    RaidIntelFact,
    RaidPreparation,
    MaterializationLease,
    Drifter,
    Caravan,
    Mission,
    SettlementFacility,
    SettlementProject,
    Ruin,
    Conflict
}

public readonly record struct EntityId(EntityKind Kind, long Value)
{
    public static EntityId Create(EntityKind kind, long value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Entity ID value must be positive.");
        }

        return new EntityId(kind, value);
    }

    public override string ToString()
    {
        return $"{Kind}:{Value}";
    }
}
