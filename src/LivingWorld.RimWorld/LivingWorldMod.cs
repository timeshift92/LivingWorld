using HarmonyLib;
using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

public sealed class LivingWorldMod : Mod
{
    private const float ScrollbarWidth = 20f;

    private readonly LivingWorldSettings settings;
    private Vector2 settingsScrollPosition;

    public LivingWorldMod(ModContentPack content)
        : base(content)
    {
        settings = GetSettings<LivingWorldSettings>();
        new Harmony("nakhmedov.livingworld").PatchAll();
        Log.Message("[LivingWorld] Loaded Living World RimWorld shell.");
    }

    public override string SettingsCategory()
    {
        return "LW_MainTitle".Translate();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        // The grouped sections do not fit a fixed settings window, so scroll them.
        var viewRect = new Rect(0f, 0f, inRect.width - ScrollbarWidth, LivingWorldSettingsDrawer.PreferredHeight);
        Widgets.BeginScrollView(inRect, ref settingsScrollPosition, viewRect);
        LivingWorldSettingsDrawer.Draw(viewRect, settings);
        Widgets.EndScrollView();
        settings.Write();
    }
}
