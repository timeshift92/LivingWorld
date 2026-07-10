namespace LivingWorld.Core;

/// <summary>
/// The single next action mobilization should take for one colonist this tick. The driver derives exactly one
/// of these from the pawn's live state and executes it; deriving is pure so the sequencing (the part that
/// historically broke) is unit-tested in isolation.
/// </summary>
public enum MobPhase
{
    None,
    Wake,
    SetCombatPolicy,
    Equip,
    Engage,
    Draft,
    SteadyCombat,
    ClearCombat,
    SetCivilianPolicy,
    ReturnKit,
    SteadyCivilian,
}

/// <summary>
/// A plain-data snapshot of one colonist's mobilization-relevant state. Built from a live RimWorld pawn by the
/// driver, but itself free of RimWorld types so the decision logic can be tested without the game.
/// </summary>
public readonly struct PawnMobState
{
    /// <summary>Combat-capable colonist we are allowed to act on (violence-able, draftable, skilled enough).</summary>
    public bool IsCandidate { get; init; }

    /// <summary>On a life-or-base-saving job (firefighting, tending, rescuing) — leave them on it.</summary>
    public bool IsBusyUrgent { get; init; }

    /// <summary>Currently asleep.</summary>
    public bool Asleep { get; init; }

    /// <summary>Current apparel policy is our combat policy (armor allowed).</summary>
    public bool PolicyIsCombat { get; init; }

    /// <summary>Current apparel policy is our civilian policy (armor forbidden).</summary>
    public bool PolicyIsCivilian { get; init; }

    /// <summary>Wearing/holding the combat kit (proxy: carrying a primary weapon).</summary>
    public bool InCombatKit { get; init; }

    /// <summary>Owns a personal outfit stand to swap the kit from.</summary>
    public bool HasStand { get; init; }

    /// <summary>The pawn's stand currently holds a weapon to don. An empty/weaponless stand cannot arm them.</summary>
    public bool KitAvailable { get; init; }

    /// <summary>Actually drafted right now (by anyone).</summary>
    public bool Drafted { get; init; }

    /// <summary>Drafted specifically by this system (so stand-down may undraft it).</summary>
    public bool DraftedByUs { get; init; }

    /// <summary>We have given this pawn a CAI combat duty this alert.</summary>
    public bool HasLwDuty { get; init; }

    /// <summary>CAI 5000 is loaded and its custom-duty API resolved.</summary>
    public bool CaiAvailable { get; init; }
}

/// <summary>
/// Pure decision core for colony mobilization. <see cref="NextAction"/> returns the one action to take for a
/// colonist this tick given whether the colony is mobilized and the colonist's state. One action per call keeps
/// the driver idempotent: it re-derives from live state every recheck and converges without stored step
/// counters.
/// </summary>
public static class MobilizationPlan
{
    public static MobPhase NextAction(bool mobilized, in PawnMobState s)
    {
        if (!s.IsCandidate || s.IsBusyUrgent)
        {
            return MobPhase.None;
        }

        return mobilized ? Mobilize(in s) : StandDown(in s);
    }

    private static MobPhase Mobilize(in PawnMobState s)
    {
        if (s.Asleep)
        {
            return MobPhase.Wake;
        }

        if (!s.PolicyIsCombat)
        {
            return MobPhase.SetCombatPolicy;
        }

        // Kit lives only on the stand, so only a stand owner with a stocked stand can arm. Equip until armed.
        if (s.HasStand && s.KitAvailable && !s.InCombatKit)
        {
            return MobPhase.Equip;
        }

        // Ready to fight once armed, or when there is no stand / no kit to arm from — so they don't idle
        // through a raid waiting on a stand that can never equip them.
        var ready = s.InCombatKit || !s.HasStand || !s.KitAvailable;
        if (ready)
        {
            if (s.CaiAvailable && !s.HasLwDuty)
            {
                return MobPhase.Engage;
            }

            if (!s.CaiAvailable && !s.Drafted)
            {
                return MobPhase.Draft;
            }
        }

        return MobPhase.SteadyCombat;
    }

    private static MobPhase StandDown(in PawnMobState s)
    {
        // Release combat control first (our CAI duty / our draft), then re-dress as a civilian.
        if (s.HasLwDuty || s.DraftedByUs)
        {
            return MobPhase.ClearCombat;
        }

        if (!s.PolicyIsCivilian)
        {
            return MobPhase.SetCivilianPolicy;
        }

        if (s.HasStand && s.InCombatKit)
        {
            return MobPhase.ReturnKit;
        }

        return MobPhase.SteadyCivilian;
    }
}
