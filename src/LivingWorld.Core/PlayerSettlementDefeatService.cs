namespace LivingWorld.Core;

public sealed record PlayerSettlementDefeatResult(
    bool Destroyed,
    int RefugeesCreated,
    int ConflictsPressured,
    bool AggressionRecorded,
    EntityId? RuinId);

public static class PlayerSettlementDefeatService
{
    public static PlayerSettlementDefeatResult RecordDefeat(
        WorldState state,
        EntityId settlementId,
        string defeatedFactionId,
        int tick,
        string reason,
        int observedDefenderLosses = 0)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(defeatedFactionId))
        {
            throw new ArgumentException("Defeated faction id cannot be empty.", nameof(defeatedFactionId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Settlement defeat reason cannot be empty.", nameof(reason));
        }

        var settlement = state.GetSettlement(settlementId)
            ?? throw new InvalidOperationException($"Settlement {settlementId} does not exist.");
        if (!settlement.IsActive)
        {
            return new PlayerSettlementDefeatResult(false, 0, 0, false, null);
        }

        var destroyed = SettlementLifecycleService.DestroySettlement(
            state,
            settlementId,
            Math.Max(0, tick),
            reason);

        var intervention = PlayerConflictInterventionService.RecordSettlementAttack(
            state,
            defeatedFactionId,
            Math.Max(0, observedDefenderLosses),
            settlementId,
            Math.Max(0, tick));

        return new PlayerSettlementDefeatResult(
            true,
            destroyed.RefugeesCreated,
            intervention.ConflictsPressured,
            intervention.AggressionRecorded,
            destroyed.Ruin.Id);
    }
}
