using System;
using System.Linq;
using HarmonyLib;
using LivingWorld.Core;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(Game), nameof(Game.DeinitAndRemoveMap))]
[HarmonyAfter("helldan.finitepopulation", "rimworld.torann.rimwar", "com.Matathias.Empire")]
[HarmonyPriority(Priority.Last)]
public static class LivingWorldSettlementMapDeinitPatch
{
    public static bool Prefix(Map map, out WorldObject_LivingWorldSettlementVisitSite? __state)
    {
        __state = map?.Parent as WorldObject_LivingWorldSettlementVisitSite;
        var component = LivingWorldWorldComponent.Instance;
        if (component == null || map == null || !OwnsMapLifecycle(map.Parent))
        {
            return true;
        }

        var mapComponent = LivingWorldSettlementVisitMapComponent.For(map);
        if (mapComponent == null || mapComponent.Reconciled)
        {
            return true;
        }

        if (mapComponent.Lifecycle == LivingWorldMapMaterializationLifecycle.Failed)
        {
            if (!mapComponent.RollbackPending)
            {
                return true;
            }

            var recovered = LivingWorldSettlementMapMaterializationService.RetryFailedMaterializationRollback(
                component.State,
                map,
                mapComponent.PurposeKey,
                "settlement map deinit retried failed preparation rollback");
            if (!recovered)
            {
                Log.Error(
                    "[LivingWorld] Settlement map removal was cancelled because failed materialization still owns ledger state.");
                return false;
            }

            mapComponent.MarkRollbackRecovered();
            return true;
        }

        if (mapComponent.Lifecycle != LivingWorldMapMaterializationLifecycle.Materialized)
        {
            return true;
        }

        var reconciled = TryReconcile(
            mapComponent,
            LivingWorldMapReconciliationLayer.Resources,
            () => LivingWorldSettlementMapResourceTracker.ReconcileMap(
                component.State,
                map,
                "settlement map deinit"),
            "resources");
        reconciled &= TryReconcile(
            mapComponent,
            LivingWorldMapReconciliationLayer.Facilities,
            () => LivingWorldSettlementMapFacilityTracker.ReconcileMap(
                component.State,
                map,
                "settlement map deinit"),
            "facilities and floors");
        reconciled &= TryReconcile(
            mapComponent,
            LivingWorldMapReconciliationLayer.Animals,
            () => LivingWorldAnimalMapPawnTracker.ReconcileMap(
                component.State,
                map,
                "settlement map deinit"),
            "animals");
        reconciled &= TryReconcile(
            mapComponent,
            LivingWorldMapReconciliationLayer.Residents,
            () => ReconcileResidents(component.State, map, mapComponent.PurposeKey),
            "residents");

        if (!reconciled)
        {
            Log.Error(
                "[LivingWorld] Settlement map removal was cancelled because one or more ledger reconciliation layers failed."
                + " The map remains loaded and the completed layers are persisted so a retry is idempotent.");
            return false;
        }

        mapComponent.MarkReconciled();
        var visitSite = Find.WorldObjects?.AllWorldObjects
            .OfType<WorldObject_LivingWorldSettlementVisitSite>()
            .FirstOrDefault(worldObject => worldObject.ID == mapComponent.VisitSiteWorldObjectId);
        visitSite?.MarkReconciled();
        return true;
    }

    public static void Postfix(WorldObject_LivingWorldSettlementVisitSite? __state)
    {
        if (__state != null && !__state.Destroyed && !__state.HasMap && __state.Reconciled)
        {
            __state.Destroy();
        }
    }

    private static void ReconcileResidents(WorldState state, Map map, string purposeKey)
    {
        var leases = state.MaterializationLeases
            .Where(lease =>
                lease.IsActive
                && string.Equals(lease.PurposeKey, purposeKey, StringComparison.Ordinal))
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

    private static bool OwnsMapLifecycle(MapParent parent)
    {
        if (parent is WorldObject_LivingWorldSettlementVisitSite)
        {
            return true;
        }

        if (parent is not Settlement || parent.GetType() != typeof(Settlement))
        {
            return false;
        }

        return !ModsConfig.IsActive("helldan.economicsdemography")
            && !ModsConfig.IsActive("Torann.RimWar");
    }

    private static bool TryReconcile(
        LivingWorldSettlementVisitMapComponent mapComponent,
        LivingWorldMapReconciliationLayer layerFlag,
        Action reconcile,
        string layer)
    {
        if (mapComponent.IsLayerReconciled(layerFlag))
        {
            return true;
        }

        try
        {
            reconcile();
            mapComponent.MarkLayerReconciled(layerFlag);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"[LivingWorld] Settlement map {layer} reconciliation failed: {ex}");
            return false;
        }
    }
}
