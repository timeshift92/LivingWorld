namespace LivingWorld.Core;

/// <summary>
/// Pure hysteresis for the threat tier so mobilization does not flicker at a boundary (pawns yo-yoing to the
/// stand and back). Escalation is immediate; de-escalation to a lower tier only happens after the raw reading
/// has stayed at-or-below the current tier for <c>required</c> consecutive rechecks. The caller holds the
/// running <c>belowCount</c> and feeds it back in each recheck.
/// </summary>
public static class ThreatDebounce
{
    public static (ThreatTier tier, int belowCount) Step(ThreatTier current, ThreatTier raw, int belowCount, int required)
    {
        // Raw threat is as-bad-or-worse than what we hold -> commit immediately, reset the clear counter.
        if (raw >= current)
        {
            return (raw, 0);
        }

        // Raw is lower than current -> only step down after enough consecutive lower rechecks.
        var next = belowCount + 1;
        if (next >= required)
        {
            return (raw, 0);
        }

        return (current, next);
    }
}
