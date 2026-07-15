using LivingWorld.Core;
using RimWorld;
using RimWorld.Planet;
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

            // Never drag a colonist out of childbirth or the middle of forming a caravan: drafting a mother in
            // labour can abort the birth ritual, and drafting a caravan-forming pawn leaves the caravan Lord
            // waiting on a member who never returns (it is not a voluntarily-joinable lord that self-heals).
            if (IsInLabor(pawn) || pawn.IsFormingCaravan())
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
            //
            // BUT a Downed or mentally-broken colonist is OUT of the system entirely — vanilla rescue / the
            // mental break handles them. Without this guard a fighter who is briefly downed or berserk stops
            // being IsCandidate, gets reclassified as a non-combatant, and is herded to shelter + swapped to
            // civilian gear, then re-mobilized on recovery — pointless thrash (a downed/berserk pawn does not
            // move to an area anyway). Observed live as the "Зоя" flicker in Player.log.
            return pawn != null && pawn.IsColonist && !pawn.Dead && !pawn.Downed && !pawn.InMentalState
                   && !IsCandidate(pawn);
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

    // On a job we must not yank them off: firefighting, tending, rescuing — plus performing a MEDICAL operation
    // (a surgery aborts mid-operation, wasting medicine and leaving the patient cut open) and carrying anyone (a
    // downed ally would be dropped on the spot). Drafting/ordering force-interrupts all of these. NOTE: only a
    // medical bill counts — an ordinary crafting/cooking bill does NOT stop a fighter answering a raid.
    public static bool IsBusyUrgent(Pawn pawn)
    {
        try
        {
            var job = pawn?.CurJobDef;
            if (job == JobDefOf.BeatFire || job == JobDefOf.TendPatient || job == JobDefOf.Rescue)
            {
                return true;
            }

            // A surgery / medical operation in progress — do not interrupt it. (Bill_Medical, not any bill.)
            if (pawn?.CurJob?.bill is Bill_Medical)
            {
                return true;
            }

            // Carrying a pawn (rescue/capture/haul-to-bed variants, drafted or not) — do not make them drop it.
            return pawn?.carryTracker?.CarriedThing is Pawn;
        }
        catch
        {
            return false;
        }
    }

    // A player combat creature the Arsenal drafts and CAI-drives alongside the fighters: a draftable, non-colonist
    // pawn such as an Odyssey ghoul. (War-trained animals are not draftable and instead follow their drafted
    // master via vanilla; mechanitor mechs are out of scope.) Fail-safe.
    public static bool IsCombatCreature(Pawn pawn)
    {
        try
        {
            return pawn != null && pawn.Spawned && !pawn.Dead && !pawn.Downed && !pawn.InMentalState
                   && pawn.Faction == Faction.OfPlayer && !pawn.IsColonist
                   && pawn.drafter != null && pawn.IsGhoul;
        }
        catch
        {
            return false;
        }
    }

    // A player animal that should take shelter with the non-combatants: a colony animal that is NOT war-trained
    // (a war-trained animal fights, following its drafted master). Fail-safe: unknown state reads as false.
    public static bool IsShelterAnimal(Pawn pawn)
    {
        try
        {
            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Faction != Faction.OfPlayer
                || pawn.RaceProps?.Animal != true)
            {
                return false;
            }

            // War-trained (Release) animals are combatants — leave them to fight, do not herd them to shelter.
            return pawn.training?.HasLearned(TrainableDefOf.Release) != true;
        }
        catch
        {
            return false;
        }
    }

    // In active childbirth. Matched by defName (string) so it needs no hard Biotech dependency: "PregnancyLabor"
    // is the dilation stage, "PregnancyLaborPushing" the active push. Fail-safe.
    private static bool IsInLabor(Pawn pawn)
    {
        try
        {
            var hediffs = pawn?.health?.hediffSet?.hediffs;
            if (hediffs == null)
            {
                return false;
            }

            foreach (var h in hediffs)
            {
                var name = h?.def?.defName;
                if (name == "PregnancyLabor" || name == "PregnancyLaborPushing")
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
}
