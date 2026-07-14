using System;
using System.Globalization;

namespace LivingWorld.Core;

/// <summary>
/// Converts the persisted expedition identity into a stable location token. The token is derived
/// exclusively from fields already present in the save format, so old saves remain readable and
/// new expeditions keep the same intended site across save/load without adding another codec field.
/// RimWorld maps this token to an actual free world tile.
/// </summary>
public static class SettlementExpansionSiteSelector
{
    public const string LocationTokenPrefix = "lw-site-";

    public static string CreateLocationToken(
        EntityId sourceSettlementId,
        string factionId,
        string plannedSettlementSlug)
    {
        var identity = string.Concat(
            sourceSettlementId.Kind.ToString(), ":", sourceSettlementId.Value.ToString(CultureInfo.InvariantCulture), "|",
            Clean(factionId, "unknown-faction"), "|",
            Clean(plannedSettlementSlug, "unnamed-colony"));
        return LocationTokenPrefix + StableHash(identity).ToString("x8");
    }

    internal static int StableBucket(string token, int bucketCount)
    {
        if (bucketCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bucketCount));
        }

        return (int)(StableHash(Clean(token, "unknown-site")) % (uint)bucketCount);
    }

    private static uint StableHash(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in value)
            {
                hash ^= character;
                hash *= 16777619u;
            }

            hash ^= hash >> 13;
            hash *= 2246822519u;
            hash ^= hash >> 15;
            return hash;
        }
    }

    private static string Clean(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
