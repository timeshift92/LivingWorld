namespace LivingWorld.Core;

public readonly record struct PhysicalSettlementFact(
    string StableKey,
    string Name,
    string FactionId,
    int Tile);

public sealed record SettlementFactionChange(EntityId SettlementId, string NewFactionId);

public sealed record SettlementReconciliationPlan(
    IReadOnlyList<PhysicalSettlementFact> Imports,
    IReadOnlyList<EntityId> Destructions,
    IReadOnlyList<SettlementFactionChange> FactionChanges);

/// <summary>
/// Pure diff between the physical RimWorld NPC settlements (facts gathered by the RimWorld
/// layer) and the active ledger settlements. Matching is by tile — the only stable link,
/// since <see cref="WorldSettlement"/> has no tile field and encodes it in its slug.
/// No mutation: the RimWorld layer applies the returned plan via the lifecycle services.
/// </summary>
public static class SettlementReconciliationService
{
    public static SettlementReconciliationPlan ComputePlan(
        IReadOnlyList<PhysicalSettlementFact> physical,
        WorldState state,
        bool includeDestructions)
    {
        if (physical == null)
        {
            throw new ArgumentNullException(nameof(physical));
        }

        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var activeByTile = new Dictionary<int, WorldSettlement>();
        foreach (var settlement in state.Settlements.Where(s => s.IsActive))
        {
            var tile = SettlementSlug.ParseTile(settlement.Slug);
            if (tile >= 0 && !activeByTile.ContainsKey(tile))
            {
                activeByTile[tile] = settlement;
            }
        }

        var physicalTiles = new HashSet<int>(physical.Where(f => f.Tile >= 0).Select(f => f.Tile));

        var imports = new List<PhysicalSettlementFact>();
        var factionChanges = new List<SettlementFactionChange>();
        foreach (var fact in physical.Where(f => f.Tile >= 0).OrderBy(f => f.Tile))
        {
            if (!activeByTile.TryGetValue(fact.Tile, out var ledger))
            {
                imports.Add(fact);
            }
            else if (!string.Equals(ledger.FactionId, fact.FactionId, StringComparison.Ordinal))
            {
                factionChanges.Add(new SettlementFactionChange(ledger.Id, fact.FactionId));
            }
        }

        var destructions = new List<EntityId>();
        if (includeDestructions)
        {
            foreach (var pair in activeByTile.OrderBy(p => p.Key))
            {
                if (!physicalTiles.Contains(pair.Key))
                {
                    destructions.Add(pair.Value.Id);
                }
            }
        }

        return new SettlementReconciliationPlan(imports, destructions, factionChanges);
    }
}
