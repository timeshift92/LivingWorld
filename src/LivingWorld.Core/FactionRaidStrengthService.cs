using System;
using System.Linq;

namespace LivingWorld.Core;

/// <summary>
/// Ties a faction's ledger economy to the real strength of its raids: a faction wealthier than the
/// world average fields better-equipped raiders (a higher points multiplier), a poorer one fields
/// weaker ones. Self-calibrating against the average faction wealth, so it needs no magic thresholds
/// and adapts as the economy simulation evolves. This is what makes facilities/wealth/development
/// actually matter for the flagship live system — raids — instead of being display-only analytics.
/// </summary>
public static class FactionRaidStrengthService
{
    public const float MinWealthMultiplier = 0.75f;
    public const float MaxWealthMultiplier = 1.5f;

    /// <summary>
    /// Multiplier on a faction's raid points from its wealth relative to the world average. Returns 1.0
    /// when wealth data is unavailable (never penalises a faction just because snapshots are missing).
    /// </summary>
    public static float WealthRaidMultiplier(WorldState state, string factionId)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var wealth = state.GetFactionWealth(factionId)?.TotalWealth ?? 0;
        if (wealth <= 0)
        {
            return 1f;
        }

        var positive = state.FactionWealth.Where(snapshot => snapshot.TotalWealth > 0).ToList();
        if (positive.Count == 0)
        {
            return 1f;
        }

        var average = positive.Average(snapshot => (double)snapshot.TotalWealth);
        if (average <= 0)
        {
            return 1f;
        }

        var ratio = (float)(wealth / average);
        return Math.Max(MinWealthMultiplier, Math.Min(MaxWealthMultiplier, ratio));
    }
}
