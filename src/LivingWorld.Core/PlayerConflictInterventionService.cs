using System;
using System.Linq;

namespace LivingWorld.Core;

public sealed record PlayerInterventionResult(int ConflictsPressured, bool AggressionRecorded);

/// <summary>
/// Models the player intervening in the world's wars as a third party (Slice 1 of player war
/// participation). Attacking a settlement applies pressure to the <b>victim</b> faction in every war it
/// is currently fighting — raising the victim's war exhaustion and, on a capture, claiming the
/// settlement — so the player's blows actually help whoever the victim is at war with (including a
/// player ally). It deliberately does <b>not</b> make the player a belligerent or an ally. When the
/// victim is at peace, the attack is recorded as a standalone aggression event so it can later seed
/// diplomacy or revenge. Deterministic; touches only conflict records and append-only events.
/// </summary>
public static class PlayerConflictInterventionService
{
    public static PlayerInterventionResult RecordSettlementAttack(
        WorldState state,
        string victimFactionId,
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
            || string.IsNullOrWhiteSpace(victimFactionId)
            || string.Equals(playerId, victimFactionId, StringComparison.Ordinal))
        {
            // No player faction, or the "victim" is the player itself — never pressure the player.
            return new PlayerInterventionResult(0, false);
        }

        var victimWars = state.Conflicts
            .Where(conflict => conflict.Status == WorldConflictStatus.Active
                && conflict.Involves(victimFactionId))
            .OrderBy(conflict => conflict.Id.Value)
            .ToList();

        if (victimWars.Count == 0)
        {
            // No war to pressure: record the aggression so future diplomacy/revenge can react to it.
            state.RecordEvent(
                WorldEventKind.ConflictUpdated,
                capturedSettlementId,
                $"Player aggression against {victimFactionId} (no active war).");
            return new PlayerInterventionResult(0, true);
        }

        var losses = Math.Max(0, defenderLosses);
        var claimRecorded = false;
        foreach (var war in victimWars)
        {
            // Raise the victim's exhaustion in this war (player is the attacker so the exhaustion lands
            // on the victim/defender; the player is not a participant, so its own losses are a no-op).
            // Attribute a captured settlement to the player once, not once per war.
            var claimForThisWar = !claimRecorded && capturedSettlementId.HasValue ? capturedSettlementId : null;
            ConflictService.RecordBattleOutcome(
                state,
                war.Id,
                attackerFactionId: playerId!,
                defenderFactionId: victimFactionId,
                attackerLosses: 0,
                defenderLosses: losses,
                capturedSettlementId: claimForThisWar,
                tick: Math.Max(0, tick));
            if (claimForThisWar != null)
            {
                claimRecorded = true;
            }
        }

        return new PlayerInterventionResult(victimWars.Count, false);
    }
}
