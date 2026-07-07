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
        if (cachedStableKey == stableKey
            && currentTick - cachedAtTick < CacheRefreshIntervalTicks)
        {
            return cachedLine;
        }

        cachedStableKey = stableKey;
        cachedAtTick = currentTick;
        cachedLine = BuildInspectLine(state, worldObject, stableKey);
        return cachedLine;
    }

    private static string BuildInspectLine(WorldState state, Settlement worldObject, string stableKey)
    {
        var settlement = FindSettlementForWorldObject(state, worldObject, stableKey);
        if (settlement == null)
        {
            return "LW_InspectPopulationMissingLine".Translate(
                state.Settlements.Count.Named("settlements")).ToString();
        }

        var known = state.GetKnownSettlementInfo(settlement.Id);
        var knowledgeLine = known == null
            ? "LW_KnowledgeUnknown".Translate()
            : "LW_KnowledgeLine".Translate(
                known.SourceKind.Named("source"),
                known.Confidence.Named("confidence"),
                known.Tick.Named("tick"),
                known.PopulationBand.Named("populationBand"),
                known.Food.Named("food"),
                known.Migration.Named("migration"));

        return "LW_InspectPopulationLine".Translate(
            knowledgeLine.Named("knowledge")).ToString();
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
