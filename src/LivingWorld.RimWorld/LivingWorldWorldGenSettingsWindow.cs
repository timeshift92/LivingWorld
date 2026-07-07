using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

public sealed class LivingWorldWorldGenSettingsWindow : Window
{
    private const float HeaderHeight = 36f;
    private const float FooterHeight = 64f;
    private const float ScrollbarWidth = 16f;
    private const float CloseButtonWidth = 160f;
    private const float CloseButtonHeight = 38f;

    private Vector2 scrollPosition;

    public LivingWorldWorldGenSettingsWindow()
    {
        closeOnCancel = true;
        doCloseButton = false;
        doCloseX = true;
        absorbInputAroundWindow = true;
        forcePause = true;
    }

    public override Vector2 InitialSize => new Vector2(620f, 500f);

    public override void DoWindowContents(Rect inRect)
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();

        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 28f), "LW_WorldGenTitle".Translate());
        var settingsOutRect = new Rect(
            inRect.x,
            inRect.y + HeaderHeight,
            inRect.width,
            Mathf.Max(0f, inRect.height - HeaderHeight - FooterHeight));
        var settingsViewRect = new Rect(
            0f,
            0f,
            settingsOutRect.width - ScrollbarWidth,
            Mathf.Max(settingsOutRect.height, LivingWorldSettingsDrawer.PreferredHeight));

        Widgets.BeginScrollView(settingsOutRect, ref scrollPosition, settingsViewRect);
        LivingWorldSettingsDrawer.Draw(settingsViewRect, settings);
        Widgets.EndScrollView();

        var footerY = inRect.yMax - FooterHeight + 12f;
        Widgets.DrawLineHorizontal(inRect.x, footerY - 12f, inRect.width);
        var closeRect = new Rect(
            inRect.center.x - CloseButtonWidth / 2f,
            footerY,
            CloseButtonWidth,
            CloseButtonHeight);
        if (Widgets.ButtonText(closeRect, "CloseButton".Translate()))
        {
            Close();
        }

        settings.Write();
    }
}
