using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

public static class LivingWorldSettlementVisitMapEntryService
{
    private static readonly IntVec3 VisitMapSize = new(120, 1, 120);

    public static void OpenOrEnter(WorldObject_LivingWorldSettlementVisitSite visitSite, Caravan caravan)
    {
        if (visitSite == null || caravan == null || caravan.Destroyed || caravan.PawnsListForReading.Count == 0)
        {
            Reject(caravan, "invalid or empty caravan");
            return;
        }

        var caravanPawns = caravan.PawnsListForReading.ToList();
        var caravanLabel = caravan.LabelCap;
        LongEventHandler.QueueLongEvent(
            () => EnterNow(visitSite, caravan, caravanPawns, caravanLabel),
            "GeneratingMap",
            false,
            GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap);
    }

    private static void EnterNow(
        WorldObject_LivingWorldSettlementVisitSite visitSite,
        Caravan caravan,
        IReadOnlyList<Pawn> caravanPawns,
        string caravanLabel)
    {
        Map? map = null;
        var generatedForThisEntry = false;
        visitSite.BeginMapSession();

        try
        {
            map = visitSite.Map;
            if (map == null)
            {
                generatedForThisEntry = true;
                map = MapGenerator.GenerateMap(
                    VisitMapSize,
                    visitSite,
                    visitSite.MapGeneratorDef,
                    visitSite.ExtraGenStepDefs);
            }

            var mapComponent = LivingWorldSettlementVisitMapComponent.For(map);
            mapComponent?.ConfigureFrom(visitSite);
            LivingWorldSettlementMapMaterializationService.MaterializeSettlementMap(map, visitSite);
            mapComponent = LivingWorldSettlementVisitMapComponent.For(map);
            if (mapComponent?.IsPlayable != true)
            {
                throw new InvalidOperationException(
                    mapComponent?.FailureReason.NullOrEmpty() == false
                        ? mapComponent.FailureReason
                        : "settlement ledger produced no playable population or structures");
            }

            if (caravan.Destroyed)
            {
                throw new InvalidOperationException("caravan disappeared before map entry");
            }

            var entryCells = BuildEntryCells(map, caravanPawns.Count);
            if (entryCells.Count < caravanPawns.Count)
            {
                throw new InvalidOperationException("no safe map edge cells are available for every caravan pawn");
            }

            var cellsByPawnId = caravanPawns
                .Select((pawn, index) => new { pawn.thingIDNumber, Cell = entryCells[index] })
                .ToDictionary(entry => entry.thingIDNumber, entry => entry.Cell);
            CaravanEnterMapUtility.Enter(
                caravan,
                map,
                pawn => cellsByPawnId[pawn.thingIDNumber],
                CaravanDropInventoryMode.DoNotDrop,
                draftColonists: false);

            var missingPawn = caravanPawns.FirstOrDefault(pawn => pawn == null || !pawn.Spawned || pawn.Map != map);
            if (missingPawn != null || !caravan.Destroyed)
            {
                throw new InvalidOperationException(
                    missingPawn == null
                        ? "caravan remained on the world map after transfer"
                        : $"caravan pawn {missingPawn.LabelShortCap} did not enter the settlement map");
            }

            visitSite.MarkPlayerCaravanEntered();
            Current.Game.CurrentMap = map;
            CameraJumper.TryJump(map.Center, map);
            Messages.Message(
                "LW_SettlementVisitSiteOpened".Translate(visitSite.Label),
                MessageTypeDefOf.TaskCompletion,
                historical: false);
        }
        catch (Exception exception)
        {
            visitSite.AbortMapSession();
            Log.Error($"[LivingWorld] Could not enter settlement '{visitSite.Label}' with caravan '{caravanLabel}': {exception}");
            Reject(caravan, exception.Message);

            if (generatedForThisEntry
                && map != null
                && !map.mapPawns.PawnsInFaction(Faction.OfPlayer).Any(pawn => pawn.Spawned && !pawn.Dead))
            {
                Current.Game.DeinitAndRemoveMap(map, notifyPlayer: false);
            }
        }
    }

    private static List<IntVec3> BuildEntryCells(Map map, int count)
    {
        return map.AllCells
            .Where(cell =>
                cell.InBounds(map)
                && cell.Standable(map)
                && !cell.Fogged(map)
                && DistanceToEdge(cell, map) <= 6)
            .OrderBy(cell => DistanceToEdge(cell, map))
            .ThenBy(cell => cell.x)
            .ThenBy(cell => cell.z)
            .Take(Math.Max(0, count))
            .ToList();
    }

    private static int DistanceToEdge(IntVec3 cell, Map map)
    {
        return Math.Min(Math.Min(cell.x, map.Size.x - 1 - cell.x), Math.Min(cell.z, map.Size.z - 1 - cell.z));
    }

    private static void Reject(Caravan? caravan, string reason)
    {
        Messages.Message(
            "LW_SettlementVisitSiteUnavailable".Translate(reason ?? "unknown error"),
            caravan,
            MessageTypeDefOf.RejectInput,
            historical: false);
    }
}
