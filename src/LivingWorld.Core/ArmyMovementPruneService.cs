using System;
using System.Linq;

namespace LivingWorld.Core;

public sealed record ArmyMovementPruneRequest(int CurrentTick, int RetentionDays);

public sealed record ArmyMovementPruneResult(int Pruned);

public static class ArmyMovementPruneService
{
    public static ArmyMovementPruneResult Prune(WorldState state, ArmyMovementPruneRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.CurrentTick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Prune tick cannot be negative.");
        }

        if (request.RetentionDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Retention days cannot be negative.");
        }

        state.AdvanceToTick(request.CurrentTick);

        var retentionTicks = request.RetentionDays * 60_000;
        var pruneBeforeOrAt = request.CurrentTick - retentionTicks;
        var toPrune = state.ArmyMovements
            .Where(movement => movement.Status != ArmyMovementStatus.Traveling)
            .Where(movement => movement.StatusTick < pruneBeforeOrAt)
            .OrderBy(movement => movement.ArmyId.Value)
            .Select(movement => movement.ArmyId)
            .ToList();

        foreach (var armyId in toPrune)
        {
            state.RemoveArmyMovementForLedger(armyId);
        }

        return new ArmyMovementPruneResult(toPrune.Count);
    }
}
