using LivingWorld.Core;
using Verse;

namespace LivingWorld.RimWorld;

internal static class LivingWorldKnowledgeLabels
{
    public static string Source(IntelSourceKind value) => Translate("LW_IntelSource_", value);

    public static string Confidence(KnowledgeConfidence value) => Translate("LW_KnowledgeConfidence_", value);

    public static string Population(SettlementPopulationBand value) => Translate("LW_KnowledgePopulation_", value);

    public static string Food(SettlementFoodKnowledge value) => Translate("LW_KnowledgeFood_", value);

    public static string Migration(SettlementMigrationKnowledge value) => Translate("LW_KnowledgeMigration_", value);

    public static string Production(SettlementProductionKnowledge value) => Translate("LW_KnowledgeProduction_", value);

    public static string Boolean(bool value) => (value ? "LW_Common_Yes" : "LW_Common_No").Translate();

    public static string Summary(KnownSettlementInfo info)
    {
        return "LW_KnowledgeSummary".Translate(
            Population(info.PopulationBand).Named("population"),
            Food(info.Food).Named("food"),
            Migration(info.Migration).Named("migration"),
            Production(info.Production).Named("production")).ToString();
    }

    private static string Translate<T>(string prefix, T value) where T : struct
    {
        return (prefix + value).Translate();
    }
}
