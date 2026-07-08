using System;
using System.Linq;

namespace LivingWorld.Core;

public sealed record ConflictResolutionResult(int ResolvedConflicts);

/// <summary>
/// Ends wars that have been decided. A conflict resolves when one belligerent is decisively broken —
/// a large war-exhaustion gap, or one side has lost all its active settlements. This is the missing
/// piece that lets wars actually conclude (and, with it, player victory rewards). Deterministic; no RNG.
/// </summary>
public static class ConflictResolutionService
{
    public const int ExhaustionGapToResolve = 30;

    public static ConflictResolutionResult SimulateDay(WorldState state, int tick)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var resolvedCount = 0;
        foreach (var conflict in state.Conflicts
            .Where(candidate => candidate.Status == WorldConflictStatus.Active)
            .OrderBy(candidate => candidate.Id.Value)
            .ToList())
        {
            var aSettlements = CountActiveSettlements(state, conflict.FactionA);
            var bSettlements = CountActiveSettlements(state, conflict.FactionB);
            var exhaustionGap = Math.Abs(conflict.WarExhaustionA - conflict.WarExhaustionB);

            // Exactly one side wiped out is a decisive end even without an exhaustion gap.
            var settlementWipe = (aSettlements == 0) != (bSettlements == 0);
            if (exhaustionGap < ExhaustionGapToResolve && !settlementWipe)
            {
                continue;
            }

            // Exhaustion decides the winner (fewer losses prevails); a settlement wipe breaks a tie.
            var winner = WinnerOf(conflict)
                ?? (aSettlements == 0 ? conflict.FactionB : conflict.FactionA);

            state.RecordConflictForLedger(conflict with
            {
                Status = WorldConflictStatus.Resolved,
                StatusTick = Math.Max(0, tick)
            });
            state.RecordEvent(
                WorldEventKind.ConflictUpdated,
                conflict.Id,
                $"Conflict {conflict.Id} resolved: {winner} prevailed.");
            resolvedCount++;
        }

        return new ConflictResolutionResult(resolvedCount);
    }

    /// <summary>The faction that took fewer losses (lower war exhaustion) prevailed; null on a draw.</summary>
    public static string? WinnerOf(WorldConflict conflict)
    {
        if (conflict == null)
        {
            throw new ArgumentNullException(nameof(conflict));
        }

        if (conflict.WarExhaustionA == conflict.WarExhaustionB)
        {
            return null;
        }

        return conflict.WarExhaustionA < conflict.WarExhaustionB ? conflict.FactionA : conflict.FactionB;
    }

    private static int CountActiveSettlements(WorldState state, string factionId)
    {
        return state.Settlements.Count(settlement =>
            settlement.IsActive && string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal));
    }
}
