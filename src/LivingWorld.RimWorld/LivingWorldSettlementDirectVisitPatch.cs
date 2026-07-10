using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LivingWorld.Core;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
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
    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Settlement __instance)
    {
        foreach (var gizmo in __result)
        {
            yield return gizmo;
        }

        Command_Action? observerCommand = null;
        Command_Action? realBaseCommand = null;
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

                if (Prefs.DevMode)
                {
                    realBaseCommand = new Command_Action
                    {
                        defaultLabel = "LW_DebugOpenRealSettlementMap".Translate(),
                        defaultDesc = "LW_DebugOpenRealSettlementMapTooltip".Translate(),
                        icon = TexButton.Info,
                        action = () => OpenRealSettlementMap(__instance, settlementId),
                    };
                }
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

        if (realBaseCommand != null)
        {
            yield return realBaseCommand;
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

    private static void OpenRealSettlementMap(Settlement worldObject, EntityId settlementId)
    {
        var component = LivingWorldWorldComponent.Instance;
        if (component == null || worldObject == null)
        {
            return;
        }

        PlayerKnowledgeService.RecordDirectVisitSettlementInfo(
            component.State,
            settlementId,
            "player dev-opened the real settlement map");

        LongEventHandler.QueueLongEvent(
            () =>
            {
                var map = worldObject.Map;
                if (map == null)
                {
                    map = MapGenerator.GenerateMap(
                        new IntVec3(120, 1, 120),
                        worldObject,
                        worldObject.MapGeneratorDef,
                        worldObject.ExtraGenStepDefs);
                }

                Current.Game.CurrentMap = map;
                CameraJumper.TryJump(map.Center, map);
            },
            "GeneratingMap",
            false,
            GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap);
    }

    private static bool TryResolveLedgerSettlement(Settlement worldObject, out EntityId settlementId)
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
        var stableKey = BuildStableKey(worldObject);
        var bySlug = component.State.Settlements.FirstOrDefault(settlement =>
            settlement.IsActive && settlement.Slug == stableKey);
        var ledgerSettlement = bySlug ?? component.State.Settlements.FirstOrDefault(settlement =>
            settlement.IsActive
            && string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal)
            && settlement.Slug.Contains($":{worldObject.Tile}:"));
        if (ledgerSettlement == null)
        {
            return false;
        }

        settlementId = ledgerSettlement.Id;
        return true;
    }

    private static string BuildStableKey(WorldObject obj)
    {
        var defName = obj.def?.defName ?? obj.GetType().Name;
        var factionId = obj.Faction?.def?.defName ?? "UnknownFaction";
        return $"worldobject:{defName}:{obj.Tile}:{factionId}";
    }
}
