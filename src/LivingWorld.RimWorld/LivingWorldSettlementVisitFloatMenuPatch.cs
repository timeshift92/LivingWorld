using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(Settlement), "GetFloatMenuOptions")]
public static class LivingWorldSettlementVisitFloatMenuPatch
{
    public static void Postfix(Settlement __instance, Caravan caravan, ref IEnumerable<FloatMenuOption> __result)
    {
        __result = AppendLivingWorldVisitOptions(__result, __instance, caravan);
    }

    private static IEnumerable<FloatMenuOption> AppendLivingWorldVisitOptions(
        IEnumerable<FloatMenuOption> baseOptions,
        Settlement __instance,
        Caravan caravan)
    {
        foreach (var option in baseOptions)
        {
            yield return option;
        }

        if (__instance == null || caravan == null)
        {
            yield break;
        }

        foreach (var option in CaravanArrivalActionUtility.GetFloatMenuOptions(
            () => CanVisit(__instance),
            () => new CaravanArrivalAction_LivingWorldSettlementVisitSite(__instance),
            "LW_SettlementVisitSiteFloatMenu".Translate(__instance.Label),
            caravan,
            __instance.Tile,
            __instance))
        {
            yield return option;
        }
    }

    private static FloatMenuAcceptanceReport CanVisit(Settlement settlement)
    {
        if (settlement == null || !settlement.Spawned)
        {
            return Reject("LW_SettlementVisitUnavailableMissing");
        }

        if (!settlement.Visitable)
        {
            return Reject("LW_SettlementVisitUnavailableNotVisitable");
        }

        if (settlement.Faction == null || settlement.Faction.HostileTo(Faction.OfPlayer))
        {
            return Reject("LW_SettlementVisitUnavailableHostile");
        }

        if (settlement.HasMap)
        {
            return Reject("LW_SettlementVisitUnavailableMapActive");
        }

        if (!LivingWorldSettlementDirectVisitPatch.TryResolveLedgerSettlement(settlement, out _))
        {
            return Reject("LW_SettlementVisitUnavailableNoLedger");
        }

        return true;
    }

    private static FloatMenuAcceptanceReport Reject(string translationKey)
    {
        return FloatMenuAcceptanceReport.WithFailReason(translationKey.Translate().ToString());
    }
}
