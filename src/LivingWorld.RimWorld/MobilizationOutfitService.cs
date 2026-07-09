using System;
using System.Linq;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Bridges mobilization to RimWorld's own apparel-policy ("outfit") system so the robust vanilla machinery
/// handles dressing, undressing and hauling clothes. When a colonist arms up we switch them to an
/// auto-created "combat" policy (which permits armour, so vanilla never strips the kit we equipped) and
/// remember their previous policy; on stand-down we restore that policy, and once the combat armour is off
/// vanilla re-dresses them in civvies from the clothing rack. Everything is fail-safe — any error just skips,
/// leaving the colonist on whatever policy they had.
/// </summary>
public static class MobilizationOutfitService
{
    private const string CombatLabel = "LivingWorld_Combat";

    // The combat policy allows every apparel (including armour) so vanilla keeps the mobilization kit on.
    public static ApparelPolicy? CombatPolicy()
    {
        try
        {
            var db = Current.Game?.outfitDatabase;
            if (db?.AllOutfits == null)
            {
                return null;
            }

            var existing = db.AllOutfits.FirstOrDefault(policy => policy != null && policy.label == CombatLabel);
            if (existing != null)
            {
                return existing;
            }

            var created = db.MakeNewOutfit();
            created.label = CombatLabel;
            created.filter.SetAllow(ThingCategoryDefOf.Apparel, true, null, null);
            created.filter.SetAllow(ThingCategoryDefOf.ApparelArmor, true, null, null);
            return created;
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Armory combat outfit lookup failed safely: {ex.Message}");
            return null;
        }
    }

    // Move a colonist onto the combat policy, remembering the one they had so stand-down can restore it.
    public static void ToCombat(Pawn pawn)
    {
        try
        {
            var tracker = pawn?.outfits;
            var combat = CombatPolicy();
            if (tracker == null || combat == null)
            {
                return;
            }

            if (tracker.CurrentApparelPolicy?.label == CombatLabel)
            {
                return;
            }

            ArmoryAssignmentComponent.Instance?.RememberPolicy(pawn, tracker.CurrentApparelPolicy);
            tracker.CurrentApparelPolicy = combat;
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Armory switch-to-combat outfit failed safely: {ex.Message}");
        }
    }

    // Restore the policy the colonist had before mobilizing (so vanilla re-dresses them in civvies).
    public static void Restore(Pawn pawn)
    {
        try
        {
            var tracker = pawn?.outfits;
            if (tracker == null)
            {
                return;
            }

            // Only touch colonists we actually moved to the combat policy.
            if (tracker.CurrentApparelPolicy?.label != CombatLabel)
            {
                return;
            }

            var db = Current.Game?.outfitDatabase;
            var previous = ArmoryAssignmentComponent.Instance?.TakeRememberedPolicy(pawn);
            tracker.CurrentApparelPolicy = previous ?? db?.DefaultOutfit();
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Armory restore outfit failed safely: {ex.Message}");
        }
    }
}
