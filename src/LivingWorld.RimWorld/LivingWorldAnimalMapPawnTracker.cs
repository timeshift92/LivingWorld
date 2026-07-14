using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

public static class LivingWorldAnimalMapPawnTracker
{
    private static readonly Dictionary<int, int> MapIdByPawnThingId = new();

    public static void Track(Pawn? pawn, MaterializedAnimalStack? animal)
    {
        var map = pawn?.MapHeld;
        if (pawn == null || animal == null || animal.Count <= 0 || map == null)
        {
            return;
        }

        LivingWorldSettlementVisitMapComponent.For(map)?.TrackAnimal(pawn, animal);
        MapIdByPawnThingId[pawn.thingIDNumber] = map.uniqueID;
    }

    public static void ReindexMap(Map? map)
    {
        var component = map == null ? null : LivingWorldSettlementVisitMapComponent.For(map);
        if (map == null || component == null)
        {
            return;
        }

        foreach (var animal in component.Animals)
        {
            MapIdByPawnThingId[animal.PawnThingId] = map.uniqueID;
        }
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

    public static int ReconcileMap(WorldState state, Map? map, string reason)
    {
        var component = map == null ? null : LivingWorldSettlementVisitMapComponent.For(map);
        if (state == null || map == null || component == null)
        {
            return 0;
        }

        var resolved = 0;
        var pawns = map.mapPawns?.AllPawns ?? new List<Pawn>();
        foreach (var tracked in component.Animals.ToList())
        {
            var pawn = pawns.FirstOrDefault(candidate => candidate?.thingIDNumber == tracked.PawnThingId);
            var fate = pawn == null
                ? AnimalMapFateKind.Missing
                : pawn.Dead
                    ? AnimalMapFateKind.Dead
                    : pawn.Faction == Faction.OfPlayer || pawn.Faction?.IsPlayer == true
                        ? AnimalMapFateKind.TakenByPlayer
                        : AnimalMapFateKind.Returned;

            Resolve(state, component, tracked, fate, reason);
            MapIdByPawnThingId.Remove(tracked.PawnThingId);
            resolved++;
        }

        return resolved;
    }

    private static bool TryResolve(Pawn? pawn, AnimalMapFateKind fate, string reason)
    {
        var component = LivingWorldWorldComponent.Instance;
        if (component == null || pawn == null || !TryFindTracked(pawn, out var mapComponent, out var tracked))
        {
            return false;
        }

        Resolve(component.State, mapComponent, tracked, fate, reason);
        MapIdByPawnThingId.Remove(pawn.thingIDNumber);
        return true;
    }

    private static void Resolve(
        WorldState state,
        LivingWorldSettlementVisitMapComponent component,
        LivingWorldTrackedMapAnimal tracked,
        AnimalMapFateKind fate,
        string reason)
    {
        component.RemoveAnimal(tracked.PawnThingId);
        AnimalMapFateSyncService.Resolve(
            state,
            new AnimalMapFateSyncRequest(
                tracked.CohortId,
                tracked.AnimalKind,
                tracked.Type,
                Count: 1,
                fate,
                Find.TickManager?.TicksGame ?? state.CurrentTick,
                string.IsNullOrWhiteSpace(reason) ? "settlement map animal fate" : reason));
    }

    private static bool TryFindTracked(
        Pawn pawn,
        out LivingWorldSettlementVisitMapComponent component,
        out LivingWorldTrackedMapAnimal tracked)
    {
        component = null!;
        tracked = null!;
        var map = pawn.MapHeld;
        if (map != null)
        {
            component = LivingWorldSettlementVisitMapComponent.For(map)!;
            if (component != null && component.TryGetAnimal(pawn.thingIDNumber, out tracked))
            {
                MapIdByPawnThingId[pawn.thingIDNumber] = map.uniqueID;
                return true;
            }
        }

        if (MapIdByPawnThingId.TryGetValue(pawn.thingIDNumber, out var mapId))
        {
            var indexedMap = Find.Maps.FirstOrDefault(candidate => candidate.uniqueID == mapId);
            component = indexedMap == null ? null! : LivingWorldSettlementVisitMapComponent.For(indexedMap)!;
            if (component != null && component.TryGetAnimal(pawn.thingIDNumber, out tracked))
            {
                return true;
            }
        }

        foreach (var candidateMap in Find.Maps)
        {
            var candidate = LivingWorldSettlementVisitMapComponent.For(candidateMap);
            if (candidate != null && candidate.TryGetAnimal(pawn.thingIDNumber, out tracked))
            {
                component = candidate;
                MapIdByPawnThingId[pawn.thingIDNumber] = candidateMap.uniqueID;
                return true;
            }
        }

        MapIdByPawnThingId.Remove(pawn.thingIDNumber);
        return false;
    }
}
