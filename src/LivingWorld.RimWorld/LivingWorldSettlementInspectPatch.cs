using System;
using System.Linq;
using HarmonyLib;
using LivingWorld.Core;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(Settlement), "GetInspectString")]
public static class LivingWorldSettlementInspectPatch
{
    private const int CacheRefreshIntervalTicks = 120;
    private static string? cachedStableKey;
    private static int cachedAtTick = -CacheRefreshIntervalTicks;
    private static string cachedLine = string.Empty;

    public static void Postfix(Settlement __instance, ref string __result)
    {
        var component = LivingWorldWorldComponent.Instance;
        if (component == null)
        {
            return;
        }

        var stableKey = BuildStableKey(__instance);
        var line = GetCachedInspectLine(component.State, __instance, stableKey);
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        __result = string.IsNullOrWhiteSpace(__result)
            ? line
            : $"{line}\n{__result}";
    }

    private static string GetCachedInspectLine(WorldState state, Settlement worldObject, string stableKey)
    {
        var currentTick = Find.TickManager?.TicksGame ?? 0;
        var visiblePawns = CountVisibleSettlementPawns(worldObject);
        var cacheKey = $"{stableKey}:visible:{visiblePawns}";
        if (cachedStableKey == cacheKey
            && currentTick - cachedAtTick < CacheRefreshIntervalTicks)
        {
            return cachedLine;
        }

        cachedStableKey = cacheKey;
        cachedAtTick = currentTick;
        cachedLine = BuildInspectLine(state, worldObject, stableKey, visiblePawns);
        return cachedLine;
    }

    private static string BuildInspectLine(
        WorldState state,
        Settlement worldObject,
        string stableKey,
        int visiblePawns)
    {
        var settlement = FindSettlementForWorldObject(state, worldObject, stableKey);
        if (settlement == null)
        {
            var missingLine = "LW_InspectPopulationMissingLine".Translate(
                state.Settlements.Count.Named("settlements")).ToString();
            return AppendVisiblePawnsLine(missingLine, visiblePawns);
        }

        var known = state.GetKnownSettlementInfo(settlement.Id);
        if (worldObject.HasMap
            && !PlayerKnowledgeService.HasFreshExactSnapshot(
                known,
                Find.TickManager?.TicksGame ?? 0))
        {
            known = PlayerKnowledgeService.RecordDirectVisitSettlementInfo(
                state,
                settlement.Id,
                "settlement map is loaded");
        }

        var freshness = known == null
            ? default
            : PlayerKnowledgeService.GetFreshness(
                known,
                Find.TickManager?.TicksGame ?? 0,
                PlayerKnowledgeService.ExactIntelStaleAfterTicks);
        var exactSnapshot = PlayerKnowledgeService.HasFreshExactSnapshot(
            known,
            Find.TickManager?.TicksGame ?? 0)
            ? known!.ExactSnapshot
            : null;

        var knowledgeLine = known == null
            ? "LW_KnowledgeUnknown".Translate()
            : "LW_KnowledgeLine".Translate(
                known.SourceKind.Named("source"),
                known.Confidence.Named("confidence"),
                known.Tick.Named("tick"),
                freshness.AgeDays.Named("ageDays"),
                freshness.IsStale.Named("stale"),
                known.PopulationBand.Named("populationBand"),
                known.Food.Named("food"),
                known.Migration.Named("migration"),
                known.Production.Named("production"));

        var inspectLine = "LW_InspectPopulationLine".Translate(
            knowledgeLine.Named("knowledge")).ToString();
        var productionLine = exactSnapshot != null
            ? FormatProductionLine(exactSnapshot)
            : "LW_ProductionHiddenLine".Translate().ToString();
        inspectLine = $"{inspectLine}\n{productionLine}";

        var facilitiesLine = BuildFacilitiesLine(exactSnapshot);
        if (facilitiesLine != null)
        {
            inspectLine = $"{inspectLine}\n{facilitiesLine}";
        }

        var animalsLine = BuildAnimalsLine(exactSnapshot);
        if (animalsLine != null)
        {
            inspectLine = $"{inspectLine}\n{animalsLine}";
        }

        // Threat header: if a world-war army is marching on this settlement it is already visible
        // on the map (the warband marker), so surfacing it here is consistent with fog-of-war and
        // ties the map object to the settlement. Famine/growth stay inside the knowledge line so we
        // never leak exact ledger state the player has not scouted.
        var threatLine = BuildThreatLine(state, settlement.Id);
        if (threatLine != null)
        {
            inspectLine = $"{threatLine}\n{inspectLine}";
        }

        return AppendVisiblePawnsLine(inspectLine, visiblePawns);
    }

