using System;
using System.Collections.Generic;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

public static class LivingWorldAnimalMapPawnTracker
{
    private sealed record TrackedAnimal(EntityId CohortId, string AnimalKind, AnimalCohortType Type);

    private static readonly Dictionary<int, TrackedAnimal> AnimalsByPawnThingId = new();

    public static void Track(Pawn? pawn, MaterializedAnimalStack? animal)
    {
        if (pawn == null || animal == null || animal.Count <= 0)
        {
            return;
        }

        AnimalsByPawnThingId[pawn.thingIDNumber] = new TrackedAnimal(
            animal.CohortId,
            animal.AnimalKind,
            animal.Type);
    }

    public static bool TryMarkDead(Pawn? pawn, string reason)
    {
        return TryResolve(pawn, AnimalMapFateKind.Dead, reason);
    }

    public static bool TryMarkReturned(Pawn? pawn, string reason)
    {
        if (pawn?.Dead == true)
        {
            return false;
        }

        if (pawn?.Faction == Faction.OfPlayer || pawn?.Faction?.IsPlayer == true)
        {
            return TryResolve(pawn, AnimalMapFateKind.TakenByPlayer, reason);
        }

        return TryResolve(pawn, AnimalMapFateKind.Returned, reason);
    }

    private static bool TryResolve(Pawn? pawn, AnimalMapFateKind fate, string reason)
    {
        var component = LivingWorldWorldComponent.Instance;
        if (component == null || pawn == null)
        {
            return false;
        }

        if (!AnimalsByPawnThingId.TryGetValue(pawn.thingIDNumber, out var tracked))
        {
            return false;
        }

        AnimalsByPawnThingId.Remove(pawn.thingIDNumber);
        AnimalMapFateSyncService.Resolve(
            component.State,
            new AnimalMapFateSyncRequest(
                tracked.CohortId,
                tracked.AnimalKind,
                tracked.Type,
                Count: 1,
                fate,
                Find.TickManager?.TicksGame ?? component.State.CurrentTick,
                string.IsNullOrWhiteSpace(reason) ? "settlement map animal fate" : reason));
        return true;
    }
}
