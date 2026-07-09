namespace LivingWorld.Core;

/// <summary>A weapon available to a mobilizing colonist, reduced to what selection needs.</summary>
public sealed record WeaponOption(string DefName, bool IsRanged, int Value);

/// <summary>
/// A piece of armour available to a mobilizing colonist. <paramref name="Value"/> is a protection score
/// (higher = more armour), not market value, so selection prefers the piece that actually protects more.
/// </summary>
public sealed record ArmorOption(string DefName, int Value, bool IsHeavy);

public static class MobilizationTuning
{
    /// <summary>A colonist mobilizes only if their best combat skill reaches this. Below it they keep working.</summary>
    public const int CombatSkillThreshold = 4;
}

/// <summary>
/// Pure, deterministic skill-aware loadout selection for the armory / mobilization system. Given a pawn's
/// combat skills, an optional assigned kit, and the pool available on the racks, it decides which weapon and
/// armour that pawn should take. No RimWorld types, no side effects — unit-tested in isolation. The RimWorld
/// adapter builds the options from real things and applies the choice.
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

    public static WeaponOption? SelectWeapon(
        int shootingSkill,
        int meleeSkill,
        WeaponOption? assigned,
        IReadOnlyList<WeaponOption> pool)
    {
        if (assigned != null)
        {
            return assigned;
        }

        if (pool == null || pool.Count == 0)
        {
            return null;
        }

        var preferRanged = shootingSkill >= meleeSkill;
        var preferred = pool
            .Where(weapon => weapon.IsRanged == preferRanged)
            .OrderByDescending(weapon => weapon.Value)
            .ThenBy(weapon => weapon.DefName, StringComparer.Ordinal)
            .FirstOrDefault();
        if (preferred != null)
        {
            return preferred;
        }

        // No weapon of the preferred kind — take the best of what is there rather than go unarmed.
        return pool
            .OrderByDescending(weapon => weapon.Value)
            .ThenBy(weapon => weapon.DefName, StringComparer.Ordinal)
            .First();
    }

    /// <summary>
    /// Orders the available armour by the priority a mobilizing colonist should wear it in: an assigned piece
    /// first, then (for melee-leaning fighters) heavy armour, then most protective, then a stable name
    /// tie-break. The adapter walks this order and dons every piece that can be worn together, so a colonist
    /// puts on a full protective set — not just one item. Empty pool → empty list.
    /// </summary>
    public static IReadOnlyList<ArmorOption> RankArmor(
        int shootingSkill,
        int meleeSkill,
        ArmorOption? assigned,
        IReadOnlyList<ArmorOption> pool)
    {
        if (pool == null || pool.Count == 0)
        {
            return Array.Empty<ArmorOption>();
        }

        // Melee-leaning fighters take the front line, so give them the heavy armour first when available.
        var preferHeavy = meleeSkill > shootingSkill;
        return pool
            .OrderByDescending(armor => assigned != null && armor.DefName == assigned.DefName ? 1 : 0)
            .ThenByDescending(armor => preferHeavy && armor.IsHeavy ? 1 : 0)
            .ThenByDescending(armor => armor.Value)
            .ThenBy(armor => armor.DefName, StringComparer.Ordinal)
            .ToList();
    }
}
