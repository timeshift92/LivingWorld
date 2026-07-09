using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Per-colonist combat-kit assignments for the armory / mobilization module: the weapon and armour def a
/// colonist should always take when mobilized, overriding the skill-based pool pick (the "hybrid" half). A
/// colonist with no assignment falls back to the skill selection. Persisted with the game; keyed by pawn
/// reference. Auto-created by RimWorld (any GameComponent with a (Game) constructor).
/// </summary>
public sealed class ArmoryAssignmentComponent : GameComponent
{
    private Dictionary<Pawn, string> weaponDefByPawn = new();
    private Dictionary<Pawn, string> armorDefByPawn = new();

    private List<Pawn>? weaponKeysScratch;
    private List<string>? weaponValsScratch;
    private List<Pawn>? armorKeysScratch;
    private List<string>? armorValsScratch;

    public ArmoryAssignmentComponent(Game game)
    {
    }

    public string? AssignedWeaponDef(Pawn pawn)
    {
        return pawn != null && weaponDefByPawn.TryGetValue(pawn, out var def) ? def : null;
    }

    public string? AssignedArmorDef(Pawn pawn)
    {
        return pawn != null && armorDefByPawn.TryGetValue(pawn, out var def) ? def : null;
    }

    public bool HasAssignment(Pawn pawn)
    {
        return pawn != null && (weaponDefByPawn.ContainsKey(pawn) || armorDefByPawn.ContainsKey(pawn));
    }

    // Captures whatever the colonist is currently carrying/wearing as their assigned combat kit.
    public void AssignFromCurrent(Pawn pawn)
    {
        if (pawn == null)
        {
            return;
        }

        var weapon = pawn.equipment?.Primary?.def?.defName;
        if (!string.IsNullOrEmpty(weapon))
        {
            weaponDefByPawn[pawn] = weapon!;
        }

        var armor = pawn.apparel?.WornApparel?
            .Where(apparel => apparel.def.GetStatValueAbstract(StatDefOf.ArmorRating_Sharp) >= 0.4f)
            .OrderByDescending(apparel => apparel.MarketValue)
            .FirstOrDefault()?.def?.defName;
        if (!string.IsNullOrEmpty(armor))
        {
            armorDefByPawn[pawn] = armor!;
        }
    }

    public void Clear(Pawn pawn)
    {
        if (pawn == null)
        {
            return;
        }

        weaponDefByPawn.Remove(pawn);
        armorDefByPawn.Remove(pawn);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(
            ref weaponDefByPawn, "livingWorld_armoryWeaponByPawn",
            LookMode.Reference, LookMode.Value, ref weaponKeysScratch, ref weaponValsScratch);
        Scribe_Collections.Look(
            ref armorDefByPawn, "livingWorld_armoryArmorByPawn",
            LookMode.Reference, LookMode.Value, ref armorKeysScratch, ref armorValsScratch);
        weaponDefByPawn ??= new Dictionary<Pawn, string>();
        armorDefByPawn ??= new Dictionary<Pawn, string>();
    }

    public static ArmoryAssignmentComponent? Instance => Current.Game?.GetComponent<ArmoryAssignmentComponent>();
}
