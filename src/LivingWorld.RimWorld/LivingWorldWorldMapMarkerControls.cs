using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

public enum LivingWorldWorldMapMarkerKind
{
    Warband,
    Caravan,
    Scout,
    Diplomat,
    Settler
}

public static class LivingWorldWorldMapMarkerControls
{
    public static bool IsVisible(LivingWorldWorldMapMarkerKind kind, LivingWorldSettings settings)
    {
        return kind switch
        {
            LivingWorldWorldMapMarkerKind.Warband => settings.showWarbandMarkers,
            LivingWorldWorldMapMarkerKind.Caravan => settings.showCaravanMarkers,
            LivingWorldWorldMapMarkerKind.Scout => settings.showScoutMarkers,
            LivingWorldWorldMapMarkerKind.Diplomat => settings.showDiplomatMarkers,
            LivingWorldWorldMapMarkerKind.Settler => settings.showSettlerMarkers,
            _ => true
        };
    }

    public static void DrawSettings(Listing_Standard listing, LivingWorldSettings settings)
    {
        listing.Label("LW_MapMarkerLegend".Translate());
        listing.CheckboxLabeled("LW_MapMarkerFilter_Warband".Translate(), ref settings.showWarbandMarkers);
        listing.CheckboxLabeled("LW_MapMarkerFilter_Caravan".Translate(), ref settings.showCaravanMarkers);
        listing.CheckboxLabeled("LW_MapMarkerFilter_Scout".Translate(), ref settings.showScoutMarkers);
        listing.CheckboxLabeled("LW_MapMarkerFilter_Diplomat".Translate(), ref settings.showDiplomatMarkers);
        listing.CheckboxLabeled("LW_MapMarkerFilter_Settler".Translate(), ref settings.showSettlerMarkers);

        var previous = GUI.color;
        GUI.color = new Color(0.72f, 0.72f, 0.72f);
        listing.Label("LW_MapMarkerLegendTip".Translate());
        GUI.color = previous;
    }
}
