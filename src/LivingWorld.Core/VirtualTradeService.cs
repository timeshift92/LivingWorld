namespace LivingWorld.Core;

public sealed record VirtualTradeRequest(
    EntityId SellerSettlementId,
    EntityId BuyerSettlementId,
    string ResourceKey,
    int RequestedQuantity,
    string SilverResourceKey,
    int BaseUnitPrice);

public sealed record VirtualTradeQuote(
    int UnitPrice,
    int QuantityAvailable,
    int QuantityAffordable,
    int QuantityQuoted,
    int TotalPrice);

public sealed record VirtualTradeResult(
    VirtualTradeQuote Quote,
    int QuantityTransferred,
    int SilverTransferred);

public static class VirtualTradeService
{
    public static VirtualTradeQuote GetQuote(WorldState state, VirtualTradeRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        Validate(request);

        var sellerStock = state.GetOwnedResourceQuantity(request.SellerSettlementId, request.ResourceKey);
        var buyerStock = state.GetOwnedResourceQuantity(request.BuyerSettlementId, request.ResourceKey);
        var buyerSilver = state.GetOwnedResourceQuantity(request.BuyerSettlementId, request.SilverResourceKey);
        var sellerSilver = state.GetOwnedResourceQuantity(request.SellerSettlementId, request.SilverResourceKey);

        var pricePercent = 100;
        if (buyerStock < request.RequestedQuantity)
        {
            pricePercent += 25;
        }

        if (sellerStock > request.RequestedQuantity * 3)
        {
            pricePercent -= 10;
        }

        if (buyerSilver > sellerSilver)
        {
            pricePercent += 10;
        }

        var unitPrice = Math.Max(1, (request.BaseUnitPrice * pricePercent + 99) / 100);
        var available = Math.Min(request.RequestedQuantity, sellerStock);
        var affordable = buyerSilver / unitPrice;
        var quoted = Math.Max(0, Math.Min(available, affordable));

        return new VirtualTradeQuote(
            unitPrice,
            available,
            affordable,
            quoted,
            quoted * unitPrice);
    }

    public static VirtualTradeResult Execute(WorldState state, VirtualTradeRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var quote = GetQuote(state, request);
        if (quote.QuantityQuoted <= 0)
        {
            return new VirtualTradeResult(quote, 0, 0);
        }

        var goods = state.TransferResource(
            request.SellerSettlementId,
            request.BuyerSettlementId,
            request.ResourceKey,
            quote.QuantityQuoted,
            "virtual inter-faction trade goods");
        if (goods.Status != OwnershipTransferStatus.Success)
        {
            return new VirtualTradeResult(quote, 0, 0);
        }

        var silver = state.TransferResource(
            request.BuyerSettlementId,
            request.SellerSettlementId,
            request.SilverResourceKey,
            quote.TotalPrice,
            "virtual inter-faction trade silver");
        if (silver.Status != OwnershipTransferStatus.Success)
        {
            state.TransferResource(
                request.BuyerSettlementId,
                request.SellerSettlementId,
                request.ResourceKey,
                quote.QuantityQuoted,
                "virtual trade rollback");
            return new VirtualTradeResult(quote, 0, 0);
        }

        state.RecordEvent(
            WorldEventKind.SettlementTradeRecorded,
            request.SellerSettlementId,
            $"Virtual trade moved {quote.QuantityQuoted} {request.ResourceKey} for {quote.TotalPrice} {request.SilverResourceKey}.");

        return new VirtualTradeResult(quote, quote.QuantityQuoted, quote.TotalPrice);
    }

    private static void Validate(VirtualTradeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ResourceKey))
        {
            throw new ArgumentException("Trade resource key cannot be empty.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.SilverResourceKey))
        {
            throw new ArgumentException("Silver resource key cannot be empty.", nameof(request));
        }

        if (request.RequestedQuantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Trade quantity must be positive.");
        }

        if (request.BaseUnitPrice <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Base unit price must be positive.");
        }
    }
}
