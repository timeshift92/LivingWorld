using System;

namespace LivingWorld.Core;

/// <summary>
/// A settlement's derived military power. Unlike Rim War's abstract stored "points",
/// this is computed from the settlement's real living population — nothing exists from
/// thin air, and losses in battle remove actual citizens.
/// </summary>
public sealed record SettlementPower(int Combatants, int CombatPower);

/// <summary>
/// Derives a settlement's combat power from its living adult residents.
///
/// Only citizens that are Alive, adult, and still owned by the settlement count — children,
/// the elderly, prisoners, the dead/missing, and citizens away in an army are excluded (this
/// mirrors <see cref="SettlementQueryService.GetPopulation"/>'s residency rule). Power grows
/// linearly with combatants up to a threshold, then at a diminished rate, so a very large
/// settlement never yields unbounded linear military strength.
///
/// Pure and deterministic: the RimWorld layer caches the result per day; this service never
/// mutates state and does no per-tick work of its own.
/// </summary>
public static class SettlementPowerService
{
    public const int PointsPerCombatant = 100;
    public const int DiminishingThreshold = 50;
    public const int DiminishedPointsPerCombatant = 50;

    public static SettlementPower GetSettlementPower(WorldState state, EntityId settlementId)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var combatants = state.GetSettlementPopulation(settlementId).Adults;

        var atFullRate = Math.Min(combatants, DiminishingThreshold);
        var beyondThreshold = Math.Max(0, combatants - DiminishingThreshold);
        var combatPower = (atFullRate * PointsPerCombatant)
            + (beyondThreshold * DiminishedPointsPerCombatant);

        return new SettlementPower(combatants, combatPower);
    }
}
