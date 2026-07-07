namespace LivingWorld.Core;

public enum RaidPawnExitAction
{
    /// <summary>Leave the raid pawn link untouched (death handled elsewhere, or still unresolved).</summary>
    Ignore,

    /// <summary>The pawn left the map under its own power and returned to its source settlement.</summary>
    Return,

    /// <summary>The pawn was taken prisoner.</summary>
    Capture
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
            // A downed pawn has not made it home. Leave it active until it dies,
            // is captured, or recovers and leaves under its own power.
            return RaidPawnExitAction.Ignore;
        }

        return RaidPawnExitAction.Return;
    }
}
