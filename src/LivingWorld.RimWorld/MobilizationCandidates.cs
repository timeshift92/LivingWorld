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

    // True once the pawn is carrying a weapon (its combat kit is on).
    public static bool IsArmed(Pawn pawn)
    {
        return pawn?.equipment?.Primary != null;
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
