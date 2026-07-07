using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(Page_CreateWorldParams), "DoWindowContents")]
public static class LivingWorldWorldGenSettingsPatch
{
    private static void Postfix(Rect rect)
    {
        Text.Font = GameFont.Small;

        var buttonRect = new Rect(160f, rect.y + rect.height - 118f, 170f, 32f);
        if (Widgets.ButtonText(buttonRect, "LW_MainTitle".Translate()))
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
