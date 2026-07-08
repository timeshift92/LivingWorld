namespace LivingWorld.Core;

internal sealed record WorldWarActionExecutionResult(
    int WarbandsLaunched,
    int ColoniesFounded,
    int CaravansCompleted,
    int ScoutingReports,
    int DiplomaticMissions,
    int DevelopmentsCompleted);

internal static class WorldWarActionDispatcher
{
    public static WorldWarActionExecutionResult Execute(
        WorldState state,
        WorldWarRequest request,
        IReadOnlyList<FactionActionPlan> plans)
    {
        var launched = 0;
        var founded = 0;
        var caravans = 0;
        var scoutingReports = 0;
        var diplomaticMissions = 0;
        var developments = 0;

        foreach (var plan in plans)
        {
            switch (plan.Action)
            {
                case WarAction.Warband:
                    if (WarbandActionExecutor.Execute(state, plan, request))
                    {
                        launched++;
                    }
                    break;
                case WarAction.Settler:
                    if (SettlementExpansionExecutor.Execute(state, plan.FactionId, request.SettlerCount))
                    {
                        founded++;
                    }
                    break;
                case WarAction.Caravan:
                    if (CaravanActionExecutor.Execute(state, plan.FactionId, request))
                    {
                        caravans++;
                    }
                    break;
                case WarAction.ScoutingParty:
                    if (ScoutingActionExecutor.Execute(state, plan.FactionId))
                    {
                        scoutingReports++;
                    }
                    break;
                case WarAction.Diplomat:
                    if (DiplomacyActionExecutor.Execute(state, plan.FactionId, request.DiplomatGoodwill))
                    {
                        diplomaticMissions++;
                    }
                    break;
                case WarAction.Develop:
                    if (plan.TargetSettlementId.HasValue
                        && SettlementDevelopmentActionExecutor.Execute(state, plan.TargetSettlementId.Value, request))
                    {
                        developments++;
                    }
                    break;
            }
        }

        return new WorldWarActionExecutionResult(
            launched,
            founded,
            caravans,
            scoutingReports,
            diplomaticMissions,
            developments);
    }
}
