using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using Verse;

namespace LivingWorld.RimWorld;

public static class LivingWorldSettlementMapFacilityTracker
{
    public static void Track(Thing? thing, EntityId facilityId)
    {
        var map = thing?.MapHeld;
        if (thing == null || map == null || facilityId.Kind != EntityKind.SettlementFacility)
        {
            return;
        }

        LivingWorldSettlementVisitMapComponent.For(map)?.TrackFacilityThing(thing, facilityId);
    }

    public static int ReconcileMap(WorldState state, Map? map, string reason)
    {
        if (state == null || map == null)
        {
            return 0;
        }

        var component = LivingWorldSettlementVisitMapComponent.For(map);
        if (component == null || (component.FacilityThings.Count == 0 && component.Floors.Count == 0))
        {
            return 0;
        }

        var allThings = map.listerThings?.AllThings ?? new List<Thing>();
        var updates = 0;
        var facilityIds = component.FacilityThings.Select(entry => entry.FacilityId)
            .Concat(component.Floors.Select(entry => entry.FacilityId))
            .Distinct()
            .OrderBy(id => id.Value)
            .ToList();
        foreach (var facilityId in facilityIds)
        {
            if (component.IsFacilityReconciled(facilityId))
            {
                continue;
            }

            var things = component.FacilityThings.Where(entry => entry.FacilityId == facilityId).ToList();
            var floors = component.Floors.Where(entry => entry.FacilityId == facilityId).ToList();
            var total = things.Count + floors.Count;
            var survivingThings = things.Count(entry =>
            {
                var thing = allThings.FirstOrDefault(candidate => candidate?.thingIDNumber == entry.ThingId);
                return thing != null && thing.Spawned && thing.Map == map;
            });
            var survivingFloors = floors.Count(entry =>
            {
                var cell = new IntVec3(entry.X, 0, entry.Z);
                return cell.InBounds(map)
                    && map.terrainGrid.TerrainAt(cell)?.defName == entry.TerrainDefName;
            });

            var result = SettlementMapDamageService.ReconcileFacilityDamage(
                state,
                new SettlementMapDamageRequest(
                    facilityId,
                    total,
                    survivingThings + survivingFloors,
                    reason));
            if (result.Status == SettlementMapDamageStatus.Success)
            {
                updates++;
            }

            component.MarkFacilityReconciled(facilityId);
        }

        return updates;
    }
}
