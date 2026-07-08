using System;
using System.Linq;

namespace LivingWorld.Core;

public sealed record WorldMissionRequest(int Tick);

public sealed record WorldMissionResult(int ScoutingArrivals, int DiplomaticArrivals);

/// <summary>
/// Advances in-transit world-war missions and applies their effect on arrival: a scout records intel
/// about the target, a diplomat improves relations. Arrived missions are removed immediately (they
/// carry nothing to conserve). Mirrors <see cref="ArmyMovementService"/>/<see cref="CaravanMovementService"/>.
/// </summary>
public static class WorldMissionService
{
    public static WorldMissionResult SimulateDay(WorldState state, WorldMissionRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.Tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Mission tick cannot be negative.");
        }

        state.AdvanceToTick(request.Tick);

        var scouting = 0;
        var diplomatic = 0;

        foreach (var mission in state.Missions
            .Where(candidate => candidate.Status == WorldMissionStatus.Traveling && candidate.ArrivalTick <= request.Tick)
            .OrderBy(candidate => candidate.Id.Value)
            .ToList())
        {
            switch (mission.Kind)
            {
                case WorldMissionKind.Scout:
                    var summary = $"Scouts from {mission.OriginSettlementId} surveyed {mission.TargetSettlementId}.";
                    state.RecordIntelReport(IntelSourceKind.Scout, mission.FactionId, Math.Max(1, mission.Amount), summary);
                    PlayerKnowledgeService.RecordScoutSettlementInfo(state, mission.TargetSettlementId, summary);
                    scouting++;
                    break;

                case WorldMissionKind.Diplomat:
                    var before = DiplomacyService.GetGoodwill(state, mission.FactionId, mission.TargetFactionId);
                    var after = DiplomacyService.AdjustGoodwill(state, mission.FactionId, mission.TargetFactionId, mission.Amount);
                    if (after != before)
                    {
                        state.RecordEvent(
                            WorldEventKind.DiplomaticMissionSent,
                            mission.OriginSettlementId,
                            $"Diplomats from {mission.OriginSettlementId} improved relations between {mission.FactionId} and {mission.TargetFactionId} to {after}.");
                    }

                    diplomatic++;
                    break;
            }

            state.RemoveMissionForLedger(mission.Id);
        }

        return new WorldMissionResult(scouting, diplomatic);
    }
}
