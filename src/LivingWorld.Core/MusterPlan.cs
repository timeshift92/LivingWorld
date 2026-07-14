namespace LivingWorld.Core;

/// <summary>What a single fighter should be doing with respect to the muster line this recheck.</summary>
public enum MusterPhase
{
    /// <summary>Not part of muster (not a fighter, not mobilized, or on an urgent job).</summary>
    None,

    /// <summary>March to the muster anchor (vanilla drafted Goto).</summary>
    March,

    /// <summary>Arrived — hold the line here (vanilla Wait_Combat, no CAI duty).</summary>
    Hold,

    /// <summary>Break the line and free-engage (hand to CAI, or vanilla Wait_Combat without CAI).</summary>
    Release,
}

/// <summary>Plain-data snapshot of one fighter's muster-relevant state. No RimWorld types.</summary>
public readonly struct MusterState
{
    /// <summary>This pawn is an active combatant (on the roster and able to fight).</summary>
    public bool IsFighter { get; init; }

    /// <summary>The colony is mobilized (manual toggle or live threat).</summary>
    public bool Mobilized { get; init; }

    /// <summary>The tier wants a held line (Raid/Serious, line not breached) and muster is enabled.</summary>
    public bool WantsMuster { get; init; }

    /// <summary>This pawn is within the hold radius of the muster anchor.</summary>
    public bool AtAnchor { get; init; }

    /// <summary>This pawn can path to the muster anchor.</summary>
    public bool AnchorReachable { get; init; }

    /// <summary>The squad-wide release latch is set (enough gathered, or the line was breached).</summary>
    public bool Released { get; init; }

    /// <summary>On a life-or-base-saving job we must not interrupt (firefight / tend / rescue).</summary>
    public bool IsBusyUrgent { get; init; }
}

/// <summary>
/// Pure, deterministic per-fighter muster decision. A mustering fighter marches to a fortified anchor and holds
/// there with the vanilla drafted think-tree (Wait_Combat — fires from the cell without chasing), instead of
/// scattering to meet the enemy in the open. Once the squad is released (enough gathered, or the line is
/// breached) it free-engages. Deliberately does NOT decide WHERE the anchor is (that is game-side glue) or HOW
/// holding is executed — only which phase applies. No RimWorld types, so it is unit-testable without the game.
/// </summary>
public static class MusterPlan
{
    public static MusterPhase NextAction(in MusterState s)
    {
        // Not a combatant, colony not mobilized, or on an untouchable urgent job — muster leaves them alone.
        if (!s.IsFighter || !s.Mobilized || s.IsBusyUrgent)
        {
            return MusterPhase.None;
        }

        // Free engage: the squad has been released, this tier does not want a held line, or this particular
        // fighter cannot reach the anchor (do not freeze one straggler at the marshalling stage).
        if (s.Released || !s.WantsMuster || !s.AnchorReachable)
        {
            return MusterPhase.Release;
        }

        // Wants a held line and can reach it: march until arrived, then hold.
        return s.AtAnchor ? MusterPhase.Hold : MusterPhase.March;
    }
}
