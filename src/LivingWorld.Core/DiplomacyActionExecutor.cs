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

        var crew = TravelCrewService.FindAvailableCrew(state, source.Id);
        if (crew == null)
        {
            return false;
        }

        // The mission travels to a settlement of the target faction (for the world-map marker + tile).
        var targetSettlement = WorldWarTargetSelector.FindDiplomacyTargetSettlement(state, factionId, targetFaction);
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
        var mission = state.DispatchMission(
            WorldMissionKind.Diplomat,
            factionId,
            source.Id,
            targetSettlement.Id,
            request.Tick,
            arrivalTick,
            targetFactionId: targetFaction,
            amount: delta,
            crewCitizenId: crew.Id);
        if (TravelCrewService.ReserveCrew(state, source.Id, mission.Id, crew.Id, "diplomatic mission launched"))
        {
            return true;
        }

        state.RemoveMissionForLedger(mission.Id);
        return false;
    }
}
