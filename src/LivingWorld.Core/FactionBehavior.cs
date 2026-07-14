using System;
using System.Linq;

namespace LivingWorld.Core;

/// <summary>
/// The world-simulation archetype that shapes how a faction spends its accumulated power:
/// how far it reaches for targets, how it weighs attacking vs growing vs trading, and its
/// growth/combat/movement tendencies. Player, Vassal, Excluded and Undefined never drive the
/// world-war loop.
/// </summary>
public enum FactionBehavior
{
    Undefined,
    Expansionist,
    Cautious,
    Merchant,
    Aggressive,
    Warmonger,
    Random,
    Player,
    Vassal,
    Excluded,
}

/// <summary>
/// The tuning a <see cref="FactionBehavior"/> contributes: multipliers applied to a faction's
/// growth, combat and movement, how many world tiles it reaches for targets, and whether it
/// takes autonomous world-war actions at all.
/// </summary>
public sealed record FactionBehaviorProfile(
    FactionBehavior Behavior,
    float GrowthMultiplier,
    float CombatMultiplier,
    float MovementMultiplier,
    int EngagementRange,
    bool ParticipatesInWorldWar);

/// <summary>
/// Pure lookup from a behavior archetype to its <see cref="FactionBehaviorProfile"/>.
///
/// Passive archetypes (Player/Vassal/Excluded/Undefined) get a neutral profile — all
/// multipliers 1.0, range 0, non-participating — so the world-war loop never fabricates
/// actions or bonuses for a faction the player controls or a mod excludes. Deterministic and
/// side-effect free.
/// </summary>
public static class FactionBehaviorService
{
    public static FactionBehaviorProfile GetProfile(FactionBehavior behavior)
    {
        return behavior switch
        {
            FactionBehavior.Expansionist => Active(behavior, growth: 1.10f, combat: 0.95f, movement: 1.00f, range: 2),
            FactionBehavior.Cautious => Active(behavior, growth: 1.00f, combat: 0.90f, movement: 0.95f, range: 1),
            FactionBehavior.Merchant => Active(behavior, growth: 1.05f, combat: 0.85f, movement: 1.00f, range: 1),
            FactionBehavior.Aggressive => Active(behavior, growth: 0.95f, combat: 1.10f, movement: 1.05f, range: 3),
            FactionBehavior.Warmonger => Active(behavior, growth: 0.90f, combat: 1.20f, movement: 1.10f, range: 4),
            FactionBehavior.Random => Active(behavior, growth: 1.00f, combat: 1.00f, movement: 1.00f, range: 2),
            _ => Passive(behavior),
        };
    }

    private static FactionBehaviorProfile Active(
        FactionBehavior behavior, float growth, float combat, float movement, int range)
    {
        return new FactionBehaviorProfile(behavior, growth, combat, movement, range, ParticipatesInWorldWar: true);
    }

    private static FactionBehaviorProfile Passive(FactionBehavior behavior)
    {
        return new FactionBehaviorProfile(behavior, 1f, 1f, 1f, EngagementRange: 0, ParticipatesInWorldWar: false);
    }
}

/// <summary>
/// Migrates the legacy production bootstrap where every ordinary human faction was assigned
/// <see cref="FactionBehavior.Aggressive"/>. The old distribution made expansion, trade and
/// cautious development unreachable unless a test or another mod assigned those archetypes by
/// hand. Existing non-combat assignments are preserved and one aggressive faction is always kept.
/// </summary>
public static class FactionBehaviorBootstrapService
{
    private static readonly FactionBehavior[] RequiredNonCombatBehaviors =
    {
        FactionBehavior.Expansionist,
        FactionBehavior.Merchant,
        FactionBehavior.Cautious,
        FactionBehavior.Random,
    };

    public static int EnsureProductionActionReachability(WorldState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var factionIds = state.Settlements
            .Where(settlement => settlement.IsActive)
            .Select(settlement => settlement.FactionId)
            .Distinct(StringComparer.Ordinal)
            .Where(factionId => !state.IsPlayerFaction(factionId))
            .Where(factionId => !state.IsFactionCollapsed(factionId))
            .OrderBy(factionId => factionId, StringComparer.Ordinal)
            .ToList();

        var aggressive = factionIds
            .Where(factionId => state.GetFactionBehavior(factionId) == FactionBehavior.Aggressive)
            .ToList();
        if (aggressive.Count <= 1)
        {
            return 0;
        }

        // Keep the first stable faction aggressive. Remaining legacy assignments fill only
        // missing peaceful archetypes, so existing peaceful behavior choices are never replaced.
        var available = aggressive.Skip(1).GetEnumerator();
        var changed = 0;
        foreach (var behavior in RequiredNonCombatBehaviors)
        {
            if (factionIds.Any(factionId => state.GetFactionBehavior(factionId) == behavior))
            {
                continue;
            }

            if (!available.MoveNext())
            {
                break;
            }

            state.AssignFactionBehavior(available.Current, behavior);
            changed++;
        }

        return changed;
    }
}
