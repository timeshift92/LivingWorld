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
            else if (!ConsumeTravelSupplies(state, movement, request.Tick))
            {
                RecallArmy(state, movement.ArmyId, "army ran out of travel supplies");
                recalled++;
            }
            else if (!IsStillValidTarget(state, movement))
            {
                // IsStillValidTarget already gates on target?.IsActive, so a destroyed/abandoned settlement
                // (kept in the ledger as a ruin) recalls the army here instead of letting it "arrive" on a
                // ruin — routed through RecallArmy so the reserved pawns return to their faction.
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
        ArmyTerminalResolutionService.ReturnToFaction(
            state,
            armyId,
            ArmyMovementStatus.Recalled,
            reason);
    }

    private static bool ConsumeTravelSupplies(WorldState state, WorldArmyMovement movement, int tick)
    {
        if (string.IsNullOrWhiteSpace(movement.SupplyResourceKey)
            || movement.SupplyPerCitizenPerDay <= 0)
        {
            return true;
        }

        var elapsedDays = Math.Max(0, tick - movement.LastSupplyTick) / 60_000;
        if (elapsedDays <= 0)
        {
            return true;
        }

        var travelers = state.GetCitizensOwnedBy(movement.ArmyId)
            .Count(citizen => citizen.Status == CitizenStatus.Alive);
        var required = travelers * movement.SupplyPerCitizenPerDay * elapsedDays;
        var consumed = state.ConsumeResource(
            movement.ArmyId,
            movement.SupplyResourceKey,
            required,
            "army travel supplies");
        state.RestoreArmyMovementForLedger(movement with
        {
            LastSupplyTick = movement.LastSupplyTick + (elapsedDays * 60_000)
        });
        return consumed == required;
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
                || FactionConflictPolicy.AreHostile(state, army.FactionId, target.FactionId, state.CurrentTick));
    }
}
