namespace LivingWorld.Core;

public enum SettlementTradeDirection
{
    SettlementReceives,
    SettlementProvides
}

public enum SettlementTradeLedgerStatus
{
    Success,
    PartialStock,
    NoSettlement,
    Ignored,
    InvalidRequest
}

public sealed record SettlementTradeLedgerRequest(
    string FactionId,
    string ResourceKey,
    int Quantity,
    SettlementTradeDirection Direction,
    int ObservedMarketValue,
    int SensitiveGoodsCount,
    string Summary,
    EntityId? PreferredSettlementId = null);

public sealed record SettlementTradeLedgerResult(
    SettlementTradeLedgerStatus Status,
    EntityId? SettlementId,
    int QuantityApplied,
    IntelReportResult Intel);

public static class SettlementTradeLedgerService
{
    public static SettlementTradeLedgerResult RecordTrade(
        WorldState state,
        SettlementTradeLedgerRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(request.FactionId)
            || string.IsNullOrWhiteSpace(request.ResourceKey)
            || request.Quantity <= 0)
        {
            return new SettlementTradeLedgerResult(
                SettlementTradeLedgerStatus.InvalidRequest,
                null,
                0,
                new IntelReportResult(
                    IntelReportStatus.InvalidRequest,
                    "Invalid trade request did not reveal intel.",
                    null,
                    null));
        }

        var settlement = ResolveSettlement(state, request);

        var intel = RecordIntel(state, request);
        if (settlement == null)
        {
            return new SettlementTradeLedgerResult(
                SettlementTradeLedgerStatus.NoSettlement,
                null,
                0,
                intel);
        }

        var applied = request.Direction == SettlementTradeDirection.SettlementReceives
            ? AddReceivedGoods(state, settlement.Id, request)
            : ConsumeProvidedGoods(state, settlement.Id, request);

        if (applied <= 0)
        {
            return new SettlementTradeLedgerResult(
                SettlementTradeLedgerStatus.PartialStock,
                settlement.Id,
                0,
                intel);
        }

        state.RecordEvent(
            WorldEventKind.SettlementTradeRecorded,
            settlement.Id,
            $"Settlement {settlement.Id} trade {request.Direction}: {applied} {request.ResourceKey}. {request.Summary}");

        return new SettlementTradeLedgerResult(
            applied == request.Quantity ? SettlementTradeLedgerStatus.Success : SettlementTradeLedgerStatus.PartialStock,
            settlement.Id,
            applied,
            intel);
    }

    private static WorldSettlement? ResolveSettlement(
        WorldState state,
        SettlementTradeLedgerRequest request)
    {
        if (request.PreferredSettlementId.HasValue)
        {
            var preferred = state.GetSettlement(request.PreferredSettlementId.Value);
            if (preferred is { IsActive: true }
                && string.Equals(preferred.FactionId, request.FactionId, StringComparison.Ordinal))
            {
                return preferred;
            }
        }

        return state.Settlements
            .Where(candidate => candidate.IsActive)
            .Where(candidate => string.Equals(candidate.FactionId, request.FactionId, StringComparison.Ordinal))
            .OrderBy(candidate => candidate.Id.Value)
            .FirstOrDefault();
    }

    private static int AddReceivedGoods(
        WorldState state,
        EntityId settlementId,
        SettlementTradeLedgerRequest request)
    {
        state.AddResource(settlementId, request.ResourceKey, request.Quantity);
        return request.Quantity;
    }

    private static int ConsumeProvidedGoods(
        WorldState state,
        EntityId settlementId,
        SettlementTradeLedgerRequest request)
    {
        return state.ConsumeResource(
            settlementId,
            request.ResourceKey,
            request.Quantity,
            $"trade: {request.Summary}");
    }

    private static IntelReportResult RecordIntel(
        WorldState state,
        SettlementTradeLedgerRequest request)
    {
        return RaidIntelService.RecordTradeIntel(
            state,
            new TradeIntelRequest(
                request.FactionId,
                request.ObservedMarketValue,
                request.SensitiveGoodsCount,
                request.Summary));
    }
}
