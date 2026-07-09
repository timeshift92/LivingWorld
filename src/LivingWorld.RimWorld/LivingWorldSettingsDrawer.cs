using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

public static class LivingWorldSettingsDrawer
{
    // Content height for the scrollable mod-settings page (the grouped sections do not fit a fixed
    // window, so LivingWorldMod scrolls this).
    public const float PreferredHeight = 1020f;

    // The world-generation screen only needs the one decision a player makes before the world
    // exists - whether Living World is active. Planet size and population are the vanilla planet
    // options; every numeric knob lives in Options - Mod Settings, so world creation stays clean.
    public static void DrawWorldGenEssentials(Rect inRect, LivingWorldSettings settings)
    {
        var listing = new Listing_Standard();
        listing.Begin(inRect);

        listing.CheckboxLabeled(
            "LW_Settings_BootstrapLedger".Translate(),
            ref settings.bootstrapLedgerDuringWorldGeneration,
            "LW_Settings_BootstrapLedgerTip".Translate());

        listing.Gap(8f);

        var previousColor = GUI.color;
        GUI.color = new Color(0.72f, 0.72f, 0.72f);
        listing.Label("LW_WorldGenBlurb".Translate());
        listing.Gap(6f);
        listing.Label("LW_WorldGenAdvancedHint".Translate());
        GUI.color = previousColor;

        listing.End();
    }

    // The ongoing mod-settings page, grouped by player intent (world population, faction activity,
    // settlement development, initial baseline, performance, compatibility, and a clearly-separate
    // debug block).
    public static void Draw(Rect inRect, LivingWorldSettings settings)
    {
        var listing = new Listing_Standard();
        listing.Begin(inRect);

        // World Population — ongoing inflow and density.
        DrawSectionHeader(listing, "LW_SettingsSection_Population");
        DrawIntSlider(listing, "LW_Settings_TargetDensity".Translate(), ref settings.targetWorldPopulationPerSettlement, 0, 120);
        DrawIntSlider(listing, "LW_SettingDrifterCeiling".Translate(), ref settings.drifterHardCeiling, 100, 10000);
        listing.CheckboxLabeled("LW_SettingDrifterFlow".Translate(), ref settings.drifterFlowEnabled);
        DrawIntSlider(listing, "LW_SettingDrifterArrivals".Translate(), ref settings.maxDrifterArrivalsPerDay, 0, 10);
        DrawIntSlider(listing, "LW_Settings_DrifterAssimilations".Translate(), ref settings.maxDrifterAssimilationsPerDay, 0, 10);

        // Faction Activity — the NPC world war.
        DrawSectionHeader(listing, "LW_SettingsSection_FactionActivity");
        listing.CheckboxLabeled("LW_Settings_WorldWarEnabled".Translate(), ref settings.worldWarEnabled, "LW_Settings_WorldWarEnabledTip".Translate());
        listing.CheckboxLabeled("LW_Settings_TravelingRaids".Translate(), ref settings.travelingRaidsEnabled, "LW_Settings_TravelingRaidsTip".Translate());
        listing.CheckboxLabeled("LW_Settings_EconomicDiversity".Translate(), ref settings.economicDiversityEnabled, "LW_Settings_EconomicDiversityTip".Translate());
        listing.CheckboxLabeled("LW_Settings_MechClusters".Translate(), ref settings.mechClustersEnabled, "LW_Settings_MechClustersTip".Translate());
        listing.CheckboxLabeled("LW_Settings_ArrivalsTravel".Translate(), ref settings.arrivalsTravelEnabled, "LW_Settings_ArrivalsTravelTip".Translate());
        listing.CheckboxLabeled("LW_Settings_ArmoryMobilization".Translate(), ref settings.armoryMobilizationEnabled, "LW_Settings_ArmoryMobilizationTip".Translate());
        DrawIntSlider(listing, "LW_Settings_MobilizationSkill".Translate(), ref settings.mobilizationSkillThreshold, 0, 20);
        DrawIntSlider(listing, "LW_Settings_WorldWarTravelDays".Translate(), ref settings.worldWarTravelDays, 1, 15);
        DrawIntSlider(listing, "LW_Settings_WorldWarRaidCombatants".Translate(), ref settings.worldWarRaidCombatants, 1, 30);
        DrawIntSlider(listing, "LW_Settings_WorldWarCooldown".Translate(), ref settings.worldWarWarbandCooldownDays, 0, 30);
        DrawIntSlider(listing, "LW_Settings_WorldWarLetterCooldown".Translate(), ref settings.worldWarLetterCooldownDays, 0, 30);

        // Settlement Development — how NPC bases grow their infrastructure.
        DrawSectionHeader(listing, "LW_SettingsSection_Development");
        listing.CheckboxLabeled("LW_Settings_DevelopmentEnabled".Translate(), ref settings.settlementDevelopmentEnabled);
        DrawIntSlider(listing, "LW_Settings_DevelopmentStep".Translate(), ref settings.settlementDevelopmentStep, 1, 20);
        DrawIntSlider(listing, "LW_Settings_HousingHeadroom".Translate(), ref settings.settlementHousingHeadroom, 0, 40);

        // Settlement Baseline — mostly the shape of the initial world at generation.
        DrawSectionHeader(listing, "LW_SettingsSection_Baseline");
        DrawIntSlider(listing, "LW_Settings_HumanAdults".Translate(), ref settings.baselineHumanSettlementAdults, 0, 120);
        DrawIntSlider(listing, "LW_Settings_NonHumanAdults".Translate(), ref settings.baselineNonHumanSettlementAdults, 0, 120);
        DrawIntSlider(listing, "LW_Settings_MinAdults".Translate(), ref settings.minSettlementAdults, 0, 120);
        DrawIntSlider(listing, "LW_Settings_MaxAdults".Translate(), ref settings.maxSettlementAdults, 1, 200);
        DrawIntSlider(listing, "LW_Settings_FoodPerCitizen".Translate(), ref settings.foodPerCitizen, 0, 50);
        DrawIntSlider(listing, "LW_Settings_SteelPerCitizen".Translate(), ref settings.steelPerCitizen, 0, 100);

        // Performance — cadence/throughput knobs that do not change conservation.
        DrawSectionHeader(listing, "LW_SettingsSection_Performance");
        listing.CheckboxLabeled(
            "LW_Settings_WorldMapSpeedTest".Translate(),
            ref settings.worldMapSpeedTestEnabled,
            "LW_Settings_WorldMapSpeedTestTip".Translate());
        DrawWorldMapSpeedChoice(listing, settings);

        // Compatibility — what Living World cedes to other mods (read-only; reflects active mods).
        DrawSectionHeader(listing, "LW_SettingsSection_Compatibility");
        DrawCompatibilityState(listing);

        // Debug — developer diagnostics, kept visibly separate from normal player options.
        DrawSectionHeader(listing, "LW_SettingsSection_Debug");
        listing.CheckboxLabeled("LW_Settings_DebugLogging".Translate(), ref settings.debugLogging, "LW_Settings_DebugLoggingTip".Translate());
        listing.CheckboxLabeled("LW_Settings_BootstrapLedger".Translate(), ref settings.bootstrapLedgerDuringWorldGeneration, "LW_Settings_BootstrapLedgerTip".Translate());

        if (settings.maxSettlementAdults < settings.minSettlementAdults)
        {
            settings.maxSettlementAdults = settings.minSettlementAdults;
        }

        listing.End();
    }

