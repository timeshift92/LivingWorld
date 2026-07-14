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
    public bool debugLogging = false;
    public bool drifterFlowEnabled = true;
    public int targetWorldPopulationPerSettlement = 24;
    public int drifterHardCeiling = 2000;
    public int maxDrifterArrivalsPerDay = 2;
    public int maxDrifterAssimilationsPerDay = 2;
    public int drifterMinFounders = 4;
    public int drifterLeaderAptitudeThreshold = 70;
    public bool worldWarEnabled = true;
    public bool showWarbandMarkers = true;
    public bool showTraderMarkers = true;
    public bool showScoutMarkers = true;
    public bool showDiplomatMarkers = true;
    public bool showSettlerMarkers = true;
    public bool travelingRaidsEnabled = true;
    public bool economicDiversityEnabled = true;
    public bool mechClustersEnabled = true;
    public bool arrivalsTravelEnabled = true;
    public bool armoryMobilizationEnabled = true;
    public int mobilizationSkillThreshold = 4;
    public bool autoMobilizeOnThreat = true;
    public bool mobilizationDiagnostics = false;
    public int worldWarTravelDays = 3;
    public int worldWarRaidCombatants = 8;
    public int worldWarWarbandCooldownDays = 8;
    public int worldWarLetterCooldownDays = 10;
    public bool settlementDevelopmentEnabled = true;
    public int settlementDevelopmentStep = 2;
    public int settlementHousingHeadroom = 4;
    public bool worldMapSpeedTestEnabled = false;
    public int worldMapSpeedMultiplier = 5;
    public int mobilizationBigRaidThreshold = 12;
    public float mobilizationAtBaseRadius = 18f;
    public int mobilizationDeescalateRechecks = 2;
    public float mobilizationDangerousAnimalBodySize = 2f;
    public bool musterEnabled = true;
    public float musterReadyFraction = 0.7f;
    public float musterHoldRadius = 8f;
    public int musterReleaseTimeoutRechecks = 10;

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
        Scribe_Values.Look(ref debugLogging, "debugLogging", false);
        Scribe_Values.Look(ref drifterFlowEnabled, "drifterFlowEnabled", true);
        Scribe_Values.Look(ref targetWorldPopulationPerSettlement, "targetWorldPopulationPerSettlement", 24);
        Scribe_Values.Look(ref drifterHardCeiling, "drifterHardCeiling", 2000);
        Scribe_Values.Look(ref maxDrifterArrivalsPerDay, "maxDrifterArrivalsPerDay", 2);
        Scribe_Values.Look(ref maxDrifterAssimilationsPerDay, "maxDrifterAssimilationsPerDay", 2);
        Scribe_Values.Look(ref drifterMinFounders, "drifterMinFounders", 4);
        Scribe_Values.Look(ref drifterLeaderAptitudeThreshold, "drifterLeaderAptitudeThreshold", 70);
        Scribe_Values.Look(ref worldWarEnabled, "worldWarEnabled", true);
        Scribe_Values.Look(ref showWarbandMarkers, "showWarbandMarkers", true);
        Scribe_Values.Look(ref showTraderMarkers, "showTraderMarkers", true);
        Scribe_Values.Look(ref showScoutMarkers, "showScoutMarkers", true);
        Scribe_Values.Look(ref showDiplomatMarkers, "showDiplomatMarkers", true);
        Scribe_Values.Look(ref showSettlerMarkers, "showSettlerMarkers", true);
        Scribe_Values.Look(ref travelingRaidsEnabled, "travelingRaidsEnabled", true);
        Scribe_Values.Look(ref economicDiversityEnabled, "economicDiversityEnabled", true);
        Scribe_Values.Look(ref mechClustersEnabled, "mechClustersEnabled", true);
        Scribe_Values.Look(ref arrivalsTravelEnabled, "arrivalsTravelEnabled", true);
        Scribe_Values.Look(ref armoryMobilizationEnabled, "armoryMobilizationEnabled", true);
        Scribe_Values.Look(ref mobilizationSkillThreshold, "mobilizationSkillThreshold", 4);
        Scribe_Values.Look(ref autoMobilizeOnThreat, "autoMobilizeOnThreat", true);
        Scribe_Values.Look(ref mobilizationDiagnostics, "mobilizationDiagnostics", false);
        Scribe_Values.Look(ref worldWarTravelDays, "worldWarTravelDays", 3);
        Scribe_Values.Look(ref worldWarRaidCombatants, "worldWarRaidCombatants", 8);
        Scribe_Values.Look(ref worldWarWarbandCooldownDays, "worldWarWarbandCooldownDays", 8);
        Scribe_Values.Look(ref worldWarLetterCooldownDays, "worldWarLetterCooldownDays", 10);
        Scribe_Values.Look(ref settlementDevelopmentEnabled, "settlementDevelopmentEnabled", true);
        Scribe_Values.Look(ref settlementDevelopmentStep, "settlementDevelopmentStep", 2);
        Scribe_Values.Look(ref settlementHousingHeadroom, "settlementHousingHeadroom", 4);
        Scribe_Values.Look(ref worldMapSpeedTestEnabled, "worldMapSpeedTestEnabled", false);
        Scribe_Values.Look(ref worldMapSpeedMultiplier, "worldMapSpeedMultiplier", 5);
        Scribe_Values.Look(ref mobilizationBigRaidThreshold, "mobilizationBigRaidThreshold", 12);
        Scribe_Values.Look(ref mobilizationAtBaseRadius, "mobilizationAtBaseRadius", 18f);
        Scribe_Values.Look(ref mobilizationDeescalateRechecks, "mobilizationDeescalateRechecks", 2);
        Scribe_Values.Look(ref mobilizationDangerousAnimalBodySize, "mobilizationDangerousAnimalBodySize", 2f);
        Scribe_Values.Look(ref musterEnabled, "musterEnabled", true);
        Scribe_Values.Look(ref musterReadyFraction, "musterReadyFraction", 0.7f);
        Scribe_Values.Look(ref musterHoldRadius, "musterHoldRadius", 8f);
        Scribe_Values.Look(ref musterReleaseTimeoutRechecks, "musterReleaseTimeoutRechecks", 10);

        if (worldMapSpeedMultiplier != 3 && worldMapSpeedMultiplier != 5 && worldMapSpeedMultiplier != 10)
        {
            worldMapSpeedMultiplier = 5;
        }
    }
}
