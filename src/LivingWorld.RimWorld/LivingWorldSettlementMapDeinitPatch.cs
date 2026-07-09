using HarmonyLib;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(MapDeiniter), "Deinit")]
public static class LivingWorldSettlementMapDeinitPatch
{
    public static void Prefix(Map map)
    {
        var component = LivingWorldWorldComponent.Instance;
        if (component == null || map == null)
        {
            return;
        }

        LivingWorldSettlementMapResourceTracker.ReconcileMap(
            component.State,
            map,
            "settlement map deinit");
        LivingWorldSettlementMapFacilityTracker.ReconcileMap(
            component.State,
            map,
            "settlement map deinit");
    }
}
