using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using Verse;

namespace LivingWorld.RimWorld;

public static class LivingWorldSettlementMapFloorTracker
{
    private sealed record TrackedFloor(int MapId, EntityId FacilityId, int X, int Z, string TerrainDefName);

    private static readonly List<TrackedFloor> Floors = new();

    public static void Track(Map? map, EntityId facilityId, IntVec3 cell, string terrainDefName)
    {
        if (map == null
            || facilityId.Kind != EntityKind.SettlementFacility
            || !cell.InBounds(map)
            || string.IsNullOrWhiteSpace(terrainDefName))
        {
            return;
        }

        Floors.Add(new TrackedFloor(
            map.uniqueID,
            facilityId,
            cell.x,
            cell.z,
            terrainDefName.Trim()));
    }

    public static int ReconcileMap(WorldState state, Map? map, string reason)
    {
        if (state == null || map == null)
        {
            return 0;
        }

        var trackedForMap = Floors
            .Where(floor => floor.MapId == map.uniqueID)
            .ToList();
        if (trackedForMap.Count == 0)
        {
            return 0;
        }

        var updates = 0;
        foreach (var group in trackedForMap.GroupBy(floor => floor.FacilityId))
        {
            var total = group.Count();
            var surviving = group.Count(floor =>
            {
                var cell = new IntVec3(floor.X, 0, floor.Z);
                if (!cell.InBounds(map))
                {
                    return false;
                }

                var terrain = map.terrainGrid.TerrainAt(cell);
                return terrain != null && terrain.defName == floor.TerrainDefName;
            });

            var result = SettlementMapDamageService.ReconcileFacilityDamage(
                state,
                new SettlementMapDamageRequest(
                    group.Key,
                    total,
                    surviving,
                    reason));
            if (result.Status == SettlementMapDamageStatus.Success)
            {
                updates++;
            }
        }

        Floors.RemoveAll(floor => floor.MapId == map.uniqueID);
        return updates;
    }
}
