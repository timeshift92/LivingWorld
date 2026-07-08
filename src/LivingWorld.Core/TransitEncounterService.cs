namespace LivingWorld.Core;

public sealed record TransitEncounterRequest(int Tick);

public sealed record TransitEncounterResult(
    int CaravansDestroyed,
    int MissionsDisrupted);

/// <summary>
/// Resolves deterministic encounters between travelling combat forces and non-combat traffic.
/// This is intentionally conservative: only exact opposite endpoint routes are considered a
/// crossing. It avoids radius math in Core while preventing obvious "they pass through each other"
/// cases on the world map.
/// </summary>
public static class TransitEncounterService
{
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

            state.SetMissionStatus(mission.Id, WorldMissionStatus.Failed);
            state.RecordEvent(
                WorldEventKind.WorldMissionDisrupted,
                mission.Id,
                $"Mission {mission.Id} disrupted by {attacker.ArmyId}.");
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

        return army.SourceSettlementId == caravan.TargetSettlementId
            && armyMovement.TargetSettlementId == caravan.SourceSettlementId;
    }

    private static bool CanThreatenMission(WorldState state, WorldArmyMovement armyMovement, WorldMission mission)
    {
        var army = state.GetArmy(armyMovement.ArmyId);
        if (army == null || !IsHostile(state, army.FactionId, mission.FactionId))
        {
            return false;
        }

        return army.SourceSettlementId == mission.TargetSettlementId
            && armyMovement.TargetSettlementId == mission.OriginSettlementId;
    }

    private static bool IsHostile(WorldState state, string factionA, string factionB)
    {
        return !string.Equals(factionA, factionB, StringComparison.Ordinal)
            && DiplomacyService.GetStance(state, factionA, factionB) != RelationStance.Ally;
    }
}
