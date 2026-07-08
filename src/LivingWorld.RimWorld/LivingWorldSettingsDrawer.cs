using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

public static class LivingWorldSettingsDrawer
{
    public const float PreferredHeight = 390f;

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

    public static void Draw(Rect inRect, LivingWorldSettings settings)
    {
        var listing = new Listing_Standard();
        listing.Begin(inRect);

        listing.CheckboxLabeled(
            "LW_Settings_BootstrapLedger".Translate(),
            ref settings.bootstrapLedgerDuringWorldGeneration,
            "LW_Settings_BootstrapLedgerTip".Translate());

        listing.CheckboxLabeled(
            "LW_Settings_DebugLogging".Translate(),
            ref settings.debugLogging,
            "LW_Settings_DebugLoggingTip".Translate());

        listing.GapLine();

        DrawIntSlider(listing, "LW_Settings_HumanAdults".Translate(), ref settings.baselineHumanSettlementAdults, 0, 120);
        DrawIntSlider(listing, "LW_Settings_NonHumanAdults".Translate(), ref settings.baselineNonHumanSettlementAdults, 0, 120);
        DrawIntSlider(listing, "LW_Settings_MinAdults".Translate(), ref settings.minSettlementAdults, 0, 120);
        DrawIntSlider(listing, "LW_Settings_MaxAdults".Translate(), ref settings.maxSettlementAdults, 1, 200);
        DrawIntSlider(listing, "LW_Settings_FoodPerCitizen".Translate(), ref settings.foodPerCitizen, 0, 50);
        DrawIntSlider(listing, "LW_Settings_SteelPerCitizen".Translate(), ref settings.steelPerCitizen, 0, 100);

        listing.GapLine();

        listing.CheckboxLabeled(
            "LW_SettingDrifterFlow".Translate(),
            ref settings.drifterFlowEnabled);
        DrawIntSlider(listing, "LW_SettingDrifterArrivals".Translate(), ref settings.maxDrifterArrivalsPerDay, 0, 10);
        DrawIntSlider(listing, "LW_SettingDrifterCeiling".Translate(), ref settings.drifterHardCeiling, 100, 10000);

        if (settings.maxSettlementAdults < settings.minSettlementAdults)
        {
            settings.maxSettlementAdults = settings.minSettlementAdults;
        }

        listing.End();
    }

    private static void DrawIntSlider(Listing_Standard listing, string label, ref int value, int min, int max)
    {
        listing.Label($"{label}: {value}");
        value = (int)listing.Slider(value, min, max);
    }
}
