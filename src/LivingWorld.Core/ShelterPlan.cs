namespace LivingWorld.Core;

/// <summary>The one shelter action for a non-combatant this tick.</summary>
public enum ShelterPhase
{
    None,
    Flee,
    Restore,
}

/// <summary>
/// Plain-data snapshot of a non-combatant colonist's shelter-relevant state. Built from a live pawn by the
/// shelter driver, but free of RimWorld types so the decision is unit-testable without the game.
/// </summary>
public readonly struct NonCombatantState
{
    /// <summary>A colonist who is not on the Fighters roster (children/incapable/non-rostered included).</summary>
    public bool IsNonCombatant { get; init; }

    /// <summary>On a life-or-base-saving job (firefighting, tending, rescuing) — do not move them.</summary>
    public bool IsBusyUrgent { get; init; }

    /// <summary>The current threat tier is serious enough to shelter (Raid or worse).</summary>
    public bool TierWantsShelter { get; init; }

    /// <summary>Their allowed area is already the shelter area.</summary>
    public bool InShelterArea { get; init; }

    /// <summary>We changed their allowed area (so we owe them a restore when the threat passes).</summary>
    public bool AreaChangedByUs { get; init; }
}

/// <summary>
/// Pure decision for a non-combatant: flee to the shelter area while a real threat is up, restore their normal
/// area afterwards, and never yank someone off an urgent job. One action per call keeps the driver idempotent.
/// </summary>
public static class ShelterPlan
{
    public static ShelterPhase NextAction(in NonCombatantState s)
    {
        var shouldShelter = s.IsNonCombatant && s.TierWantsShelter;

        // Restore whenever we changed their area but they should no longer be sheltered — threat passed, or
        // they were promoted into the Fighters roster. Safe even mid-job: it only changes the allowed area.
        if (s.AreaChangedByUs && !shouldShelter)
        {
            return ShelterPhase.Restore;
        }

        // Never path a busy-urgent pawn to shelter.
        if (!shouldShelter || s.IsBusyUrgent)
        {
            return ShelterPhase.None;
        }

        return s.InShelterArea ? ShelterPhase.None : ShelterPhase.Flee;
    }
}
