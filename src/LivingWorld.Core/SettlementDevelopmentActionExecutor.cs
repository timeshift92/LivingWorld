namespace LivingWorld.Core;

internal static class SettlementDevelopmentActionExecutor
{
    public static ActionAttemptResult Execute(WorldState state, EntityId settlementId, WorldWarRequest request)
    {
        var result = SettlementDevelopmentService.DevelopSettlement(
            state,
            settlementId,
            new SettlementDevelopmentRequest(
                request.Tick,
                request.DevelopmentFoodResourceKey,
                request.DevelopmentHousingHeadroom,
                request.DevelopmentStep,
                request.DevelopmentMaxHousing)
            {
                SilverResourceKey = request.DevelopmentSilverResourceKey,
                DevelopmentSilverCost = request.DevelopmentSilverCost,
                SpecialistGrowthStep = request.DevelopmentSpecialistGrowthStep,
            });

        return result.SettlementsDeveloped > 0
            ? ActionAttemptResult.Success("settlement developed")
            : ActionAttemptResult.Failed(ActionAttemptReason.InsufficientSupplies, "settlement development requirements were not met");
    }
}
