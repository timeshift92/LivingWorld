using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(Page_CreateWorldParams), "DoWindowContents")]
public static class LivingWorldWorldGenSettingsPatch
{
    private const float ButtonWidth = 210f;
    private const float ButtonHeight = 34f;

    private static void Postfix(Rect rect)
    {
        Text.Font = GameFont.Small;

        // Top-right corner of the params page: clear of the vanilla planet sliders on the left and
        // the bottom navigation buttons, so we never overlap vanilla UI at any resolution.
        var buttonRect = new Rect(rect.xMax - ButtonWidth, rect.y, ButtonWidth, ButtonHeight);
        if (Widgets.ButtonText(buttonRect, "LW_WorldGenButton".Translate()))
        {
            OpenSettingsWindow();
        }
    }

    private static void OpenSettingsWindow()
    {
        if (!Find.WindowStack.TryRemove(typeof(LivingWorldWorldGenSettingsWindow)))
        {
            Find.WindowStack.Add(new LivingWorldWorldGenSettingsWindow());
        }
    }
}
