namespace LivingWorld.Core;

public sealed record CaravanMovementRequest(int Tick);

public sealed record CaravanMovementResult(int Arrived)
{
    public int ReachedTarget { get; init; }

    public int BeganReturn { get; init; }
}

public static class CaravanMovementService
{
    public static CaravanMovementResult SimulateDay(WorldState state, CaravanMovementRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        state.AdvanceToTick(request.Tick);

        var arrived = 0;
        var reachedTarget = 0;
        var beganReturn = 0;
        foreach (var caravan in state.Caravans
            .Where(caravan => caravan.Status == CaravanStatus.Traveling)
            .OrderBy(caravan => caravan.Id.Value)
            .ToList())
        {
            if (caravan.Phase == WorldTransitPhase.Outbound && request.Tick >= caravan.ArrivalTick)
            {
                var target = state.GetSettlement(caravan.TargetSettlementId);
                if (target?.IsActive != true
                    || DiplomacyService.GetStance(state, caravan.FactionId, target.FactionId) == RelationStance.Hostile)
                {
                    state.BeginCaravanReturn(caravan.Id, completeAsRecalled: true, "target settlement unavailable or hostile");
                    beganReturn++;
                }
                else
                {
                    state.MarkCaravanArrived(caravan.Id);
                    reachedTarget++;
                }
            }
            else if (caravan.Phase == WorldTransitPhase.AtTarget && request.Tick > caravan.StatusTick)
            {
                var target = state.GetSettlement(caravan.TargetSettlementId);
                if (target?.IsActive != true
                    || DiplomacyService.GetStance(state, caravan.FactionId, target.FactionId) == RelationStance.Hostile)
                {
                    state.BeginCaravanReturn(caravan.Id, completeAsRecalled: true, "trade target became unavailable or hostile");
                }
                else
                {
                    state.ExecuteCaravanTradeAndBeginReturn(caravan.Id);
                }
                beganReturn++;
            }
            else if (caravan.Phase == WorldTransitPhase.Returning && request.Tick >= caravan.ReturnArrivalTick)
            {
                var completed = state.CompleteCaravanReturn(caravan.Id);
                if (completed.Status == CaravanStatus.Arrived)
                {
                    arrived++;
                }
            }
        }

        return new CaravanMovementResult(arrived)
        {
            ReachedTarget = reachedTarget,
            BeganReturn = beganReturn,
        };
    }
}
