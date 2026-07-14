namespace LivingWorld.Core;

/// <summary>
/// Ledger settlement slugs equal the world-object scanner StableKey:
/// "worldobject:{defName}:{tile}:{factionId}". The physical RimWorld tile is the only
/// stable link between a ledger <see cref="WorldSettlement"/> and its world object.
/// </summary>
public static class SettlementSlug
{
    public static int ParseTile(string? slug)
    {
        if (string.IsNullOrEmpty(slug))
        {
            return -1;
        }

        var parts = slug!.Split(':');
        return parts.Length >= 3 && int.TryParse(parts[2], out var tile) ? tile : -1;
    }
}
