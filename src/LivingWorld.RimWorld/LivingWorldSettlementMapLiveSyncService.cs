using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LivingWorld.RimWorld;

/// <summary>
/// Keeps a loaded NPC settlement map attached to the ledger instead of treating it as a one-time snapshot.
/// The ledger remains authoritative; map pawns and warehouse stacks are conserved materializations.
/// </summary>
internal static class LivingWorldSettlementMapLiveSyncService
{
    public static bool CheckpointWarehouseForWorldSimulation(
        Map map,
        LivingWorldSettlementVisitMapComponent mapComponent)
    {
        var worldComponent = LivingWorldWorldComponent.Instance;
        var settlementId = mapComponent?.SettlementId;
        if (worldComponent == null
            || map == null
            || mapComponent == null
            || !settlementId.HasValue
            || mapComponent.Lifecycle != LivingWorldMapMaterializationLifecycle.Materialized)
        {
            return false;
        }

        var settlement = worldComponent.State.GetSettlement(settlementId.Value);
        if (settlement is not { IsActive: true })
        {
            return false;
        }

        LivingWorldSettlementMapFacilityTracker.ReconcileLiveMap(
            worldComponent.State,
            map,
            "loaded settlement daily facility checkpoint");
        LivingWorldSettlementMapResourceTracker.ReconcileMap(
            worldComponent.State,
            map,
            "loaded settlement daily ledger checkpoint",
            checkpointOnly: true);
        mapComponent.RefreshWarehouseMaterializedCounts();
        return true;
    }

    public static void Sync(Map map, LivingWorldSettlementVisitMapComponent mapComponent, int tick)
    {
        var worldComponent = LivingWorldWorldComponent.Instance;
        var settlementId = mapComponent.SettlementId;
        if (worldComponent == null || !settlementId.HasValue)
        {
            return;
        }

        var state = worldComponent.State;
        var settlement = state.GetSettlement(settlementId.Value);
        var faction = map.ParentFaction;
        if (settlement == null || !settlement.IsActive || faction == null)
        {
            return;
        }

        try
        {
            SyncResidents(state, map, mapComponent, settlementId.Value, faction);
            SyncAnimalBirths(map, mapComponent, faction);
            SyncWarehouse(state, map, mapComponent, settlementId.Value);
            ApplyPeacefulSchedules(state, map, mapComponent, tick);
            mapComponent.RefreshMaterializedPopulation();
        }
        catch (Exception error)
        {
            Log.Error($"[LivingWorld] Loaded settlement map live sync will retry after failure: {error}");
        }
    }

    private static void SyncResidents(
        WorldState state,
        Map map,
        LivingWorldSettlementVisitMapComponent mapComponent,
        EntityId settlementId,
        Faction faction)
    {
        var mapped = map.mapPawns.AllPawns
            .Where(pawn => pawn?.RaceProps?.Humanlike == true)
            .OrderBy(pawn => pawn.thingIDNumber)
            .ToList();
        foreach (var pawn in mapped)
        {
            var identity = pawn.GetComp<CompLivingWorldIdentity>();
            if (identity?.HasLedgerId != true)
            {
                continue;
            }

            var citizen = state.GetCitizen(identity.LedgerId);
            if (pawn.Dead)
            {
                if (citizen?.Status == CitizenStatus.Alive)
                {
                    LivingWorldPawnSyncService.Apply(
                        state,
                        new PawnFateSyncRequest(
                            identity.LedgerId,
                            PawnFateKind.Dead,
                            "loaded settlement recovered an unsynchronized pawn death"));
                }

                continue;
            }

            if (citizen?.Status == CitizenStatus.Alive
                || (citizen?.Status == CitizenStatus.Prisoner && pawn.IsPrisoner))
            {
                continue;
            }

            var lease = state.MaterializationLeases.FirstOrDefault(candidate =>
                candidate.IsActive && candidate.CitizenId == identity.LedgerId);
            if (citizen?.Status == CitizenStatus.Dead)
            {
                LivingWorldPawnSyncService.Apply(
                    state,
                    new PawnFateSyncRequest(identity.LedgerId, PawnFateKind.Dead, "loaded settlement ledger death"));
                pawn.Kill(null);
            }
            else
            {
                if (lease != null)
                {
                    MaterializationLeaseService.Release(state, lease.Id, "citizen left loaded settlement ledger");
                }

                LivingWorldSettlementMapMaterializationService.DetachRelationsAndDestroy(pawn);
            }
        }

        ReleaseLostResidentLeases(state, map, mapComponent.PurposeKey);

        var prepared = SettlementResidentMaterializationService.PrepareResidents(
            state,
            new SettlementResidentMaterializationRequest(
                settlementId,
                60_000 * 5,
                mapComponent.PurposeKey));
        if (prepared.Status == SettlementResidentMaterializationStatus.Success)
        {
            LivingWorldSettlementMapMaterializationService.SpawnGeneratedResidents(
                state,
                map,
                faction,
                settlementId,
                prepared.Leases);
        }

        SettlementResidentMaterializationService.ReleaseUnmaterialized(state, mapComponent.PurposeKey);
    }

