namespace LivingWorld.Core;

public sealed record CaravanMovementRequest(int Tick);

public sealed record CaravanMovementResult(int Arrived);

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
        foreach (var caravan in state.Caravans
            .Where(caravan => caravan.Status == CaravanStatus.Traveling && request.Tick >= caravan.ArrivalTick)
            .OrderBy(caravan => caravan.Id.Value)
            .ToList())
        {
            var updated = state.MarkCaravanArrived(caravan.Id);
            if (updated.Status == CaravanStatus.Arrived)
            {
                arrived++;
            }
        }

        return new CaravanMovementResult(arrived);
    }
}
