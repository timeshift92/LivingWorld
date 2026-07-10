namespace LivingWorld.Core;

public static class MobilizationTuning
{
    /// <summary>A colonist mobilizes only if their best combat skill reaches this. Below it they keep working.</summary>
    public const int CombatSkillThreshold = 4;
}

/// <summary>
/// Pure, deterministic combat-eligibility check for the mobilization system: given a colonist's shooting and
/// melee skills and a threshold, decide whether they are a combat colonist. No RimWorld types, no side
/// effects — unit-tested in isolation. Kit storage and equipping live on the Odyssey Outfit Stands.
/// </summary>
public static class LoadoutSelectionService
{
    public static bool IsCombatEligible(int shootingSkill, int meleeSkill, int threshold)
    {
        return System.Math.Max(shootingSkill, meleeSkill) >= threshold;
    }

    public static bool IsCombatEligible(int shootingSkill, int meleeSkill)
    {
        return IsCombatEligible(shootingSkill, meleeSkill, MobilizationTuning.CombatSkillThreshold);
    }
}
