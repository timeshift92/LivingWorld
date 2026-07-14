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
            .Where(candidate => candidate.Status == WorldMissionStatus.Traveling)
            .OrderBy(candidate => candidate.Id.Value)
            .ToList())
        {
            if (mission.Phase == WorldTransitPhase.Returning)
            {
                if (request.Tick >= mission.ReturnArrivalTick)
                {
                    state.CompleteMissionReturn(mission.Id);
                }

                continue;
            }

            var endpointAvailable = mission.TargetsPlayerContact
                && state.PlayerContactEndpoint is { IsAvailable: true } endpoint
                && string.Equals(endpoint.StableKey, mission.TargetContactKey, StringComparison.Ordinal)
                && string.Equals(endpoint.FactionId, mission.TargetFactionId, StringComparison.Ordinal);
            if (!state.IsActiveSettlement(mission.OriginSettlementId)
                || (mission.TargetsPlayerContact
                    ? !endpointAvailable
                    : !mission.TargetSettlementId.HasValue
                        || !state.IsActiveSettlement(mission.TargetSettlementId.Value)))
            {
                state.RecallMission(mission.Id, "mission endpoint unavailable");
                continue;
            }

            var target = mission.TargetSettlementId.HasValue
                ? state.GetSettlement(mission.TargetSettlementId.Value)
                : null;
            if (!mission.TargetsPlayerContact
                && mission.Kind == WorldMissionKind.Diplomat
                && !string.Equals(target!.FactionId, mission.TargetFactionId, StringComparison.Ordinal))
            {
                state.RecallMission(mission.Id, "diplomatic target changed ownership");
                continue;
            }

            if (mission.Phase == WorldTransitPhase.Outbound)
            {
                if (mission.ArrivalTick <= request.Tick)
                {
                    state.SetMissionPhase(mission.Id, WorldTransitPhase.AtTarget);
                }

                continue;
            }

            if (mission.Phase != WorldTransitPhase.AtTarget || request.Tick <= mission.StatusTick)
            {
                continue;
            }

            switch (mission.Kind)
            {
                case WorldMissionKind.Scout:
                    if (!mission.TargetSettlementId.HasValue)
                    {
                        state.RecallMission(mission.Id, "scout mission has no settlement target");
                        continue;
                    }

                    var summary = $"Scouts from {mission.OriginSettlementId} surveyed {mission.TargetSettlementId}.";
                    state.RecordIntelReport(IntelSourceKind.Scout, mission.FactionId, Math.Max(1, mission.Amount), summary);
                    state.RecordFactionSettlementIntel(
                        mission.FactionId,
                        mission.TargetSettlementId.Value,
                        IntelSourceKind.Scout,
                        request.Tick,
                        Math.Max(1, Math.Min(100, mission.Amount)));
                    // NPC scouts inform their own faction ledger. They must not grant the player
                    // omniscient knowledge merely because the mission exists in the simulation.
                    if (state.IsPlayerFaction(mission.FactionId))
                    {
                        PlayerKnowledgeService.RecordScoutSettlementInfo(state, mission.TargetSettlementId.Value, summary);
                    }
                    scouting++;
                    break;

                case WorldMissionKind.Diplomat:
                    state.RecordEvent(
                        WorldEventKind.DiplomaticMissionArrived,
                        mission.Id,
                        mission.TargetsPlayerContact
                            ? $"Diplomats from {mission.OriginSettlementId} reached player contact {mission.TargetContactKey} on behalf of {mission.FactionId}."
                            : $"Diplomats from {mission.OriginSettlementId} reached {mission.TargetSettlementId} on behalf of {mission.FactionId}.");
                    if (!state.IsPlayerFaction(mission.TargetFactionId))
                    {
                        var before = DiplomacyService.GetGoodwill(state, mission.FactionId, mission.TargetFactionId);
                        var after = DiplomacyService.AdjustGoodwill(state, mission.FactionId, mission.TargetFactionId, mission.Amount);
                        if (after != before)
                        {
                            state.RecordEvent(
                                WorldEventKind.DiplomaticMissionSent,
                                mission.OriginSettlementId,
                                $"Diplomats from {mission.OriginSettlementId} improved relations between {mission.FactionId} and {mission.TargetFactionId} to {after}.");
                        }
                    }

                    diplomatic++;
                    break;
            }

            state.SetMissionPhase(mission.Id, WorldTransitPhase.Returning, effectApplied: true);
        }

        return new WorldMissionResult(scouting, diplomatic);
    }
}
