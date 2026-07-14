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
    public static bool Prefix(
        ThingWithComps __instance,
        Thing other,
        ref bool __result,
        out LivingWorldTrackedResourceAbsorbState __state)
    {
        __state = new LivingWorldTrackedResourceAbsorbState(
            __instance?.stackCount ?? 0,
            other?.stackCount ?? 0);
        if (LivingWorldSettlementMapResourceTracker.AllowStack(__instance, other))
        {
            return true;
        }

        __result = false;
        return false;
    }

    public static void Postfix(
        ThingWithComps __instance,
        Thing other,
        LivingWorldTrackedResourceAbsorbState __state)
    {
        LivingWorldSettlementMapResourceTracker.NotifyAbsorbed(
            __instance,
            other,
            __state.DestinationCountBefore,
            __state.SourceCountBefore);
    }
}

public sealed record LivingWorldTrackedResourceAbsorbState(
    int DestinationCountBefore,
    int SourceCountBefore);
