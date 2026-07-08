using System;
using System.Linq;

namespace LivingWorld.Core;

public sealed record CaravanPruneRequest(int CurrentTick, int RetentionDays);

public sealed record CaravanPruneResult(int Pruned);

/// <summary>
/// Prunes terminal-state caravans (arrived or destroyed) whose intended arrival is older than the
/// retention window, mirroring <see cref="ArmyMovementPruneService"/>. Without this, completed and
/// lost caravans accumulate in the ledger and the save forever. Terminal caravans own no resources
/// (delivered on arrival, zeroed on destroy), so removing them is conservation-safe.
/// </summary>
public static class CaravanPruneService
{
    public static CaravanPruneResult Prune(WorldState state, CaravanPruneRequest request)
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
        var toPrune = state.Caravans
            .Where(caravan => caravan.Status != CaravanStatus.Traveling)
            .Where(caravan => caravan.ArrivalTick < pruneBeforeOrAt)
            .OrderBy(caravan => caravan.Id.Value)
            .Select(caravan => caravan.Id)
            .ToList();

        foreach (var caravanId in toPrune)
        {
            state.RemoveCaravanForLedger(caravanId);
        }

        return new CaravanPruneResult(toPrune.Count);
    }
}
