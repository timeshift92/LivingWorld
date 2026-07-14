namespace LivingWorld.Core;

public sealed record TransitEncounterRequest(int Tick);

public sealed record TransitEncounterResult(
    int CaravansDestroyed,
    int MissionsDisrupted);

/// <summary>
/// Resolves deterministic encounters between travelling combat forces and non-combat traffic.
/// This is intentionally conservative: exact opposite endpoint routes and same-target convergence
/// are considered crossings. It avoids radius math in Core while preventing visible traffic from
/// passing through the same war objective without consequences.
/// </summary>
public static class TransitEncounterService
{
    public static bool TryResolvePhysicalContact(
        WorldState state,
        EntityId armyId,
        EntityId trafficId,
        int tick)
    {
        if (state == null || tick < 0)
        {
            return false;
        }

        var movement = state.GetArmyMovement(armyId);
        var army = state.GetArmy(armyId);
        if (movement?.Status != ArmyMovementStatus.Traveling || army == null)
        {
            return false;
        }

        state.AdvanceToTick(tick);
        if (trafficId.Kind == EntityKind.Caravan)
        {
            var caravan = state.GetCaravan(trafficId);
            if (caravan?.Status != CaravanStatus.Traveling
                || !IsHostile(state, army.FactionId, caravan.FactionId))
            {
                return false;
            }

            state.DestroyCaravan(caravan.Id, $"physically intercepted by {armyId}");
            return true;
        }

        if (trafficId.Kind == EntityKind.Mission)
        {
            var mission = state.GetMission(trafficId);
            if (mission?.Status != WorldMissionStatus.Traveling
                || !IsHostile(state, army.FactionId, mission.FactionId))
            {
                return false;
            }

            state.FailMission(mission.Id, $"physically disrupted by {armyId}");
            return true;
        }

        if (trafficId.Kind == EntityKind.MigrationGroup)
        {
            var group = state.GetMigrationGroup(trafficId);
            if (group?.Status != MigrationGroupStatus.Traveling
                || !IsHostile(state, army.FactionId, group.FactionId))
            {
                return false;
            }

            state.DisruptSettlementExpedition(group.Id, army.Id, $"physically intercepted by {armyId}");
            return true;
        }

        if (trafficId.Kind == EntityKind.DrifterAssimilationJourney)
        {
            var journey = state.GetDrifterAssimilationJourney(trafficId);
            if (journey?.Status != DrifterAssimilationJourneyStatus.Traveling
                || !IsHostile(state, army.FactionId, journey.ExpectedTargetFactionId))
            {
                return false;
            }

            state.CancelDrifterAssimilationJourney(journey.Id, $"physically intercepted by {armyId}");
            return true;
        }

        if (trafficId.Kind == EntityKind.DrifterFoundingJourney)
        {
            var journey = state.GetDrifterFoundingJourney(trafficId);
            if (journey?.Status != DrifterFoundingJourneyStatus.Traveling
                || !IsHostile(state, army.FactionId, journey.FactionId))
            {
                return false;
            }

            state.DisruptDrifterFoundingJourney(journey.Id, army.Id, $"physically intercepted by {armyId}");
            return true;
        }

        return false;
    }

    public static TransitEncounterResult SimulateDay(WorldState state, TransitEncounterRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.Tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Transit encounter tick cannot be negative.");
        }

        state.AdvanceToTick(request.Tick);
        var armies = state.ArmyMovements
            .Where(movement => movement.Status == ArmyMovementStatus.Traveling)
            .OrderBy(movement => movement.ArmyId.Value)
            .ToList();

        var caravansDestroyed = 0;
        foreach (var caravan in state.Caravans
            .Where(caravan => caravan.Status == CaravanStatus.Traveling)
            .OrderBy(caravan => caravan.Id.Value)
            .ToList())
        {
            var attacker = armies.FirstOrDefault(army => CanThreatenCaravan(state, army, caravan));
            if (attacker == null)
            {
                continue;
            }

            state.DestroyCaravan(caravan.Id, $"intercepted by {attacker.ArmyId}");
            caravansDestroyed++;
        }

        var missionsDisrupted = 0;
        foreach (var mission in state.Missions
            .Where(mission => mission.Status == WorldMissionStatus.Traveling)
            .OrderBy(mission => mission.Id.Value)
            .ToList())
        {
            var attacker = armies.FirstOrDefault(army => CanThreatenMission(state, army, mission));
            if (attacker == null)
            {
                continue;
            }

            state.FailMission(mission.Id, $"disrupted by {attacker.ArmyId}");
            missionsDisrupted++;
        }

        return new TransitEncounterResult(caravansDestroyed, missionsDisrupted);
    }

    private static bool CanThreatenCaravan(WorldState state, WorldArmyMovement armyMovement, WorldCaravan caravan)
    {
        var army = state.GetArmy(armyMovement.ArmyId);
        if (army == null || !IsHostile(state, army.FactionId, caravan.FactionId))
        {
            return false;
        }

        return (army.SourceSettlementId == caravan.TargetSettlementId
                && armyMovement.TargetSettlementId == caravan.SourceSettlementId)
            || armyMovement.TargetSettlementId == caravan.TargetSettlementId;
    }

    private static bool CanThreatenMission(WorldState state, WorldArmyMovement armyMovement, WorldMission mission)
    {
        var army = state.GetArmy(armyMovement.ArmyId);
        if (army == null || !IsHostile(state, army.FactionId, mission.FactionId))
        {
            return false;
        }

        return (army.SourceSettlementId == mission.TargetSettlementId
                && armyMovement.TargetSettlementId == mission.OriginSettlementId)
            || armyMovement.TargetSettlementId == mission.TargetSettlementId;
    }

    private static bool IsHostile(WorldState state, string factionA, string factionB)
    {
        return !string.Equals(factionA, factionB, StringComparison.Ordinal)
            && DiplomacyService.GetStance(state, factionA, factionB) != RelationStance.Ally;
    }
}
