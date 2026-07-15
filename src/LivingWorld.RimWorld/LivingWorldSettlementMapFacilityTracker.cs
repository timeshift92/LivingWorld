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
        return Reconcile(state, map, reason, finalize: true);
    }

    public static int ReconcileLiveMap(WorldState state, Map? map, string reason)
    {
        return Reconcile(state, map, reason, finalize: false);
    }

    private static int Reconcile(WorldState state, Map? map, string reason, bool finalize)
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
            if (finalize && component.IsFacilityReconciled(facilityId))
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

            var surviving = survivingThings + survivingFloors;
            var previousSurviving = component.GetFacilitySurvivorCheckpoint(facilityId, total);
            if (surviving < previousSurviving)
            {
                var result = SettlementMapDamageService.ReconcileFacilityDamage(
                    state,
                    new SettlementMapDamageRequest(
                        facilityId,
                        previousSurviving,
                        surviving,
                        reason));
                if (result.Status == SettlementMapDamageStatus.Success)
                {
                    updates++;
                }
                else if (result.Status is not SettlementMapDamageStatus.NoDamage)
                {
                    throw new System.InvalidOperationException(result.Reason);
                }
            }

            component.SetFacilitySurvivorCheckpoint(facilityId, surviving);
            if (finalize)
            {
                component.MarkFacilityReconciled(facilityId);
            }
        }

        return updates;
    }
}
