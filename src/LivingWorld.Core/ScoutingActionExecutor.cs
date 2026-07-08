namespace LivingWorld.Core;

internal static class ScoutingActionExecutor
{
    public static bool Execute(WorldState state, string factionId)
    {
        var source = WorldWarTargetSelector.FindReadySourceSettlement(state, factionId);
        var target = WorldWarTargetSelector.FindScoutingTarget(state, factionId);
        if (source == null || target == null)
        {
            return false;
        }

        var summary = $"Scouts from {source.Id} surveyed {target.Id}.";
        state.RecordIntelReport(IntelSourceKind.Scout, factionId, valueScore: 100, summary);
        PlayerKnowledgeService.RecordScoutSettlementInfo(state, target.Id, summary);
        return true;
    }
}
