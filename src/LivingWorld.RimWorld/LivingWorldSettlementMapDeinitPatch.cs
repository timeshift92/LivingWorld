using HarmonyLib;
using LivingWorld.Core;
using System.Linq;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(MapDeiniter), "Deinit")]
public static class LivingWorldSettlementMapDeinitPatch
{
    public static void Prefix(Map map)
    {
        var component = LivingWorldWorldComponent.Instance;
        if (component == null || map == null)
        {
            return;
        }

        var mapComponent = LivingWorldSettlementVisitMapComponent.For(map);
        if (mapComponent == null
            || mapComponent.Reconciled
            || mapComponent.Lifecycle != LivingWorldMapMaterializationLifecycle.Materialized)
        {
            return;
        }

        LivingWorldSettlementMapResourceTracker.ReconcileMap(
            component.State,
            map,
            "settlement map deinit");
        LivingWorldSettlementMapFacilityTracker.ReconcileMap(
            component.State,
            map,
            "settlement map deinit");
        LivingWorldAnimalMapPawnTracker.ReconcileMap(
            component.State,
            map,
            "settlement map deinit");
        ReconcileResidents(component.State, map, mapComponent.PurposeKey);

        mapComponent.MarkReconciled();
        var visitSite = Find.WorldObjects?.AllWorldObjects
            .OfType<WorldObject_LivingWorldSettlementVisitSite>()
            .FirstOrDefault(worldObject => worldObject.ID == mapComponent.VisitSiteWorldObjectId);
        visitSite?.MarkReconciled();
    }

    private static void ReconcileResidents(WorldState state, Map map, string purposeKey)
    {
        var leases = state.MaterializationLeases
            .Where(lease =>
                lease.IsActive
                && string.Equals(lease.PurposeKey, purposeKey, System.StringComparison.Ordinal))
            .OrderBy(lease => lease.Id.Value)
            .ToList();
        var pawns = map.mapPawns.AllPawns.ToList();
        foreach (var lease in leases)
        {
            if (lease.Lifecycle == MaterializationLeaseLifecycle.Reserved || !lease.PawnThingId.HasValue)
            {
                state.ReleaseMaterializationLease(lease.Id);
                continue;
            }

            var pawn = pawns.FirstOrDefault(candidate => candidate.thingIDNumber == lease.PawnThingId.Value);
            var fate = pawn == null
                ? PawnFateKind.Missing
                : pawn.Dead
                    ? PawnFateKind.Dead
                    : pawn.IsPrisoner
                        ? PawnFateKind.Prisoner
                        : PawnFateKind.Returned;
            LivingWorldPawnSyncService.Apply(
                state,
                new PawnFateSyncRequest(
                    lease.CitizenId,
                    fate,
                    "settlement map deinitialized"));
        }
    }
}
