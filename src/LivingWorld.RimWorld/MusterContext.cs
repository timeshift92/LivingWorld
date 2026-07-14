using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// The squad-wide muster facts the driver needs this recheck, computed once by
/// <see cref="MobilizationMapComponent"/> and handed to every fighter: where to hold, how close counts as "at
/// the line", whether this tier wants a held line at all, and whether the line has already been released to
/// free-engage. Keeping these on one struct keeps the per-pawn <see cref="MobilizationDriver"/> loop cheap.
/// </summary>
public readonly struct MusterContext
{
    public IntVec3 Anchor { get; init; }

    public float HoldRadius { get; init; }

    public bool WantsMuster { get; init; }

    public bool Released { get; init; }
}
