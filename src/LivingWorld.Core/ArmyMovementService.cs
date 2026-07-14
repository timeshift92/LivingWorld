using System;
using System.Linq;

namespace LivingWorld.Core;

public sealed record ArmyMovementRequest(int Tick);

public sealed record ArmyMovementResult(int Arrived, int Recalled, int Disbanded);

/// <summary>
/// Advances travelling armies one day: a Traveling army whose army no longer exists is
/// Disbanded, one whose target settlement is gone is Recalled, and one whose ETA has passed is
/// marked Arrived (ready for battle resolution). Deterministic, runs on the daily scheduler
/// path — not per game tick — and reads only army/settlement lookups, no per-citizen scan.
///
/// Combatant accounting (an "empty" army losing on arrival) belongs to battle resolution, not
/// to movement, so an army with no members still arrives and is resolved there.
/// </summary>
public static class ArmyMovementService
{
    public static ArmyMovementResult SimulateDay(WorldState state, ArmyMovementRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        state.AdvanceToTick(request.Tick);

        var arrived = 0;
        var recalled = 0;
        var disbanded = 0;

        foreach (var movement in state.ArmyMovements
            .Where(movement => movement.Status == ArmyMovementStatus.Traveling)
            .OrderBy(movement => movement.ArmyId.Value)
            .ToList())
        {
            if (state.GetArmy(movement.ArmyId) == null)
            {
                state.SetArmyMovementStatus(movement.ArmyId, ArmyMovementStatus.Disbanded);
                disbanded++;
            }
            else if (!IsStillValidTarget(state, movement))
            {
                RecallArmy(state, movement.ArmyId, "army target or diplomatic precondition changed");
                recalled++;
            }
            else if (request.Tick >= movement.ArrivalTick)
            {
                state.SetArmyMovementStatus(movement.ArmyId, ArmyMovementStatus.Arrived);
                arrived++;
            }
        }

        return new ArmyMovementResult(arrived, recalled, disbanded);
    }

    public static void RecallArmy(WorldState state, EntityId armyId, string reason)
    {
        var army = state.GetArmy(armyId);
        if (army == null)
        {
            return;
        }

        var destination = state.GetSettlement(army.SourceSettlementId);
        if (destination?.IsActive != true
            || !string.Equals(destination.FactionId, army.FactionId, StringComparison.Ordinal))
        {
            destination = state.Settlements
                .Where(settlement => settlement.IsActive)
                .Where(settlement => string.Equals(settlement.FactionId, army.FactionId, StringComparison.Ordinal))
                .OrderBy(settlement => settlement.Id.Value)
                .FirstOrDefault();
        }

        if (destination != null)
        {
            foreach (var citizen in state.GetCitizensOwnedBy(armyId).ToList())
            {
                state.TransferAsset(citizen.Id, armyId, destination.Id, reason);
            }

            foreach (var resource in state.ResourcesForOwner(armyId).ToList())
            {
                var transfer = state.TransferResource(
                    armyId,
                    destination.Id,
                    resource.ResourceKey,
                    resource.Quantity,
                    reason);
                if (transfer.Status != OwnershipTransferStatus.Success)
                {
                    throw new InvalidOperationException(transfer.Reason);
                }
            }
        }

        state.SetArmyMovementStatus(armyId, ArmyMovementStatus.Recalled);
    }

    private static bool IsStillValidTarget(WorldState state, WorldArmyMovement movement)
    {
        var army = state.GetArmy(movement.ArmyId);
        var target = state.GetSettlement(movement.TargetSettlementId);
        return army != null
            && target?.IsActive == true
            && !string.Equals(army.FactionId, target.FactionId, StringComparison.Ordinal)
            && !state.IsPlayerFaction(target.FactionId)
            && (string.IsNullOrWhiteSpace(movement.ExpectedTargetFactionId)
                || string.Equals(target.FactionId, movement.ExpectedTargetFactionId, StringComparison.Ordinal))
            && DiplomacyService.GetStance(state, army.FactionId, target.FactionId) != RelationStance.Ally
            && !ConflictService.IsTruceActive(state, army.FactionId, target.FactionId, state.CurrentTick)
            && (!movement.RequiresHostileRelation
                || DiplomacyService.GetStance(state, army.FactionId, target.FactionId) == RelationStance.Hostile);
    }
}
