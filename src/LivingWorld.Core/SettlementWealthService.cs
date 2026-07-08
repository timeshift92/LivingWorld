namespace LivingWorld.Core;

public sealed record ResourcePriceBook(string SilverResourceKey, IReadOnlyDictionary<string, int> UnitPrices)
{
    public static ResourcePriceBook FromSilver(string silverResourceKey, IReadOnlyDictionary<string, int> unitPrices)
    {
        if (string.IsNullOrWhiteSpace(silverResourceKey))
        {
            throw new ArgumentException("Silver resource key cannot be empty.", nameof(silverResourceKey));
        }

        return new ResourcePriceBook(
            silverResourceKey.Trim(),
            unitPrices
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
                .ToDictionary(pair => pair.Key.Trim(), pair => pair.Value, StringComparer.Ordinal));
    }

    public int PriceOf(string resourceKey)
    {
        if (string.Equals(resourceKey, SilverResourceKey, StringComparison.Ordinal))
        {
            return 1;
        }

        return UnitPrices.TryGetValue(resourceKey, out var price) ? price : 0;
    }
}

public sealed record SettlementWealthSnapshot(
    EntityId SettlementId,
    string FactionId,
    int Silver,
    int MaterialWealth,
    int TotalWealth);

public sealed record FactionWealthSnapshot(
    string FactionId,
    int Silver,
    int MaterialWealth,
    int TotalWealth);

public static class SettlementWealthService
{
    public static SettlementWealthSnapshot RefreshSettlement(
        WorldState state,
        EntityId settlementId,
        ResourcePriceBook prices)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var settlement = state.GetSettlement(settlementId)
            ?? throw new InvalidOperationException($"Settlement {settlementId} does not exist.");

        var silver = state.GetOwnedResourceQuantity(settlementId, prices.SilverResourceKey);
        var material = 0;
        foreach (var resource in state.ResourcesForOwner(settlementId))
        {
            if (string.Equals(resource.ResourceKey, prices.SilverResourceKey, StringComparison.Ordinal))
            {
                continue;
            }

            material += resource.Quantity * prices.PriceOf(resource.ResourceKey);
        }

        var snapshot = new SettlementWealthSnapshot(
            settlementId,
            settlement.FactionId,
            silver,
            material,
            silver + material);
        state.RecordSettlementWealth(snapshot);
        return snapshot;
    }

    public static FactionWealthSnapshot RefreshFaction(
        WorldState state,
        string factionId,
        ResourcePriceBook prices)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(factionId))
        {
            throw new ArgumentException("Faction id cannot be empty.", nameof(factionId));
        }

        var settlements = state.Settlements
            .Where(settlement => string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal))
            .OrderBy(settlement => settlement.Id.Value)
            .ToList();

        var silver = 0;
        var material = 0;
        foreach (var settlement in settlements)
        {
            var snapshot = RefreshSettlement(state, settlement.Id, prices);
            silver += snapshot.Silver;
            material += snapshot.MaterialWealth;
        }

        var faction = new FactionWealthSnapshot(factionId, silver, material, silver + material);
        state.RecordFactionWealth(faction);
        return faction;
    }
}
