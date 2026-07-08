namespace LivingWorld.Core;

public sealed record ArmyInterceptionRequest(int Tick);

public sealed record ArmyInterceptionResult(int Interceptions);

public static class ArmyInterceptionService
{
    private const int WinnerLossPercent = 20;
    private const int LoserLossPercent = 60;

    public static ArmyInterceptionResult SimulateDay(WorldState state, ArmyInterceptionRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.Tick < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Army interception tick cannot be negative.");
        }

        state.AdvanceToTick(request.Tick);
        var interceptions = 0;
        var consumed = new HashSet<EntityId>();
        var movements = state.ArmyMovements
            .Where(movement => movement.Status == ArmyMovementStatus.Traveling)
            .OrderBy(movement => movement.ArmyId.Value)
            .ToList();

        for (var leftIndex = 0; leftIndex < movements.Count; leftIndex++)
        {
            var left = movements[leftIndex];
            if (consumed.Contains(left.ArmyId))
            {
                continue;
            }

            for (var rightIndex = leftIndex + 1; rightIndex < movements.Count; rightIndex++)
            {
                var right = movements[rightIndex];
                if (consumed.Contains(right.ArmyId) || !RoutesMeetHeadOn(state, left, right))
                {
                    continue;
                }

                ResolveInterception(state, left, right, request.Tick);
                consumed.Add(left.ArmyId);
                consumed.Add(right.ArmyId);
                interceptions++;
                break;
            }
        }

        return new ArmyInterceptionResult(interceptions);
    }

    private static bool RoutesMeetHeadOn(WorldState state, WorldArmyMovement left, WorldArmyMovement right)
    {
        var leftArmy = state.GetArmy(left.ArmyId);
        var rightArmy = state.GetArmy(right.ArmyId);
        if (leftArmy == null || rightArmy == null)
        {
            return false;
        }

        if (string.Equals(leftArmy.FactionId, rightArmy.FactionId, StringComparison.Ordinal)
            || DiplomacyService.GetStance(state, leftArmy.FactionId, rightArmy.FactionId) == RelationStance.Ally)
        {
            return false;
        }

        return leftArmy.SourceSettlementId == right.TargetSettlementId
            && rightArmy.SourceSettlementId == left.TargetSettlementId;
    }

    private static void ResolveInterception(
        WorldState state,
        WorldArmyMovement leftMovement,
        WorldArmyMovement rightMovement,
        int tick)
    {
        var leftArmy = state.GetArmy(leftMovement.ArmyId)!;
        var rightArmy = state.GetArmy(rightMovement.ArmyId)!;
        var leftCombatants = Combatants(state, leftArmy.Id);
        var rightCombatants = Combatants(state, rightArmy.Id);
        var leftPower = SettlementPowerService.CombatPowerOf(leftCombatants.Count);
        var rightPower = SettlementPowerService.CombatPowerOf(rightCombatants.Count);
        var leftWins = leftPower >= rightPower;

        var leftLosses = LossCount(leftCombatants.Count, leftWins ? WinnerLossPercent : LoserLossPercent);
        var rightLosses = LossCount(rightCombatants.Count, leftWins ? LoserLossPercent : WinnerLossPercent);
        KillFirst(state, leftCombatants, leftLosses, $"fell intercepting {rightArmy.Id}");
        KillFirst(state, rightCombatants, rightLosses, $"fell intercepting {leftArmy.Id}");

        var winner = leftWins ? leftArmy : rightArmy;
        var loser = leftWins ? rightArmy : leftArmy;
        TransferSurvivors(state, loser.Id, loser.SourceSettlementId, "army intercepted and recalled");
        state.SetArmyMovementStatus(loser.Id, ArmyMovementStatus.Recalled);

        ConflictService.RecordBattleOutcome(
            state,
            leftArmy.FactionId,
            rightArmy.FactionId,
            leftLosses,
            rightLosses,
            capturedSettlementId: null,
            tick);

        state.RecordEvent(
            WorldEventKind.ConflictUpdated,
            winner.Id,
            $"Army {winner.Id} intercepted {loser.Id}; {winner.FactionId} continued and {loser.FactionId} recalled.");
    }

    private static List<WorldCitizen> Combatants(WorldState state, EntityId armyId)
    {
        return state.Citizens
            .Where(citizen =>
                citizen.Status == CitizenStatus.Alive
                && citizen.IsAdult
                && state.GetOwner(citizen.Id) == armyId)
            .OrderBy(citizen => citizen.Id.Value)
            .ToList();
    }

    private static int LossCount(int combatants, int percent)
    {
        return combatants * percent / 100;
    }

    private static void KillFirst(WorldState state, IReadOnlyList<WorldCitizen> combatants, int count, string reason)
    {
        foreach (var citizen in combatants.Take(count))
        {
            state.MarkCitizenDead(citizen.Id, reason);
        }
    }

    private static void TransferSurvivors(WorldState state, EntityId armyId, EntityId destinationId, string reason)
    {
        foreach (var citizen in state.Citizens
            .Where(citizen =>
                citizen.Status == CitizenStatus.Alive
                && state.GetOwner(citizen.Id) == armyId)
            .OrderBy(citizen => citizen.Id.Value)
            .ToList())
        {
            var transfer = state.TransferAsset(citizen.Id, armyId, destinationId, reason);
            if (transfer.Status != OwnershipTransferStatus.Success)
            {
                throw new InvalidOperationException(transfer.Reason);
            }

            if (destinationId.Kind == EntityKind.Settlement && citizen.SettlementId != destinationId)
            {
                state.ReplaceCitizenForSimulation(citizen with { SettlementId = destinationId });
            }
        }
    }
}
