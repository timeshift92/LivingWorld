using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

public static class LivingWorldSettlementMapMaterializationService
{
    private const int DefenseLeaseLifetimeTicks = 60_000 * 5;

    public static int MaterializeSettlementMap(Map map, MapParent parent)
    {
        if (map == null || parent is not Settlement settlement || settlement.Faction == null)
        {
            return 0;
        }

        if (settlement.Faction == Faction.OfPlayer)
        {
            return 0;
        }

        var component = LivingWorldWorldComponent.Instance;
        if (component == null || component.State.IsInitialWorldSeedingActive)
        {
            return 0;
        }

        var factionDefName = settlement.Faction.def?.defName;
        if (string.IsNullOrEmpty(factionDefName))
        {
            return 0;
        }

        var ledgerSettlement = ResolveLedgerSettlement(component.State, settlement, factionDefName!);
        if (ledgerSettlement == null)
        {
            return 0;
        }

        var bindableDefenders = map.mapPawns.AllPawnsSpawned
            .Where(pawn =>
                pawn != null
                && !pawn.Dead
                && pawn.Faction == settlement.Faction
                && pawn.RaceProps?.Humanlike == true
                && pawn.GetComp<CompLivingWorldIdentity>()?.HasLedgerId != true)
            .OrderBy(pawn => pawn.thingIDNumber)
            .ToList();
        if (bindableDefenders.Count == 0)
        {
            return 0;
        }

        var purposeKey = $"settlement-defense:{settlement.ID}:{map.uniqueID}";
        var prepared = SettlementMaterializationService.PrepareDefense(
            component.State,
            new SettlementDefenseMaterializationRequest(
                ledgerSettlement.Id,
                bindableDefenders.Count,
                DefenseLeaseLifetimeTicks,
                BuildResourceRequest(component.State, ledgerSettlement.Id),
                purposeKey));
        if (prepared.Status != SettlementDefenseMaterializationStatus.Success)
        {
            return 0;
        }

        var bound = BindDefenders(component.State, prepared.DefenderLeases, bindableDefenders);
        if (bound == 0)
        {
            SettlementMaterializationService.AbortDefense(component.State, purposeKey, "settlement map had no bindable defenders");
            return 0;
        }

        ReleaseUnboundLeases(component.State, prepared.DefenderLeases);
        SpawnReservedResources(component.State, map, prepared);

        if ((LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging)
        {
            Log.Message(
                $"[LivingWorld] materialized settlement map '{settlement.LabelCap}'"
                + $" with {bound} ledger defender(s) and {prepared.Resources.Sum(resource => resource.Quantity)} resource unit(s).");
        }

        return bound;
    }

    private static int BindDefenders(WorldState state, IReadOnlyList<MaterializationLease> leases, IReadOnlyList<Pawn> pawns)
    {
        var bound = 0;
        for (var i = 0; i < leases.Count && i < pawns.Count; i++)
        {
            var lease = leases[i];
            var pawn = pawns[i];
            var bind = MaterializationLeaseService.BindPawn(state, lease.Id, pawn.thingIDNumber);
            if (bind.Status != MaterializationLeaseBindStatus.Success)
            {
                continue;
            }

            var identity = pawn.GetComp<CompLivingWorldIdentity>();
            if (identity == null)
            {
                identity = new CompLivingWorldIdentity { parent = pawn };
                pawn.AllComps.Add(identity);
            }

            identity.SetLedgerId(lease.CitizenId);
            bound++;
        }

        return bound;
    }

    private static void ReleaseUnboundLeases(WorldState state, IReadOnlyList<MaterializationLease> leases)
    {
        foreach (var lease in leases)
        {
            var current = state.GetMaterializationLease(lease.Id);
            if (current?.Lifecycle == MaterializationLeaseLifecycle.Reserved)
            {
                state.ReleaseMaterializationLease(lease.Id);
            }
        }
    }

    private static IReadOnlyDictionary<string, int> BuildResourceRequest(WorldState state, EntityId settlementId)
    {
        var requested = new Dictionary<string, int>(StringComparer.Ordinal);
        RequestIfAvailable("Steel", 80);
        RequestIfAvailable("PackagedSurvivalMeal", 25);
        RequestIfAvailable("MedicineIndustrial", 8);
        RequestIfAvailable("ComponentIndustrial", 8);
        RequestIfAvailable("Silver", 250);
        return requested;

        void RequestIfAvailable(string resourceKey, int cap)
        {
            var available = ResourceLedgerService.GetQuantity(state, settlementId, resourceKey);
            if (available > 0)
            {
                requested[resourceKey] = Math.Min(available, cap);
            }
        }
    }

    private static void SpawnReservedResources(
        WorldState state,
        Map map,
        SettlementDefenseMaterializationResult prepared)
    {
        if (prepared.ResourceOwnerId == null || prepared.Resources.Count == 0)
        {
            return;
        }

        var resourceOwnerId = prepared.ResourceOwnerId.Value;
        var lease = state.GetMaterializationLease(resourceOwnerId);
        if (lease == null)
        {
            return;
        }

        foreach (var resource in prepared.Resources)
        {
            var spawned = TrySpawnResourceStack(map, resource.ResourceKey, resource.Quantity);
            if (spawned > 0)
            {
                ResourceLedgerService.ConsumeResource(
                    state,
                    resourceOwnerId,
                    resource.ResourceKey,
                    spawned,
                    "settlement map resource spawned");
            }

            var remainder = resource.Quantity - spawned;
            if (remainder > 0)
            {
                state.TransferResource(
                    resourceOwnerId,
                    lease.ReturnOwnerId,
                    resource.ResourceKey,
                    remainder,
                    "settlement map resource spawn failed");
            }
        }
    }

    private static int TrySpawnResourceStack(Map map, string resourceKey, int quantity)
    {
        if (quantity <= 0 || !TryFindSpawnCell(map, out var cell))
        {
            return 0;
        }

        ThingDef def;
        try
        {
            def = ThingDef.Named(resourceKey);
        }
        catch
        {
            return 0;
        }

        var remaining = quantity;
        var spawned = 0;
        while (remaining > 0)
        {
            var stack = Math.Min(remaining, Math.Max(1, def.stackLimit));
            var thing = ThingMaker.MakeThing(def);
            thing.stackCount = stack;
            GenSpawn.Spawn(thing, cell, map);
            remaining -= stack;
            spawned += stack;
        }

        return spawned;
    }

    private static bool TryFindSpawnCell(Map map, out IntVec3 cell)
    {
        foreach (var candidate in map.AllCells
            .OrderBy(candidate => candidate.DistanceToSquared(map.Center))
            .ThenBy(candidate => candidate.x)
            .ThenBy(candidate => candidate.z))
        {
            if (candidate.InBounds(map) && candidate.Standable(map) && !candidate.Fogged(map))
            {
                cell = candidate;
                return true;
            }
        }

        cell = IntVec3.Invalid;
        return false;
    }

    private static WorldSettlement? ResolveLedgerSettlement(WorldState state, Settlement worldObject, string factionId)
    {
        var tileToken = $":{worldObject.Tile}:";
        return state.Settlements.FirstOrDefault(candidate =>
            candidate.IsActive
            && string.Equals(candidate.FactionId, factionId, StringComparison.Ordinal)
            && candidate.Slug.Contains(tileToken));
    }
}
