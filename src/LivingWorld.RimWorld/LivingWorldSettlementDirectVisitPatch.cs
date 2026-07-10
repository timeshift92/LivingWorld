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

                realBaseCommand = new Command_Action
                {
                    defaultLabel = "LW_OpenRealSettlementMap".Translate(),
                    defaultDesc = "LW_OpenRealSettlementMapTooltip".Translate(),
                    icon = TexButton.Info,
                    action = () => OpenRealSettlementMap(__instance, settlementId),
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

    internal static void OpenRealSettlementMap(Settlement worldObject, EntityId settlementId)
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
                        ResolveLivingWorldMapGenerator(),
                        Enumerable.Empty<GenStepWithParams>());
                }

                Current.Game.CurrentMap = map;
                CameraJumper.TryJump(map.Center, map);
            },
            "GeneratingMap",
            false,
            GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap);
    }

    private static MapGeneratorDef ResolveLivingWorldMapGenerator()
    {
        return DefDatabase<MapGeneratorDef>.GetNamedSilentFail("Base_Player")
            ?? DefDatabase<MapGeneratorDef>.AllDefsListForReading.First();
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

[HarmonyPatch(typeof(Settlement), "GetFloatMenuOptions")]
public static class LivingWorldSettlementCaravanVisitPatch
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

        if (__instance == null
            || caravan == null
            || !LivingWorldSettlementDirectVisitPatch.TryResolveLedgerSettlement(__instance, out _))
        {
            yield break;
        }

        foreach (var option in CaravanArrivalActionUtility.GetFloatMenuOptions(
            () => CanVisit(__instance),
            () => new CaravanArrivalAction_LivingWorldVisitSettlement(__instance),
            "LW_OpenRealSettlementMapFloatMenu".Translate(__instance.Label),
            caravan,
            __instance.Tile,
            __instance))
        {
            yield return option;
        }
    }

    private static FloatMenuAcceptanceReport CanVisit(Settlement settlement)
    {
        return settlement != null
            && settlement.Spawned
            && LivingWorldSettlementDirectVisitPatch.TryResolveLedgerSettlement(settlement, out _);
    }
}

public sealed class CaravanArrivalAction_LivingWorldVisitSettlement : CaravanArrivalAction
{
    private Settlement? settlement;

    public CaravanArrivalAction_LivingWorldVisitSettlement()
    {
    }

    public CaravanArrivalAction_LivingWorldVisitSettlement(Settlement settlement)
    {
        this.settlement = settlement;
    }

    public override string Label => settlement == null
        ? "LW_OpenRealSettlementMap".Translate()
        : "LW_OpenRealSettlementMapFloatMenu".Translate(settlement.Label);

    public override string ReportString => Label;

    public override FloatMenuAcceptanceReport StillValid(Caravan caravan, PlanetTile destinationTile)
    {
        var baseReport = base.StillValid(caravan, destinationTile);
        if (!baseReport)
        {
            return baseReport;
        }

        return settlement != null
            && settlement.Tile == destinationTile
            && LivingWorldSettlementDirectVisitPatch.TryResolveLedgerSettlement(settlement, out _);
    }

    public override void Arrived(Caravan caravan)
    {
        if (settlement == null
            || !LivingWorldSettlementDirectVisitPatch.TryResolveLedgerSettlement(settlement, out var settlementId))
        {
            Messages.Message("MessageCaravanArrivalActionNoLongerValid".Translate(Label), caravan, MessageTypeDefOf.RejectInput, false);
            return;
        }

        LivingWorldSettlementDirectVisitPatch.OpenRealSettlementMap(settlement, settlementId);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref settlement, "settlement");
    }
}
