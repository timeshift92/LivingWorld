using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(Dialog_Trade), "Close")]
public static class LivingWorldTradeIntelPatch
{
    private const int SensitiveGoodsIntelValue = 500;

    public static void Prefix(Dialog_Trade __instance)
    {
        var component = LivingWorldWorldComponent.Instance;
        var trader = TradeSession.trader;
        var faction = trader?.Faction;
        var factionId = faction?.def?.defName;

        if (component == null
            || __instance == null
            || faction == null
            || faction.IsPlayer
            || string.IsNullOrWhiteSpace(factionId))
        {
            return;
        }

        var tradeables = Traverse
            .Create(__instance)
            .Field("cachedTradeables")
            .GetValue<List<Tradeable>>();
        if (tradeables == null || tradeables.Count == 0)
        {
            return;
        }

        var observedMarketValue = 0;
        var sensitiveGoodsCount = 0;
        foreach (var tradeable in tradeables.Where(item => item != null && item.HasAnyThing))
        {
            var thingDef = tradeable.ThingDef;
            if (thingDef == null || thingDef == ThingDefOf.Silver)
            {
                continue;
            }

            var count = Math.Abs(tradeable.CountToTransfer);
            if (count <= 0)
            {
                continue;
            }

            observedMarketValue += Math.Max(1, (int)Math.Round(tradeable.BaseMarketValue * count));
            if (IsSensitiveGood(thingDef))
            {
                sensitiveGoodsCount += count;
            }
        }

        if (observedMarketValue <= 0 && sensitiveGoodsCount <= 0)
        {
            return;
        }

        RaidIntelService.RecordTradeIntel(
            component.State,
            new TradeIntelRequest(
                factionId!,
                observedMarketValue + sensitiveGoodsCount * SensitiveGoodsIntelValue,
                sensitiveGoodsCount,
                $"Trade with {factionId} revealed colony valuables."));
    }

    private static bool IsSensitiveGood(ThingDef thingDef)
    {
        var defName = thingDef.defName ?? string.Empty;
        return defName.IndexOf("Gold", StringComparison.OrdinalIgnoreCase) >= 0
            || defName.IndexOf("Psychoid", StringComparison.OrdinalIgnoreCase) >= 0
            || defName.IndexOf("Psychite", StringComparison.OrdinalIgnoreCase) >= 0
            || defName.IndexOf("Yayo", StringComparison.OrdinalIgnoreCase) >= 0
            || defName.IndexOf("Flake", StringComparison.OrdinalIgnoreCase) >= 0
            || defName.IndexOf("GoJuice", StringComparison.OrdinalIgnoreCase) >= 0
            || defName.IndexOf("Luciferium", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
