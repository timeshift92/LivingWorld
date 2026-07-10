using System;
using System.Linq;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// The apparel-policy safety net. Two auto-created policies decide, independently of the outfit-stand swap,
/// whether a colonist may wear combat armor: the combat policy permits it (so a just-equipped kit is not
/// stripped), the civilian policy forbids it (so a colonist can never end up in armor in peacetime — even if
/// the stand swap glitched, vanilla apparel optimization strips it). This is the guarantee; the stand swap is
/// only the convenience. Fail-safe.
/// </summary>
public static class MobilizationPolicyService
{
    private const string CombatLabel = "LivingWorld_Combat";
    private const string CivilianLabel = "LivingWorld_Civilian";

    public static void ApplyCombat(Pawn pawn) => SetPolicy(pawn, CombatLabel, allowArmor: true);

    public static void ApplyCivilian(Pawn pawn) => SetPolicy(pawn, CivilianLabel, allowArmor: false);

    public static bool IsCombatPolicy(Pawn pawn) => pawn?.outfits?.CurrentApparelPolicy?.label == CombatLabel;

    public static bool IsCivilianPolicy(Pawn pawn) => pawn?.outfits?.CurrentApparelPolicy?.label == CivilianLabel;

    private static void SetPolicy(Pawn pawn, string label, bool allowArmor)
    {
        try
        {
            var tracker = pawn?.outfits;
            if (tracker == null || tracker.CurrentApparelPolicy?.label == label)
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
