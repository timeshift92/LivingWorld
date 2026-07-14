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

            // Hard floor: never mobilize a colonist who cannot fight, even if rostered.
            if (pawn.WorkTagIsDisabled(WorkTags.Violent) || pawn.drafter == null)
            {
                return false;
            }

            // The Fighters roster decides who arms up (skill-eligible by default, player-overridable).
            var roster = FightersRoster.Get();
            return roster != null && roster.IsFighter(pawn);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsNonCombatant(Pawn pawn)
    {
        try
        {
            // A non-combatant is any living colonist who is NOT an active fighter (IsCandidate is "on the
            // roster AND able to fight"). This makes fighters and non-combatants a strict complement — a
            // rostered-but-incapable pawn (pacifist / child) is protected by shelter rather than left in
            // the open. Fail-safe: unknown state reads as non-combatant so they are sheltered, not exposed.
            return pawn != null && pawn.IsColonist && !pawn.Dead && !IsCandidate(pawn);
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
}
