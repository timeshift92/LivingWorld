namespace LivingWorld.Core;

/// <summary>
/// Single source of truth for whether two factions may resolve a physical encounter as combat.
/// Neutral traffic never fights merely because the faction ids differ. A declared active conflict
/// is sufficient even while goodwill catches up; an active truce always suppresses combat.
/// </summary>
public static class FactionConflictPolicy
{
    public static bool AreHostile(WorldState state, string factionA, string factionB, int tick)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (string.IsNullOrWhiteSpace(factionA)
            || string.IsNullOrWhiteSpace(factionB)
            || string.Equals(factionA, factionB, StringComparison.Ordinal)
            || ConflictService.IsTruceActive(state, factionA, factionB, tick))
        {
            return false;
        }

        return DiplomacyService.GetStance(state, factionA, factionB) == RelationStance.Hostile
            || ConflictService.IsActiveConflict(state, factionA, factionB);
    }

    public static WorldConflict DeclareWar(WorldState state, string aggressor, string defender, int tick)
    {
        if (ConflictService.IsTruceActive(state, aggressor, defender, tick))
        {
            throw new InvalidOperationException($"A truce is active between {aggressor} and {defender}.");
        }

        var current = DiplomacyService.GetGoodwill(state, aggressor, defender);
        if (current > DiplomacyService.HostileThreshold)
        {
            DiplomacyService.AdjustGoodwill(
                state,
                aggressor,
                defender,
                DiplomacyService.HostileThreshold - current);
        }

        return ConflictService.GetOrCreateConflict(state, aggressor, defender, tick);
    }
}
