using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using Verse;

namespace LivingWorld.RimWorld;

public static class LivingWorldSettlementMapResourceTracker
{
    private sealed record TrackedResource(int MapId, EntityId ReturnOwnerId, string ResourceKey);

    private static readonly Dictionary<int, TrackedResource> ResourcesByThingId = new();

    public static void Track(Thing? thing, EntityId returnOwnerId, string resourceKey)
    {
        if (thing == null || returnOwnerId.Value <= 0 || string.IsNullOrWhiteSpace(resourceKey))
        {
            return;
        }

        var map = thing.Map;
        if (map == null)
        {
            return;
        }

        ResourcesByThingId[thing.thingIDNumber] = new TrackedResource(
            map.uniqueID,
            returnOwnerId,
            resourceKey.Trim());
    }

    public static int ReconcileMap(WorldState state, Map? map, string reason)
    {
        if (state == null || map == null)
        {
            return 0;
        }

        var returned = 0;
        foreach (var pair in ResourcesByThingId.ToList())
        {
            if (pair.Value.MapId != map.uniqueID)
            {
                continue;
            }

            ResourcesByThingId.Remove(pair.Key);
            var thing = map.listerThings?.AllThings
                .FirstOrDefault(candidate => candidate?.thingIDNumber == pair.Key);
            if (thing == null || !thing.Spawned || thing.Map != map || thing.stackCount <= 0)
            {
                continue;
            }

            var ownerId = ResolveReturnOwner(state, pair.Value.ReturnOwnerId);
            ResourceLedgerService.AddResource(state, ownerId, pair.Value.ResourceKey, thing.stackCount);
            returned += thing.stackCount;
        }

        return returned;
    }

    private static EntityId ResolveReturnOwner(WorldState state, EntityId originalOwnerId)
    {
        var settlement = state.GetSettlement(originalOwnerId);
        if (settlement?.IsActive == true)
        {
            return originalOwnerId;
        }

        var ruin = state.Ruins
            .Where(candidate => candidate.Status == RuinStatus.Active)
            .OrderByDescending(candidate => candidate.CreatedTick)
            .ThenByDescending(candidate => candidate.Id.Value)
            .FirstOrDefault(candidate => candidate.OriginalSettlementId == originalOwnerId);
        return ruin?.Id ?? originalOwnerId;
    }
}
