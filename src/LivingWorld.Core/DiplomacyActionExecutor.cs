namespace LivingWorld.Core;

internal static class DiplomacyActionExecutor
{
    public static bool Execute(WorldState state, string factionId, int goodwill)
    {
        var source = WorldWarTargetSelector.FindReadySourceSettlement(state, factionId);
        var targetFaction = WorldWarTargetSelector.FindDiplomacyTargetFaction(state, factionId);
        var delta = Math.Abs(goodwill);
        if (source == null || targetFaction == null || delta <= 0)
        {
            return false;
        }

        var before = DiplomacyService.GetGoodwill(state, factionId, targetFaction);
        var after = DiplomacyService.AdjustGoodwill(state, factionId, targetFaction, delta);
        if (after == before)
        {
            return false;
        }

        state.RecordEvent(
            WorldEventKind.DiplomaticMissionSent,
            source.Id,
            $"Diplomats from {source.Id} improved relations between {factionId} and {targetFaction} to {after}.");
        return true;
    }
}
