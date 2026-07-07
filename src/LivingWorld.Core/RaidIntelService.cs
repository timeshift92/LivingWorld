namespace LivingWorld.Core;

public enum IntelReportStatus
{
    Accepted,
    Ignored,
    InvalidRequest
}

public sealed record TradeIntelRequest(
    string FactionId,
    int ObservedMarketValue,
    int SensitiveGoodsCount,
    string Summary);

public sealed record IntelReportResult(
    IntelReportStatus Status,
    string Reason,
    WorldIntelReport? IntelReport,
    RaidOpportunity? RaidOpportunity);

public static class RaidIntelService
{
    private const int MinimumTradeValueForRaidOpportunity = 500;
    private const int CombatantDemandValueDivisor = 500;

    public static IntelReportResult RecordTradeIntel(WorldState state, TradeIntelRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(request.FactionId))
        {
            return new IntelReportResult(
                IntelReportStatus.InvalidRequest,
                "Trade intel faction cannot be empty.",
                null,
                null);
        }

        if (request.ObservedMarketValue <= 0 && request.SensitiveGoodsCount <= 0)
        {
            return new IntelReportResult(
                IntelReportStatus.Ignored,
                "Trade intel did not reveal valuable goods.",
                null,
                null);
        }

        var sensitiveGoodsValue = Math.Max(0, request.SensitiveGoodsCount) * MinimumTradeValueForRaidOpportunity;
        var valueScore = Math.Max(Math.Max(0, request.ObservedMarketValue), sensitiveGoodsValue);
        var report = state.RecordIntelReport(
            IntelSourceKind.Trade,
            request.FactionId,
            valueScore,
            string.IsNullOrWhiteSpace(request.Summary) ? "Trade revealed valuable goods." : request.Summary);

        if (valueScore < MinimumTradeValueForRaidOpportunity)
        {
            return new IntelReportResult(
                IntelReportStatus.Accepted,
                "Trade intel recorded without a raid opportunity.",
                report,
                null);
        }

        var opportunity = state.CreateRaidOpportunity(
            request.FactionId,
            report.Id,
            Math.Max(1, valueScore / CombatantDemandValueDivisor),
            report.Summary);

        return new IntelReportResult(
            IntelReportStatus.Accepted,
            "Trade intel created a raid opportunity.",
            report,
            opportunity);
    }
}

public static class RaidOpportunityService
{
    public static bool TryConsumeBestOpportunity(
        WorldState state,
        string factionId,
        out RaidOpportunity? opportunity)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(factionId))
        {
            opportunity = null;
            return false;
        }

        var active = state.RaidOpportunities
            .Where(candidate =>
                candidate.Status == RaidOpportunityStatus.Active
                && string.Equals(candidate.FactionId, factionId, StringComparison.Ordinal))
            .OrderByDescending(candidate => candidate.CombatantDemand)
            .ThenBy(candidate => candidate.Id.Value)
            .FirstOrDefault();

        if (active == null)
        {
            opportunity = null;
            return false;
        }

        opportunity = state.ConsumeRaidOpportunity(active.Id);
        return true;
    }
}
