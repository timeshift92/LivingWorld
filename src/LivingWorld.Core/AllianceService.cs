using System;
using System.Linq;

namespace LivingWorld.Core;

public enum AllianceFormStatus
{
    Formed,
    NoPlayerFaction,
    AllyIsPlayer,
    AllyNotAtWar,
    AllyIrreconcilable
}

public sealed record AllianceFormResult(AllianceFormStatus Status, string Reason);

/// <summary>
/// The player's alliances in the world war (Slice 2 of player war participation). An alliance is
/// modelled with existing persisted state — an <see cref="RelationStance.Ally"/> goodwill relation
/// between the player and the ally faction — so it needs no new save section. The diplomat
/// prerequisite is enforced by the RimWorld layer (the alliance offer only appears once a diplomat
/// has been exchanged); this service enforces the ledger-side rules: the ally must actually be at war
/// and be reconcilable. While allied, defeating the ally's enemy earns the ally's gratitude.
/// </summary>
public static class AllianceService
{
    /// <summary>Goodwill the ally gains toward the player for each enemy settlement the player fells.</summary>
    public const int DefaultAllyGratitude = 8;

    public static AllianceFormResult FormAlliance(WorldState state, string allyFactionId, int tick)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var playerId = state.PlayerFactionId;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return new AllianceFormResult(AllianceFormStatus.NoPlayerFaction, "No player faction.");
        }

        if (string.IsNullOrWhiteSpace(allyFactionId)
            || string.Equals(playerId, allyFactionId, StringComparison.Ordinal))
        {
            return new AllianceFormResult(AllianceFormStatus.AllyIsPlayer, "Cannot ally with the player itself.");
        }

        if (state.IsFactionIrreconcilable(allyFactionId))
        {
            return new AllianceFormResult(AllianceFormStatus.AllyIrreconcilable, $"{allyFactionId} is irreconcilable.");
        }

        if (!IsFactionAtWar(state, allyFactionId))
        {
            return new AllianceFormResult(AllianceFormStatus.AllyNotAtWar, $"{allyFactionId} is not at war.");
        }

        var current = DiplomacyService.GetGoodwill(state, playerId!, allyFactionId);
        if (current < DiplomacyService.AllyThreshold)
        {
            DiplomacyService.AdjustGoodwill(state, playerId!, allyFactionId, DiplomacyService.AllyThreshold - current);
        }

        return new AllianceFormResult(AllianceFormStatus.Formed, $"Allied with {allyFactionId}.");
    }

    public static bool IsAlliedWithPlayer(WorldState state, string factionId)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var playerId = state.PlayerFactionId;
        return !string.IsNullOrWhiteSpace(playerId)
            && !string.IsNullOrWhiteSpace(factionId)
            && !string.Equals(playerId, factionId, StringComparison.Ordinal)
            && DiplomacyService.GetStance(state, playerId!, factionId) == RelationStance.Ally;
    }

    /// <summary>
    /// After the player defeats a faction, every player-allied faction that is at war with the defeated
    /// one gains gratitude (goodwill toward the player). Returns the number of allies credited.
    /// </summary>
    public static int CreditAlliesOnPlayerAttack(WorldState state, string defeatedFactionId, int goodwillBonus, int tick)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var playerId = state.PlayerFactionId;
        if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(defeatedFactionId))
        {
            return 0;
        }

        var allies = state.Settlements
            .Select(settlement => settlement.FactionId)
            .Distinct(StringComparer.Ordinal)
            .Where(faction => !string.Equals(faction, playerId, StringComparison.Ordinal)
                && !string.Equals(faction, defeatedFactionId, StringComparison.Ordinal)
                && IsAlliedWithPlayer(state, faction)
                && IsAtWarWith(state, faction, defeatedFactionId))
            .OrderBy(faction => faction, StringComparer.Ordinal)
            .ToList();

        foreach (var ally in allies)
        {
            DiplomacyService.AdjustGoodwill(state, playerId!, ally, Math.Abs(goodwillBonus));
        }

        return allies.Count;
    }

    private static bool IsFactionAtWar(WorldState state, string factionId)
    {
        return state.Conflicts.Any(conflict =>
            conflict.Status == WorldConflictStatus.Active && conflict.Involves(factionId));
    }

    private static bool IsAtWarWith(WorldState state, string factionId, string enemyId)
    {
        return state.Conflicts.Any(conflict =>
            conflict.Status == WorldConflictStatus.Active && conflict.IsPair(factionId, enemyId));
    }
}
