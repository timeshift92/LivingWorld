using System;
using System.Linq;

namespace LivingWorld.Core;

public sealed record ArmyMovementRequest(int Tick);

public sealed record ArmyMovementResult(int Arrived, int Recalled, int Disbanded);

/// <summary>
/// Advances travelling armies one day: a Traveling army whose army no longer exists is
/// Disbanded, one whose target settlement is gone is Recalled, and one whose ETA has passed is
/// marked Arrived (ready for battle resolution). Deterministic, runs on the daily scheduler
/// path — not per game tick — and reads only army/settlement lookups, no per-citizen scan.
///
/// Combatant accounting (an "empty" army losing on arrival) belongs to battle resolution, not
/// to movement, so an army with no members still arrives and is resolved there.
/// </summary>
public static class ArmyMovementService
{
    public static ArmyMovementResult SimulateDay(WorldState state, ArmyMovementRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        state.AdvanceToTick(request.Tick);

        var arrived = 0;
        var recalled = 0;
        var disbanded = 0;

        foreach (var movement in state.ArmyMovements
            .Where(movement => movement.Status == ArmyMovementStatus.Traveling)
            .OrderBy(movement => movement.ArmyId.Value)
            .ToList())
        {
            if (state.GetArmy(movement.ArmyId) == null)
            {
                state.SetArmyMovementStatus(movement.ArmyId, ArmyMovementStatus.Disbanded);
                disbanded++;
            }
            else if (!state.IsActiveSettlement(movement.TargetSettlementId))
            {
                // Destroyed/abandoned settlements stay in the ledger as ruins (never removed), so a null
                // check would let the army march onto a ruin and "arrive". Recall it instead, matching the
                // caravan (MarkCaravanArrived) and mission paths that both gate on IsActiveSettlement.
                state.SetArmyMovementStatus(movement.ArmyId, ArmyMovementStatus.Recalled);
                recalled++;
            }
            else if (request.Tick >= movement.ArrivalTick)
            {
                state.SetArmyMovementStatus(movement.ArmyId, ArmyMovementStatus.Arrived);
                arrived++;
            }
        }

        return new ArmyMovementResult(arrived, recalled, disbanded);
    }
}
