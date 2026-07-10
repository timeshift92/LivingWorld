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

        var transfers = new Dictionary<(string ResourceKey, SettlementTradeDirection Direction), TradeTransferAccumulator>();
        foreach (var tradeable in tradeables.Where(item => item != null && item.CountToTransfer != 0))
        {
            var thingDef = GetRepresentativeThingDef(tradeable);
            if (thingDef == null || thingDef == ThingDefOf.Silver)
            {
                continue;
            }

            var count = Math.Abs(tradeable.CountToTransfer);
            if (count <= 0)
            {
                continue;
            }

            var direction = ToTradeDirection(tradeable.CountToTransfer);
            var key = (thingDef.defName ?? thingDef.label ?? "UnknownThing", direction);
            transfers.TryGetValue(key, out var current);
            current.Quantity += count;
            current.ObservedMarketValue += Math.Max(1, (int)Math.Round(tradeable.BaseMarketValue * count));
            if (IsSensitiveGood(thingDef))
            {
                current.SensitiveGoodsCount += count;
            }

            transfers[key] = current;
        }

        if (transfers.Count == 0)
        {
            return;
        }

        foreach (var transfer in transfers)
        {
            SettlementTradeLedgerService.RecordTrade(
                component.State,
                new SettlementTradeLedgerRequest(
                    factionId!,
                    transfer.Key.ResourceKey,
                    transfer.Value.Quantity,
                    transfer.Key.Direction,
                    transfer.Value.ObservedMarketValue + transfer.Value.SensitiveGoodsCount * SensitiveGoodsIntelValue,
                    transfer.Value.SensitiveGoodsCount,
                    $"Trade with {factionId} moved {transfer.Value.Quantity} {transfer.Key.ResourceKey}."));
        }
    }

    private static SettlementTradeDirection ToTradeDirection(int countToTransfer)
    {
        return countToTransfer < 0
            ? SettlementTradeDirection.SettlementReceives
            : SettlementTradeDirection.SettlementProvides;
    }

    private static ThingDef? GetRepresentativeThingDef(Tradeable tradeable)
    {
        return GetFirstThingDef(tradeable, "thingsColony")
            ?? GetFirstThingDef(tradeable, "thingsTrader");
    }

    private static ThingDef? GetFirstThingDef(Tradeable tradeable, string fieldName)
    {
        try
        {
            var things = Traverse
                .Create(tradeable)
                .Field(fieldName)
                .GetValue<IEnumerable<Thing>>();
            return things?.FirstOrDefault(thing => thing?.def != null)?.def;
        }
        catch
        {
            return null;
        }
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

    private struct TradeTransferAccumulator
    {
        public int Quantity;
        public int ObservedMarketValue;
        public int SensitiveGoodsCount;
    }
}
