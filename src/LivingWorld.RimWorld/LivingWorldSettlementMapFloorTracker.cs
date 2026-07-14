using System.Linq;
using LivingWorld.Core;
using Verse;

namespace LivingWorld.RimWorld;

public static class LivingWorldSettlementMapFloorTracker
{
    public static void Track(Map? map, EntityId facilityId, IntVec3 cell, string terrainDefName)
    {
        if (map == null || !cell.InBounds(map))
        {
            return;
        }

        LivingWorldSettlementVisitMapComponent.For(map)?.TrackFloor(facilityId, cell, terrainDefName);
    }

    public static int ReconcileMap(WorldState state, Map? map, string reason)
    {
        if (state == null || map == null)
        {
            return 0;
        }

        var component = LivingWorldSettlementVisitMapComponent.For(map);
        if (component == null || component.Floors.Count == 0)
        {
            return 0;
        }

        var updates = 0;
        foreach (var group in component.Floors.GroupBy(entry => entry.FacilityId))
        {
            var total = group.Count();
            var surviving = group.Count(entry =>
            {
                var cell = new IntVec3(entry.X, 0, entry.Z);
                if (!cell.InBounds(map))
                {
                    return false;
                }

                var terrain = map.terrainGrid.TerrainAt(cell);
                return terrain != null && terrain.defName == entry.TerrainDefName;
            });

            var result = SettlementMapDamageService.ReconcileFacilityDamage(
                state,
                new SettlementMapDamageRequest(group.Key, total, surviving, reason));
            if (result.Status == SettlementMapDamageStatus.Success)
            {
                updates++;
            }
        }

        return updates;
    }
}
