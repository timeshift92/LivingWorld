namespace LivingWorld.Core;

public sealed record DrifterFoundingRequest(
    int Tick,
    int MinFounders,
    int LeaderAptitudeThreshold)
{
    // Runtime callers provide real RimWorld-backed faction ids. Null preserves the standalone Core
    // behavior for API consumers that deliberately own their own faction materialization layer.
    public IReadOnlyCollection<string>? EligibleFactionIds { get; init; }

    public string? PreferredFactionId { get; init; }

    public string? PhysicalStableKey { get; init; }

    public EntityId? SponsorSettlementId { get; init; }
}

public sealed record DrifterFoundingResult(
    bool Founded,
    string Reason,
    EntityId? SettlementId,
    string? FactionId,
    int FounderCount,
    bool IsRaiderBand);

/// <summary>
/// Lets a group of drifters found a brand-new settlement
/// when one of them is a capable enough leader.
///
/// The leader's profile decides both whether founding happens (best aptitude must clear the
/// threshold) and what is founded: a combat-minded leader forms a raider band, an organizer
/// forms a settlement. The threshold and group size are tunable knobs, not fixed design.
///
/// Runtime callers pass real faction ids so every founded settlement can be placed on the RimWorld
/// globe. Standalone Core consumers may omit them and materialize the synthetic faction themselves.
/// </summary>
public static class DrifterFoundingService
{
    public static DrifterFoundingResult SimulateFounding(WorldState state, DrifterFoundingRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        state.AdvanceToTick(request.Tick);

        var minFounders = Math.Max(1, request.MinFounders);
        if (state.Drifters.Count < minFounders)
        {
            return NotFounded("Not enough drifters to form a founding group.");
        }

        var leader = state.Drifters
            .OrderByDescending(drifter => drifter.LeadershipAptitude)
            .ThenBy(drifter => drifter.Id.Value)
            .First();

        if (leader.LeadershipAptitude < request.LeaderAptitudeThreshold)
        {
            return NotFounded("No drifter is capable enough to lead a new settlement.");
        }

        var members = state.Drifters
            .Where(drifter => drifter.Id != leader.Id)
            .OrderBy(drifter => drifter.ArrivalTick)
            .ThenBy(drifter => drifter.Id.Value)
            .Take(minFounders - 1)
            .Select(drifter => drifter.Id)
            .ToList();

        var isRaiderBand = leader.LeadsRaiderBand;
        var ordinal = state.Settlements.Count + 1;
        var factionId = SelectFaction(
                state,
                request.EligibleFactionIds,
                isRaiderBand,
                request.PreferredFactionId)
            ?? (request.EligibleFactionIds == null
                ? (isRaiderBand ? "DrifterBand" : "DrifterSettlement") + ordinal
                : null);
        if (string.IsNullOrWhiteSpace(factionId))
        {
            return NotFounded("No physical non-player faction can sponsor the founding group.");
        }

        if (request.EligibleFactionIds != null
            && (string.IsNullOrWhiteSpace(request.PhysicalStableKey)
                || SettlementSlug.ParseTile(request.PhysicalStableKey) < 0))
        {
            return NotFounded("No physical world tile was reserved for the founding group.");
        }

        var sponsor = request.SponsorSettlementId.HasValue
            ? state.GetSettlement(request.SponsorSettlementId.Value)
            : null;
        if (request.EligibleFactionIds != null
            && (sponsor == null
                || !sponsor.IsActive
                || !string.Equals(sponsor.FactionId, factionId, StringComparison.Ordinal)))
        {
            return NotFounded("No active physical settlement can sponsor the founding group.");
        }

        var founderCount = members.Count + 1;
        var foodCost = founderCount * 3;
        var steelCost = founderCount * 10;
        var componentCost = Math.Max(1, founderCount / 2);
        if (sponsor != null
            && (state.GetOwnedResourceQuantity(sponsor.Id, "PackagedSurvivalMeal") < foodCost
                || state.GetOwnedResourceQuantity(sponsor.Id, "Steel") < steelCost
                || state.GetOwnedResourceQuantity(sponsor.Id, "ComponentIndustrial") < componentCost))
        {
            return NotFounded("The sponsor cannot provide real food, steel, and components for the founding group.");
        }

        var name = (isRaiderBand ? "Drifter band " : "Drifter settlement ") + ordinal;
        var slug = string.IsNullOrWhiteSpace(request.PhysicalStableKey)
            ? (isRaiderBand ? "drifter-band-" : "drifter-settlement-") + ordinal
            : request.PhysicalStableKey!.Trim();

        var settlement = state.FoundSettlement(slug, name, factionId!, leader.Id, members);
        if (sponsor != null)
        {
            TransferSponsorResource(state, sponsor.Id, settlement.Id, "PackagedSurvivalMeal", foodCost);
            TransferSponsorResource(state, sponsor.Id, settlement.Id, "Steel", steelCost);
            TransferSponsorResource(state, sponsor.Id, settlement.Id, "ComponentIndustrial", componentCost);
        }

        return new DrifterFoundingResult(
            true,
            $"Founded {factionId} with {members.Count + 1} drifters.",
            settlement.Id,
            factionId,
            founderCount,
            isRaiderBand);
    }

    private static DrifterFoundingResult NotFounded(string reason)
    {
        return new DrifterFoundingResult(false, reason, null, null, 0, false);
    }

    public static string? SelectFaction(
        WorldState state,
        IReadOnlyCollection<string>? eligibleFactionIds,
        bool raiderBand,
        string? preferredFactionId = null)
    {
        if (eligibleFactionIds == null)
        {
            return null;
        }

        var eligible = new HashSet<string>(
            eligibleFactionIds.Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(state.PlayerFactionId))
        {
            eligible.Remove(state.PlayerFactionId!);
        }

        if (!string.IsNullOrWhiteSpace(preferredFactionId) && eligible.Contains(preferredFactionId!))
        {
            return preferredFactionId;
        }

        var candidates = state.Settlements
            .Where(settlement => settlement.IsActive && eligible.Contains(settlement.FactionId))
            .GroupBy(settlement => settlement.FactionId, StringComparer.Ordinal)
            .Select(group => new
            {
                FactionId = group.Key,
                SettlementCount = group.Count(),
                Population = group.Sum(settlement => state.GetSettlementPopulation(settlement.Id).Total),
                Preferred = state.IsFactionIrreconcilable(group.Key) == raiderBand,
            })
            .OrderByDescending(candidate => candidate.Preferred)
            .ThenBy(candidate => candidate.SettlementCount)
            .ThenBy(candidate => candidate.Population)
            .ThenBy(candidate => candidate.FactionId, StringComparer.Ordinal)
            .FirstOrDefault();
        return candidates?.FactionId;
    }

    private static void TransferSponsorResource(
        WorldState state,
        EntityId sponsorId,
        EntityId settlementId,
        string resourceKey,
        int quantity)
    {
        var transfer = state.TransferResource(
            sponsorId,
            settlementId,
            resourceKey,
            quantity,
            "drifter settlement founding supplies");
        if (transfer.Status != OwnershipTransferStatus.Success)
        {
            throw new InvalidOperationException(transfer.Reason);
        }
    }
}
