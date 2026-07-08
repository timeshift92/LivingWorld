using System;
using System.Linq;

namespace LivingWorld.Core;

internal static class ScoutingActionExecutor
{
    private const int ScoutIntelValue = 100;

    public static bool Execute(WorldState state, string factionId, WorldWarRequest request)
    {
        var source = WorldWarTargetSelector.FindReadySourceSettlement(state, factionId);
        var target = WorldWarTargetSelector.FindScoutingTarget(state, factionId);
        if (source == null || target == null)
        {
            return false;
        }

        // One scouting party per faction in transit at a time — do not stack new ones each day.
        if (state.Missions.Any(mission =>
            mission.Status == WorldMissionStatus.Traveling
            && mission.Kind == WorldMissionKind.Scout
            && string.Equals(mission.FactionId, factionId, StringComparison.Ordinal)))
        {
            return false;
        }

        var arrivalTick = request.Tick + (Math.Max(1, request.TravelDays) * 60_000);
        state.DispatchMission(
            WorldMissionKind.Scout,
            factionId,
            source.Id,
            target.Id,
            request.Tick,
            arrivalTick,
            amount: ScoutIntelValue);
        return true;
    }
}
