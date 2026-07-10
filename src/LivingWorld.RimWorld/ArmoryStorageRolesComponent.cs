using System.Collections.Generic;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Per-map record of which ordinary storage buildings the player has designated as armory sources, and in
/// what role (weapons / armour / clothing). Lets the mobilization system draw gear from ANY storage — a
/// vanilla shelf, an outfit stand, a weapon-rack mod — not only the mod's own <see cref="Building_ArmoryRack"/>.
/// Keyed by building thingIDNumber so it scribes as plain values (no cross-reference list). Auto-created by
/// RimWorld for every map.
/// </summary>
public sealed class ArmoryStorageRolesComponent : MapComponent
{
    private Dictionary<int, int> roleByBuildingId = new();

    private List<int>? keysScratch;
    private List<int>? valsScratch;

    public ArmoryStorageRolesComponent(Map map)
        : base(map)
    {
    }

    public bool TryGetRole(Building building, out ArmoryRackKind kind)
    {
        kind = ArmoryRackKind.Weapon;
        if (building == null || !roleByBuildingId.TryGetValue(building.thingIDNumber, out var value))
        {
            return false;
        }

        kind = (ArmoryRackKind)value;
        return true;
    }

    public ArmoryRackKind? RoleOf(Building building)
    {
        return TryGetRole(building, out var kind) ? kind : (ArmoryRackKind?)null;
    }

    public void SetRole(Building building, ArmoryRackKind? kind)
    {
        if (building == null)
        {
            return;
        }

        if (kind == null)
        {
            roleByBuildingId.Remove(building.thingIDNumber);
        }
        else
        {
            roleByBuildingId[building.thingIDNumber] = (int)kind.Value;
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(
            ref roleByBuildingId, "livingWorld_armoryStorageRoles",
            LookMode.Value, LookMode.Value, ref keysScratch, ref valsScratch);
        roleByBuildingId ??= new Dictionary<int, int>();
    }

    public static ArmoryStorageRolesComponent? For(Map map)
    {
        return map?.GetComponent<ArmoryStorageRolesComponent>();
    }
}
