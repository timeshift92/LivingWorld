using System;
using System.Collections.Generic;
using System.Linq;

namespace LivingWorld.Core;

public sealed record PlayerVictory(long ConflictId, string AllyFactionId, string EnemyFactionId);

/// <summary>
/// Player victory rewards (Slice 4 of player war participation). When a war the player joined resolves
/// in the player-allied side's favour, the player shares in the win with a goodwill windfall from the
/// victorious ally. Returns the victories newly rewarded so the caller can persist them (a war is
/// rewarded once) and announce them.
/// </summary>
public static class PlayerVictoryService
{
    public const int VictoryGoodwill = 15;

    public static IReadOnlyList<PlayerVictory> GrantVictoryRewards(
        WorldState state,
        ISet<long> alreadyRewardedConflictIds,
        int tick)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (alreadyRewardedConflictIds == null)
        {
            throw new ArgumentNullException(nameof(alreadyRewardedConflictIds));
        }

        var playerId = state.PlayerFactionId;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Array.Empty<PlayerVictory>();
        }

        var rewarded = new List<PlayerVictory>();
        foreach (var conflict in state.Conflicts
            .Where(candidate => candidate.Status == WorldConflictStatus.Resolved
                && !alreadyRewardedConflictIds.Contains(candidate.Id.Value))
            .OrderBy(candidate => candidate.Id.Value))
        {
            var winner = ConflictResolutionService.WinnerOf(conflict);
            if (winner == null
                || string.Equals(winner, playerId, StringComparison.Ordinal)
                || !AllianceService.IsAlliedWithPlayer(state, winner))
            {
                continue;
            }

            var enemy = string.Equals(winner, conflict.FactionA, StringComparison.Ordinal)
                ? conflict.FactionB
                : conflict.FactionA;

            DiplomacyService.AdjustGoodwill(state, playerId!, winner, VictoryGoodwill);
            state.RecordEvent(
                WorldEventKind.ConflictUpdated,
                conflict.Id,
                $"Player shares in {winner}'s victory over {enemy} (conflict {conflict.Id}).");
            rewarded.Add(new PlayerVictory(conflict.Id.Value, winner, enemy));
        }

        return rewarded;
    }
}
