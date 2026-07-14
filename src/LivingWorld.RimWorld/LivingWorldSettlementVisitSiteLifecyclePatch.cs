using HarmonyLib;
using RimWorld.Planet;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(WorldObjectsHolder), nameof(WorldObjectsHolder.Remove))]
public static class LivingWorldSettlementVisitSiteLifecyclePatch
{
    public static void Postfix(WorldObject o)
    {
        if (o == null || o is WorldObject_LivingWorldSettlementVisitSite)
        {
            return;
        }

        LivingWorldSettlementVisitSiteService.CloseProxiesForRemovedSource(o);
    }
}
