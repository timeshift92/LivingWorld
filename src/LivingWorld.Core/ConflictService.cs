namespace LivingWorld.Core;

public static class ConflictService
{
    public static WorldConflict GetOrCreateConflict(
        WorldState state,
        string factionA,
        string factionB,
        int tick)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var (left, right) = NormalizePair(factionA, factionB);
        var existing = state.Conflicts
            .Where(conflict => conflict.IsPair(left, right))
            .OrderByDescending(conflict => conflict.StartedTick)
            .FirstOrDefault();
        if (existing != null)
        {
            if (existing.Status == WorldConflictStatus.Truce && tick >= existing.TruceExpiresTick)
            {
                return state.RecordConflictForLedger(existing with
                {
                    Status = WorldConflictStatus.Active,
                    StatusTick = Math.Max(0, tick)
                });
            }

            return existing;
        }

        var conflict = state.CreateConflictForLedger(left, right, Math.Max(0, tick));
        state.RecordEvent(
            WorldEventKind.ConflictStarted,
            conflict.Id,
            $"Conflict {conflict.Id} started between {left} and {right}.");
        return conflict;
    }

    public static bool IsTruceActive(WorldState state, string factionA, string factionB, int tick)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var conflict = state.Conflicts
            .Where(candidate => candidate.IsPair(factionA, factionB))
            .OrderByDescending(candidate => candidate.StartedTick)
            .FirstOrDefault();
        return conflict != null
            && conflict.Status == WorldConflictStatus.Truce
            && tick < conflict.TruceExpiresTick;
    }

    public static WorldConflict StartTruce(
        WorldState state,
        string factionA,
        string factionB,
        int startTick,
        int expiresTick)
    {
        var conflict = GetOrCreateConflict(state, factionA, factionB, startTick);
        var updated = state.RecordConflictForLedger(conflict with
        {
            Status = WorldConflictStatus.Truce,
            StatusTick = Math.Max(0, startTick),
            TruceExpiresTick = Math.Max(startTick, expiresTick)
        });
        state.RecordEvent(
            WorldEventKind.ConflictTruceStarted,
            updated.Id,
            $"Conflict {updated.Id} truce until {updated.TruceExpiresTick}.");
        return updated;
    }

    public static WorldConflict RecordBattleOutcome(
        WorldState state,
        EntityId conflictId,
        string attackerFactionId,
        string defenderFactionId,
        int attackerLosses,
        int defenderLosses,
        EntityId? capturedSettlementId,
        int tick)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var conflict = state.GetConflict(conflictId)
            ?? throw new InvalidOperationException($"Conflict {conflictId} does not exist.");
        var updated = AddExhaustion(conflict, attackerFactionId, Math.Max(0, attackerLosses));
        updated = AddExhaustion(updated, defenderFactionId, Math.Max(0, defenderLosses)) with
        {
            Status = WorldConflictStatus.Active,
            StatusTick = Math.Max(0, tick)
        };
        updated = state.RecordConflictForLedger(updated);

        if (capturedSettlementId.HasValue)
        {
            state.RecordConflictClaimForLedger(new ConflictClaim(
                updated.Id,
                capturedSettlementId.Value,
                attackerFactionId,
                Math.Max(0, tick)));
            state.RecordEvent(
                WorldEventKind.ConflictClaimRecorded,
                updated.Id,
                $"Conflict {updated.Id} claim: {attackerFactionId} captured {capturedSettlementId.Value}.");
        }

        state.RecordEvent(
            WorldEventKind.ConflictUpdated,
            updated.Id,
            $"Conflict {updated.Id} battle losses: {attackerFactionId} {attackerLosses}, {defenderFactionId} {defenderLosses}.");
        return updated;
    }

    public static WorldConflict RecordBattleOutcome(
        WorldState state,
        string attackerFactionId,
        string defenderFactionId,
        int attackerLosses,
        int defenderLosses,
        EntityId? capturedSettlementId,
        int tick)
    {
        var conflict = GetOrCreateConflict(state, attackerFactionId, defenderFactionId, tick);
        return RecordBattleOutcome(
            state,
            conflict.Id,
            attackerFactionId,
            defenderFactionId,
            attackerLosses,
            defenderLosses,
            capturedSettlementId,
            tick);
    }

    public static WorldConflict RecordWarRefugees(
        WorldState state,
        EntityId conflictId,
        string factionId,
        int refugeesCreated,
        int tick)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var conflict = state.GetConflict(conflictId)
            ?? throw new InvalidOperationException($"Conflict {conflictId} does not exist.");
        if (!conflict.Involves(factionId))
        {
            throw new InvalidOperationException($"Conflict {conflictId} does not involve {factionId}.");
        }

        var updated = state.RecordConflictForLedger(conflict with
        {
            RefugeesCreated = conflict.RefugeesCreated + Math.Max(0, refugeesCreated),
            StatusTick = Math.Max(0, tick)
        });
        state.RecordEvent(
            WorldEventKind.WarRefugeesRecorded,
            updated.Id,
            $"Conflict {updated.Id} recorded {Math.Max(0, refugeesCreated)} refugees from {factionId}.");
        return updated;
    }

    private static WorldConflict AddExhaustion(WorldConflict conflict, string factionId, int amount)
    {
        if (string.Equals(conflict.FactionA, factionId, StringComparison.Ordinal))
        {
            return conflict with { WarExhaustionA = conflict.WarExhaustionA + amount };
        }

        if (string.Equals(conflict.FactionB, factionId, StringComparison.Ordinal))
        {
            return conflict with { WarExhaustionB = conflict.WarExhaustionB + amount };
        }

        return conflict;
    }

    private static (string Left, string Right) NormalizePair(string factionA, string factionB)
    {
        if (string.IsNullOrWhiteSpace(factionA))
        {
            throw new ArgumentException("Faction cannot be empty.", nameof(factionA));
        }

        if (string.IsNullOrWhiteSpace(factionB))
        {
            throw new ArgumentException("Faction cannot be empty.", nameof(factionB));
        }

        return string.CompareOrdinal(factionA, factionB) <= 0
            ? (factionA, factionB)
            : (factionB, factionA);
    }
}
