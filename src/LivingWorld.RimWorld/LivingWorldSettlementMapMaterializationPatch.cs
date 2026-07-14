using System;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(MapGenerator), "GenerateMap")]
[HarmonyAfter("helldan.finitepopulation", "rimworld.torann.rimwar", "com.Matathias.Empire")]
[HarmonyPriority(Priority.Last)]
public static class LivingWorldSettlementMapMaterializationPatch
{
    public static void Postfix(Map __result, MapParent parent)
    {
        try
        {
            if (ShouldMaterialize(parent))
            {
                LivingWorldSettlementMapMaterializationService.MaterializeSettlementMap(__result, parent);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Settlement map materialization skipped safely: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            // The orphan repair is signal-only and intentionally runs once for each generated map.
            try
            {
                LivingWorldOrphanedLordReferenceCleaner.CleanMap(__result);
            }
            catch (Exception ex)
            {
                Log.Warning($"[LivingWorld] Orphan dormancy signal repair skipped safely after map generation: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static bool ShouldMaterialize(MapParent parent)
    {
        if (parent is WorldObject_LivingWorldSettlementVisitSite)
        {
            return true;
        }

        if (parent is not Settlement || parent.GetType() != typeof(Settlement))
        {
            // Modded Settlement subclasses (notably Empire) own their own map lifecycle.
            return false;
        }

        // E&D and RimWar both patch vanilla settlement map generation. Until an explicit adapter
        // exists, Living World steps aside instead of spawning a second population/resource layer.
        return !ModsConfig.IsActive("helldan.economicsdemography")
            && !ModsConfig.IsActive("Torann.RimWar");
    }
}
