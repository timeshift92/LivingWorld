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
/// Reconciles only a successfully executed vanilla deal. Purchases backed by a Living World
/// settlement are clamped before vanilla moves the Things, so ledger stock cannot go negative. For a
/// materialized traveling caravan, inventory was already debited from the ledger at spawn and is the
/// physical source of truth; trades are observed without a second debit. Gift-mode transfers use the
/// same reconciliation path.
/// </summary>
[HarmonyPatch(typeof(TradeDeal), "TryExecute")]
public static class LivingWorldTradeIntelPatch
{
    private const int SensitiveGoodsIntelValue = 500;

    public static bool Prefix(TradeDeal __instance, ref bool __result, out TradeExecutionState? __state)
    {
        __state = null;
        if (__instance == null)
        {
            return true;
        }

        var component = LivingWorldWorldComponent.Instance;
        var trader = TradeSession.trader;
        var faction = trader?.Faction;
        var factionId = faction?.def?.defName;
        if (component == null
            || trader == null
            || faction == null
            || faction.IsPlayer
            || string.IsNullOrWhiteSpace(factionId))
        {
            return true;
        }

        var trackedFaction = component.State.Settlements.Any(settlement =>
            settlement.IsActive
            && string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal));
        try
        {
            var source = ResolveSource(component, trader, factionId!);
            var externalTradeables = new HashSet<Tradeable>();
            var stockLimited = false;
            if (source != null && !source.PhysicalBacked)
            {
                stockLimited |= ClampPurchases(
                    component.State,
                    source.Settlement.Id,
                    __instance.AllTradeables,
                    externalTradeables);
                if (!TradeSession.giftMode)
                {
                    stockLimited |= ClampPlayerSalesToSilver(
                        component.State,
                        source.Settlement.Id,
                        __instance);
                }
            }

            __instance.UpdateCurrencyCount();
            var transfers = CaptureTransfers(
                __instance.AllTradeables,
                source?.Settlement.Id,
                externalTradeables,
                source?.PhysicalBacked ?? false);
            if (transfers.Count == 0)
            {
                return true;
            }

            __state = new TradeExecutionState(
                component.State,
                factionId!,
                source?.Settlement.Id,
                source?.IsExact ?? false,
                trader as Pawn,
                transfers);

            if (stockLimited)
            {
                Messages.Message(
                    "LW_TradeStockLimited".Translate(),
                    MessageTypeDefOf.CautionInput,
                    historical: false);
            }

            return true;
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Trade pre-reconciliation failed: {ex.GetType().Name}: {ex.Message}");
            __state = null;
            if (!trackedFaction)
            {
                return true;
            }

            __result = false;
            Messages.Message(
                "LW_TradeStockLimited".Translate(),
                MessageTypeDefOf.CautionInput,
                historical: false);
            return false;
        }
    }

    public static void Postfix(bool __result, bool actuallyTraded, TradeExecutionState? __state)
    {
        if (!__result || !actuallyTraded || __state == null || __state.Applied)
        {
            return;
        }

        try
        {
            ApplyCompletedTrade(__state);
        }
        catch (Exception error)
        {
            // Applied remains false. Per-transfer ledger baselines let a safe retry recognize already
            // committed mutations instead of applying them twice.
            Log.Error(
                $"[LivingWorld] Trade post-reconciliation remains pending: "
                + $"{error.GetType().Name}: {error.Message}");
        }
    }

    public static void ApplyCompletedTrade(TradeExecutionState execution)
    {
        if (execution == null || execution.Applied)
        {
            return;
        }

        foreach (var progress in execution.Progress)
        {
            var transfer = progress.Transfer;
            if (transfer.LedgerBacked && execution.SettlementId.HasValue && !progress.LedgerApplied)
            {
                var settlementId = execution.SettlementId.Value;
                var expected = transfer.Direction == SettlementTradeDirection.SettlementReceives
                    ? progress.LedgerQuantityBefore + transfer.Quantity
                    : progress.LedgerQuantityBefore - transfer.Quantity;
                var current = execution.State.GetOwnedResourceQuantity(settlementId, transfer.ResourceKey);
                if (current == progress.LedgerQuantityBefore)
                {
                    if (transfer.Direction == SettlementTradeDirection.SettlementReceives)
                    {
                        execution.State.AddResource(settlementId, transfer.ResourceKey, transfer.Quantity);
                    }
                    else
                    {
                        var consumed = execution.State.ConsumeResource(
                            settlementId,
                            transfer.ResourceKey,
                            transfer.Quantity,
                            "confirmed vanilla trade reconciliation");
                        if (consumed != transfer.Quantity)
                        {
                            throw new InvalidOperationException(
                                $"Trade ledger supplied {consumed}/{transfer.Quantity} {transfer.ResourceKey}.");
                        }
                    }

                    current = execution.State.GetOwnedResourceQuantity(settlementId, transfer.ResourceKey);
                }

                if (current != expected)
                {
                    throw new InvalidOperationException(
                        $"Trade ledger mismatch for {transfer.ResourceKey}: expected {expected}, found {current}.");
                }

                progress.LedgerApplied = true;
                execution.State.RecordEvent(
                    WorldEventKind.SettlementTradeRecorded,
                    settlementId,
                    $"Confirmed vanilla trade moved {transfer.Quantity} {transfer.ResourceKey} ({transfer.Direction}).");
            }

            if (transfer.PhysicalBacked && !progress.PhysicalManifestApplied)
            {
                if (transfer.Direction == SettlementTradeDirection.SettlementReceives
                    && execution.TraderPawn != null)
                {
                    LivingWorldWorldComponent.Instance?.RegisterApproachingGroupReceivedResource(
                        execution.TraderPawn,
                        transfer.ResourceKey);
                }

                progress.PhysicalManifestApplied = true;
            }

            if (!progress.IntelApplied)
            {
                var provenance = transfer.PhysicalBacked
                    ? "Reserved physical caravan trade"
                    : transfer.LedgerBacked
                        ? "Confirmed vanilla trade"
                        : "External vanilla trade";
                RaidIntelService.RecordTradeIntel(
                    execution.State,
                    new TradeIntelRequest(
                    execution.FactionId,
                    transfer.ObservedMarketValue,
                    transfer.SensitiveGoodsCount,
                    $"{provenance} moved {transfer.Quantity} {transfer.ResourceKey}."));
                progress.IntelApplied = true;
            }
        }

        if (execution.Progress.All(progress =>
                (!progress.Transfer.LedgerBacked || progress.LedgerApplied)
                && (!progress.Transfer.PhysicalBacked || progress.PhysicalManifestApplied)
                && progress.IntelApplied))
        {
            execution.Applied = true;
            if (!execution.IsExactSource)
            {
                DebugLog("trade used faction-level or external source fallback");
            }
        }
    }

    private static bool ClampPurchases(
        WorldState state,
        EntityId settlementId,
        IReadOnlyList<Tradeable> tradeables,
        ISet<Tradeable> externalTradeables)
    {
        var candidates = tradeables
            .Where(tradeable => tradeable != null && tradeable.CountToTransfer > 0)
            .Select(tradeable => CreateCandidate(tradeable, Transactor.Trader))
            .Where(candidate => candidate != null && candidate.Def != ThingDefOf.Silver)
            .Select(candidate => candidate!)
            .ToList();
        if (candidates.Count == 0)
        {
            return false;
        }

        var requests = candidates
            .Select(candidate => new SettlementTradeStockRequest(
                candidate.ResourceKey,
                candidate.Tradeable.CountToTransfer,
                SettlementTradeDirection.SettlementProvides,
                true))
            .ToList();
        var allocations = SettlementTradeReconciliationService.Plan(state, settlementId, requests);

        var limited = false;
        for (var i = 0; i < candidates.Count && i < allocations.Count; i++)
        {
            var candidate = candidates[i];
            var allocation = allocations[i];
            if (allocation.AllowedQuantity < candidate.Tradeable.CountToTransfer)
            {
                candidate.Tradeable.ForceToSource(allocation.AllowedQuantity);
                limited = true;
            }
        }

        return limited;
    }

    private static bool ClampPlayerSalesToSilver(
        WorldState state,
        EntityId settlementId,
        TradeDeal deal)
    {
        deal.UpdateCurrencyCount();
        var currency = FindCurrencyTradeable(deal.AllTradeables);
        if (currency == null)
        {
            return false;
        }

        var silverBudget = state.GetOwnedResourceQuantity(settlementId, ThingDefOf.Silver.defName);
        if (currency.CountToTransferToSource <= silverBudget)
        {
            return false;
        }

        var sales = deal.AllTradeables
            .Where(tradeable => tradeable != null
                && tradeable != currency
                && tradeable.CountToTransfer < 0)
            .Reverse()
            .ToList();
        foreach (var sale in sales)
        {
            var current = sale.CountToTransferToDestination;
            var low = 0;
            var high = current;
            while (low < high)
            {
                var mid = low + (high - low + 1) / 2;
                sale.ForceToDestination(mid);
                deal.UpdateCurrencyCount();
                if (currency.CountToTransferToSource <= silverBudget)
                {
                    low = mid;
                }
                else
                {
                    high = mid - 1;
                }
            }

            sale.ForceToDestination(low);
            deal.UpdateCurrencyCount();
            if (currency.CountToTransferToSource <= silverBudget)
            {
                break;
            }
        }

        return true;
    }

    private static List<TradeTransfer> CaptureTransfers(
        IReadOnlyList<Tradeable> tradeables,
        EntityId? settlementId,
        ISet<Tradeable> externalTradeables,
        bool physicalBacked)
    {
        var transfers = new Dictionary<(string ResourceKey, SettlementTradeDirection Direction, bool LedgerBacked, bool PhysicalBacked), TradeTransfer>();
        foreach (var tradeable in tradeables.Where(item => item != null && item.CountToTransfer != 0))
        {
            var direction = ToTradeDirection(tradeable.CountToTransfer);
            var transactor = direction == SettlementTradeDirection.SettlementProvides
                ? Transactor.Trader
                : Transactor.Colony;
            var candidate = CreateCandidate(tradeable, transactor);
            if (candidate == null)
            {
                continue;
            }

            var count = Math.Abs(tradeable.CountToTransfer);
            var ledgerBacked = !physicalBacked && settlementId.HasValue
                && (direction == SettlementTradeDirection.SettlementReceives
                    || !externalTradeables.Contains(tradeable));
            var key = (candidate.ResourceKey, direction, ledgerBacked, physicalBacked);
            transfers.TryGetValue(key, out var current);
            current ??= new TradeTransfer(candidate.ResourceKey, 0, direction, ledgerBacked, physicalBacked, 0, 0);

            var observedValue = candidate.Def == ThingDefOf.Silver
                ? 0
                : Math.Max(1, (int)Math.Round(candidate.MarketValue * count));
            transfers[key] = current with
            {
                Quantity = current.Quantity + count,
                ObservedMarketValue = current.ObservedMarketValue + observedValue,
                SensitiveGoodsCount = current.SensitiveGoodsCount
                    + (IsSensitiveGood(candidate.Def) ? count : 0)
            };
        }

        return transfers.Values
            .OrderBy(transfer => transfer.ResourceKey, StringComparer.Ordinal)
            .ThenBy(transfer => transfer.Direction)
            .ThenBy(transfer => transfer.LedgerBacked ? 0 : 1)
            .ToList();
    }

    private static TradeCandidate? CreateCandidate(Tradeable tradeable, Transactor transactor)
    {
        var things = transactor == Transactor.Trader
            ? tradeable.thingsTrader
            : tradeable.thingsColony;
        var thing = things?.FirstOrDefault(candidate => candidate != null && !candidate.Destroyed);
        if (thing == null || thing is Pawn)
        {
            return null;
        }

        var inner = thing.GetInnerIfMinified();
        var def = inner?.def;
        if (def == null)
        {
            return null;
        }

        return new TradeCandidate(
            tradeable,
            def,
            def.defName ?? def.label ?? "UnknownThing",
            Math.Max(0.01f, inner!.MarketValue));
    }

    private static Tradeable? FindCurrencyTradeable(IEnumerable<Tradeable> tradeables)
    {
        return tradeables.FirstOrDefault(tradeable =>
            tradeable != null
            && (tradeable.thingsColony?.Any(thing => thing?.def == ThingDefOf.Silver) == true
                || tradeable.thingsTrader?.Any(thing => thing?.def == ThingDefOf.Silver) == true));
    }

    private static ResolvedTradeSource? ResolveSource(
        LivingWorldWorldComponent component,
        ITrader trader,
        string factionId)
    {
        var state = component.State;
        if (trader is Settlement settlement)
        {
            var tile = (int)settlement.Tile;
            var exact = state.Settlements.FirstOrDefault(candidate =>
                candidate.IsActive
                && string.Equals(candidate.FactionId, factionId, StringComparison.Ordinal)
                && ParseTile(candidate.Slug) == tile);
            if (exact != null)
            {
                return new ResolvedTradeSource(exact, true, false);
            }
        }

        if (trader is Pawn pawn)
        {
            if (component.TryResolvePhysicalTradeGroup(pawn, out var physicalSourceId))
            {
                var physicalSource = ResolveOwnedSettlement(state, physicalSourceId, factionId);
                if (physicalSource != null)
                {
                    return new ResolvedTradeSource(physicalSource, true, true);
                }
            }

            var lease = state.MaterializationLeases
                .Where(candidate => candidate.IsActive && candidate.PawnThingId == pawn.thingIDNumber)
                .OrderBy(candidate => candidate.Id.Value)
                .FirstOrDefault();
            var leasedSource = ResolveOwnedSettlement(state, lease?.SourceOwnerId, factionId);
            if (leasedSource != null)
            {
                return new ResolvedTradeSource(leasedSource, true, false);
            }

            var identity = pawn.GetComp<CompLivingWorldIdentity>();
            var identitySource = identity?.HasLedgerId == true
                ? ResolveOwnedSettlement(state, state.GetOwner(identity.LedgerId), factionId)
                : null;
            if (identitySource != null)
            {
                return new ResolvedTradeSource(identitySource, true, false);
            }
        }

        if (trader is not Pawn && trader is not Settlement && trader is not Caravan)
        {
            return null;
        }

        var fallback = state.Settlements
            .Where(candidate => candidate.IsActive)
            .Where(candidate => string.Equals(candidate.FactionId, factionId, StringComparison.Ordinal))
            .OrderByDescending(candidate => state.GetSettlementPopulation(candidate.Id).Total)
            .ThenBy(candidate => candidate.Id.Value)
            .FirstOrDefault();
        return fallback == null ? null : new ResolvedTradeSource(fallback, false, false);
    }

    private static WorldSettlement? ResolveOwnedSettlement(
        WorldState state,
        EntityId? ownerId,
        string factionId)
    {
        if (!ownerId.HasValue || ownerId.Value.Kind != EntityKind.Settlement)
        {
            return null;
        }

        var settlement = state.GetSettlement(ownerId.Value);
        return settlement is { IsActive: true }
            && string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal)
                ? settlement
                : null;
    }

    private static int ParseTile(string? slug)
    {
        if (string.IsNullOrEmpty(slug))
        {
            return -1;
        }

        var parts = slug!.Split(':');
        return parts.Length >= 3 && int.TryParse(parts[2], out var tile) ? tile : -1;
    }

    private static SettlementTradeDirection ToTradeDirection(int countToTransfer)
    {
        return countToTransfer < 0
            ? SettlementTradeDirection.SettlementReceives
            : SettlementTradeDirection.SettlementProvides;
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

    private static void DebugLog(string message)
    {
        if ((LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging)
        {
            Log.Message("[LivingWorld] Trade reconciliation: " + message);
        }
    }

    public sealed class TradeExecutionState
    {
        public TradeExecutionState(
            WorldState state,
            string factionId,
            EntityId? settlementId,
            bool isExactSource,
            Pawn? traderPawn,
            IReadOnlyList<TradeTransfer> transfers)
        {
            State = state;
            FactionId = factionId;
            SettlementId = settlementId;
            IsExactSource = isExactSource;
            TraderPawn = traderPawn;
            Transfers = transfers;
            Progress = BuildProgress(state, settlementId, transfers);
        }

        public WorldState State { get; }

        public string FactionId { get; }

        public EntityId? SettlementId { get; }

        public bool IsExactSource { get; }

        public Pawn? TraderPawn { get; }

        public IReadOnlyList<TradeTransfer> Transfers { get; }

        public IReadOnlyList<TradeTransferProgress> Progress { get; }

        public bool Applied { get; set; }

        private static IReadOnlyList<TradeTransferProgress> BuildProgress(
            WorldState state,
            EntityId? settlementId,
            IReadOnlyList<TradeTransfer> transfers)
        {
            var runningLedgerQuantities = new Dictionary<string, int>(StringComparer.Ordinal);
            var progress = new List<TradeTransferProgress>(transfers.Count);
            foreach (var transfer in transfers)
            {
                var before = 0;
                if (transfer.LedgerBacked && settlementId.HasValue)
                {
                    if (!runningLedgerQuantities.TryGetValue(transfer.ResourceKey, out before))
                    {
                        before = state.GetOwnedResourceQuantity(settlementId.Value, transfer.ResourceKey);
                    }

                    runningLedgerQuantities[transfer.ResourceKey] = transfer.Direction
                        == SettlementTradeDirection.SettlementReceives
                            ? before + transfer.Quantity
                            : before - transfer.Quantity;
                }

                progress.Add(new TradeTransferProgress(transfer, before));
            }

            return progress;
        }
    }

    public sealed class TradeTransferProgress
    {
        public TradeTransferProgress(TradeTransfer transfer, int ledgerQuantityBefore)
        {
            Transfer = transfer;
            LedgerQuantityBefore = Math.Max(0, ledgerQuantityBefore);
        }

        public TradeTransfer Transfer { get; }

        public int LedgerQuantityBefore { get; }

        public bool LedgerApplied { get; set; }

        public bool PhysicalManifestApplied { get; set; }

        public bool IntelApplied { get; set; }
    }

    public sealed record TradeTransfer(
        string ResourceKey,
        int Quantity,
        SettlementTradeDirection Direction,
        bool LedgerBacked,
        bool PhysicalBacked,
        int ObservedMarketValue,
        int SensitiveGoodsCount);

    private sealed record TradeCandidate(
        Tradeable Tradeable,
        ThingDef Def,
        string ResourceKey,
        float MarketValue);

    private sealed record ResolvedTradeSource(WorldSettlement Settlement, bool IsExact, bool PhysicalBacked);
}
