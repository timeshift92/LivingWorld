using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Adds a colony "Mobilize" toggle to player colonists. It flips the per-map manual mobilization flag; the
/// colony is also mobilized automatically while enemies are present (see <see cref="MobilizationMapComponent"/>).
/// Gated by the armory setting; fail-open — any error just omits the gizmo.
/// </summary>
[HarmonyPatch(typeof(Pawn), "GetGizmos")]
public static class LivingWorldMobilizationGizmoPatch
{
    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
    {
        foreach (var gizmo in __result)
        {
            yield return gizmo;
        }

        Command_Toggle? toggle = null;
        try
        {
            if (__instance != null && __instance.IsColonist && __instance.Faction == Faction.OfPlayer)
            {
                var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
                var component = MobilizationMapComponent.For(__instance.Map);
                if (settings.armoryMobilizationEnabled && component != null)
                {
                    toggle = new Command_Toggle
                    {
                        defaultLabel = "LW_MobilizeToggle".Translate(),
                        defaultDesc = "LW_MobilizeTooltip".Translate(),
                        icon = ContentFinder<Texture2D>.Get("UI/Commands/DraftMode", false),
                        isActive = () => component.ManualMobilized,
                        toggleAction = () => component.ToggleManual(),
                    };
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Mobilization gizmo skipped safely: {ex.Message}");
            toggle = null;
        }

        if (toggle != null)
        {
            yield return toggle;
        }
    }
}
