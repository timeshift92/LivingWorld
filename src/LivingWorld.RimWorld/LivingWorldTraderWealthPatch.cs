using HarmonyLib;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// A trader from a Living World faction wealthier than the world average arrives carrying extra silver,
/// so the ledger economy (facilities, development, wealth) actually shows up at the trade window — richer
/// settlements make richer trade partners — instead of being display-only. Postfix on the single point
/// that produces the trader pawn; purely additive (only ever adds silver, never removes goods).
/// </summary>
[HarmonyPatch(typeof(PawnGroupKindWorker_Trader), "GenerateTrader")]
public static class LivingWorldTraderWealthPatch
{
    public static void Postfix(Pawn __result, PawnGroupMakerParms parms)
    {
        if (__result?.inventory == null)
        {
            return;
        }

        var component = LivingWorldWorldComponent.Instance;
        if (component == null)
        {
            return;
        }

        var factionDefName = parms?.faction?.def?.defName;
        if (string.IsNullOrEmpty(factionDefName))
        {
            return;
        }

        var bonusSilver = TraderWealthService.BonusSilver(component.State, factionDefName!);
        if (bonusSilver <= 0)
        {
            return;
        }

        var silver = ThingMaker.MakeThing(ThingDefOf.Silver);
        silver.stackCount = bonusSilver;
        __result.inventory.innerContainer.TryAdd(silver, canMergeWithExistingStacks: true);
    }
}
