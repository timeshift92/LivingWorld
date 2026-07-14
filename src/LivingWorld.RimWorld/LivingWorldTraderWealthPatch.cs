using HarmonyLib;
using LivingWorld.Core;
using RimWorld;
using System;
using System.Linq;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Prevents vanilla trader generation from advertising more silver than the trader's deterministic
/// Living World source settlement owns. The transaction patch performs the actual debit only when a
/// deal succeeds, so cancelled windows and traders that leave do not leak or consume ledger silver.
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

        var source = component.State.Settlements
            .Where(settlement => settlement.IsActive)
            .Where(settlement => string.Equals(settlement.FactionId, factionDefName, StringComparison.Ordinal))
            .OrderByDescending(settlement => component.State.GetSettlementPopulation(settlement.Id).Total)
            .ThenBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
        if (source == null)
        {
            return;
        }

        try
        {
            var ledgerSilver = component.State.GetOwnedResourceQuantity(source.Id, ThingDefOf.Silver.defName);
            var silverStacks = __result.inventory.innerContainer
                .Where(thing => thing?.def == ThingDefOf.Silver)
                .ToList();
            var excess = Math.Max(0, silverStacks.Sum(stack => stack.stackCount) - ledgerSilver);
            foreach (var stack in silverStacks)
            {
                if (excess <= 0)
                {
                    break;
                }

                var removed = Math.Min(excess, stack.stackCount);
                if (removed == stack.stackCount)
                {
                    __result.inventory.innerContainer.Remove(stack);
                    stack.Destroy(DestroyMode.Vanish);
                }
                else
                {
                    stack.stackCount -= removed;
                }

                excess -= removed;
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] trader silver cap failed safely: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
