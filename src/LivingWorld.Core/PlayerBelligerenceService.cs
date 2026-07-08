namespace LivingWorld.Core;

/// <summary>
/// The player as a belligerent in the world's wars. The player joins by acting on the ground: a
/// decisive attack on an NPC settlement is recorded as a battle in the conflict ledger with the
/// player as the attacker, so it weakens that faction in every war it is fighting and surfaces the
/// player as a participant in the World Conflicts UI. Slice 1 of player war participation.
/// </summary>
public static class PlayerBelligerenceService
{
    /// <summary>
    /// Record a decisive player attack on a non-player faction. Returns the updated conflict, or null
    /// when there is no player faction, no defender, or the defender is the player itself (all no-ops).
    /// </summary>
    public static WorldConflict? RecordPlayerAttack(
        WorldState state,
        string defenderFactionId,
        int defenderLosses,
        EntityId? capturedSettlementId,
        int tick)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var playerId = state.PlayerFactionId;
        if (string.IsNullOrWhiteSpace(playerId)
            || string.IsNullOrWhiteSpace(defenderFactionId)
            || string.Equals(playerId, defenderFactionId, StringComparison.Ordinal))
        {
            return null;
        }

        return ConflictService.RecordBattleOutcome(
            state,
            playerId!,
            defenderFactionId,
            attackerLosses: 0,
            defenderLosses: Math.Max(0, defenderLosses),
            capturedSettlementId,
            Math.Max(0, tick));
    }
}
