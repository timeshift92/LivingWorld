using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LivingWorld.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Player-facing direct settlement observation. This is deliberately an explicit command on a selected
/// NPC settlement: it records DirectVisit intel and opens an exact observer scoped to that settlement,
/// rather than turning the global ledger UI into omniscient information.
/// </summary>
[HarmonyPatch(typeof(Settlement), "GetGizmos")]
public static class LivingWorldSettlementDirectVisitPatch
{
    public static void Postfix(Settlement __instance, ref IEnumerable<Gizmo> __result)
    {
        __result = AppendLivingWorldGizmos(__result, __instance);
    }

    private static IEnumerable<Gizmo> AppendLivingWorldGizmos(IEnumerable<Gizmo> baseGizmos, Settlement __instance)
    {
        foreach (var gizmo in baseGizmos)
        {
            yield return gizmo;
        }

        Command_Action? observerCommand = null;
        try
        {
            if (TryResolveLedgerSettlement(__instance, out var settlementId))
            {
                observerCommand = new Command_Action
                {
                    defaultLabel = "LW_DirectVisitObserver".Translate(),
                    defaultDesc = "LW_DirectVisitObserverTooltip".Translate(),
                    icon = TexButton.Search,
                    action = () => OpenDirectObserver(settlementId),
                };
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Direct settlement observer gizmo skipped safely: {ex.Message}");
        }

        if (observerCommand != null)
        {
            yield return observerCommand;
        }
    }

    private static void OpenDirectObserver(EntityId settlementId)
    {
        var component = LivingWorldWorldComponent.Instance;
        if (component == null)
        {
            return;
        }

        PlayerKnowledgeService.RecordDirectVisitSettlementInfo(
            component.State,
            settlementId,
            "player directly inspected the settlement");
        Find.WindowStack.Add(new LivingWorldSettlementObserverWindow(settlementId, allowExactWithoutDebug: true));
    }

    internal static bool TryResolveLedgerSettlement(Settlement worldObject, out EntityId settlementId)
    {
        settlementId = default;
        var component = LivingWorldWorldComponent.Instance;
        if (component == null
            || worldObject == null
            || worldObject.Faction == null
            || worldObject.Faction == Faction.OfPlayer)
        {
            return false;
        }

        var factionId = worldObject.Faction.def?.defName ?? "UnknownFaction";
        var ledgerSettlement = ResolveLedgerSettlement(component.State, worldObject, factionId);
        if (ledgerSettlement == null)
        {
            return false;
        }

        settlementId = ledgerSettlement.Id;
        return true;
    }

    private static WorldSettlement? ResolveLedgerSettlement(WorldState state, Settlement worldObject, string factionId)
    {
        var stableKey = BuildStableKey(worldObject);
        var bySlug = state.Settlements.FirstOrDefault(settlement =>
            settlement.IsActive
            && string.Equals(settlement.Slug, stableKey, StringComparison.Ordinal));
        if (bySlug != null)
        {
            return bySlug;
        }

        var label = worldObject.LabelCap;
        if (!string.IsNullOrWhiteSpace(label))
        {
            var byNameAndFaction = state.Settlements.FirstOrDefault(settlement =>
                settlement.IsActive
                && string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal)
                && string.Equals(settlement.Name, label, StringComparison.Ordinal));
            if (byNameAndFaction != null)
            {
                return byNameAndFaction;
            }
        }

        var tileToken = $":{worldObject.Tile}:";
        return state.Settlements.FirstOrDefault(settlement =>
            settlement.IsActive
            && string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal)
            && settlement.Slug.Contains(tileToken));
    }

    private static string BuildStableKey(WorldObject obj)
    {
        var defName = obj.def?.defName ?? obj.GetType().Name;
        var factionId = obj.Faction?.def?.defName ?? "UnknownFaction";
        return $"worldobject:{defName}:{obj.Tile}:{factionId}";
    }
}
