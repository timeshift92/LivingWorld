using System;
using System.Linq;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Controls a mobilized colonist's apparel policy so vanilla apparel optimization does not fight the outfit
/// stand. In peacetime a combat colonist is moved to a "civilian" policy that forbids combat armour — so they
/// never wander off and put on a flak vest from storage. While mobilized they use a "combat" policy that
/// permits armour, so the kit the stand equipped stays on. Two auto-created policies; fail-safe.
/// </summary>
public static class MobilizationOutfitService
{
    private const string CombatLabel = "LivingWorld_Combat";
    private const string CivilianLabel = "LivingWorld_Civilian";

    public static void ToCombat(Pawn pawn) => SetPolicy(pawn, CombatLabel, allowArmor: true);

    public static void ToCivilian(Pawn pawn) => SetPolicy(pawn, CivilianLabel, allowArmor: false);

    private static void SetPolicy(Pawn pawn, string label, bool allowArmor)
    {
        try
        {
            var tracker = pawn?.outfits;
            if (tracker == null)
            {
                return;
            }

            if (tracker.CurrentApparelPolicy?.label == label)
            {
                return;
            }

            var policy = EnsurePolicy(label, allowArmor);
            if (policy != null)
            {
                tracker.CurrentApparelPolicy = policy;
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Armory apparel-policy switch failed safely: {ex.Message}");
        }
    }

    private static ApparelPolicy? EnsurePolicy(string label, bool allowArmor)
    {
        var db = Current.Game?.outfitDatabase;
        if (db?.AllOutfits == null)
        {
            return null;
        }

        var existing = db.AllOutfits.FirstOrDefault(policy => policy != null && policy.label == label);
        if (existing != null)
        {
            existing.filter.SetAllow(ThingCategoryDefOf.ApparelArmor, allowArmor, null, null);
            return existing;
        }

        var created = db.MakeNewOutfit();
        created.label = label;
        created.filter.SetAllow(ThingCategoryDefOf.Apparel, true, null, null);
        created.filter.SetAllow(ThingCategoryDefOf.ApparelArmor, allowArmor, null, null);
        return created;
    }
}
