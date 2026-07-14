namespace LivingWorld.Core;

internal sealed record WorldWarActionExecutionResult(
    int WarbandsLaunched,
    int SettlerExpeditionsLaunched,
    int CaravansLaunched,
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
        var settlerExpeditions = 0;
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
                    if (SettlementExpansionExecutor.Execute(state, plan.FactionId, request))
                    {
                        settlerExpeditions++;
                    }
                    break;
                case WarAction.Caravan:
                    if (CaravanActionExecutor.Execute(state, plan, request))
                    {
                        caravans++;
                    }
                    break;
                case WarAction.ScoutingParty:
                    if (ScoutingActionExecutor.Execute(state, plan, request))
                    {
                        scoutingReports++;
                    }
                    break;
                case WarAction.Diplomat:
                    if (DiplomacyActionExecutor.Execute(state, plan, request))
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
            settlerExpeditions,
            caravans,
            scoutingReports,
            diplomaticMissions,
            developments);
    }
}
