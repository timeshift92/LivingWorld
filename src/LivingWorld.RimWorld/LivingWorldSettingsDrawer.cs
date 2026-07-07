using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

public static class LivingWorldSettingsDrawer
{
    public const float PreferredHeight = 390f;

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
