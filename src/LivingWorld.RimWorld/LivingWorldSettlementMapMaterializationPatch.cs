using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(MapGenerator), "GenerateMap")]
public static class LivingWorldSettlementMapMaterializationPatch
{
    public static void Postfix(Map __result, MapParent parent)
    {
        LivingWorldSettlementMapMaterializationService.MaterializeSettlementMap(__result, parent);
        LivingWorldOrphanedLordReferenceCleaner.CleanMap(__result);
    }
}