    private static void ReleaseLostResidentLeases(WorldState state, Map map, string purposeKey)
    {
        var mappedPawnIds = map.mapPawns.AllPawns
            .Where(pawn => pawn != null)
            .Select(pawn => pawn.thingIDNumber)
            .ToHashSet();
        var worldPawnIds = (Find.WorldPawns?.AllPawnsAliveOrDead?.Cast<Pawn>() ?? Enumerable.Empty<Pawn>())
            .Where(pawn => pawn != null)
            .Select(pawn => pawn.thingIDNumber)
            .ToHashSet();
        var lost = state.MaterializationLeases
            .Where(lease =>
                lease.IsActive
                && lease.Lifecycle == MaterializationLeaseLifecycle.Materialized
                && lease.PawnThingId.HasValue
                && string.Equals(lease.PurposeKey, purposeKey, StringComparison.Ordinal)
                && !mappedPawnIds.Contains(lease.PawnThingId.Value)
                && !worldPawnIds.Contains(lease.PawnThingId.Value))
            .OrderBy(lease => lease.Id.Value)
            .ToList();
        foreach (var lease in lost)
        {
            MaterializationLeaseService.Release(
                state,
                lease.Id,
                "loaded settlement could not find the materialized resident pawn");
        }
    }

    private static void SyncAnimalBirths(
        Map map,
        LivingWorldSettlementVisitMapComponent mapComponent,
        Faction faction)
    {
        var trackedByPawnId = mapComponent.Animals.ToDictionary(animal => animal.PawnThingId);
        var lineageByPawnId = mapComponent.AnimalLineages.ToDictionary(animal => animal.PawnThingId);
        if (trackedByPawnId.Count == 0 && lineageByPawnId.Count == 0)
        {
            return;
        }

        foreach (var pawn in map.mapPawns.AllPawnsSpawned
            .Where(pawn => pawn != null
                && !pawn.Dead
                && pawn.RaceProps?.Animal == true
                && pawn.Faction == faction
                && !trackedByPawnId.ContainsKey(pawn.thingIDNumber))
            .OrderBy(pawn => pawn.thingIDNumber))
        {
            var parent = pawn.relations?.DirectRelations
                .Where(relation => relation.def == PawnRelationDefOf.Parent && relation.otherPawn != null)
                .Select(relation => relation.otherPawn)
                .FirstOrDefault(candidate => trackedByPawnId.ContainsKey(candidate.thingIDNumber)
                    || lineageByPawnId.ContainsKey(candidate.thingIDNumber));
            var parentRecord = parent != null && trackedByPawnId.TryGetValue(parent.thingIDNumber, out var activeParent)
                ? activeParent
                : parent != null && lineageByPawnId.TryGetValue(parent.thingIDNumber, out var historicParent)
                    ? historicParent
                    : mapComponent.AnimalLineages
                        .Where(candidate => string.Equals(candidate.AnimalKind, pawn.kindDef?.defName, StringComparison.Ordinal))
                        .OrderBy(candidate => candidate.CohortIdValue)
                        .FirstOrDefault();
            if (parentRecord == null
                || !string.Equals(parentRecord.AnimalKind, pawn.kindDef?.defName, StringComparison.Ordinal))
            {
                continue;
            }

            var newborn = new MaterializedAnimalStack(
                parentRecord.CohortId,
                parentRecord.AnimalKind,
                parentRecord.Type,
                Count: 1);
            LivingWorldAnimalMapPawnTracker.Track(pawn, newborn);
            trackedByPawnId[pawn.thingIDNumber] = new LivingWorldTrackedMapAnimal(
                pawn.thingIDNumber,
                newborn.CohortId.Value,
                newborn.AnimalKind,
                (int)newborn.Type);
        }
    }

    private static void SyncWarehouse(
        WorldState state,
        Map map,
        LivingWorldSettlementVisitMapComponent mapComponent,
        EntityId settlementId)
    {
        mapComponent.RefreshWarehouseMaterializedCounts();
        var physical = mapComponent.WarehouseManifest
            .Where(entry => entry.MaterializedQuantity > 0)
            .ToDictionary(entry => entry.ResourceKey, entry => entry.MaterializedQuantity, StringComparer.Ordinal);
        var desired = LivingWorldSettlementMapMaterializationService.BuildResourceRequest(
            state,
            settlementId,
            physical);
        foreach (var pair in desired.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            mapComponent.UpdateWarehouseTarget(pair.Key, pair.Value);
            var actual = physical.TryGetValue(pair.Key, out var materialized) ? materialized : 0;
            var shortfall = pair.Value - actual;
            if (shortfall <= 0)
            {
                continue;
            }

            var consumed = ResourceLedgerService.ConsumeResource(
                state,
                settlementId,
                pair.Key,
                shortfall,
                "loaded settlement warehouse sync");
            if (consumed <= 0)
            {
                continue;
            }

            var spawned = LivingWorldSettlementMapMaterializationService.TrySpawnResourceStack(
                map,
                settlementId,
                pair.Key,
                consumed,
                Array.Empty<SettlementMapStockpileCell>());
            if (spawned < consumed)
            {
                ResourceLedgerService.AddResource(state, settlementId, pair.Key, consumed - spawned);
            }
        }

        mapComponent.RefreshWarehouseMaterializedCounts();
    }

