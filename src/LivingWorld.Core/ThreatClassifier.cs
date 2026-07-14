namespace LivingWorld.Core;

/// <summary>How serious the current attack is, deciding the proportionate response.</summary>
public enum ThreatTier
{
    None,
    Nuisance,
    Raid,
    Serious,
}

/// <summary>
/// Plain-data snapshot of the current hostiles on the map. Built from live RimWorld pawns by the map
/// component, but itself free of RimWorld types so tier classification is unit-testable without the game.
/// </summary>
public readonly struct ThreatSignals
{
    /// <summary>Any real hostile is present.</summary>
    public bool AnyHostile { get; init; }

    /// <summary>Every hostile is a wild animal (manhunter pack).</summary>
    public bool OnlyAnimals { get; init; }

    /// <summary>At least one hostile is a large/dangerous animal (big body size — elephant, thrumbo, rhino,
    /// bear...). A single revenge-seeking megafauna is a real fight, not a squirrel-tier nuisance.</summary>
    public bool AnyDangerousAnimal { get; init; }

    /// <summary>A mechanoid is present.</summary>
    public bool AnyMechanoid { get; init; }

    /// <summary>An Anomaly entity / mutant (shambler etc.) is present.</summary>
    public bool AnyEntity { get; init; }

    /// <summary>An insectoid is present.</summary>
    public bool AnyInsect { get; init; }

    /// <summary>A wall-breaching sapper is present.</summary>
    public bool AnySapper { get; init; }

    /// <summary>The nearest hostile is already close to the colony.</summary>
    public bool EnemyAtBase { get; init; }

    /// <summary>Number of live hostiles.</summary>
    public int HostileCount { get; init; }

    /// <summary>The raid is large (count/points over the configured threshold).</summary>
    public bool BigRaid { get; init; }
}

/// <summary>
/// Pure, deterministic threat-tier classification. Proportionate: a lone squirrel is a nuisance the fighters
/// shrug off, a big pack or a serious enemy type puts the colony on a war footing. No RimWorld types.
/// </summary>
public static class ThreatClassifier
{
    public static ThreatTier Classify(in ThreatSignals s)
    {
        if (!s.AnyHostile)
        {
            return ThreatTier.None;
        }

        // Serious enemy types, a wall breach, an enemy already at the base, or sheer size — hold the line.
        if (s.AnyEntity || s.AnyMechanoid || s.AnyInsect || s.AnySapper || s.EnemyAtBase || s.BigRaid)
        {
            return ThreatTier.Serious;
        }

        // All-animal threat: a big dangerous beast (elephant/thrumbo/bear) is a proper Raid-tier fight; a
        // small pack (squirrels, rats) is a nuisance the fighters shrug off while the colony keeps working.
        if (s.OnlyAnimals)
        {
            return s.AnyDangerousAnimal ? ThreatTier.Raid : ThreatTier.Nuisance;
        }

        // Otherwise a normal humanlike raid.
        return ThreatTier.Raid;
    }
}
