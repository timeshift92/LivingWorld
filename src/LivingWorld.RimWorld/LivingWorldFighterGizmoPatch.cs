using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Adds a per-colonist "Fighter" toggle to player colonists — the roster the mobilization system arms up.
/// Skill-eligible colonists show checked by default; the player checks/unchecks to override. Gated by the
/// armory setting; fail-open — any error just omits the gizmo.
/// </summary>
[HarmonyPatch(typeof(Pawn), "GetGizmos")]
public static class LivingWorldFighterGizmoPatch
{
    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
    {
        foreach (var gizmo in __result)
        {
            yield return gizmo;
        }

        var gizmos = new List<Gizmo>();
        try
        {
            if (__instance != null && __instance.IsColonist && __instance.Faction == Faction.OfPlayer
                && __instance.drafter != null)
            {
                var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
                var roster = FightersRoster.Get();
                if (settings.armoryMobilizationEnabled && roster != null)
                {
                    var pawn = __instance;
                    gizmos.Add(new Command_Toggle
                    {
                        defaultLabel = "LW_FighterToggle".Translate(),
                        defaultDesc = "LW_FighterTooltip".Translate(),
                        icon = TexCommand.Attack,
                        isActive = () => roster.IsFighter(pawn),
                        toggleAction = () => roster.Toggle(pawn),
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Fighter gizmo skipped safely: {ex.Message}");
            gizmos.Clear();
        }

        foreach (var gizmo in gizmos)
        {
            yield return gizmo;
        }
    }
}
