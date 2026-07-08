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
    public bool drifterFlowEnabled = true;
    public int targetWorldPopulationPerSettlement = 24;
    public int drifterHardCeiling = 2000;
    public int maxDrifterArrivalsPerDay = 2;
    public int maxDrifterAssimilationsPerDay = 2;
    public int drifterMinFounders = 4;
    public int drifterLeaderAptitudeThreshold = 70;
    public bool worldWarEnabled = true;
    public bool travelingRaidsEnabled = true;
    public int worldWarTravelDays = 3;
    public int worldWarRaidCombatants = 8;
    public int worldWarWarbandCooldownDays = 8;
    public int worldWarLetterCooldownDays = 10;
    public bool settlementDevelopmentEnabled = true;
    public int settlementDevelopmentStep = 2;
    public int settlementHousingHeadroom = 4;
    public bool worldMapSpeedTestEnabled = false;
    public int worldMapSpeedMultiplier = 5;

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
        Scribe_Values.Look(ref drifterFlowEnabled, "drifterFlowEnabled", true);
        Scribe_Values.Look(ref targetWorldPopulationPerSettlement, "targetWorldPopulationPerSettlement", 24);
        Scribe_Values.Look(ref drifterHardCeiling, "drifterHardCeiling", 2000);
        Scribe_Values.Look(ref maxDrifterArrivalsPerDay, "maxDrifterArrivalsPerDay", 2);
        Scribe_Values.Look(ref maxDrifterAssimilationsPerDay, "maxDrifterAssimilationsPerDay", 2);
        Scribe_Values.Look(ref drifterMinFounders, "drifterMinFounders", 4);
        Scribe_Values.Look(ref drifterLeaderAptitudeThreshold, "drifterLeaderAptitudeThreshold", 70);
        Scribe_Values.Look(ref worldWarEnabled, "worldWarEnabled", true);
        Scribe_Values.Look(ref travelingRaidsEnabled, "travelingRaidsEnabled", true);
        Scribe_Values.Look(ref worldWarTravelDays, "worldWarTravelDays", 3);
        Scribe_Values.Look(ref worldWarRaidCombatants, "worldWarRaidCombatants", 8);
        Scribe_Values.Look(ref worldWarWarbandCooldownDays, "worldWarWarbandCooldownDays", 8);
        Scribe_Values.Look(ref worldWarLetterCooldownDays, "worldWarLetterCooldownDays", 10);
        Scribe_Values.Look(ref settlementDevelopmentEnabled, "settlementDevelopmentEnabled", true);
        Scribe_Values.Look(ref settlementDevelopmentStep, "settlementDevelopmentStep", 2);
        Scribe_Values.Look(ref settlementHousingHeadroom, "settlementHousingHeadroom", 4);
        Scribe_Values.Look(ref worldMapSpeedTestEnabled, "worldMapSpeedTestEnabled", false);
        Scribe_Values.Look(ref worldMapSpeedMultiplier, "worldMapSpeedMultiplier", 5);

        if (worldMapSpeedMultiplier != 3 && worldMapSpeedMultiplier != 5 && worldMapSpeedMultiplier != 10)
        {
            worldMapSpeedMultiplier = 5;
        }
    }
}
