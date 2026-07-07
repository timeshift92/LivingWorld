using System;
using System.Linq;

namespace LivingWorld.Core;

public enum RelationStance
{
    Hostile,
    Neutral,
    Ally,
}

/// <summary>
/// Ledger-side faction relations: a symmetric goodwill value per faction pair that classifies
/// into hostile / neutral / ally, drifts back toward neutral over time, and drops when factions
/// war on each other. Irreconcilable factions (e.g. pirates) are locked in hostility — goodwill
/// gestures never lift them out and they never drift toward neutral.
///
/// This is the ledger's own model; it does not touch RimWorld's vanilla goodwill by default —
/// materializing these relations into the game is a later, conservative/flagged step so it never
/// silently rewrites the player's standings.
/// </summary>
public static class DiplomacyService
{
    public const int MinGoodwill = -100;
    public const int MaxGoodwill = 100;
    public const int HostileThreshold = -50;
    public const int AllyThreshold = 50;
    public const int DriftStep = 2;

    public static int GetGoodwill(WorldState state, string factionA, string factionB)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        return state.GetFactionGoodwill(factionA, factionB);
    }

    public static RelationStance GetStance(WorldState state, string factionA, string factionB)
    {
        return StanceOf(GetGoodwill(state, factionA, factionB));
    }

    public static RelationStance StanceOf(int goodwill)
    {
        if (goodwill <= HostileThreshold)
        {
            return RelationStance.Hostile;
        }

        return goodwill >= AllyThreshold
            ? RelationStance.Ally
            : RelationStance.Neutral;
    }

    public static int AdjustGoodwill(WorldState state, string factionA, string factionB, int delta)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var next = Math.Clamp(state.GetFactionGoodwill(factionA, factionB) + delta, MinGoodwill, MaxGoodwill);

        // Irreconcilable factions can never climb out of hostility, whatever the gesture.
        if (state.IsFactionIrreconcilable(factionA) || state.IsFactionIrreconcilable(factionB))
        {
            next = Math.Min(next, MinGoodwill);
        }

        state.SetFactionGoodwillForLedger(factionA, factionB, next);
        return next;
    }

    public static int RecordAggression(WorldState state, string aggressor, string victim, int severity)
    {
        return AdjustGoodwill(state, aggressor, victim, -Math.Abs(severity));
    }

    public static void SimulateDay(WorldState state, int tick)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        state.AdvanceToTick(tick);

        foreach (var relation in state.FactionRelations
            .OrderBy(pair => pair.Key.Item1, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.Item2, StringComparer.Ordinal)
            .ToList())
        {
            var (factionA, factionB) = relation.Key;

            // Locked hatreds do not thaw.
            if (state.IsFactionIrreconcilable(factionA) || state.IsFactionIrreconcilable(factionB))
            {
                continue;
            }

            var goodwill = relation.Value;
            if (goodwill == 0)
            {
                continue;
            }

            var drifted = goodwill > 0
                ? Math.Max(0, goodwill - DriftStep)
                : Math.Min(0, goodwill + DriftStep);
            state.SetFactionGoodwillForLedger(factionA, factionB, drifted);
        }
    }
}
