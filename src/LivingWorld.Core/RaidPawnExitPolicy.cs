namespace LivingWorld.Core;

public enum RaidPawnExitAction
{
    /// <summary>Leave the raid pawn link untouched (death handled elsewhere, or still unresolved).</summary>
    Ignore,

    /// <summary>The pawn left the map under its own power and returned to its source settlement.</summary>
    Return,

    /// <summary>The pawn was taken prisoner.</summary>
    Capture,

    /// <summary>The pawn left the map without a known fate (e.g. downed and despawned): it is lost.</summary>
    Miss
}

/// <summary>
/// Pure decision for what a raid pawn's map exit or despawn means for its Living World link.
/// Kept free of RimWorld types so the logic is unit-testable.
/// </summary>
public static class RaidPawnExitPolicy
{
    public static RaidPawnExitAction Resolve(bool isDead, bool isPrisoner, bool isDowned)
    {
        if (isDead)
        {
            // The kill patch records the casualty; the exit hook must not double-resolve it.
            return RaidPawnExitAction.Ignore;
        }

        if (isPrisoner)
        {
            return RaidPawnExitAction.Capture;
        }

        if (isDowned)
        {
            // A downed pawn that is leaving the map has not made it home and was
            // neither killed nor captured: its fate is unknown, so it is lost.
            return RaidPawnExitAction.Miss;
        }

        return RaidPawnExitAction.Return;
    }
}
