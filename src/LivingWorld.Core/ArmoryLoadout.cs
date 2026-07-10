using System;
using System.Collections.Generic;
using System.Linq;

namespace LivingWorld.Core;

public static class MobilizationTuning
{
    /// <summary>A colonist mobilizes only if their best combat skill reaches this. Below it they keep working.</summary>
    public const int CombatSkillThreshold = 4;

    /// <summary>Melee is only preferred over ranged when it beats shooting by at least this many skill points.</summary>
    public const int MeleeAdvantage = 5;
}

/// <summary>A weapon available to a mobilizing colonist, reduced to what selection needs.</summary>
public sealed record WeaponOption(string DefName, bool IsRanged, int Value);

/// <summary>
/// Pure, deterministic mobilization selection logic: combat eligibility and best-weapon choice by skill. No
/// RimWorld types, no side effects — unit-tested in isolation. Armour/clothing live on the Odyssey Outfit
/// Stands; the weapon is auto-picked from the colony via this rule.
/// </summary>
public static class LoadoutSelectionService
{
    public static bool IsCombatEligible(int shootingSkill, int meleeSkill, int threshold)
    {
        return Math.Max(shootingSkill, meleeSkill) >= threshold;
    }

    public static bool IsCombatEligible(int shootingSkill, int meleeSkill)
    {
        return IsCombatEligible(shootingSkill, meleeSkill, MobilizationTuning.CombatSkillThreshold);
    }

    /// <summary>
    /// Picks the best weapon for a colonist. Ranged is preferred; melee is chosen only when the colonist's
    /// melee beats their shooting by at least <paramref name="meleeAdvantage"/> points. Within the preferred
    /// kind the highest-value weapon wins (stable name tie-break); if no weapon of the preferred kind exists,
    /// the best of any kind is taken rather than leaving them unarmed. Null when the pool is empty.
    /// </summary>
    public static WeaponOption? SelectWeapon(
        int shootingSkill,
        int meleeSkill,
        int meleeAdvantage,
        IReadOnlyList<WeaponOption> pool)
    {
        if (pool == null || pool.Count == 0)
        {
            return null;
        }

        var preferMelee = meleeSkill - shootingSkill >= meleeAdvantage;
        var preferred = pool
            .Where(weapon => weapon.IsRanged != preferMelee)
            .OrderByDescending(weapon => weapon.Value)
            .ThenBy(weapon => weapon.DefName, StringComparer.Ordinal)
            .FirstOrDefault();
        if (preferred != null)
        {
            return preferred;
        }

        return pool
            .OrderByDescending(weapon => weapon.Value)
            .ThenBy(weapon => weapon.DefName, StringComparer.Ordinal)
            .First();
    }
}
