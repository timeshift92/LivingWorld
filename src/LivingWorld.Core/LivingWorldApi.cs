namespace LivingWorld.Core;

public interface ILivingWorldApi
{
    WorldCitizen? GetCitizen(EntityId id);

    WorldSettlement? GetSettlement(EntityId id);

    SettlementPopulation GetSettlementPopulation(EntityId settlementId);

    EntityId? GetOwner(EntityId assetId);

    int GetOwnedResourceQuantity(EntityId ownerId, string resourceKey);

    WorldFactionRecord? GetFactionRecord(string factionId);
}

public sealed class LivingWorldApi : ILivingWorldApi
{
    private readonly WorldState _state;

    public LivingWorldApi(WorldState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    public WorldCitizen? GetCitizen(EntityId id)
    {
        return _state.GetCitizen(id);
    }

    public WorldSettlement? GetSettlement(EntityId id)
    {
        return _state.GetSettlement(id);
    }

    public SettlementPopulation GetSettlementPopulation(EntityId settlementId)
    {
        return _state.GetSettlementPopulation(settlementId);
    }

    public EntityId? GetOwner(EntityId assetId)
    {
        return _state.GetOwner(assetId);
    }

    public int GetOwnedResourceQuantity(EntityId ownerId, string resourceKey)
    {
        return _state.GetOwnedResourceQuantity(ownerId, resourceKey);
    }

    public WorldFactionRecord? GetFactionRecord(string factionId)
    {
        return _state.GetFactionRecord(factionId);
    }
}
