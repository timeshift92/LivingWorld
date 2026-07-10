using System;
using System.Linq;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Bridges mobilization to RimWorld's own apparel-policy ("outfit") system so the robust vanilla machinery
/// handles dressing, undressing and hauling clothes. Two auto-created policies drive it: a "combat" policy
/// that permits armour (so vanilla keeps the equipped kit on while mobilized) and a "civilian" policy that
/// forbids combat armour (so on stand-down vanilla strips the armour back to the racks and re-dresses the
/// colonist in civvies — and does not just put the armour straight back on, which is what happens if we
/// restore a normal outfit that allows armour). Everything is fail-safe — any error just skips, leaving the
/// colonist on whatever policy they had.
/// </summary>
public static class MobilizationOutfitService
{
    private const string CombatLabel = "LivingWorld_Combat";
    private const string CivilianLabel = "LivingWorld_Civilian";

    // Combat policy allows all apparel including armour; civilian policy allows all apparel except armour.
    public static ApparelPolicy? CombatPolicy() => EnsurePolicy(CombatLabel, allowArmor: true);

    public static ApparelPolicy? CivilianPolicy() => EnsurePolicy(CivilianLabel, allowArmor: false);

    private static ApparelPolicy? EnsurePolicy(string label, bool allowArmor)
    {
        try
        {
            var db = Current.Game?.outfitDatabase;
            if (db?.AllOutfits == null)
            {
                return null;
            }

            var existing = db.AllOutfits.FirstOrDefault(policy => policy != null && policy.label == label);
            if (existing != null)
            {
                // Keep the armour rule in sync in case it was edited, so stand-down always strips armour.
                existing.filter.SetAllow(ThingCategoryDefOf.ApparelArmor, allowArmor, null, null);
                return existing;
            }

            var created = db.MakeNewOutfit();
            created.label = label;
            created.filter.SetAllow(ThingCategoryDefOf.Apparel, true, null, null);
            created.filter.SetAllow(ThingCategoryDefOf.ApparelArmor, allowArmor, null, null);
            return created;
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Armory outfit '{label}' lookup failed safely: {ex.Message}");
            return null;
        }
    }

    // Move a colonist onto the combat policy so vanilla keeps the mobilization kit on.
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

            tracker.CurrentApparelPolicy = combat;
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Armory switch-to-combat outfit failed safely: {ex.Message}");
        }
    }

    // On stand-down move colonists we mobilized to the civilian policy: vanilla then strips the combat armour
    // to the racks and re-dresses them in civvies. Only touches pawns currently on our combat policy, so it
    // never overrides the outfit of a colonist we did not mobilize.
    public static void ToCivilian(Pawn pawn)
    {
        try
        {
            var tracker = pawn?.outfits;
            var civilian = CivilianPolicy();
            if (tracker == null || civilian == null)
            {
                return;
            }

            if (tracker.CurrentApparelPolicy?.label != CombatLabel)
            {
                return;
            }

            tracker.CurrentApparelPolicy = civilian;
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Armory switch-to-civilian outfit failed safely: {ex.Message}");
        }
    }
}
