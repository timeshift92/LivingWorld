using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Adds an "armory role" command to any player <see cref="Building_Storage"/> (not the mod's own racks), so
/// the player can designate a vanilla shelf, an outfit stand, or any storage-mod rack as a weapons / armour /
/// clothing source that the mobilization system draws from. Gated by the armory setting; fail-open.
/// </summary>
[HarmonyPatch(typeof(Building_Storage), "GetGizmos")]
public static class LivingWorldArmoryRoleGizmoPatch
{
    public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Building_Storage __instance)
    {
        foreach (var gizmo in __result)
        {
            yield return gizmo;
        }

        Gizmo? roleGizmo = null;
        try
        {
            var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
            if (settings.armoryMobilizationEnabled
                && __instance != null
                && __instance is not Building_ArmoryRack
                && __instance.Faction == Faction.OfPlayer)
            {
                var roles = ArmoryStorageRolesComponent.For(__instance.Map);
                if (roles != null)
                {
                    var building = __instance;
                    var current = roles.RoleOf(building);
                    roleGizmo = new Command_Action
                    {
                        defaultLabel = "LW_ArmoryRole".Translate(RoleLabel(current)),
                        defaultDesc = "LW_ArmoryRoleTooltip".Translate(),
                        icon = TexCommand.Draft,
                        action = () => OpenRoleMenu(roles, building),
                    };
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Armory-role gizmo skipped safely: {ex.Message}");
            roleGizmo = null;
        }

        if (roleGizmo != null)
        {
            yield return roleGizmo;
        }
    }

    private static void OpenRoleMenu(ArmoryStorageRolesComponent roles, Building building)
    {
        var options = new List<FloatMenuOption>
        {
            new FloatMenuOption("LW_ArmoryRoleOff".Translate(), () => roles.SetRole(building, null)),
            new FloatMenuOption("LW_ArmoryRoleWeapon".Translate(), () => roles.SetRole(building, ArmoryRackKind.Weapon)),
            new FloatMenuOption("LW_ArmoryRoleArmor".Translate(), () => roles.SetRole(building, ArmoryRackKind.Armor)),
            new FloatMenuOption("LW_ArmoryRoleApparel".Translate(), () => roles.SetRole(building, ArmoryRackKind.Apparel)),
        };
        Find.WindowStack.Add(new FloatMenu(options));
    }

    private static string RoleLabel(ArmoryRackKind? kind)
    {
        return kind switch
        {
            ArmoryRackKind.Weapon => "LW_ArmoryRoleWeapon".Translate(),
            ArmoryRackKind.Armor => "LW_ArmoryRoleArmor".Translate(),
            ArmoryRackKind.Apparel => "LW_ArmoryRoleApparel".Translate(),
            _ => "LW_ArmoryRoleOff".Translate(),
        };
    }
}
