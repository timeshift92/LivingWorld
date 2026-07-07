namespace LivingWorld.Core;

public static class RaidOutcomeService
{
    public static WorldRaidOutcome? TryRecordResolvedRaidOutcome(WorldState state, EntityId armyId)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (state.GetRaidOutcome(armyId) != null)
        {
            return state.GetRaidOutcome(armyId);
        }

        var army = state.GetArmy(armyId);
        if (army == null)
        {
            return null;
        }

        var links = state.RaidPawnLinks
            .Where(link => link.ArmyId == armyId)
            .ToList();
        if (links.Count == 0)
        {
            return null;
        }

        var active = links.Count(link => link.Status == RaidPawnLinkStatus.Active);
        if (active > 0)
        {
            return null;
        }

        return state.RecordRaidOutcome(
            armyId,
            links.Count,
            active,
            links.Count(link => link.Status == RaidPawnLinkStatus.Dead),
            links.Count(link => link.Status == RaidPawnLinkStatus.Returned),
            links.Count(link => link.Status == RaidPawnLinkStatus.Prisoner));
    }
}
