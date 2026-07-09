using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using Verse;

namespace LivingWorld.RimWorld;

public static class LivingWorldSettlementMapFacilityTracker
{
    private sealed record TrackedFacilityThing(int MapId, EntityId FacilityId);

    private static readonly Dictionary<int, TrackedFacilityThing> ThingsByThingId = new();

    public static void Track(Thing? thing, EntityId facilityId)
    {
        if (thing == null || facilityId.Kind != EntityKind.SettlementFacility)
        {
            return;
        }

        var map = thing.Map;
        if (map == null)
        {
            return;
        }

        ThingsByThingId[thing.thingIDNumber] = new TrackedFacilityThing(map.uniqueID, facilityId);
    }

    public static int ReconcileMap(WorldState state, Map? map, string reason)
    {
        if (state == null || map == null)
        {
            return 0;
        }

        var trackedForMap = ThingsByThingId
            .Where(pair => pair.Value.MapId == map.uniqueID)
            .ToList();
        if (trackedForMap.Count == 0)
        {
            return 0;
        }

        var allThings = map.listerThings?.AllThings ?? new List<Thing>();
        var updates = 0;
        foreach (var group in trackedForMap.GroupBy(pair => pair.Value.FacilityId))
        {
            var total = group.Count();
            var surviving = group.Count(pair =>
            {
                var thing = allThings.FirstOrDefault(candidate => candidate?.thingIDNumber == pair.Key);
                return thing != null && thing.Spawned && thing.Map == map;
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

        foreach (var pair in trackedForMap)
        {
            ThingsByThingId.Remove(pair.Key);
        }

        return updates;
    }
}
