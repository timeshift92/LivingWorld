using System;
using System.Linq;
using HarmonyLib;
using LivingWorld.Core;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(MapDeiniter), "Deinit")]
[HarmonyAfter("helldan.finitepopulation", "rimworld.torann.rimwar", "com.Matathias.Empire")]
[HarmonyPriority(Priority.Last)]
public static class LivingWorldSettlementMapDeinitPatch
{
    public static void Prefix(Map map)
    {
        var component = LivingWorldWorldComponent.Instance;
        if (component == null || map == null || !OwnsMapLifecycle(map.Parent))
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

        TryReconcile(
            () => LivingWorldSettlementMapResourceTracker.ReconcileMap(
                component.State,
                map,
                "settlement map deinit"),
            "resources");
        TryReconcile(
            () => LivingWorldSettlementMapFacilityTracker.ReconcileMap(
                component.State,
                map,
                "settlement map deinit"),
            "facilities and floors");
        TryReconcile(
            () => LivingWorldAnimalMapPawnTracker.ReconcileMap(
                component.State,
                map,
                "settlement map deinit"),
            "animals");
        TryReconcile(
            () => ReconcileResidents(component.State, map, mapComponent.PurposeKey),
            "residents");

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

    private static void TryReconcile(Action reconcile, string layer)
    {
        try
        {
            reconcile();
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Settlement map {layer} reconciliation skipped safely: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
