using LivingWorld.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace LivingWorld.RimWorld;

/// <summary>
/// Decides which colonists mobilization acts on and reads the two live facts the driver needs about them.
/// Kit storage and equipping live on the outfit stand (<see cref="OutfitStandKit"/>); this only answers
/// "can this colonist fight", "is it armed", and "is it on an urgent job we must not interrupt". Fail-safe —
/// a bad state reads as not-a-candidate / not-busy.
/// </summary>
public static class MobilizationCandidates
{
    public static bool IsCandidate(Pawn pawn)
    {
        try
        {
            if (pawn == null || !pawn.IsColonist || pawn.Dead || pawn.Downed || pawn.InMentalState)
            {
                return false;
            }

            // Never mobilize a colonist who cannot fight: pacifists / violence-incapable pawns, and pawns that
            // cannot be drafted at all (children, etc.).
            if (pawn.WorkTagIsDisabled(WorkTags.Violent) || pawn.drafter == null)
            {
                return false;
            }

            var shooting = SkillLevel(pawn, SkillDefOf.Shooting);
            var melee = SkillLevel(pawn, SkillDefOf.Melee);
            var threshold = LivingWorldSettings.Instance?.mobilizationSkillThreshold
                            ?? MobilizationTuning.CombatSkillThreshold;
            return LoadoutSelectionService.IsCombatEligible(shooting, melee, threshold);
        }
        catch
        {
            return false;
        }
    }

    // NOTE (known limitation, deferred): "armed" is a coarse proxy for "in the combat kit" — a colonist who
    // habitually carries a weapon reads as equipped and may skip the stand. Revisit with live diagnostics.
    // True once the pawn is carrying a weapon (its combat kit is on).
    public static bool IsArmed(Pawn pawn)
    {
        try
        {
            return pawn?.equipment?.Primary != null;
        }
        catch
        {
            return false;
        }
    }

    // "In the combat kit" = holding a weapon AND wearing at least one armor piece — the state the outfit-stand
    // swap produces. Stricter than IsArmed so a colonist habitually carrying a weapon (e.g. Simple Sidearms, a
    // hunter's rifle) but wearing no armor still reads as "not equipped" and is sent to their stand to gear up.
    public static bool IsInCombatKit(Pawn pawn)
    {
        try
        {
            if (pawn?.equipment?.Primary == null)
            {
                return false;
            }

            var worn = pawn.apparel?.WornApparel;
            if (worn == null)
            {
                return false;
            }

            foreach (var apparel in worn)
            {
                var cats = apparel?.def?.thingCategories;
                if (cats != null && cats.Contains(ThingCategoryDefOf.ApparelArmor))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    // On a life-or-base-saving job we must not yank them off: firefighting, tending a patient, rescuing downed.
    public static bool IsBusyUrgent(Pawn pawn)
    {
        try
        {
            var job = pawn?.CurJobDef;
            return job == JobDefOf.BeatFire || job == JobDefOf.TendPatient || job == JobDefOf.Rescue;
        }
        catch
        {
            return false;
        }
    }

    private static int SkillLevel(Pawn pawn, SkillDef skill)
    {
        return pawn?.skills?.GetSkill(skill)?.Level ?? 0;
    }
}