    private static string? BuildThreatLine(WorldState state, EntityId settlementId)
    {
        var currentTick = Find.TickManager?.TicksGame ?? 0;
        var soonestArrival = int.MaxValue;
        foreach (var movement in state.ArmyMovements)
        {
            if (movement.Status == ArmyMovementStatus.Traveling
                && movement.TargetSettlementId == settlementId
                && movement.ArrivalTick < soonestArrival)
            {
                soonestArrival = movement.ArrivalTick;
            }
        }

        if (soonestArrival == int.MaxValue)
        {
            return null;
        }

        var days = Math.Max(0, (int)Math.Round((soonestArrival - currentTick) / 60000f));
        return "LW_InspectThreatLine".Translate(days.Named("days")).ToString();
    }

    private static string AppendVisiblePawnsLine(string inspectLine, int visiblePawns)
    {
        if (visiblePawns < 0)
        {
            return inspectLine;
        }

        var visibleLine = "LW_MapVisiblePawnsLine".Translate(
            visiblePawns.Named("visiblePawns")).ToString();
        return $"{inspectLine}\n{visibleLine}";
    }

    private static int CountVisibleSettlementPawns(Settlement worldObject)
    {
        if (!worldObject.HasMap || worldObject.Map?.mapPawns == null)
        {
            return -1;
        }

        var faction = worldObject.Faction;
        return worldObject.Map.mapPawns.AllPawnsSpawned.Count(pawn =>
            pawn?.RaceProps?.Humanlike == true
            && !pawn.Dead
            && (faction == null || pawn.Faction == faction));
    }

    private static string FormatProductionLine(SettlementKnowledgeSnapshot production)
    {
        return "LW_ProductionLine".Translate(
            production.AdultWorkers.Named("workers"),
            production.FoodPerDay.Named("food"),
            production.SteelPerDay.Named("steel"),
            production.MedicinePerDay.Named("medicine"),
            production.ComponentsPerDay.Named("components"),
            production.Biome.Named("biome"),
            production.Hilliness.Named("hilliness"),
            production.TechLevel.Named("tech")).ToString();
    }

    // Remote inspection may show only counts captured by the last fresh direct-visit snapshot.
    private static string? BuildFacilitiesLine(SettlementKnowledgeSnapshot? snapshot)
    {
        if (snapshot == null)
        {
            return null;
        }
        return "LW_InspectFacilitiesSnapshotLine".Translate(
            snapshot.FacilityCount.Named("facilities"),
            snapshot.ActiveProjectCount.Named("projects")).ToString();
    }

    // Animal counts follow the same immutable direct-visit snapshot rule.
    private static string? BuildAnimalsLine(SettlementKnowledgeSnapshot? snapshot)
    {
        if (snapshot == null)
        {
            return null;
        }
        return "LW_InspectAnimalsSnapshotLine".Translate(
            snapshot.AnimalCount.Named("animals")).ToString();
    }

    private static WorldSettlement? FindSettlementForWorldObject(
        WorldState state,
        Settlement worldObject,
        string stableKey)
    {
        var direct = state.Settlements.FirstOrDefault(candidate => candidate.Slug == stableKey);
        if (direct != null)
        {
            return direct;
        }

        var factionId = worldObject.Faction?.def?.defName ?? "UnknownFaction";
        var label = worldObject.LabelCap;
        if (!string.IsNullOrWhiteSpace(label))
        {
            var byNameAndFaction = state.Settlements.FirstOrDefault(candidate =>
                string.Equals(candidate.FactionId, factionId, StringComparison.Ordinal)
                && string.Equals(candidate.Name, label, StringComparison.Ordinal));
            if (byNameAndFaction != null)
            {
                return byNameAndFaction;
            }
        }

        var tileToken = $":{worldObject.Tile}:";
        return state.Settlements.FirstOrDefault(candidate =>
            string.Equals(candidate.FactionId, factionId, StringComparison.Ordinal)
            && candidate.Slug.Contains(tileToken));
    }

    private static string BuildStableKey(WorldObject obj)
    {
        var defName = obj.def?.defName ?? obj.GetType().Name;
        var factionId = obj.Faction?.def?.defName ?? "UnknownFaction";
        return $"worldobject:{defName}:{obj.Tile}:{factionId}";
    }
}
