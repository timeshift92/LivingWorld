using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

public static class LivingWorldSettlementMapResourceTracker
{
    public static void Track(Thing? thing, EntityId returnOwnerId, string resourceKey, int reservedQuantity = 0)
    {
        var map = thing?.MapHeld;
        if (thing == null || map == null)
        {
            return;
        }

        LivingWorldSettlementVisitMapComponent.For(map)?.TrackResource(
            thing,
            returnOwnerId,
            resourceKey,
            reservedQuantity);
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

        var returned = 0;
        foreach (var tracked in component.Resources.ToList())
        {
            var thing = FindTrackedThing(map, tracked.ThingId, out var heldByPlayer);
            if (thing == null || thing.Destroyed || heldByPlayer)
            {
                component.RemoveResource(tracked.ThingId);
                continue;
            }

            var quantity = tracked.ReservedQuantity > 0
                ? tracked.ReservedQuantity
                : thing.stackCount;
            if (quantity <= 0)
            {
                component.RemoveResource(tracked.ThingId);
                continue;
            }

            var ownerId = ResolveReturnOwner(state, tracked.ReturnOwnerId);
            ResourceLedgerService.AddResource(state, ownerId, tracked.ResourceKey, quantity);
            component.RemoveResource(tracked.ThingId);
            returned += quantity;
            if (!thing.Destroyed)
            {
                thing.Destroy(DestroyMode.Vanish);
            }
        }

        return returned;
    }

    private static Thing? FindTrackedThing(Map map, int thingId, out bool heldByPlayer)
    {
        heldByPlayer = false;
        var spawned = (map.listerThings?.AllThings ?? new List<Thing>())
            .FirstOrDefault(candidate => candidate?.thingIDNumber == thingId);
        if (spawned != null)
        {
            return spawned;
        }

        foreach (var pawn in map.mapPawns.AllPawns)
        {
            if (pawn == null)
            {
                continue;
            }

            var held = (pawn.equipment?.AllEquipmentListForReading.Cast<Thing>() ?? Enumerable.Empty<Thing>())
                .Concat(pawn.apparel?.WornApparel.Cast<Thing>() ?? Enumerable.Empty<Thing>())
                .Concat(pawn.inventory?.innerContainer.InnerListForReading.Cast<Thing>() ?? Enumerable.Empty<Thing>())
                .FirstOrDefault(candidate => candidate.thingIDNumber == thingId);
            if (held == null)
            {
                continue;
            }

            heldByPlayer = pawn.Faction == Faction.OfPlayer;
            return held;
        }

        return null;
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
