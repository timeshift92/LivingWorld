namespace LivingWorld.Core;

/// <summary>
/// Gives each settlement a deterministic economic "character" derived from the world seed and its id,
/// so the world is not economically flat. Two settlements in the same biome no longer read as identical:
/// some specialize (archetype) and some are simply richer or poorer (economy scale). This is what makes
/// the wealth-driven systems — raid strength, trader bonus silver, "wealthy faction" warnings — actually
/// vary and feel alive instead of every faction sitting at the world average with a 1.0 multiplier.
///
/// Everything here is a pure function of (worldSeed, settlementId): deterministic, side-effect free, and
/// stable across saves, so the same settlement always has the same character.
/// </summary>
public static class SettlementEconomicCharacterService
{
    // Economy scale spread. Wide enough that rich and poor factions read very differently both at the
    // start (endowment) and over time (production diverges by the same factor).
    public const int MinEconomyScalePercent = 50;
    public const int MaxEconomyScalePercent = 180;

    // Baseline silver reserve a fully "average" settlement is seeded with, per adult. Prosperous
    // settlements start with a real silver reserve, poor ones with little — an immediate, day-one wealth
    // spread rather than waiting for production to diverge. Silver carries no starvation risk (unlike
    // scaling food), so it is the safe lever for the starting spread.
    public const int BaseSilverPerAdult = 30;

    private const int ArchetypeSalt = 101;
    private const int ScaleSalt = 211;

    public static ProductionArchetype ArchetypeFor(int worldSeed, long settlementId)
    {
        var roll = Roll(worldSeed, settlementId, ArchetypeSalt) % 100u;

        // Weighted: mostly Balanced, with specialists sprinkled in so the mix of tracked resources — and
        // therefore material wealth — differs between settlements.
        if (roll < 40u)
        {
            return ProductionArchetype.Balanced;
        }

        if (roll < 58u)
        {
            return ProductionArchetype.Farmer;
        }

        if (roll < 76u)
        {
            return ProductionArchetype.Miner;
        }

        if (roll < 88u)
        {
            return ProductionArchetype.Medical;
        }

        return ProductionArchetype.Warrior;
    }

    public static int EconomyScaleFor(int worldSeed, long settlementId)
    {
        var span = (uint)(MaxEconomyScalePercent - MinEconomyScalePercent);
        var roll = Roll(worldSeed, settlementId, ScaleSalt) % (span + 1u);
        return MinEconomyScalePercent + (int)roll;
    }

    /// <summary>
    /// Applies the rolled character to a freshly created profile. For a given (seed, id) the result is
    /// always the same. Preserves an economy scale that some other system has already raised above the
    /// untouched default of 100 (e.g. crop-strain tech), so upgrading a profile never lowers it.
    /// </summary>
    public static SettlementProductionProfile Apply(SettlementProductionProfile profile, int worldSeed)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        var rolledScale = EconomyScaleFor(worldSeed, profile.SettlementId.Value);
        var scale = profile.EconomyScalePercent == 100
            ? rolledScale
            : Math.Max(rolledScale, profile.EconomyScalePercent);

        return profile with
        {
            Archetype = ArchetypeFor(worldSeed, profile.SettlementId.Value),
            EconomyScalePercent = scale,
        };
    }

    /// <summary>
    /// Scales a baseline seeding endowment (e.g. steel, silver) by the settlement's economic character.
    /// A prosperous settlement starts richer, a poor one leaner. Returns the baseline unchanged for
    /// non-positive inputs.
    /// </summary>
    public static int ScaleEndowment(int baseline, int worldSeed, long settlementId)
    {
        if (baseline <= 0)
        {
            return baseline;
        }

        var scale = EconomyScaleFor(worldSeed, settlementId);
        return (int)((long)baseline * scale / 100L);
    }

    /// <summary>
    /// The starting silver reserve for a settlement of the given adult count, already scaled by its
    /// economic character. Zero for empty settlements.
    /// </summary>
    public static int SilverEndowment(int adults, int worldSeed, long settlementId)
    {
        if (adults <= 0)
        {
            return 0;
        }

        return ScaleEndowment(adults * BaseSilverPerAdult, worldSeed, settlementId);
    }

    // SplitMix-style avalanche hash so nearby settlement ids and seeds produce well-spread, uncorrelated
    // rolls. Deterministic and allocation-free.
    private static uint Roll(int worldSeed, long settlementId, int salt)
    {
        unchecked
        {
            var h = (uint)worldSeed;
            h = (h ^ (uint)(settlementId * 2654435761L)) * 2246822519u;
            h = (h ^ (uint)salt) * 3266489917u;
            h ^= h >> 15;
            h *= 668265263u;
            h ^= h >> 13;
            return h;
        }
    }
}
