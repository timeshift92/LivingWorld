using System;
using System.Linq;

namespace LivingWorld.Core;

internal static class DiplomacyActionExecutor
{
    public static bool Execute(WorldState state, string factionId, WorldWarRequest request)
    {
        var source = WorldWarTargetSelector.FindReadySourceSettlement(state, factionId);
        var targetFaction = WorldWarTargetSelector.FindDiplomacyTargetFaction(state, factionId);
        var delta = Math.Abs(request.DiplomatGoodwill);
        if (source == null || targetFaction == null || delta <= 0)
        {
            return false;
        }

        // The mission travels to a settlement of the target faction (for the world-map marker + tile).
        var targetSettlement = state.Settlements
            .Where(settlement => string.Equals(settlement.FactionId, targetFaction, StringComparison.Ordinal))
            .OrderBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
        if (targetSettlement == null)
        {
            return false;
        }

        // One diplomatic mission per faction in transit at a time.
        if (state.Missions.Any(mission =>
            mission.Status == WorldMissionStatus.Traveling
            && mission.Kind == WorldMissionKind.Diplomat
            && string.Equals(mission.FactionId, factionId, StringComparison.Ordinal)))
        {
            return false;
        }

        var arrivalTick = request.Tick + (Math.Max(1, request.TravelDays) * 60_000);
        state.DispatchMission(
            WorldMissionKind.Diplomat,
            factionId,
            source.Id,
            targetSettlement.Id,
            request.Tick,
            arrivalTick,
            targetFactionId: targetFaction,
            amount: delta);
        return true;
    }
}
