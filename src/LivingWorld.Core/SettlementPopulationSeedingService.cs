namespace LivingWorld.Core;

public static class SettlementPopulationSeedingService
{
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

    public static int CalculateAdultAge(int worldSeed, long settlementStableId, int citizenIndex)
    {
        if (citizenIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(citizenIndex), "Citizen index cannot be negative.");
        }

        return 18 + StableRange(worldSeed, settlementStableId, 101 + citizenIndex, 71);
    }

    private static int StableRange(int worldSeed, long settlementStableId, int salt, int range)
    {
        if (range <= 0)
        {
            return 0;
        }

        var value = unchecked(
            worldSeed
            ^ ((int)settlementStableId * 397)
            ^ (salt * 1_103_515_245));
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        return Math.Abs(value == int.MinValue ? 0 : value) % range;
    }
}
