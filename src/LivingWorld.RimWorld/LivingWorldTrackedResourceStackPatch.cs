using HarmonyLib;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.SplitOff))]
public static class LivingWorldTrackedResourceSplitPatch
{
    public static void Postfix(ThingWithComps __instance, Thing __result)
    {
        LivingWorldSettlementMapResourceTracker.TrackSplit(__instance, __result);
    }
}

[HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.TryAbsorbStack))]
public static class LivingWorldTrackedResourceAbsorbPatch
{
    public static bool Prefix(ThingWithComps __instance, Thing other, ref bool __result)
    {
        if (LivingWorldSettlementMapResourceTracker.AllowStack(__instance, other))
        {
            return true;
        }

        __result = false;
        return false;
    }

    public static void Postfix(Thing other, bool __result)
    {
        if (__result)
        {
            LivingWorldSettlementMapResourceTracker.NotifyAbsorbed(other);
        }
    }
}
