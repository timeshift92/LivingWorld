namespace LivingWorld.Core;

public sealed record WorldMissionPruneRequest(int CurrentTick, int RetentionDays);

public sealed record WorldMissionPruneResult(int Pruned);

public static class WorldMissionPruneService
{
    public static WorldMissionPruneResult Prune(WorldState state, WorldMissionPruneRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.CurrentTick < 0 || request.RetentionDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }

        var cutoff = request.CurrentTick - (request.RetentionDays * 60_000);
        var ids = state.Missions
            .Where(mission => mission.Status != WorldMissionStatus.Traveling)
            .Where(mission => Math.Max(mission.StatusTick, mission.ArrivalTick) <= cutoff)
            .OrderBy(mission => mission.Id.Value)
            .Select(mission => mission.Id)
            .ToList();
        foreach (var id in ids)
        {
            state.RemoveMissionForLedger(id);
        }

        return new WorldMissionPruneResult(ids.Count);
    }
}
