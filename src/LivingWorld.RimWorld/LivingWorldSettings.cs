using Verse;

namespace LivingWorld.RimWorld;

public sealed class LivingWorldSettings : ModSettings
{
    public static LivingWorldSettings? Instance { get; private set; }

    public bool bootstrapLedgerDuringWorldGeneration = true;
    public int baselineHumanSettlementAdults = 24;
    public int baselineNonHumanSettlementAdults = 12;
    public int minSettlementAdults = 8;
    public int maxSettlementAdults = 80;
    public int foodPerCitizen = 8;
    public int steelPerCitizen = 15;
    public bool debugLogging = true;

    public LivingWorldSettings()
    {
        Instance = this;
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref bootstrapLedgerDuringWorldGeneration, "bootstrapLedgerDuringWorldGeneration", true);
        Scribe_Values.Look(ref baselineHumanSettlementAdults, "baselineHumanSettlementAdults", 24);
        Scribe_Values.Look(ref baselineNonHumanSettlementAdults, "baselineNonHumanSettlementAdults", 12);
        Scribe_Values.Look(ref minSettlementAdults, "minSettlementAdults", 8);
        Scribe_Values.Look(ref maxSettlementAdults, "maxSettlementAdults", 80);
        Scribe_Values.Look(ref foodPerCitizen, "foodPerCitizen", 8);
        Scribe_Values.Look(ref steelPerCitizen, "steelPerCitizen", 15);
        Scribe_Values.Look(ref debugLogging, "debugLogging", true);
    }
}
