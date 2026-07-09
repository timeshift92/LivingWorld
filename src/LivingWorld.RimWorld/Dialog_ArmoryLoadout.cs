using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Per-colonist combat-loadout configuration window. Lets the player pick the exact weapon and armour pieces
/// a colonist takes when mobilized, chosen from what is stocked on the armory racks. An empty choice means
/// "auto by skill". Reads and writes <see cref="ArmoryAssignmentComponent"/> directly. Fail-safe: a missing
/// component or map just shows the auto state.
/// </summary>
public sealed class Dialog_ArmoryLoadout : Window
{
    private readonly Pawn pawn;

    public Dialog_ArmoryLoadout(Pawn pawn)
    {
        this.pawn = pawn;
        doCloseX = true;
        doCloseButton = true;
        forcePause = true;
        absorbInputAroundWindow = true;
    }

    public override Vector2 InitialSize => new Vector2(540f, 520f);

    public override void DoWindowContents(Rect inRect)
    {
        var comp = ArmoryAssignmentComponent.Instance;

        var listing = new Listing_Standard();
        listing.Begin(inRect);

        Text.Font = GameFont.Medium;
        listing.Label("LW_LoadoutTitle".Translate(pawn.LabelShortCap));
        Text.Font = GameFont.Small;
        listing.GapLine();

        if (comp == null)
        {
            listing.Label("LW_LoadoutAuto".Translate());
            listing.End();
            return;
        }

        // Weapon row.
        var weaponDef = comp.AssignedWeaponDef(pawn);
        var weaponRow = listing.GetRect(30f);
        Widgets.Label(
            new Rect(weaponRow.x, weaponRow.y, weaponRow.width * 0.6f, weaponRow.height),
            "LW_LoadoutWeapon".Translate() + ": " + LabelOf(weaponDef));
        if (Widgets.ButtonText(
                new Rect(weaponRow.x + weaponRow.width * 0.62f, weaponRow.y, weaponRow.width * 0.38f, weaponRow.height),
                "LW_LoadoutChange".Translate()))
        {
            OpenWeaponMenu(comp);
        }

        listing.Gap();
        listing.Label("LW_LoadoutArmor".Translate() + ":");

        var armorDefs = comp.AssignedArmorDefs(pawn);
        if (armorDefs.Count == 0)
        {
            listing.Label("   " + "LW_LoadoutAuto".Translate());
        }
        else
        {
            foreach (var def in armorDefs.ToList())
            {
                var row = listing.GetRect(26f);
                Widgets.Label(
                    new Rect(row.x + 12f, row.y, row.width * 0.7f, row.height), LabelOf(def));
                if (Widgets.ButtonText(
                        new Rect(row.x + row.width * 0.74f, row.y, row.width * 0.26f, row.height),
                        "LW_LoadoutRemove".Translate()))
                {
                    var reduced = armorDefs.Where(d => d != def).ToList();
                    comp.SetLoadout(pawn, comp.AssignedWeaponDef(pawn), reduced);
                }
            }
        }

        listing.Gap(6f);
        if (listing.ButtonText("LW_LoadoutAddArmor".Translate()))
        {
            OpenArmorMenu(comp);
        }

        listing.Gap();
        if (listing.ButtonText("LW_LoadoutClear".Translate()))
        {
            comp.Clear(pawn);
        }

        listing.End();
    }

    private void OpenWeaponMenu(ArmoryAssignmentComponent comp)
    {
        var options = new List<FloatMenuOption>
        {
            new FloatMenuOption("LW_LoadoutAuto".Translate(),
                () => comp.SetLoadout(pawn, null, comp.AssignedArmorDefs(pawn))),
        };
        foreach (var def in AvailableDefs(ArmoryRackKind.Weapon))
        {
            var captured = def;
            options.Add(new FloatMenuOption(
                captured.LabelCap,
                () => comp.SetLoadout(pawn, captured.defName, comp.AssignedArmorDefs(pawn))));
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }

    private void OpenArmorMenu(ArmoryAssignmentComponent comp)
    {
        var current = comp.AssignedArmorDefs(pawn);
        var options = new List<FloatMenuOption>();
        foreach (var def in AvailableDefs(ArmoryRackKind.Armor).Where(d => !current.Contains(d.defName)))
        {
            var captured = def;
            options.Add(new FloatMenuOption(captured.LabelCap, () =>
            {
                var set = new List<string>(comp.AssignedArmorDefs(pawn)) { captured.defName };
                comp.SetLoadout(pawn, comp.AssignedWeaponDef(pawn), set);
            }));
        }

        if (options.Count == 0)
        {
            options.Add(new FloatMenuOption("LW_LoadoutNothingStocked".Translate(), null));
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }

    // Distinct thing defs stocked on the colony's armory racks of the given kind.
    private IEnumerable<ThingDef> AvailableDefs(ArmoryRackKind kind)
    {
        var racks = pawn?.Map?.listerBuildings?.AllBuildingsColonistOfClass<Building_ArmoryRack>();
        if (racks == null)
        {
            return Enumerable.Empty<ThingDef>();
        }

        return racks
            .Where(rack => rack.Kind == kind)
            .SelectMany(rack => rack.StoredItems)
            .Where(thing => thing?.def != null
                            && (kind == ArmoryRackKind.Weapon ? thing.def.IsWeapon : thing is Apparel))
            .Select(thing => thing.def)
            .Distinct()
            .OrderBy(def => def.label);
    }

    private static string LabelOf(string? defName)
    {
        if (string.IsNullOrEmpty(defName))
        {
            return "LW_LoadoutAuto".Translate();
        }

        return DefDatabase<ThingDef>.GetNamedSilentFail(defName)?.LabelCap ?? defName!;
    }
}
