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
    // Canonical price book for the resources the ledger actually tracks, in silver-equivalent unit
    // values close to their RimWorld market values. Silver itself is the numeraire (price 1).
    public static ResourcePriceBook DefaultPriceBook { get; } = ResourcePriceBook.FromSilver(
        "Silver",
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Steel"] = 2,
            ["PackagedSurvivalMeal"] = 14,
            ["MedicineIndustrial"] = 18,
            ["ComponentIndustrial"] = 24,
        });

    // Recomputes every settlement and faction wealth snapshot from current stock. Deterministic and
    // conservation-safe (reads quantities, writes only snapshots). Call once per simulated day after
    // the day's economy has settled so the numbers reflect end-of-day stock.
    public static void RefreshAll(WorldState state, ResourcePriceBook prices)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var factionTotals = new Dictionary<string, (int Silver, int Material)>(StringComparer.Ordinal);
        foreach (var settlement in state.Settlements.OrderBy(settlement => settlement.Id.Value))
        {
            var snapshot = RefreshSettlement(state, settlement.Id, prices);
            factionTotals.TryGetValue(settlement.FactionId, out var current);
            factionTotals[settlement.FactionId] = (
                current.Silver + snapshot.Silver,
                current.Material + snapshot.MaterialWealth);
        }

        foreach (var pair in factionTotals.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            state.RecordFactionWealth(new FactionWealthSnapshot(
                pair.Key,
                pair.Value.Silver,
                pair.Value.Material,
                pair.Value.Silver + pair.Value.Material));
        }
    }

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
