using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Small helpers for deciding which colonists mobilization acts on. Combat-kit storage and equipping now live
/// on the Odyssey Outfit Stands (see <see cref="OutfitStandDriver"/>); this only answers "can this colonist
/// fight" and "are they armed". Fail-safe — a bad state reads as not-a-candidate.
/// </summary>
public static class LoadoutAdapter
{
    public static bool IsMobilizationCandidate(Pawn pawn)
    {
        try
        {
            if (pawn == null || !pawn.IsColonist || pawn.Dead || pawn.Downed || pawn.InMentalState)
            {
                return false;
            }

            // Never mobilize a colonist who cannot fight: pacifists / violence-incapable pawns, and pawns
            // that cannot be drafted at all (children, etc.). They would only be sent to a stand in vain.
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

    private static int SkillLevel(Pawn pawn, SkillDef skill)
    {
        return pawn?.skills?.GetSkill(skill)?.Level ?? 0;
    }
}
