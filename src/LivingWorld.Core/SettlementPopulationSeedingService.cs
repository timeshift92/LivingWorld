namespace LivingWorld.Core;

public static class SettlementPopulationSeedingService
{
    public static long StableSettlementSeed(string stableKey)
    {
        if (string.IsNullOrWhiteSpace(stableKey))
        {
            return 1;
        }

        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offset;
        foreach (var ch in stableKey.Trim())
        {
            hash ^= ch;
            hash *= prime;
        }

        return (long)(hash & 0x7FFFFFFFFFFFFFFFUL);
    }

    public static int CalculateAdultCount(
        int worldSeed,
        long settlementStableId,
        int configuredAdults,
        int minAdults,
        int maxAdults)
    {
        var floor = Math.Max(0, minAdults);
        var ceiling = Math.Max(floor, maxAdults);
        var baseline = Math.Max(floor, Math.Min(ceiling, configuredAdults));
        if (baseline == 0 || floor == ceiling)
        {
            return baseline;
        }

        var swing = Math.Max(1, baseline / 5);
        var offset = StableRange(worldSeed, settlementStableId, salt: 17, (swing * 2) + 1) - swing;
        return Math.Max(floor, Math.Min(ceiling, baseline + offset));
    }

    public static int CalculateChildCount(int worldSeed, long settlementStableId, int adultCount)
    {
        if (adultCount <= 1)
        {
            return 0;
        }

        var min = Math.Max(1, adultCount / 8);
        var max = Math.Max(min, adultCount / 3);
        return min + StableRange(worldSeed, settlementStableId, salt: 31, (max - min) + 1);
    }

    public static int CalculateAdultAge(int worldSeed, long settlementStableId, int citizenIndex)
    {
        if (citizenIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(citizenIndex), "Citizen index cannot be negative.");
        }

        return 18 + StableRange(worldSeed, settlementStableId, 101 + citizenIndex, 71);
    }

    public static int CalculateChildAge(int worldSeed, long settlementStableId, int childIndex)
    {
        if (childIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(childIndex), "Child index cannot be negative.");
        }

        return StableRange(worldSeed, settlementStableId, 211 + childIndex, 18);
    }

    private static int StableRange(int worldSeed, long settlementStableId, int salt, int range)
    {
        if (range <= 0)
        {
            return 0;
        }

        var value = unchecked(
            ((ulong)(uint)worldSeed << 32)
            ^ (ulong)settlementStableId
            ^ ((ulong)(uint)salt * 0x9E3779B185EBCA87UL));
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        value ^= value >> 31;
        return (int)(value % (uint)range);
    }
}
