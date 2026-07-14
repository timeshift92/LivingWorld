using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using Verse;

namespace LivingWorld.RimWorld;

public static class LivingWorldSettlementMapResourceTracker
{
    public static void Track(Thing? thing, EntityId returnOwnerId, string resourceKey)
    {
        var map = thing?.MapHeld;
        if (thing == null || map == null)
        {
            return;
        }

        LivingWorldSettlementVisitMapComponent.For(map)?.TrackResource(thing, returnOwnerId, resourceKey);
    }

    public static void TrackSplit(Thing? source, Thing? split)
    {
        if (source == null || split == null || !TryGetTrackedResource(source, out var tracked, out var component))
        {
            return;
        }

        component.TrackResource(split, tracked.ReturnOwnerId, tracked.ResourceKey);
    }

    public static bool AllowStack(Thing? destination, Thing? source)
    {
        var destinationTracked = TryGetTrackedResource(destination, out var destinationInfo, out _);
        var sourceTracked = TryGetTrackedResource(source, out var sourceInfo, out _);
        if (!destinationTracked && !sourceTracked)
        {
            return true;
        }

        if (destinationTracked != sourceTracked)
        {
            return false;
        }

        return destinationInfo.ReturnOwnerKind == sourceInfo.ReturnOwnerKind
            && destinationInfo.ReturnOwnerValue == sourceInfo.ReturnOwnerValue
            && destinationInfo.ResourceKey == sourceInfo.ResourceKey;
    }

    public static void NotifyAbsorbed(Thing? source)
    {
        if (source == null || !source.Destroyed || !TryGetTrackedResource(source, out _, out var component))
        {
            return;
        }

        component.RemoveResource(source.thingIDNumber);
    }

    public static int ReconcileMap(WorldState state, Map? map, string reason)
    {
        if (state == null || map == null)
        {
            return 0;
        }

        var component = LivingWorldSettlementVisitMapComponent.For(map);
        if (component == null || component.Resources.Count == 0)
        {
            return 0;
        }

        var allThings = map.listerThings?.AllThings ?? new List<Thing>();
        var returned = 0;
        foreach (var tracked in component.Resources.ToList())
        {
            var thing = allThings.FirstOrDefault(candidate => candidate?.thingIDNumber == tracked.ThingId);
            if (thing == null || !thing.Spawned || thing.Map != map || thing.stackCount <= 0)
            {
                component.RemoveResource(tracked.ThingId);
                continue;
            }

            var ownerId = ResolveReturnOwner(state, tracked.ReturnOwnerId);
            ResourceLedgerService.AddResource(state, ownerId, tracked.ResourceKey, thing.stackCount);
            component.RemoveResource(tracked.ThingId);
            returned += thing.stackCount;
        }

        return returned;
    }

    private static bool TryGetTrackedResource(
        Thing? thing,
        out LivingWorldTrackedMapResource tracked,
        out LivingWorldSettlementVisitMapComponent component)
    {
        tracked = null!;
        component = null!;
        if (thing == null)
        {
            return false;
        }

        var map = thing.MapHeld;
        if (map != null)
        {
            component = LivingWorldSettlementVisitMapComponent.For(map)!;
            if (component != null && component.TryGetResource(thing.thingIDNumber, out tracked))
            {
                return true;
            }
        }

        foreach (var candidateMap in Find.Maps)
        {
            var candidate = LivingWorldSettlementVisitMapComponent.For(candidateMap);
            if (candidate != null && candidate.TryGetResource(thing.thingIDNumber, out tracked))
            {
                component = candidate;
                return true;
            }
        }

        return false;
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
