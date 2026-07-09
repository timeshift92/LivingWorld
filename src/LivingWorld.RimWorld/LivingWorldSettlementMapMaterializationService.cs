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
    private const int MaxMapAnimals = 6;

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
        var layout = SettlementMapLayoutService.BuildFacilityLayout(
            component.State,
            new SettlementMapLayoutRequest(
                ledgerSettlement.Id,
                map.Center.x,
                map.Center.z,
                MaxFacilities: 8));
        var roomCount = SpawnSettlementRooms(map, settlement.Faction, layout);
        SpawnReservedResources(component.State, map, prepared, layout);
        var animalCount = SpawnSettlementAnimals(component.State, map, ledgerSettlement.Id, settlement.Faction, purposeKey);
        var facilityCount = SpawnFacilityLayout(map, settlement.Faction, layout);
        var cityFeatureCount = SpawnCityFeatures(map, settlement.Faction, layout);

        if ((LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging)
        {
            Log.Message(
                $"[LivingWorld] materialized settlement map '{settlement.LabelCap}'"
                + $" with {bound} ledger defender(s), {animalCount} animal(s),"
                + $" {facilityCount} facility feature(s), {roomCount} room shell(s),"
                + $" {cityFeatureCount} city feature(s),"
                + $" and {prepared.Resources.Sum(resource => resource.Quantity)} resource unit(s).");
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
        SettlementDefenseMaterializationResult prepared,
        SettlementMapLayoutResult layout)
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
            var spawned = TrySpawnResourceStack(map, lease.ReturnOwnerId, resource.ResourceKey, resource.Quantity, layout.StockpileCells);
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

    private static int TrySpawnResourceStack(
        Map map,
        EntityId returnOwnerId,
        string resourceKey,
        int quantity,
        IReadOnlyList<SettlementMapStockpileCell> stockpileCells)
    {
        if (quantity <= 0)
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
        var stackIndex = 0;
        while (remaining > 0)
        {
            if (!TryFindResourceCell(map, stockpileCells, stackIndex, out var cell))
            {
                break;
            }

            var stack = Math.Min(remaining, Math.Max(1, def.stackLimit));
            var thing = ThingMaker.MakeThing(def);
            thing.stackCount = stack;
            GenSpawn.Spawn(thing, cell, map);
            LivingWorldSettlementMapResourceTracker.Track(thing, returnOwnerId, resourceKey);
            remaining -= stack;
            spawned += stack;
            stackIndex++;
        }

        return spawned;
    }

    private static int SpawnSettlementAnimals(
        WorldState state,
        Map map,
        EntityId settlementId,
        Faction faction,
        string purposeKey)
    {
        var withdrawn = AnimalMapMaterializationService.WithdrawForSettlementMap(
            state,
            new AnimalMapMaterializationRequest(
                settlementId,
                MaxMapAnimals,
                Find.TickManager?.TicksGame ?? state.CurrentTick,
                purposeKey));
        if (withdrawn.Status != AnimalMapMaterializationStatus.Success)
        {
            return 0;
        }

        var spawned = 0;
        var failed = new List<MaterializedAnimalStack>();
        foreach (var animal in withdrawn.Animals)
        {
            var failedCount = 0;
            for (var i = 0; i < animal.Count; i++)
            {
                if (TrySpawnAnimal(map, faction, animal.AnimalKind, out var pawn))
                {
                    LivingWorldAnimalMapPawnTracker.Track(pawn, animal with { Count = 1 });
                    spawned++;
                }
                else
                {
                    failedCount++;
                }
            }

            if (failedCount > 0)
            {
                failed.Add(animal with { Count = failedCount });
            }
        }

        if (failed.Count > 0)
        {
            AnimalMapMaterializationService.ReturnToCohorts(
                state,
                failed,
                Find.TickManager?.TicksGame ?? state.CurrentTick,
                "settlement map animal spawn failed");
        }

        return spawned;
    }

    private static bool TrySpawnAnimal(Map map, Faction faction, string animalKind, out Pawn pawn)
    {
        pawn = null!;
        if (!TryFindSpawnCell(map, out var cell))
        {
            return false;
        }

        PawnKindDef pawnKind;
        try
        {
            pawnKind = PawnKindDef.Named(animalKind);
        }
        catch
        {
            return false;
        }

        var request = new PawnGenerationRequest(
            pawnKind,
            faction,
            PawnGenerationContext.NonPlayer,
            forceGenerateNewPawn: true);
        pawn = PawnGenerator.GeneratePawn(request);
        GenSpawn.Spawn(pawn, cell, map);
        return true;
    }

    private static int SpawnSettlementRooms(Map map, Faction faction, SettlementMapLayoutResult layout)
    {
        if (layout.Status != SettlementMapLayoutStatus.Success)
        {
            return 0;
        }

        var spawned = 0;
        foreach (var room in layout.Rooms)
        {
            if (TrySpawnRoomShell(map, faction, room))
            {
                spawned++;
            }
        }

        return spawned;
    }

    private static bool TrySpawnRoomShell(Map map, Faction faction, SettlementMapRoom room)
    {
        var anyPlaced = false;
        TrySetRoomTerrain(map, room);

        var doorX = room.MinX + room.Width / 2;
        var doorZ = room.MinZ;
        for (var x = room.MinX; x < room.MinX + room.Width; x++)
        {
            for (var z = room.MinZ; z < room.MinZ + room.Height; z++)
            {
                var edge = x == room.MinX
                    || x == room.MinX + room.Width - 1
                    || z == room.MinZ
                    || z == room.MinZ + room.Height - 1;
                if (!edge)
                {
                    continue;
                }

                var isDoor = x == doorX && z == doorZ;
                if (TrySpawnStructure(
                    map,
                    faction,
                    new IntVec3(x, 0, z),
                    isDoor ? room.DoorThingDefName : room.WallThingDefName,
                    room.WallStuffDefName,
                    out var structure))
                {
                    LivingWorldSettlementMapFacilityTracker.Track(structure, room.FacilityId);
                    anyPlaced = true;
                }
            }
        }

        return anyPlaced;
    }

    private static void TrySetRoomTerrain(Map map, SettlementMapRoom room)
    {
        var terrain = DefDatabase<TerrainDef>.GetNamedSilentFail(room.FloorTerrainDefName);
        if (terrain == null)
        {
            return;
        }

        for (var x = room.MinX + 1; x < room.MinX + room.Width - 1; x++)
        {
            for (var z = room.MinZ + 1; z < room.MinZ + room.Height - 1; z++)
            {
                var cell = new IntVec3(x, 0, z);
                if (cell.InBounds(map) && !cell.Fogged(map))
                {
                    map.terrainGrid.SetTerrain(cell, terrain);
                    LivingWorldSettlementMapFloorTracker.Track(map, room.FacilityId, cell, room.FloorTerrainDefName);
                }
            }
        }
    }

    private static bool TrySpawnStructure(
        Map map,
        Faction faction,
        IntVec3 cell,
        string thingDefName,
        string stuffDefName,
        out Thing? spawnedThing)
    {
        spawnedThing = null;
        if (!cell.InBounds(map) || cell.Fogged(map) || cell.GetEdifice(map) != null)
        {
            return false;
        }

        var def = DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);
        if (def == null)
        {
            return false;
        }

        ThingDef? stuff = null;
        if (def.MadeFromStuff)
        {
            stuff = DefDatabase<ThingDef>.GetNamedSilentFail(stuffDefName);
            if (stuff == null)
            {
                return false;
            }
        }

        try
        {
            var thing = ThingMaker.MakeThing(def, stuff);
            if (thing is Building building && faction != null)
            {
                building.SetFactionDirect(faction);
            }

            GenSpawn.Spawn(thing, cell, map);
            spawnedThing = thing;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int SpawnFacilityLayout(Map map, Faction faction, SettlementMapLayoutResult layout)
    {
        if (layout.Status != SettlementMapLayoutStatus.Success)
        {
            return 0;
        }

        var spawned = 0;
        foreach (var feature in layout.Facilities)
        {
            if (TrySpawnFacilityThing(map, faction, feature))
            {
                spawned++;
            }
        }

        return spawned;
    }

    private static bool TrySpawnFacilityThing(Map map, Faction faction, SettlementMapFacilityFeature feature)
    {
        if (!TryFindFacilityCell(map, feature, out var cell))
        {
            return false;
        }

        ThingDef def;
        try
        {
            def = ThingDef.Named(feature.PrimaryThingDefName);
        }
        catch
        {
            return false;
        }

        try
        {
            var thing = ThingMaker.MakeThing(def);
            if (thing is Building building && faction != null)
            {
                building.SetFactionDirect(faction);
            }

            GenSpawn.Spawn(thing, cell, map);
            LivingWorldSettlementMapFacilityTracker.Track(thing, feature.FacilityId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryFindFacilityCell(Map map, SettlementMapFacilityFeature feature, out IntVec3 cell)
    {
        var anchor = new IntVec3(feature.AnchorX, 0, feature.AnchorZ);
        foreach (var candidate in map.AllCells
            .OrderBy(candidate => candidate.DistanceToSquared(anchor))
            .ThenBy(candidate => candidate.x)
            .ThenBy(candidate => candidate.z))
        {
            if (candidate.InBounds(map)
                && candidate.Standable(map)
                && !candidate.Fogged(map)
                && candidate.GetEdifice(map) == null
                && candidate.GetFirstItem(map) == null)
            {
                cell = candidate;
                return true;
            }
        }

        cell = IntVec3.Invalid;
        return false;
    }

    private static int SpawnCityFeatures(Map map, Faction faction, SettlementMapLayoutResult layout)
    {
        if (layout.Status != SettlementMapLayoutStatus.Success)
        {
            return 0;
        }

        var spawned = 0;
        foreach (var feature in layout.CityFeatures.OrderBy(feature => feature.Order))
        {
            if (TrySpawnCityFeatureThing(map, faction, feature))
            {
                spawned++;
            }
        }

        return spawned;
    }

    private static bool TrySpawnCityFeatureThing(Map map, Faction faction, SettlementMapCityFeature feature)
    {
        var cell = new IntVec3(feature.X, 0, feature.Z);
        if (!cell.InBounds(map)
            || cell.Fogged(map)
            || !cell.Standable(map)
            || cell.GetEdifice(map) != null
            || cell.GetFirstItem(map) != null)
        {
            return false;
        }

        var def = DefDatabase<ThingDef>.GetNamedSilentFail(feature.ThingDefName);
        if (def == null)
        {
            return false;
        }

        ThingDef? stuff = null;
        if (def.MadeFromStuff)
        {
            stuff = DefDatabase<ThingDef>.GetNamedSilentFail(feature.StuffDefName);
            if (stuff == null)
            {
                return false;
            }
        }

        try
        {
            var thing = ThingMaker.MakeThing(def, stuff);
            if (thing is Building building && faction != null)
            {
                building.SetFactionDirect(faction);
            }

            GenSpawn.Spawn(thing, cell, map);
            if (feature.FacilityId.HasValue)
            {
                LivingWorldSettlementMapFacilityTracker.Track(thing, feature.FacilityId.Value);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryFindResourceCell(
        Map map,
        IReadOnlyList<SettlementMapStockpileCell> stockpileCells,
        int stackIndex,
        out IntVec3 cell)
    {
        foreach (var slot in stockpileCells
            .OrderBy(slot => Math.Abs(slot.Order - stackIndex))
            .ThenBy(slot => slot.Order))
        {
            var candidate = new IntVec3(slot.X, 0, slot.Z);
            if (candidate.InBounds(map)
                && candidate.Standable(map)
                && !candidate.Fogged(map)
                && candidate.GetEdifice(map) == null
                && candidate.GetFirstItem(map) == null)
            {
                cell = candidate;
                return true;
            }
        }

        return TryFindSpawnCell(map, out cell);
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
