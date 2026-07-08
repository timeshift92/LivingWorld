using System;
using System.Collections.Generic;
using System.Linq;

namespace LivingWorld.Core;

public enum BattleWinner
{
    Attacker,
    Defender,
}

public sealed record BattleOutcome(
    EntityId ArmyId,
    EntityId TargetSettlementId,
    BattleWinner Winner,
    int AttackerPower,
    int DefenderPower,
    int AttackerLosses,
    int DefenderLosses,
    bool Captured);

public enum BattleResolutionStatus
{
    Resolved,
    BlockedPlayerSettlement
}

public sealed record BattleResolutionResult(BattleResolutionStatus Status, BattleOutcome? Outcome);

/// <summary>
/// Resolves an arrived army's attack on its target settlement, using the shared power curve
/// (<see cref="SettlementPowerService.CombatPowerOf"/>) over each side's living adult combatants.
///
/// The side with more power wins; a tie is held by the defender (home-ground advantage), so the
/// outcome is deterministic. Losses are proportional — the loser loses <see cref="LoserLossPercent"/>
/// of its combatants, the winner <see cref="WinnerLossPercent"/> — and are applied as real deaths,
/// so the world's population is conserved (nothing from thin air; casualties are actual citizens
/// turned Dead). When the attacker wins the settlement is captured: it changes faction and its
/// surviving residents come with it. Either way the army stands down afterward.
/// </summary>
public static class WorldBattleService
{
    public const int WinnerLossPercent = 20;
    public const int LoserLossPercent = 60;

    public static BattleOutcome Resolve(WorldState state, EntityId armyId)
    {
        var result = TryResolve(state, armyId);
        if (result.Status == BattleResolutionStatus.BlockedPlayerSettlement)
        {
            throw new InvalidOperationException("Battle against player faction settlement requires active-map materialization.");
        }

        return result.Outcome!;
    }

    public static BattleResolutionResult TryResolve(WorldState state, EntityId armyId)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var movement = state.GetArmyMovement(armyId)
            ?? throw new InvalidOperationException($"Army {armyId} has no movement to resolve.");
        if (movement.Status != ArmyMovementStatus.Arrived)
        {
            throw new InvalidOperationException($"Army {armyId} has not arrived at its target.");
        }

        var army = state.GetArmy(armyId)
            ?? throw new InvalidOperationException($"Army {armyId} does not exist.");
        var targetId = movement.TargetSettlementId;
        var targetSettlement = state.GetSettlement(targetId);
        if (targetSettlement == null)
        {
            throw new InvalidOperationException($"Target settlement {targetId} does not exist.");
        }

        if (state.IsPlayerFaction(targetSettlement.FactionId))
        {
            var blockedAttackers = Combatants(state, armyId);
            TransferSurvivingAttackers(state, blockedAttackers, army.Id, army.SourceSettlementId);
            state.SetArmyMovementStatus(armyId, ArmyMovementStatus.Disbanded);
            return new BattleResolutionResult(BattleResolutionStatus.BlockedPlayerSettlement, null);
        }

        var attackers = Combatants(state, armyId);
        var defenders = Combatants(state, targetId);
        var attackerPower = ApplyCombatMultiplier(
            SettlementPowerService.CombatPowerOf(attackers.Count),
            state.GetFactionBehavior(army.FactionId));
        var defenderPower = ApplyCombatMultiplier(
            SettlementPowerService.CombatPowerOf(defenders.Count),
            state.GetFactionBehavior(targetSettlement.FactionId));

        // Attacker must strictly out-power the defender; a tie is held by the defender.
        var attackerWins = attackerPower > defenderPower;

        var attackerLosses = LossCount(attackers.Count, attackerWins ? WinnerLossPercent : LoserLossPercent);
        var defenderLosses = LossCount(defenders.Count, attackerWins ? LoserLossPercent : WinnerLossPercent);

        KillFirst(state, attackers, attackerLosses, $"fell attacking {targetId}");
        KillFirst(state, defenders, defenderLosses, $"fell defending {targetId}");

        var captured = false;
        if (attackerWins)
        {
            state.CaptureSettlement(targetId, army.FactionId);
            captured = true;
        }

        var attackerDestinationId = attackerWins
            ? targetId
            : army.SourceSettlementId;
        TransferSurvivingAttackers(state, attackers, army.Id, attackerDestinationId);

        // The battle is over; the army stands down either way.
        state.SetArmyMovementStatus(armyId, ArmyMovementStatus.Disbanded);

        var outcome = new BattleOutcome(
                armyId,
                targetId,
                attackerWins ? BattleWinner.Attacker : BattleWinner.Defender,
                attackerPower,
                defenderPower,
                attackerLosses,
                defenderLosses,
                captured);
        ConflictService.RecordBattleOutcome(
            state,
            army.FactionId,
            targetSettlement.FactionId,
            attackerLosses,
            defenderLosses,
            captured ? targetId : null,
            state.CurrentTick);

        return new BattleResolutionResult(
            BattleResolutionStatus.Resolved,
            outcome);
    }

    private static int LossCount(int combatants, int percent)
    {
        return combatants * percent / 100;
    }

    private static int ApplyCombatMultiplier(int basePower, FactionBehavior behavior)
    {
        var profile = FactionBehaviorService.GetProfile(behavior);
        return (int)Math.Round(basePower * profile.CombatMultiplier, MidpointRounding.AwayFromZero);
    }

    private static List<WorldCitizen> Combatants(WorldState state, EntityId ownerId)
    {
        return state.Citizens
            .Where(citizen =>
                citizen.Status == CitizenStatus.Alive
                && citizen.IsAdult
                && state.GetOwner(citizen.Id) == ownerId)
            .OrderBy(citizen => citizen.Id.Value)
            .ToList();
    }

    private static void KillFirst(WorldState state, List<WorldCitizen> combatants, int count, string reason)
    {
        foreach (var citizen in combatants.Take(count))
        {
            state.MarkCitizenDead(citizen.Id, reason);
        }
    }

    private static void TransferSurvivingAttackers(
        WorldState state,
        List<WorldCitizen> attackers,
        EntityId armyId,
        EntityId destinationId)
    {
        foreach (var attacker in attackers)
        {
            var current = state.GetCitizen(attacker.Id);
            if (current?.Status != CitizenStatus.Alive || state.GetOwner(attacker.Id) != armyId)
            {
                continue;
            }

            var transfer = state.TransferAsset(
                attacker.Id,
                armyId,
                destinationId,
                "world battle resolved");
            if (transfer.Status != OwnershipTransferStatus.Success)
            {
                throw new InvalidOperationException(transfer.Reason);
            }

            if (destinationId.Kind == EntityKind.Settlement && current.SettlementId != destinationId)
            {
                state.ReplaceCitizenForSimulation(current with { SettlementId = destinationId });
            }
        }
    }
}
