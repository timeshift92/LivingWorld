using System;
using System.Linq;

namespace LivingWorld.Core;

public sealed record WorldMissionRequest(int Tick);

public sealed record WorldMissionResult(int ScoutingArrivals, int DiplomaticArrivals);

/// <summary>
/// Advances in-transit world-war missions. Diplomacy applies at the target; scout observations only
/// become faction knowledge after the physical scout returns. Mirrors
/// <see cref="ArmyMovementService"/>/<see cref="CaravanMovementService"/>.
/// </summary>
public static class WorldMissionService
{
    private const int PlayerScoutIntelConfidence = 70;
    private const int PlayerScoutIntelLifetimeTicks = 20 * 60_000;

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
                    var completed = state.CompleteMissionReturn(mission.Id);
                    if (completed.Status == WorldMissionStatus.Arrived
                        && completed.Kind == WorldMissionKind.Scout
                        && completed.ReportCollected)
                    {
                        PublishScoutReport(state, completed, request.Tick);
                        state.MarkMissionEffectApplied(completed.Id);
                        scouting++;
                    }
                }

                continue;
            }

            var endpointAvailable = mission.TargetsPlayerContact
                && state.PlayerContactEndpoint is { IsAvailable: true } endpoint
                && string.Equals(endpoint.StableKey, mission.TargetContactKey, StringComparison.Ordinal)
                && string.Equals(endpoint.FactionId, mission.TargetFactionId, StringComparison.Ordinal);
            var origin = state.GetSettlement(mission.OriginSettlementId);
            if (origin?.IsActive != true
                || !string.Equals(origin.FactionId, mission.FactionId, StringComparison.Ordinal)
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
            var targetFactionId = target?.FactionId ?? string.Empty;
            if (!mission.TargetsPlayerContact
                && ((!string.IsNullOrWhiteSpace(mission.TargetFactionId)
                        && !string.Equals(targetFactionId, mission.TargetFactionId, StringComparison.Ordinal))
                    || (mission.Kind == WorldMissionKind.Scout
                        && (string.Equals(targetFactionId, mission.FactionId, StringComparison.Ordinal)
                            || DiplomacyService.GetStance(state, mission.FactionId, targetFactionId) == RelationStance.Ally))))
            {
                state.RecallMission(mission.Id, "mission target ownership or relations changed");
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
                    if (mission.TargetsPlayerContact)
                    {
                        var playerEndpoint = state.PlayerContactEndpoint!;
                        state.CollectScoutReport(
                            mission.Id,
                            playerEndpoint.ValueBand,
                            Math.Max(1, Math.Min(100, playerEndpoint.CombatantDemand)),
                            playerEndpoint.StableKey);
                        break;
                    }

                    if (!mission.TargetSettlementId.HasValue)
                    {
                        state.RecallMission(mission.Id, "scout mission has no settlement target");
                        continue;
                    }

                    state.CollectScoutReport(mission.Id);
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

            state.SetMissionPhase(
                mission.Id,
                WorldTransitPhase.Returning,
                effectApplied: mission.Kind == WorldMissionKind.Diplomat);
        }

        return new WorldMissionResult(scouting, diplomatic);
    }

    private static void PublishScoutReport(WorldState state, WorldMission mission, int tick)
    {
        if (mission.TargetsPlayerContact)
        {
            var targetKey = string.IsNullOrWhiteSpace(mission.ReportedTargetKey)
                ? mission.TargetContactKey
                : mission.ReportedTargetKey;
            var playerSummary =
                $"Scouts from {mission.OriginSettlementId} returned with a {mission.ReportedValueBand} player colony report.";
            state.RecordRaidIntelFact(
                IntelSourceKind.Scout,
                mission.FactionId,
                RaidIntelTargetKind.PlayerColony,
                targetKey,
                mission.ReportedValueBand,
                PlayerScoutIntelConfidence,
                PlayerScoutIntelLifetimeTicks,
                Math.Max(1, Math.Min(100, mission.ReportedCombatantDemand)),
                playerSummary);
            return;
        }

        if (!mission.TargetSettlementId.HasValue)
        {
            return;
        }

        var summary = $"Scouts from {mission.OriginSettlementId} returned after surveying {mission.TargetSettlementId}.";
        state.RecordIntelReport(IntelSourceKind.Scout, mission.FactionId, Math.Max(1, mission.Amount), summary);
        state.RecordFactionSettlementIntel(
            mission.FactionId,
            mission.TargetSettlementId.Value,
            IntelSourceKind.Scout,
            tick,
            Math.Max(1, Math.Min(100, mission.Amount)));
        // NPC scouts inform their own faction ledger. They must not grant the player
        // omniscient knowledge merely because the mission exists in the simulation.
        if (state.IsPlayerFaction(mission.FactionId))
        {
            PlayerKnowledgeService.RecordScoutSettlementInfo(state, mission.TargetSettlementId.Value, summary);
        }
    }
}
