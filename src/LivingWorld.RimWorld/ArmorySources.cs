using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Single source of truth for the colony's armory storage: the mod's own <see cref="Building_ArmoryRack"/>
/// plus any ordinary <see cref="Building_Storage"/> the player designated with the armory-role gizmo (see
/// <see cref="ArmoryStorageRolesComponent"/>). The whole mobilization system reads gear through this so it
/// works with any storage or storage mod, not just the built-in racks.
/// </summary>
public static class ArmorySources
{
    public static List<(Building_Storage building, ArmoryRackKind kind)> All(Map? map)
    {
        var list = new List<(Building_Storage, ArmoryRackKind)>();
        if (map?.listerBuildings == null)
        {
            return list;
        }

        foreach (var rack in map.listerBuildings.AllBuildingsColonistOfClass<Building_ArmoryRack>())
        {
            if (rack != null && rack.Spawned)
            {
                list.Add((rack, rack.Kind));
            }
        }

        var roles = ArmoryStorageRolesComponent.For(map);
        if (roles != null)
        {
            foreach (var storage in map.listerBuildings.AllBuildingsColonistOfClass<Building_Storage>())
            {
                if (storage != null && storage.Spawned && storage is not Building_ArmoryRack
                    && roles.TryGetRole(storage, out var kind))
                {
                    list.Add((storage, kind));
                }
            }
        }

        return list;
    }

    public static IEnumerable<Thing> StoredItems(Building_Storage building)
    {
        var group = building?.slotGroup;
        if (group?.HeldThings == null)
        {
            yield break;
        }

        foreach (var thing in group.HeldThings)
        {
            if (thing != null)
            {
                yield return thing;
            }
        }
    }

    public static IEnumerable<Thing> Items(Map? map, ArmoryRackKind kind)
    {
        return All(map).Where(source => source.kind == kind).SelectMany(source => StoredItems(source.building));
    }

    // The nearest armory storage of a kind (for pathing a fetch/return job to it).
    public static Building_Storage? Nearest(Map? map, IntVec3 from, ArmoryRackKind kind)
    {
        return All(map)
            .Where(source => source.kind == kind)
            .Select(source => source.building)
            .OrderBy(building => from.DistanceToSquared(building.Position))
            .FirstOrDefault();
    }
}
