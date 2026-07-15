using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

public static class LivingWorldSettlementMapResourceTracker
{
    public static void Track(
        Thing? thing,
        EntityId returnOwnerId,
        string resourceKey,
        int reservedQuantity = -1,
        bool checkpointEligible = true)
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
            reservedQuantity,
            checkpointEligible);
    }

    public static void TrackSplit(Thing? source, Thing? split)
    {
        if (source == null || split == null || !TryGetTrackedResource(source, out var tracked, out var component))
        {
            return;
        }

        var splitQuantity = tracked.SplitOff(source.stackCount, split.stackCount);
        component.TrackResource(
            split,
            tracked.ReturnOwnerId,
            tracked.ResourceKey,
            splitQuantity,
            tracked.CheckpointEligible);
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
            && destinationInfo.ResourceKey == sourceInfo.ResourceKey
            && destinationInfo.CheckpointEligible == sourceInfo.CheckpointEligible;
    }

    public static void NotifyAbsorbed(
        Thing? destination,
        Thing? source,
        int destinationCountBefore,
        int sourceCountBefore)
    {
        if (destination == null
            || source == null
            || !TryGetTrackedResource(destination, out var destinationTracked, out var destinationComponent)
            || !TryGetTrackedResource(source, out var sourceTracked, out var sourceComponent)
            || destinationComponent != sourceComponent
            || destinationTracked.ReturnOwnerKind != sourceTracked.ReturnOwnerKind
            || destinationTracked.ReturnOwnerValue != sourceTracked.ReturnOwnerValue
            || destinationTracked.ResourceKey != sourceTracked.ResourceKey
            || destinationTracked.CheckpointEligible != sourceTracked.CheckpointEligible)
        {
            return;
        }

        destinationTracked.SynchronizePhysicalQuantity(Math.Max(0, destinationCountBefore));
        var moved = Math.Max(0, destination.stackCount - Math.Max(0, destinationCountBefore));
        var transferred = sourceTracked.TransferPhysicalUnits(
            moved,
            Math.Max(0, sourceCountBefore),
            source.Destroyed ? 0 : Math.Max(0, source.stackCount));
        destinationTracked.Absorb(destination.stackCount, transferred);

        if (source.Destroyed || sourceTracked.PhysicalQuantity <= 0)
        {
            sourceTracked.MarkRemoved();
            sourceComponent.RemoveResource(source.thingIDNumber);
        }
    }

    public static int ReconcileMap(WorldState state, Map? map, string reason, bool checkpointOnly = false)
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
            if (checkpointOnly && !tracked.CheckpointEligible)
            {
                continue;
            }

            if (tracked.ReconciliationState == LivingWorldTrackedResourceState.Removed)
            {
                component.RemoveResource(tracked.ThingId);
                continue;
            }

            var thing = FindTrackedThing(map, tracked.ThingId, out var heldByPlayer);
            if (heldByPlayer)
            {
                tracked.MarkRemoved();
                component.RemoveResource(tracked.ThingId);
                continue;
            }

            if (tracked.ReconciliationState == LivingWorldTrackedResourceState.Pending)
            {
                if (thing == null || thing.Destroyed)
                {
                    tracked.MarkRemoved();
                    component.RemoveResource(tracked.ThingId);
                    continue;
                }

                tracked.SynchronizePhysicalQuantity(thing.stackCount);
                if (tracked.RemainingQuantity <= 0)
                {
                    tracked.MarkRemoved();
                    component.RemoveResource(tracked.ThingId);
                    continue;
                }

                var ownerId = ResolveReturnOwner(state, tracked.ReturnOwnerId);
                tracked.BeginCredit(ownerId, state.GetOwnedResourceQuantity(ownerId, tracked.ResourceKey));
            }

            returned += EnsureCredited(state, tracked);
            if (thing != null && !thing.Destroyed)
            {
                thing.Destroy(DestroyMode.Vanish);
            }

            tracked.MarkRemoved();
            component.RemoveResource(tracked.ThingId);
        }

        return returned;
    }

    public static int ReconcilePawnGear(WorldState state, Pawn? pawn, string reason)
    {
        if (state == null || pawn == null || pawn.Faction == Faction.OfPlayer)
        {
            return 0;
        }

        var returned = 0;
        var things = (pawn.equipment?.AllEquipmentListForReading.Cast<Thing>() ?? Enumerable.Empty<Thing>())
            .Concat(pawn.apparel?.WornApparel.Cast<Thing>() ?? Enumerable.Empty<Thing>())
            .Concat(pawn.inventory?.innerContainer.InnerListForReading.Cast<Thing>() ?? Enumerable.Empty<Thing>())
            .Distinct()
            .ToList();
        foreach (var thing in things)
        {
            if (!TryGetTrackedResource(thing, out var tracked, out var component))
            {
                continue;
            }

            if (tracked.ReconciliationState == LivingWorldTrackedResourceState.Pending)
            {
                tracked.SynchronizePhysicalQuantity(thing.stackCount);
                if (tracked.RemainingQuantity <= 0)
                {
                    tracked.MarkRemoved();
                    component.RemoveResource(tracked.ThingId);
                    continue;
                }

                var ownerId = ResolveReturnOwner(state, tracked.ReturnOwnerId);
                tracked.BeginCredit(ownerId, state.GetOwnedResourceQuantity(ownerId, tracked.ResourceKey));
            }

            returned += EnsureCredited(state, tracked);
            if (!thing.Destroyed)
            {
                thing.Destroy(DestroyMode.Vanish);
            }

            tracked.MarkRemoved();
            component.RemoveResource(tracked.ThingId);
        }

        return returned;
    }

    private static int EnsureCredited(WorldState state, LivingWorldTrackedMapResource tracked)
    {
        if (tracked.ReconciliationState == LivingWorldTrackedResourceState.Credited)
        {
            return 0;
        }

        if (tracked.ReconciliationState != LivingWorldTrackedResourceState.Crediting)
        {
            throw new InvalidOperationException(
                $"Tracked resource {tracked.ThingId} cannot be credited from {tracked.ReconciliationState}.");
        }

        var ownerId = tracked.CreditOwnerId;
        var expected = tracked.CreditLedgerQuantityBefore + tracked.CreditQuantity;
        var current = state.GetOwnedResourceQuantity(ownerId, tracked.ResourceKey);
        if (current == tracked.CreditLedgerQuantityBefore)
        {
            ResourceLedgerService.AddResource(state, ownerId, tracked.ResourceKey, tracked.CreditQuantity);
            current = state.GetOwnedResourceQuantity(ownerId, tracked.ResourceKey);
        }

        if (current != expected)
        {
            throw new InvalidOperationException(
                $"Tracked resource credit mismatch for {tracked.ResourceKey}: expected {expected}, found {current}.");
        }

        tracked.MarkCredited();
        return tracked.CreditQuantity;
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