    private static void ApplyPeacefulSchedules(
        WorldState state,
        Map map,
        LivingWorldSettlementVisitMapComponent mapComponent,
        int tick)
    {
        var faction = map.ParentFaction;
        if (faction == null)
        {
            return;
        }

        var playerPresent = map.mapPawns.PawnsInFaction(Faction.OfPlayer)
            .Any(pawn => pawn != null && pawn.Spawned && !pawn.Dead);
        if (playerPresent && faction.HostileTo(Faction.OfPlayer))
        {
            LivingWorldSettlementMapMaterializationService.AssignSettlementLord(
                state,
                map,
                faction,
                mapComponent.PurposeKey,
                includeResidents: true);
            return;
        }

        var residentCitizenIds = state.MaterializationLeases
            .Where(lease =>
                lease.IsActive
                && lease.Purpose == MaterializationPurpose.SettlementVisit
                && string.Equals(lease.PurposeKey, mapComponent.PurposeKey, StringComparison.Ordinal))
            .Select(lease => lease.CitizenId)
            .ToHashSet();
        var residents = map.mapPawns.AllPawnsSpawned
            .Where(pawn =>
                pawn != null
                && !pawn.Dead
                && !pawn.Downed
                && pawn.Faction == faction
                && pawn.RaceProps?.Humanlike == true
                && pawn.GetComp<CompLivingWorldIdentity>()?.HasLedgerId == true
                && residentCitizenIds.Contains(pawn.GetComp<CompLivingWorldIdentity>()!.LedgerId))
            .OrderBy(pawn => pawn.thingIDNumber)
            .ToList();
        foreach (var pawn in residents)
        {
            pawn.GetLord()?.Notify_PawnLost(pawn, PawnLostCondition.ForcedToJoinOtherLord);
            if (pawn.jobs == null || pawn.InMentalState || pawn.IsPrisoner)
            {
                continue;
            }

            if (pawn.CurJob != null
                && pawn.CurJobDef != JobDefOf.Goto
                && pawn.CurJobDef != JobDefOf.LayDown)
            {
                continue;
            }

            var hour = GenLocalDate.HourOfDay(pawn);
            if (hour >= 22 || hour < 6)
            {
                var bed = map.listerThings.AllThings
                    .OfType<Building_Bed>()
                    .Where(candidate => candidate.Faction == faction && candidate.Position.Standable(map))
                    .OrderBy(candidate => candidate.Position.DistanceToSquared(pawn.Position))
                    .FirstOrDefault();
                if (bed != null && pawn.CurJobDef != JobDefOf.LayDown)
                {
                    pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.LayDown, bed), JobTag.Misc);
                }

                continue;
            }

            if (pawn.CurJobDef == JobDefOf.Goto && pawn.CurJob?.expiryInterval > 0)
            {
                continue;
            }

            var workAnchor = ResolveWorkAnchor(map, mapComponent, pawn, tick);
            if (workAnchor.IsValid && workAnchor.Standable(map) && pawn.Position != workAnchor)
            {
                var job = JobMaker.MakeJob(JobDefOf.Goto, workAnchor);
                job.expiryInterval = 1_800;
                job.locomotionUrgency = LocomotionUrgency.Walk;
                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }
        }
    }

    private static IntVec3 ResolveWorkAnchor(
        Map map,
        LivingWorldSettlementVisitMapComponent mapComponent,
        Pawn pawn,
        int tick)
    {
        var anchors = mapComponent.FacilityThings
            .Select(entry => map.listerThings.AllThings.FirstOrDefault(thing => thing?.thingIDNumber == entry.ThingId))
            .Where(thing => thing != null && thing.Spawned && thing.Position.Standable(map))
            .Select(thing => thing!.Position)
            .Distinct()
            .OrderBy(cell => cell.x)
            .ThenBy(cell => cell.z)
            .ToList();
        if (anchors.Count == 0)
        {
            return map.Center;
        }

        var stable = unchecked((uint)(pawn.thingIDNumber * 397 ^ tick / 2_500));
        var index = (int)(stable % (uint)anchors.Count);
        return anchors[index];
    }
}
