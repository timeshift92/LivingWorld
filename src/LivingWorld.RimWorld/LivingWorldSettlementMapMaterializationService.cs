using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI.Group;

namespace LivingWorld.RimWorld;

public static class LivingWorldSettlementMapMaterializationService
{
    private const int DefenseLeaseLifetimeTicks = 60_000 * 5;
    private const int MaxMapAnimals = 6;

    public static int MaterializeSettlementMap(Map map, MapParent parent)
    {
        if (map == null || parent == null)
        {
            return 0;
        }

        var component = LivingWorldWorldComponent.Instance;
        if (component == null || component.State.IsInitialWorldSeedingActive)
        {
            return 0;
        }

        if (!TryBuildMaterializationContext(component.State, parent, out var context))
        {
            return 0;
        }

        var visitMapComponent = LivingWorldSettlementVisitMapComponent.For(map);
        if (visitMapComponent == null)
        {
            Log.Error("[LivingWorld] Settlement map materialization requires its persistent map component; preparation was aborted.");
            return 0;
        }

        if (context.VisitSite != null)
        {
            visitMapComponent?.ConfigureFrom(context.VisitSite);
        }
        else
        {
            visitMapComponent?.ConfigureForSettlement(context.LedgerSettlement.Id);
        }

        if (visitMapComponent?.Lifecycle is LivingWorldMapMaterializationLifecycle.Preparing
            or LivingWorldMapMaterializationLifecycle.Materialized)
        {
            return visitMapComponent.MaterializedScore;
        }

        var purposeKey = $"{context.PurposeKey}:{map.uniqueID}";
        if (visitMapComponent?.BeginMaterialization(context.LedgerSettlement.Id, purposeKey) == false)
        {
            return visitMapComponent.MaterializedScore;
        }

        SettlementMapLayoutResult? generatedLayout = null;
        try
        {
            var bindableDefenders = map.mapPawns.AllPawnsSpawned
                .Where(pawn =>
                    pawn != null
                    && !pawn.Dead
                    && pawn.Faction == context.Faction
                    && pawn.RaceProps?.Humanlike == true
                    && pawn.GetComp<CompLivingWorldIdentity>()?.HasLedgerId != true)
                .OrderBy(pawn => pawn.thingIDNumber)
                .ToList();
            var requestedDefenders = bindableDefenders.Count > 0
                ? bindableDefenders.Count
                : EstimateDefenderCount(component.State, context.LedgerSettlement.Id);
            var prepared = requestedDefenders > 0
                ? SettlementMaterializationService.PrepareDefense(
                    component.State,
                    new SettlementDefenseMaterializationRequest(
                        context.LedgerSettlement.Id,
                        requestedDefenders,
                        DefenseLeaseLifetimeTicks,
                        BuildResourceRequest(component.State, context.LedgerSettlement.Id),
                        purposeKey))
                : new SettlementDefenseMaterializationResult(
                    SettlementDefenseMaterializationStatus.NoDefenders,
                    "settlement has no adult defenders",
                    Array.Empty<MaterializationLease>(),
                    Array.Empty<ResourceStack>(),
                    null);

            var layout = SettlementMapLayoutService.BuildFacilityLayout(
                component.State,
                new SettlementMapLayoutRequest(
                    context.LedgerSettlement.Id,
                    map.Center.x,
                    map.Center.z,
                    MaxFacilities: 8));
            generatedLayout = layout;
            ClearGeneratedSettlementContent(map, context.Faction, layout);
            var cityTerrainCount = SpawnDistrictTerrain(map, layout);
            var roomCount = SpawnSettlementRooms(map, context.Faction, layout);
            var facilityCount = SpawnFacilityLayout(map, context.Faction, layout);
            var cityFeatureCount = SpawnCityFeatures(map, context.Faction, layout);

            var bound = 0;
            var spawnedDefenders = 0;
            var reservedResources = 0;
            if (prepared.Status == SettlementDefenseMaterializationStatus.Success)
            {
                bound = BindDefenders(
                    component.State,
                    map,
                    context.LedgerSettlement.Id,
                    prepared.DefenderLeases,
                    bindableDefenders);
                spawnedDefenders = SpawnGeneratedDefenders(
                    component.State,
                    map,
                    context.Faction,
                    context.LedgerSettlement.Id,
                    prepared.DefenderLeases.Skip(bound).ToList());

                if (bound + spawnedDefenders == 0)
                {
                    SettlementMaterializationService.AbortDefense(
                        component.State,
                        purposeKey,
                        "settlement map had no bindable or spawnable defenders");
                }
                else
                {
                    ReleaseUnboundLeases(component.State, prepared.DefenderLeases);
                    SpawnReservedResources(component.State, map, prepared, layout);
                    reservedResources = prepared.Resources.Sum(resource => resource.Quantity);
                }
            }

            RemoveUnboundGeneratedPawns(bindableDefenders);
            var residents = SettlementResidentMaterializationService.PrepareResidents(
                component.State,
                new SettlementResidentMaterializationRequest(
                    context.LedgerSettlement.Id,
                    DefenseLeaseLifetimeTicks,
                    purposeKey));
            var spawnedResidents = residents.Status == SettlementResidentMaterializationStatus.Success
                ? SpawnGeneratedResidents(
                    component.State,
                    map,
                    context.Faction,
                    context.LedgerSettlement.Id,
                    residents.Leases)
                : 0;
            SettlementResidentMaterializationService.ReleaseUnmaterialized(component.State, purposeKey);

            var animalCount = SpawnSettlementAnimals(component.State, map, context.LedgerSettlement.Id, context.Faction, purposeKey);

            var materializedPopulation = map.mapPawns.AllPawnsSpawned.Count(pawn =>
                pawn != null
                && !pawn.Dead
                && pawn.Faction == context.Faction
                && pawn.RaceProps?.Humanlike == true
                && pawn.GetComp<CompLivingWorldIdentity>()?.HasLedgerId == true);
            var materializedScore = materializedPopulation + facilityCount + roomCount + cityFeatureCount;
            if (materializedPopulation <= 0 || materializedScore <= 0)
            {
                var rollbackComplete = RollbackFailedMaterialization(
                    component.State,
                    map,
                    context.Faction,
                    purposeKey,
                    generatedLayout,
                    "settlement map materialization did not produce a ledger population");
                visitMapComponent?.FailMaterialization(
                    "Settlement has no materializable ledger residents.",
                    requiresRecovery: !rollbackComplete);
                return 0;
            }

            AssignSettlementLord(map, context.Faction);
            var visitSite = context.VisitSite;
            var nextVersion = Math.Max(1, (visitSite?.MaterializedVersion ?? 0) + 1);
            if (visitSite != null)
            {
                visitSite.MarkMaterialized(nextVersion);
                visitMapComponent?.ConfigureFrom(visitSite);
            }

            visitMapComponent?.CommitMaterialization(nextVersion, materializedPopulation, materializedScore);

            if ((LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging)
            {
                Log.Message(
                    $"[LivingWorld] materialized settlement map '{context.Label}'"
                    + $" with {bound + spawnedDefenders} ledger defender(s), {spawnedResidents} resident(s), {animalCount} animal(s),"
                    + $" {facilityCount} facility feature(s), {roomCount} room shell(s),"
                    + $" {cityFeatureCount} city feature(s), {cityTerrainCount} district/path terrain cell(s),"
                    + $" and {reservedResources} resource unit(s).");
            }

            return materializedScore;
        }
        catch (Exception ex)
        {
            var rollbackComplete = RollbackFailedMaterialization(
                component.State,
                map,
                context.Faction,
                purposeKey,
                generatedLayout,
                $"settlement map preparation failed: {ex.GetType().Name}: {ex.Message}");
            visitMapComponent?.FailMaterialization(ex.Message, requiresRecovery: !rollbackComplete);
            Log.Error($"[LivingWorld] Settlement map materialization rolled back after failure: {ex}");
            return 0;
        }
    }

    public static bool RetryFailedMaterializationRollback(
        WorldState state,
        Map map,
        string purposeKey,
        string reason)
    {
        return RollbackFailedMaterialization(
            state,
            map,
            map.ParentFaction,
            purposeKey,
            layout: null,
            reason);
    }

    private static bool RollbackFailedMaterialization(
        WorldState state,
        Map map,
        Faction? faction,
        string purposeKey,
        SettlementMapLayoutResult? layout,
        string reason)
    {
        var mapComponent = LivingWorldSettlementVisitMapComponent.For(map);
        var trackedResources = mapComponent?.Resources.ToList()
            ?? new List<LivingWorldTrackedMapResource>();
        var trackedAnimalIds = mapComponent?.Animals.Select(entry => entry.PawnThingId).ToHashSet()
            ?? new HashSet<int>();
        var activeLeases = state.MaterializationLeases
            .Where(lease => lease.IsActive && string.Equals(lease.PurposeKey, purposeKey, StringComparison.Ordinal))
            .OrderBy(lease => lease.Id.Value)
            .ToList();
        var resourcesRecovered = TryRollback(
            () => LivingWorldSettlementMapResourceTracker.ReconcileMap(state, map, reason),
            "resources");
        var animalsRecovered = TryRollback(
            () => LivingWorldAnimalMapPawnTracker.ReconcileMap(state, map, reason),
            "animals");

        if (resourcesRecovered)
        {
            resourcesRecovered &= TryRollback(
                () =>
                {
                    foreach (var thing in map.listerThings.AllThings
                        .Where(thing => thing != null && trackedResources.Any(entry => entry.ThingId == thing.thingIDNumber))
                        .ToList())
                    {
                        if (!thing.Destroyed)
                        {
                            thing.Destroy(DestroyMode.Vanish);
                        }
                    }
                },
                "resource things");
        }

        if (animalsRecovered)
        {
            foreach (var pawn in map.mapPawns.AllPawns
                .Where(pawn => pawn != null && trackedAnimalIds.Contains(pawn.thingIDNumber))
                .ToList())
            {
                animalsRecovered &= TryRollback(
                    () => DestroyRollbackPawn(pawn),
                    $"animal pawn {pawn.thingIDNumber}");
            }
        }

        var leasesRecovered = true;
        foreach (var lease in activeLeases)
        {
            var pawn = lease.PawnThingId.HasValue
                ? map.mapPawns.AllPawns.FirstOrDefault(candidate => candidate?.thingIDNumber == lease.PawnThingId.Value)
                : null;
            var pawnRemoved = pawn == null
                || TryRollback(() => DestroyRollbackPawn(pawn), $"leased pawn {pawn.thingIDNumber}");
            if (!pawnRemoved)
            {
                leasesRecovered = false;
                continue;
            }

            leasesRecovered &= TryRollback(
                () =>
                {
                    var released = MaterializationLeaseService.Release(state, lease.Id, reason);
                    if (released.Status is not MaterializationLeaseResolveStatus.Success
                        and not MaterializationLeaseResolveStatus.AlreadyResolved)
                    {
                        throw new InvalidOperationException(released.Reason);
                    }
                },
                $"lease {lease.Id}");
        }

        var recovered = resourcesRecovered && animalsRecovered && leasesRecovered;
        if (recovered && layout != null && faction != null)
        {
            TryRollback(() => ClearGeneratedSettlementContent(map, faction, layout), "generated map content");
        }

        if (recovered)
        {
            mapComponent?.ClearManifest();
            mapComponent?.MarkRollbackRecovered();
        }

        return recovered;
    }

    private static void DestroyRollbackPawn(Pawn pawn)
    {
        pawn.GetLord()?.Notify_PawnLost(pawn, PawnLostCondition.ForcedToJoinOtherLord);
        if (pawn.Corpse is { Destroyed: false } corpse)
        {
            corpse.Destroy(DestroyMode.Vanish);
        }
        else if (!pawn.Destroyed)
        {
            pawn.Destroy(DestroyMode.Vanish);
        }
    }

    private static bool TryRollback(Action action, string layer)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"[LivingWorld] Settlement map rollback layer '{layer}' failed: {ex}");
            return false;
        }
    }

    private static bool TryBuildMaterializationContext(
        WorldState state,
        MapParent parent,
        out SettlementMapMaterializationContext context)
    {
        context = null!;
        if (parent is Settlement settlement)
        {
            if (settlement.Faction == null || settlement.Faction == Faction.OfPlayer)
            {
                return false;
            }

            var factionDefName = settlement.Faction.def?.defName;
            if (string.IsNullOrEmpty(factionDefName))
            {
                return false;
            }

            var ledgerSettlement = ResolveLedgerSettlement(state, settlement, factionDefName!);
            if (ledgerSettlement == null)
            {
                return false;
            }

            context = new SettlementMapMaterializationContext(
                ledgerSettlement,
                settlement.Faction,
                settlement.LabelCap,
                $"settlement-defense:{settlement.ID}",
                VisitSite: null);
            return true;
        }

        if (parent is WorldObject_LivingWorldSettlementVisitSite visitSite)
        {
            if (visitSite.Faction == null || visitSite.Faction == Faction.OfPlayer)
            {
                return false;
            }

            var settlementId = visitSite.SettlementId;
            if (!settlementId.HasValue)
            {
                return false;
            }

            var ledgerSettlement = state.GetSettlement(settlementId.Value);
            if (ledgerSettlement == null || !ledgerSettlement.IsActive)
            {
                return false;
            }

            context = new SettlementMapMaterializationContext(
                ledgerSettlement,
                visitSite.Faction,
                visitSite.LabelCap,
                $"settlement-visit:{visitSite.ID}:{ledgerSettlement.Id.Value}",
                visitSite);
            return true;
        }

        return false;
    }

    private static int BindDefenders(
        WorldState state,
        Map map,
        EntityId settlementId,
        IReadOnlyList<MaterializationLease> leases,
        IReadOnlyList<Pawn> pawns)
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

            StampIdentity(pawn, lease.CitizenId);
            ConservePawnGear(state, map, pawn, settlementId);
            bound++;
        }

        return bound;
    }

    private static int EstimateDefenderCount(WorldState state, EntityId settlementId)
    {
        var population = state.GetSettlementPopulation(settlementId);
        if (population.Adults <= 0)
        {
            return 0;
        }

        return Math.Min(12, Math.Max(3, population.Adults / 8));
    }

    private static int SpawnGeneratedDefenders(
        WorldState state,
        Map map,
        Faction faction,
        EntityId settlementId,
        IReadOnlyList<MaterializationLease> leases)
    {
        var spawned = 0;
        foreach (var lease in leases)
        {
            if (!TrySpawnDefender(state, map, faction, lease, out var pawn))
            {
                continue;
            }

            var bind = MaterializationLeaseService.BindPawn(state, lease.Id, pawn.thingIDNumber);
            if (bind.Status != MaterializationLeaseBindStatus.Success)
            {
                pawn.Destroy();
                continue;
            }

            StampIdentity(pawn, lease.CitizenId);
            ConservePawnGear(state, map, pawn, settlementId);
            spawned++;
        }

        return spawned;
    }

    private static int SpawnGeneratedResidents(
        WorldState state,
        Map map,
        Faction faction,
        EntityId settlementId,
        IReadOnlyList<MaterializationLease> leases)
    {
        var spawned = 0;
        foreach (var lease in leases.OrderBy(lease => lease.CitizenId.Value))
        {
            var citizen = state.GetCitizen(lease.CitizenId);
            if (citizen == null || citizen.Status != CitizenStatus.Alive || !TryFindSpawnCell(map, out var cell))
            {
                continue;
            }

            var pawnKind = faction.def?.basicMemberKind ?? faction.RandomPawnKind();
            if (pawnKind == null)
            {
                continue;
            }

            Pawn? pawn = null;
            try
            {
                pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                    pawnKind,
                    faction,
                    PawnGenerationContext.NonPlayer,
                    forceGenerateNewPawn: true,
                    canGeneratePawnRelations: false,
                    fixedBiologicalAge: Math.Max(1, citizen.Age),
                    fixedChronologicalAge: Math.Max(1, citizen.Age),
                    fixedGender: citizen.Sex == Sex.Female ? Gender.Female : Gender.Male,
                    developmentalStages: DevelopmentalStage.Newborn
                        | DevelopmentalStage.Baby
                        | DevelopmentalStage.Child
                        | DevelopmentalStage.Adult));
                if (!string.IsNullOrWhiteSpace(citizen.Name))
                {
                    pawn.Name = new NameSingle(citizen.Name);
                }

                GenSpawn.Spawn(pawn, cell, map);
                var bind = MaterializationLeaseService.BindPawn(state, lease.Id, pawn.thingIDNumber);
                if (bind.Status != MaterializationLeaseBindStatus.Success)
                {
                    pawn.Destroy(DestroyMode.Vanish);
                    continue;
                }

                StampIdentity(pawn, lease.CitizenId);
                ConservePawnGear(state, map, pawn, settlementId);
                spawned++;
            }
            catch (Exception error)
            {
                if (pawn?.Spawned == true)
                {
                    pawn.Destroy(DestroyMode.Vanish);
                }

                Log.Warning($"[LivingWorld] Could not materialize resident {citizen.Id}: {error.Message}");
            }
        }

        return spawned;
    }

    private static void RemoveUnboundGeneratedPawns(IEnumerable<Pawn> generatedPawns)
    {
        foreach (var pawn in generatedPawns.ToList())
        {
            if (pawn == null || pawn.Destroyed || pawn.GetComp<CompLivingWorldIdentity>()?.HasLedgerId == true)
            {
                continue;
            }

            pawn.GetLord()?.Notify_PawnLost(pawn, PawnLostCondition.ForcedToJoinOtherLord);
            pawn.Destroy(DestroyMode.Vanish);
        }
    }

    private static void AssignSettlementLord(Map map, Faction faction)
    {
        var pawns = map.mapPawns.AllPawnsSpawned
            .Where(pawn =>
                pawn != null
                && !pawn.Dead
                && pawn.Faction == faction
                && pawn.RaceProps?.Humanlike == true
                && pawn.GetComp<CompLivingWorldIdentity>()?.HasLedgerId == true
                && pawn.GetLord() == null)
            .OrderBy(pawn => pawn.thingIDNumber)
            .ToList();
        if (pawns.Count == 0)
        {
            return;
        }

        LordMaker.MakeNewLord(
            faction,
            new LordJob_DefendBase(faction, map.Center, 60_000, attackWhenPlayerBecameEnemy: true),
            map,
            pawns);
    }

    private static void ClearGeneratedSettlementContent(
        Map map,
        Faction faction,
        SettlementMapLayoutResult layout)
    {
        var footprint = layout.Districts.Count > 0
            ? CellRect.FromLimits(
                layout.Districts.Min(district => district.MinX),
                layout.Districts.Min(district => district.MinZ),
                layout.Districts.Max(district => district.MinX + district.Width - 1),
                layout.Districts.Max(district => district.MinZ + district.Height - 1))
            : CellRect.CenteredOn(map.Center, 45);

        var things = map.listerThings.AllThings.ToList();
        foreach (var thing in things)
        {
            if (thing == null || thing.Destroyed || thing is Pawn)
            {
                continue;
            }

            var generatedFactionBuilding = thing is Building && thing.Faction == faction;
            var generatedInventory = thing.def.category == ThingCategory.Item && footprint.Contains(thing.Position);
            if (!generatedFactionBuilding && !generatedInventory)
            {
                continue;
            }

            thing.Destroy(DestroyMode.Vanish);
        }
    }

    private static bool TrySpawnDefender(
        WorldState state,
        Map map,
        Faction faction,
        MaterializationLease lease,
        out Pawn pawn)
    {
        pawn = null!;
        if (!TryFindSpawnCell(map, out var cell))
        {
            return false;
        }

        var pawnKind = faction.RandomPawnKind();
        if (pawnKind == null)
        {
            return false;
        }

        try
        {
            pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                pawnKind,
                faction,
                PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true));

            var citizen = state.GetCitizen(lease.CitizenId);
            if (citizen != null && !string.IsNullOrWhiteSpace(citizen.Name))
            {
                pawn.Name = new NameSingle(citizen.Name);
            }

            GenSpawn.Spawn(pawn, cell, map);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void StampIdentity(Pawn pawn, EntityId citizenId)
    {
        var identity = pawn.GetComp<CompLivingWorldIdentity>();
        if (identity == null)
        {
            identity = new CompLivingWorldIdentity { parent = pawn };
            pawn.AllComps.Add(identity);
        }

        identity.SetLedgerId(citizenId);
    }

    private static void ConservePawnGear(WorldState state, Map map, Pawn pawn, EntityId settlementId)
    {
        var heldThings = (pawn.equipment?.AllEquipmentListForReading.Cast<Thing>() ?? Enumerable.Empty<Thing>())
            .Concat(pawn.apparel?.WornApparel.Cast<Thing>() ?? Enumerable.Empty<Thing>())
            .Concat(pawn.inventory?.innerContainer.InnerListForReading.Cast<Thing>() ?? Enumerable.Empty<Thing>())
            .Where(thing => thing != null && !thing.Destroyed)
            .OrderBy(thing => thing.thingIDNumber)
            .ToList();
        foreach (var thing in heldThings)
        {
            var isLooseInventory = thing.def.category == ThingCategory.Item
                && thing is not Apparel
                && thing.def.IsWeapon == false;
            var preferredResource = isLooseInventory ? thing.def.defName : thing.Stuff?.defName;
            var resourceKey = !string.IsNullOrWhiteSpace(preferredResource)
                && ResourceLedgerService.GetQuantity(state, settlementId, preferredResource!) > 0
                    ? preferredResource!
                    : "Steel";
            var quantity = isLooseInventory
                ? Math.Max(1, thing.stackCount)
                : thing.def.IsWeapon ? 10 : 1;
            var consumed = ResourceLedgerService.ConsumeResource(
                state,
                settlementId,
                resourceKey,
                quantity,
                "pawn gear materialized on settlement map");
            if (consumed != quantity)
            {
                if (consumed > 0)
                {
                    ResourceLedgerService.AddResource(state, settlementId, resourceKey, consumed);
                }

                thing.Destroy(DestroyMode.Vanish);
                continue;
            }

            LivingWorldSettlementMapResourceTracker.Track(
                thing,
                settlementId,
                resourceKey,
                reservedQuantity: quantity);
        }
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
            var consumed = ResourceLedgerService.ConsumeResource(
                state,
                resourceOwnerId,
                resource.ResourceKey,
                resource.Quantity,
                "settlement map resource materialized");
            if (consumed <= 0)
            {
                continue;
            }

            var spawned = TrySpawnResourceStack(
                map,
                lease.ReturnOwnerId,
                resource.ResourceKey,
                consumed,
                layout.StockpileCells);
            var remainder = consumed - spawned;
            if (remainder > 0)
            {
                ResourceLedgerService.AddResource(
                    state,
                    lease.ReturnOwnerId,
                    resource.ResourceKey,
                    remainder);
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
            Thing? thing = null;
            try
            {
                thing = ThingMaker.MakeThing(def);
                thing.stackCount = stack;
                GenSpawn.Spawn(thing, cell, map);
                LivingWorldSettlementMapResourceTracker.Track(thing, returnOwnerId, resourceKey);
            }
            catch (Exception ex)
            {
                if (thing is { Destroyed: false })
                {
                    thing.Destroy(DestroyMode.Vanish);
                }

                Log.Warning($"[LivingWorld] Could not spawn tracked {resourceKey} resource stack: {ex.Message}");
                break;
            }

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
                    try
                    {
                        LivingWorldAnimalMapPawnTracker.Track(pawn, animal with { Count = 1 });
                        spawned++;
                    }
                    catch (Exception ex)
                    {
                        if (!pawn.Destroyed)
                        {
                            pawn.Destroy(DestroyMode.Vanish);
                        }

                        failedCount++;
                        Log.Warning($"[LivingWorld] Could not track settlement animal '{animal.AnimalKind}': {ex.Message}");
                    }
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
        try
        {
            pawn = PawnGenerator.GeneratePawn(request);
            GenSpawn.Spawn(pawn, cell, map);
            return true;
        }
        catch (Exception ex)
        {
            if (pawn is { Destroyed: false })
            {
                pawn.Destroy(DestroyMode.Vanish);
            }

            Log.Warning($"[LivingWorld] Could not spawn settlement animal '{animalKind}': {ex.Message}");
            pawn = null!;
            return false;
        }
    }

    private static int SpawnSettlementRooms(Map map, Faction faction, SettlementMapLayoutResult layout)
    {
        if (layout.Status != SettlementMapLayoutStatus.Success)
        {
            return 0;
        }

        var spawned = 0;
        var conditionByFacility = layout.Facilities.ToDictionary(
            feature => feature.FacilityId,
            feature => feature.ConditionPercent);
        foreach (var room in layout.Rooms)
        {
            var condition = conditionByFacility.TryGetValue(room.FacilityId, out var value) ? value : 100;
            if (TrySpawnRoomShell(map, faction, room, condition))
            {
                spawned++;
            }
        }

        return spawned;
    }

    private static int SpawnDistrictTerrain(Map map, SettlementMapLayoutResult layout)
    {
        if (layout.Status != SettlementMapLayoutStatus.Success)
        {
            return 0;
        }

        var placed = 0;
        foreach (var district in layout.Districts.OrderBy(district => district.Order))
        {
            for (var x = district.MinX; x < district.MinX + district.Width; x++)
            {
                for (var z = district.MinZ; z < district.MinZ + district.Height; z++)
                {
                    var cell = new IntVec3(x, 0, z);
                    if (TrySetTerrain(map, cell, district.FloorTerrainDefName, district.FacilityId))
                    {
                        placed++;
                    }
                }
            }
        }

        foreach (var pathCell in layout.PathCells.OrderBy(cell => cell.Order))
        {
            if (TrySetTerrain(
                map,
                new IntVec3(pathCell.X, 0, pathCell.Z),
                layout.Style.RoadTerrainDefName,
                facilityId: null))
            {
                placed++;
            }
        }

        return placed;
    }

    private static bool TrySetTerrain(Map map, IntVec3 cell, string terrainDefName, EntityId? facilityId)
    {
        if (!cell.InBounds(map) || cell.Fogged(map))
        {
            return false;
        }

        var terrain = DefDatabase<TerrainDef>.GetNamedSilentFail(terrainDefName);
        if (terrain == null)
        {
            return false;
        }

        map.terrainGrid.SetTerrain(cell, terrain);
        if (facilityId.HasValue)
        {
            LivingWorldSettlementMapFloorTracker.Track(map, facilityId.Value, cell, terrainDefName);
        }

        return true;
    }

    private static bool TrySpawnRoomShell(
        Map map,
        Faction faction,
        SettlementMapRoom room,
        int conditionPercent)
    {
        var anyPlaced = false;
        TrySetRoomTerrain(map, room, conditionPercent);

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
                if (!isDoor && !ShouldMaterialize(conditionPercent, room.FacilityId, x, z, salt: 1))
                {
                    continue;
                }

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

    private static void TrySetRoomTerrain(Map map, SettlementMapRoom room, int conditionPercent)
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
                if (cell.InBounds(map)
                    && !cell.Fogged(map)
                    && ShouldMaterialize(conditionPercent, room.FacilityId, x, z, salt: 2))
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
        if (!ShouldMaterialize(
                feature.ConditionPercent,
                feature.FacilityId,
                feature.AnchorX,
                feature.AnchorZ,
                salt: 3)
            || !TryFindFacilityCell(map, feature, out var cell))
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
        var conditionByFacility = layout.Facilities.ToDictionary(
            feature => feature.FacilityId,
            feature => feature.ConditionPercent);
        foreach (var feature in layout.CityFeatures.OrderBy(feature => feature.Order))
        {
            var condition = feature.FacilityId.HasValue
                && conditionByFacility.TryGetValue(feature.FacilityId.Value, out var value)
                    ? value
                    : 100;
            if (TrySpawnCityFeatureThing(map, faction, feature, condition))
            {
                spawned++;
            }
        }

        return spawned;
    }

    private static bool TrySpawnCityFeatureThing(
        Map map,
        Faction faction,
        SettlementMapCityFeature feature,
        int conditionPercent)
    {
        var cell = new IntVec3(feature.X, 0, feature.Z);
        if (feature.FacilityId.HasValue
            && !ShouldMaterialize(
                conditionPercent,
                feature.FacilityId.Value,
                feature.X,
                feature.Z,
                salt: 4 + (int)feature.Kind))
        {
            return false;
        }

        var isPowerConduit = feature.Kind == SettlementMapCityFeatureKind.PowerConduit;
        var isPowerGenerator = feature.Kind == SettlementMapCityFeatureKind.PowerGenerator;
        var isGuardPost = feature.Kind == SettlementMapCityFeatureKind.GuardPost;
        var isActivity = feature.Kind == SettlementMapCityFeatureKind.Activity;
        var blocksItemSlot = !isPowerConduit && !isPowerGenerator && !isGuardPost && !isActivity;
        if (!cell.InBounds(map)
            || cell.Fogged(map)
            || (!isPowerConduit && !cell.Standable(map))
            || cell.GetEdifice(map) != null
            || (blocksItemSlot && cell.GetFirstItem(map) != null))
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

    private static bool ShouldMaterialize(
        int conditionPercent,
        EntityId facilityId,
        int x,
        int z,
        int salt)
    {
        conditionPercent = Math.Max(0, Math.Min(100, conditionPercent));
        if (conditionPercent <= 0)
        {
            return false;
        }

        if (conditionPercent >= 100)
        {
            return true;
        }

        unchecked
        {
            uint hash = 2166136261;
            hash = (hash ^ (uint)facilityId.Value) * 16777619;
            hash = (hash ^ (uint)(facilityId.Value >> 32)) * 16777619;
            hash = (hash ^ (uint)x) * 16777619;
            hash = (hash ^ (uint)z) * 16777619;
            hash = (hash ^ (uint)salt) * 16777619;
            return hash % 100 < conditionPercent;
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

    private sealed record SettlementMapMaterializationContext(
        WorldSettlement LedgerSettlement,
        Faction Faction,
        string Label,
        string PurposeKey,
        WorldObject_LivingWorldSettlementVisitSite? VisitSite);
}
