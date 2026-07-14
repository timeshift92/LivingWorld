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
        if (!s.IsNonCombatant || s.IsBusyUrgent)
        {
            return ShelterPhase.None;
        }

        if (s.TierWantsShelter)
        {
            return s.InShelterArea ? ShelterPhase.None : ShelterPhase.Flee;
        }

        // No shelter needed: put their area back only if we were the ones who changed it.
        return s.AreaChangedByUs ? ShelterPhase.Restore : ShelterPhase.None;
    }
}
