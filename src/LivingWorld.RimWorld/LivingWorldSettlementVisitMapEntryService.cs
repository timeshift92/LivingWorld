using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
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
        visitSite.BeginMapSession();

        try
        {
            map = visitSite.Map;
            if (map == null)
            {
                map = MapGenerator.GenerateMap(
                    VisitMapSize,
                    visitSite,
                    visitSite.MapGeneratorDef,
                    visitSite.ExtraGenStepDefs);
            }

            var mapComponent = LivingWorldSettlementVisitMapComponent.For(map);
            mapComponent?.ConfigureFrom(visitSite);
            if (mapComponent?.Lifecycle == LivingWorldMapMaterializationLifecycle.None)
            {
                // GenerateMap normally materializes through Harmony. This is only a fail-safe for
                // an integration that skipped that postfix, never a second materialization pass.
                LivingWorldSettlementMapMaterializationService.MaterializeSettlementMap(map, visitSite);
            }
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
            if (mapComponent.SettlementId.HasValue)
            {
                var worldComponent = LivingWorldWorldComponent.Instance;
                if (worldComponent != null)
                {
                    PlayerKnowledgeService.RecordDirectVisitSettlementInfo(
                        worldComponent.State,
                        mapComponent.SettlementId.Value,
                        "player caravan entered a real Living World settlement map");
                }
            }

            Current.Game.CurrentMap = map;
            CameraJumper.TryJump(map.Center, map);
            Messages.Message(
                "LW_SettlementVisitSiteOpened".Translate(visitSite.Label),
                MessageTypeDefOf.TaskCompletion,
                historical: false);
        }
        catch (Exception exception)
        {
            Log.Error($"[LivingWorld] Could not enter settlement '{visitSite.Label}' with caravan '{caravanLabel}': {exception}");
            var caravanPawnIds = caravanPawns.Select(pawn => pawn.thingIDNumber).ToHashSet();
            var enteredPlayerPawns = map?.mapPawns.PawnsInFaction(Faction.OfPlayer)
                .Where(pawn => pawn != null
                    && caravanPawnIds.Contains(pawn.thingIDNumber)
                    && pawn.Spawned
                    && !pawn.Dead)
                .ToList() ?? new List<Pawn>();
            if (map != null && enteredPlayerPawns.Count > 0)
            {
                // CaravanEnterMapUtility can fail after moving only part of a caravan. That map is
                // now player-owned runtime state and must remain active until the player leaves it.
                visitSite.MarkPlayerCaravanEntered();
                Current.Game.CurrentMap = map;
                CameraJumper.TryJump(enteredPlayerPawns[0]);
                Messages.Message(
                    "LW_SettlementVisitSitePartialEntry".Translate(caravanLabel, enteredPlayerPawns.Count),
                    MessageTypeDefOf.CautionInput,
                    historical: false);
                return;
            }

            visitSite.AbortMapSession();
            Reject(caravan, exception.Message);
            if (map != null)
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
            MessageTypeDefOf.RejectInput,
            historical: false);
    }
}