    private static void DrawSectionHeader(Listing_Standard listing, string key)
    {
        listing.Gap(8f);
        var previous = GUI.color;
        GUI.color = new Color(0.85f, 0.82f, 0.55f);
        listing.Label(key.Translate());
        GUI.color = previous;
        listing.GapLine(2f);
    }

    // Read-only: surfaces what Living World has ceded to a detected conflicting mod, so the player
    // understands why a system is quiet. Reflects the actually-active mods.
    private static void DrawCompatibilityState(Listing_Standard listing)
    {
        var previous = GUI.color;
        GUI.color = new Color(0.72f, 0.72f, 0.72f);

        var anyCede = false;
        if (ModsConfig.IsActive("Torann.RimWar"))
        {
            listing.Label("LW_WorldWarDisabledByRimWar".Translate());
            anyCede = true;
        }

        if (ModsConfig.IsActive("Matathias.Empire"))
        {
            listing.Label("LW_EmpireActiveNote".Translate());
            anyCede = true;
        }

        if (!anyCede)
        {
            listing.Label("LW_Settings_CompatNoneActive".Translate());
        }

        GUI.color = previous;
    }

    private static void DrawIntSlider(Listing_Standard listing, string label, ref int value, int min, int max)
    {
        listing.Label($"{label}: {value}");
        value = (int)listing.Slider(value, min, max);
    }

    private static void DrawWorldMapSpeedChoice(Listing_Standard listing, LivingWorldSettings settings)
    {
        if (listing.RadioButton("LW_Settings_WorldMapSpeedMultiplier".Translate(3.Named("multiplier")).ToString(), settings.worldMapSpeedMultiplier == 3))
        {
            settings.worldMapSpeedMultiplier = 3;
        }

        if (listing.RadioButton("LW_Settings_WorldMapSpeedMultiplier".Translate(5.Named("multiplier")).ToString(), settings.worldMapSpeedMultiplier == 5))
        {
            settings.worldMapSpeedMultiplier = 5;
        }

        if (listing.RadioButton("LW_Settings_WorldMapSpeedMultiplier".Translate(10.Named("multiplier")).ToString(), settings.worldMapSpeedMultiplier == 10))
        {
            settings.worldMapSpeedMultiplier = 10;
        }
    }
}
