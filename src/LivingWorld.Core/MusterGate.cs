using System;

namespace LivingWorld.Core;

/// <summary>Plain-data snapshot of the whole fighter squad's gather progress. No RimWorld types.</summary>
public readonly struct MusterSignals
{
    /// <summary>Total fighters expected on the line (reachable combatants).</summary>
    public int FightersTotal { get; init; }

    /// <summary>How many of them have reached the anchor.</summary>
    public int FightersAtAnchor { get; init; }

    /// <summary>The enemy is already inside the perimeter — holding is moot, engage now.</summary>
    public bool LineBreached { get; init; }
}

/// <summary>
/// Pure, deterministic anti-trickle release decision: keep the squad holding the muster line until enough of
/// them have actually gathered (so they do not dribble forward one at a time and get picked off), then release
/// the whole line to engage together. A breach releases immediately. Mirrors the ThreatClassifier/ThreatDebounce
/// split — the game-side glue owns the one-way per-alert latch and the timeout safety valve.
/// </summary>
public static class MusterGate
{
    public static bool WantsRelease(in MusterSignals s, double readyFraction)
    {
        // A breach makes the line pointless — everyone engages now.
        if (s.LineBreached)
        {
            return true;
        }

        // No one to wait for (or a nonsensical count) — do not block.
        if (s.FightersTotal <= 0)
        {
            return true;
        }

        var needed = (int)Math.Ceiling(s.FightersTotal * readyFraction);
        return s.FightersAtAnchor >= needed;
    }
}
