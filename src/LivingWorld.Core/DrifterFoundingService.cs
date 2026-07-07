namespace LivingWorld.Core;

public sealed record DrifterFoundingRequest(
    int Tick,
    int MinFounders,
    int LeaderAptitudeThreshold);

public sealed record DrifterFoundingResult(
    bool Founded,
    string Reason,
    EntityId? SettlementId,
    string? FactionId,
    int FounderCount,
    bool IsRaiderBand);

/// <summary>
/// Lets a group of drifters found a brand-new settlement (and thus a new ledger faction)
/// when one of them is a capable enough leader.
///
/// The leader's profile decides both whether founding happens (best aptitude must clear the
/// threshold) and what is founded: a combat-minded leader forms a raider band, an organizer
/// forms a settlement. The threshold and group size are tunable knobs, not fixed design.
///
/// This is ledger-only: a "new faction" is just a fresh faction id. Materializing it as a real
/// RimWorld <c>Faction</c> is a later, flagged step (see docs/design/population-flow.md §11).
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
        var factionId = (isRaiderBand ? "DrifterBand" : "DrifterSettlement") + ordinal;
        var name = (isRaiderBand ? "Drifter band " : "Drifter settlement ") + ordinal;
        var slug = (isRaiderBand ? "drifter-band-" : "drifter-settlement-") + ordinal;

        var settlement = state.FoundSettlement(slug, name, factionId, leader.Id, members);

        return new DrifterFoundingResult(
            true,
            $"Founded {factionId} with {members.Count + 1} drifters.",
            settlement.Id,
            factionId,
            members.Count + 1,
            isRaiderBand);
    }

    private static DrifterFoundingResult NotFounded(string reason)
    {
        return new DrifterFoundingResult(false, reason, null, null, 0, false);
    }
}
