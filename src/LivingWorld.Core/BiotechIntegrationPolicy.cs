namespace LivingWorld.Core;

public enum BiotechAdapterKind
{
    Xenotype,
    AnimalIncubation,
    GeneTrade
}

public enum BiotechIntegrationDecision
{
    Allowed,
    DisabledMissingBiotech,
    GatedByFacilities
}

public sealed record BiotechIntegrationRequest(
    bool BiotechActive,
    bool HasGeneLab,
    bool HasIncubator,
    BiotechAdapterKind RequestedAdapter);

public sealed record BiotechIntegrationResult(
    BiotechIntegrationDecision Decision,
    string Reason);

public static class BiotechIntegrationPolicy
{
    public static BiotechIntegrationResult Evaluate(BiotechIntegrationRequest request)
    {
        if (!request.BiotechActive)
        {
            return new BiotechIntegrationResult(
                BiotechIntegrationDecision.DisabledMissingBiotech,
                "Biotech adapters stay disabled when the DLC is not active.");
        }

        if ((request.RequestedAdapter is BiotechAdapterKind.Xenotype or BiotechAdapterKind.GeneTrade)
            && !request.HasGeneLab)
        {
            return new BiotechIntegrationResult(
                BiotechIntegrationDecision.GatedByFacilities,
                "Gene work requires a real gene-lab facility.");
        }

        if (request.RequestedAdapter == BiotechAdapterKind.AnimalIncubation && !request.HasIncubator)
        {
            return new BiotechIntegrationResult(
                BiotechIntegrationDecision.GatedByFacilities,
                "Incubation requires a real incubator facility.");
        }

        return new BiotechIntegrationResult(
            BiotechIntegrationDecision.Allowed,
            "Adapter can run against Living World ledger state.");
    }
}
