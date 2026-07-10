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
        if (visitSite == null || caravan == null || caravan.Destroyed)
        {
            return;
        }

        LongEventHandler.QueueLongEvent(
            () =>
            {
                var map = visitSite.Map;
                if (map == null)
                {
                    map = MapGenerator.GenerateMap(
                        VisitMapSize,
                        visitSite,
                        ResolveLivingWorldMapGenerator(),
                        Enumerable.Empty<GenStepWithParams>());
                }

                LivingWorldSettlementVisitMapComponent.For(map)?.ConfigureFrom(visitSite);
                LivingWorldSettlementMapMaterializationService.MaterializeSettlementMap(map, visitSite);

                Current.Game.CurrentMap = map;
                if (!caravan.Destroyed)
                {
                    CaravanEnterMapUtility.Enter(
                        caravan,
                        map,
                        CaravanEnterMode.Edge,
                        CaravanDropInventoryMode.DoNotDrop,
                        draftColonists: false,
                        extraCellValidator: null);
                }

                CameraJumper.TryJump(map.Center, map);
                Messages.Message(
                    "LW_SettlementVisitSiteOpened".Translate(visitSite.Label),
                    MessageTypeDefOf.TaskCompletion,
                    historical: false);
            },
            "GeneratingMap",
            false,
            GameAndMapInitExceptionHandlers.ErrorWhileGeneratingMap);
    }

    private static MapGeneratorDef ResolveLivingWorldMapGenerator()
    {
        return DefDatabase<MapGeneratorDef>.GetNamedSilentFail("Base_Player")
            ?? DefDatabase<MapGeneratorDef>.AllDefsListForReading.First();
    }
}
