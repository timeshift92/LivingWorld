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
            ActionAttemptResult result;
            switch (plan.Action)
            {
                case WarAction.Warband:
                    result = WarbandActionExecutor.Execute(state, plan, request);
                    if (result.Succeeded)
                    {
                        launched++;
                    }
                    break;
                case WarAction.Settler:
                    result = SettlementExpansionExecutor.Execute(state, plan.FactionId, request);
                    if (result.Succeeded)
                    {
                        settlerExpeditions++;
                    }
                    break;
                case WarAction.Caravan:
                    result = CaravanActionExecutor.Execute(state, plan, request);
                    if (result.Succeeded)
                    {
                        caravans++;
                    }
                    break;
                case WarAction.ScoutingParty:
                    result = ScoutingActionExecutor.Execute(state, plan, request);
                    if (result.Succeeded)
                    {
                        scoutingReports++;
                    }
                    break;
                case WarAction.Diplomat:
                    result = DiplomacyActionExecutor.Execute(state, plan, request);
                    if (result.Succeeded)
                    {
                        diplomaticMissions++;
                    }
                    break;
                case WarAction.Develop:
                    result = plan.TargetSettlementId.HasValue
                        ? SettlementDevelopmentActionExecutor.Execute(state, plan.TargetSettlementId.Value, request)
                        : ActionAttemptResult.Failed(ActionAttemptReason.NoTarget, "development plan has no settlement");
                    if (result.Succeeded)
                    {
                        developments++;
                    }
                    break;
                default:
                    result = ActionAttemptResult.Failed(ActionAttemptReason.InvalidRequest, "action plan cannot be executed");
                    break;
            }

            state.RecordActionAttempt(plan, result, request.Tick);
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
