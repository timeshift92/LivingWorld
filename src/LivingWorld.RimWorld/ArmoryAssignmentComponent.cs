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

    // The apparel policy a colonist had before mobilization moved them to the combat policy, so stand-down
    // can put them back on it. Stored by policy id so it survives save/load.
    private Dictionary<Pawn, int> previousPolicyIdByPawn = new();

    private List<Pawn>? weaponKeysScratch;
    private List<string>? weaponValsScratch;
    private List<Pawn>? armorKeysScratch;
    private List<string>? armorValsScratch;
    private List<Pawn>? policyKeysScratch;
    private List<int>? policyValsScratch;

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

    // Remembers a colonist's apparel policy the first time mobilization moves them off it.
    public void RememberPolicy(Pawn? pawn, ApparelPolicy? policy)
    {
        if (pawn == null || policy == null || previousPolicyIdByPawn.ContainsKey(pawn))
        {
            return;
        }

        previousPolicyIdByPawn[pawn] = policy.id;
    }

    // Returns and forgets the colonist's remembered policy, resolved back to a live policy (null if gone).
    public ApparelPolicy? TakeRememberedPolicy(Pawn? pawn)
    {
        if (pawn == null || !previousPolicyIdByPawn.TryGetValue(pawn, out var id))
        {
            return null;
        }

        previousPolicyIdByPawn.Remove(pawn);
        return Current.Game?.outfitDatabase?.AllOutfits?.FirstOrDefault(policy => policy != null && policy.id == id);
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
        Scribe_Collections.Look(
            ref previousPolicyIdByPawn, "livingWorld_armoryPrevPolicyByPawn",
            LookMode.Reference, LookMode.Value, ref policyKeysScratch, ref policyValsScratch);
        weaponDefByPawn ??= new Dictionary<Pawn, string>();
        armorDefByPawn ??= new Dictionary<Pawn, string>();
        previousPolicyIdByPawn ??= new Dictionary<Pawn, int>();
    }

    public static ArmoryAssignmentComponent? Instance => Current.Game?.GetComponent<ArmoryAssignmentComponent>();
}
