using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

public sealed class LivingWorldWorldGenSettingsWindow : Window
{
    private const float HeaderHeight = 40f;
    private const float FooterHeight = 58f;
    private const float CloseButtonWidth = 160f;
    private const float CloseButtonHeight = 38f;

    public LivingWorldWorldGenSettingsWindow()
    {
        closeOnCancel = true;
        doCloseButton = false;
        doCloseX = true;
        absorbInputAroundWindow = true;
        forcePause = true;
    }

    // Compact: the essentials panel is a toggle plus two short paragraphs, so no scroll is needed.
    public override Vector2 InitialSize => new Vector2(560f, 320f);

    public override void DoWindowContents(Rect inRect)
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();

        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "LW_WorldGenTitle".Translate());
        Text.Font = GameFont.Small;

        var bodyRect = new Rect(
            inRect.x,
            inRect.y + HeaderHeight,
            inRect.width,
            Mathf.Max(0f, inRect.height - HeaderHeight - FooterHeight));
        LivingWorldSettingsDrawer.DrawWorldGenEssentials(bodyRect, settings);

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
