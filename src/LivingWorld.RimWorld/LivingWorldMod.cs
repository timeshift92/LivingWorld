using HarmonyLib;
using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

public sealed class LivingWorldMod : Mod
{
    private readonly LivingWorldSettings settings;

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
        LivingWorldSettingsDrawer.Draw(inRect, settings);
        settings.Write();
    }
}
