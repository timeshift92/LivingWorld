using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

public enum LivingWorldMapMaterializationLifecycle
{
    None,
    Preparing,
    Materialized,
    Reconciled,
    Failed
}

[Flags]
public enum LivingWorldMapReconciliationLayer
{
    None = 0,
    Resources = 1 << 0,
    Facilities = 1 << 1,
    Animals = 1 << 2,
    Residents = 1 << 3,
    All = Resources | Facilities | Animals | Residents
}

/// <summary>
/// Persistent owner of a Living World settlement-map materialization session.
/// All map provenance lives here so a full process restart cannot lose reconciliation data.
/// </summary>
public sealed class LivingWorldSettlementVisitMapComponent : MapComponent
{
    private long settlementIdValue;
    private int visitSiteWorldObjectId = -1;
    private int materializedVersion;
    private int lifecycleValue;
    private string purposeKey = string.Empty;
    private string failureReason = string.Empty;
    private bool rollbackPending;
    private int materializedPopulation;
    private int materializedScore;
    private bool reconciled;
    private int reconciledLayersValue;
    private List<LivingWorldTrackedMapResource> resources = new();
    private List<LivingWorldTrackedMapThing> facilityThings = new();
    private List<LivingWorldTrackedMapFloor> floors = new();
    private List<LivingWorldTrackedMapAnimal> animals = new();
    private List<long> reconciledFacilityIds = new();
    private List<LivingWorldWarehouseManifestEntry> warehouseManifest = new();
    private int lastLiveSyncTick = -1;

    public LivingWorldSettlementVisitMapComponent(Map map)
        : base(map)
    {
    }

    public EntityId? SettlementId => settlementIdValue > 0
        ? EntityId.Create(EntityKind.Settlement, settlementIdValue)
        : null;

    public int VisitSiteWorldObjectId => visitSiteWorldObjectId;

    public int MaterializedVersion => materializedVersion;

    public LivingWorldMapMaterializationLifecycle Lifecycle =>
        Enum.IsDefined(typeof(LivingWorldMapMaterializationLifecycle), lifecycleValue)
            ? (LivingWorldMapMaterializationLifecycle)lifecycleValue
            : LivingWorldMapMaterializationLifecycle.None;

    public string PurposeKey => purposeKey;

    public string FailureReason => failureReason;

    public bool RollbackPending => rollbackPending;

    public int MaterializedPopulation => materializedPopulation;

    public int MaterializedScore => materializedScore;

    public bool Reconciled => reconciled;

    public bool IsPlayable =>
        Lifecycle == LivingWorldMapMaterializationLifecycle.Materialized
        && materializedPopulation > 0
        && materializedScore > 0;

    public IReadOnlyList<LivingWorldTrackedMapResource> Resources => resources;

    public IReadOnlyList<LivingWorldTrackedMapThing> FacilityThings => facilityThings;

    public IReadOnlyList<LivingWorldTrackedMapFloor> Floors => floors;

    public IReadOnlyList<LivingWorldTrackedMapAnimal> Animals => animals;

    public IReadOnlyList<LivingWorldWarehouseManifestEntry> WarehouseManifest => warehouseManifest;

    public void ConfigureFrom(WorldObject_LivingWorldSettlementVisitSite visitSite)
    {
        var settlementId = visitSite?.SettlementId;
        settlementIdValue = settlementId?.Value ?? 0L;
        visitSiteWorldObjectId = visitSite?.ID ?? -1;
    }

    public void ConfigureForSettlement(EntityId settlementId)
    {
        settlementIdValue = settlementId.Kind == EntityKind.Settlement ? settlementId.Value : 0L;
        visitSiteWorldObjectId = -1;
    }

    public bool BeginMaterialization(EntityId settlementId, string sessionPurposeKey)
    {
        if (Lifecycle is LivingWorldMapMaterializationLifecycle.Preparing
            or LivingWorldMapMaterializationLifecycle.Materialized)
        {
            return false;
        }

        settlementIdValue = settlementId.Kind == EntityKind.Settlement ? settlementId.Value : 0L;
        purposeKey = sessionPurposeKey ?? string.Empty;
        failureReason = string.Empty;
        rollbackPending = false;
        materializedPopulation = 0;
        materializedScore = 0;
        reconciled = false;
        reconciledLayersValue = (int)LivingWorldMapReconciliationLayer.None;
        resources.Clear();
        facilityThings.Clear();
        floors.Clear();
        animals.Clear();
        reconciledFacilityIds.Clear();
        warehouseManifest.Clear();
        lastLiveSyncTick = -1;
        lifecycleValue = (int)LivingWorldMapMaterializationLifecycle.Preparing;
        return true;
    }

    public void CommitMaterialization(int version, int population, int score)
    {
        materializedVersion = Math.Max(1, version);
        materializedPopulation = Math.Max(0, population);
        materializedScore = Math.Max(0, score);
        failureReason = string.Empty;
        reconciled = false;
        lifecycleValue = (int)LivingWorldMapMaterializationLifecycle.Materialized;
    }

    public void FailMaterialization(string reason, bool requiresRecovery = false)
    {
        failureReason = string.IsNullOrWhiteSpace(reason)
            ? "settlement map materialization failed"
            : reason.Trim();
        rollbackPending = requiresRecovery;
        lifecycleValue = (int)LivingWorldMapMaterializationLifecycle.Failed;
    }

    public void MarkRollbackRecovered()
    {
        rollbackPending = false;
    }

    public void MarkReconciled()
    {
        reconciled = true;
        reconciledLayersValue = (int)LivingWorldMapReconciliationLayer.All;
        lifecycleValue = (int)LivingWorldMapMaterializationLifecycle.Reconciled;
    }

    public bool IsLayerReconciled(LivingWorldMapReconciliationLayer layer)
    {
        return (((LivingWorldMapReconciliationLayer)reconciledLayersValue) & layer) == layer;
    }

    public void MarkLayerReconciled(LivingWorldMapReconciliationLayer layer)
    {
        reconciledLayersValue |= (int)layer;
    }

    public bool IsFacilityReconciled(EntityId facilityId)
    {
        return facilityId.Kind == EntityKind.SettlementFacility
            && reconciledFacilityIds.Contains(facilityId.Value);
    }

    public void MarkFacilityReconciled(EntityId facilityId)
    {
        if (facilityId.Kind == EntityKind.SettlementFacility
            && !reconciledFacilityIds.Contains(facilityId.Value))
        {
            reconciledFacilityIds.Add(facilityId.Value);
        }
    }

    public void TrackResource(Thing thing, EntityId returnOwnerId, string resourceKey, int reservedQuantity = -1)
    {
        if (thing == null || returnOwnerId.Value <= 0 || string.IsNullOrWhiteSpace(resourceKey))
        {
            return;
        }

        resources.RemoveAll(entry => entry.ThingId == thing.thingIDNumber);
        resources.Add(new LivingWorldTrackedMapResource(
            thing.thingIDNumber,
            (int)returnOwnerId.Kind,
            returnOwnerId.Value,
            resourceKey.Trim(),
            Math.Max(1, thing.stackCount),
            reservedQuantity >= 0 ? reservedQuantity : Math.Max(1, thing.stackCount)));
    }

    public bool TryGetResource(int thingId, out LivingWorldTrackedMapResource resource)
    {
        resource = resources.FirstOrDefault(entry => entry.ThingId == thingId)!;
        return resource != null;
    }

    public void RemoveResource(int thingId)
    {
        resources.RemoveAll(entry => entry.ThingId == thingId);
    }

    public void TrackFacilityThing(Thing thing, EntityId facilityId)
    {
        if (thing == null || facilityId.Kind != EntityKind.SettlementFacility)
        {
            return;
        }

        facilityThings.RemoveAll(entry => entry.ThingId == thing.thingIDNumber);
        facilityThings.Add(new LivingWorldTrackedMapThing(thing.thingIDNumber, facilityId.Value));
    }

    public void TrackFloor(EntityId facilityId, IntVec3 cell, string terrainDefName)
    {
        if (facilityId.Kind != EntityKind.SettlementFacility || string.IsNullOrWhiteSpace(terrainDefName))
        {
            return;
        }

        if (floors.Any(entry => entry.FacilityIdValue == facilityId.Value && entry.X == cell.x && entry.Z == cell.z))
        {
            return;
        }

        floors.Add(new LivingWorldTrackedMapFloor(facilityId.Value, cell.x, cell.z, terrainDefName.Trim()));
    }

    public void TrackAnimal(Pawn pawn, MaterializedAnimalStack animal)
    {
        if (pawn == null || animal == null || animal.Count <= 0)
        {
            return;
        }

        animals.RemoveAll(entry => entry.PawnThingId == pawn.thingIDNumber);
        animals.Add(new LivingWorldTrackedMapAnimal(
            pawn.thingIDNumber,
            animal.CohortId.Value,
            animal.AnimalKind,
            (int)animal.Type));
    }

    public bool TryGetAnimal(int pawnThingId, out LivingWorldTrackedMapAnimal animal)
    {
        animal = animals.FirstOrDefault(entry => entry.PawnThingId == pawnThingId)!;
        return animal != null;
    }

    public void RemoveAnimal(int pawnThingId)
    {
        animals.RemoveAll(entry => entry.PawnThingId == pawnThingId);
    }

    public void ClearManifest()
    {
        resources.Clear();
        facilityThings.Clear();
        floors.Clear();
        animals.Clear();
        warehouseManifest.Clear();
    }

    public void ConfigureWarehouseManifest(IReadOnlyDictionary<string, int> requested)
    {
        warehouseManifest = (requested ?? new Dictionary<string, int>())
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new LivingWorldWarehouseManifestEntry(pair.Key, pair.Value, 0))
            .ToList();
    }

    public void RefreshWarehouseMaterializedCounts()
    {
        foreach (var entry in warehouseManifest)
        {
            var quantity = resources
                .Where(resource => string.Equals(resource.ResourceKey, entry.ResourceKey, StringComparison.Ordinal))
                .Sum(resource =>
                {
                    var thing = FindTrackedThing(resource.ThingId);
                    return thing != null && !thing.Destroyed
                        ? thing.stackCount
                        : Math.Max(0, resource.ReservedQuantity);
                });
            entry.SetMaterializedQuantity(quantity);
        }
    }

    public void UpdateWarehouseTarget(string resourceKey, int targetQuantity)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return;
        }

        var entry = warehouseManifest.FirstOrDefault(candidate =>
            string.Equals(candidate.ResourceKey, resourceKey, StringComparison.Ordinal));
        if (entry == null)
        {
            warehouseManifest.Add(new LivingWorldWarehouseManifestEntry(resourceKey.Trim(), targetQuantity, 0));
        }
        else
        {
            entry.SetTargetQuantity(targetQuantity);
        }
    }

    private Thing? FindTrackedThing(int thingId)
    {
        var thing = map.listerThings?.AllThings.FirstOrDefault(candidate => candidate?.thingIDNumber == thingId);
        if (thing != null)
        {
            return thing;
        }

        return map.mapPawns.AllPawns
            .SelectMany(pawn =>
                (pawn.equipment?.AllEquipmentListForReading.Cast<Thing>() ?? Enumerable.Empty<Thing>())
                .Concat(pawn.apparel?.WornApparel.Cast<Thing>() ?? Enumerable.Empty<Thing>())
                .Concat(pawn.inventory?.innerContainer.InnerListForReading.Cast<Thing>() ?? Enumerable.Empty<Thing>()))
            .FirstOrDefault(candidate => candidate?.thingIDNumber == thingId);
    }

    public void RefreshMaterializedPopulation()
    {
        materializedPopulation = map.mapPawns.AllPawns.Count(pawn =>
            pawn != null
            && !pawn.Dead
            && pawn.RaceProps?.Humanlike == true
            && pawn.GetComp<CompLivingWorldIdentity>()?.HasLedgerId == true);
        materializedScore = Math.Max(materializedPopulation, materializedScore);
    }

    public override void MapComponentTick()
    {
        base.MapComponentTick();
        if (Lifecycle != LivingWorldMapMaterializationLifecycle.Materialized)
        {
            return;
        }

        var tick = Find.TickManager?.TicksGame ?? 0;
        if (lastLiveSyncTick >= 0 && tick - lastLiveSyncTick < 2_500)
        {
            return;
        }

        lastLiveSyncTick = tick;
        LivingWorldSettlementMapLiveSyncService.Sync(map, this, tick);
    }

    public override void FinalizeInit()
    {
        base.FinalizeInit();
        LivingWorldAnimalMapPawnTracker.ReindexMap(map);
        if (warehouseManifest.Count == 0 && resources.Count > 0)
        {
            foreach (var group in resources.GroupBy(resource => resource.ResourceKey, StringComparer.Ordinal))
            {
                var quantity = group
                    .Sum(resource =>
                    {
                        var thing = FindTrackedThing(resource.ThingId);
                        return thing != null && !thing.Destroyed
                            ? thing.stackCount
                            : Math.Max(0, resource.ReservedQuantity);
                    });
                if (quantity > 0)
                {
                    warehouseManifest.Add(new LivingWorldWarehouseManifestEntry(group.Key, quantity, quantity));
                }
            }
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref settlementIdValue, "livingWorld_settlementIdValue", 0L);
        Scribe_Values.Look(ref visitSiteWorldObjectId, "livingWorld_visitSiteWorldObjectId", -1);
        Scribe_Values.Look(ref materializedVersion, "livingWorld_materializedVersion", 0);
        Scribe_Values.Look(ref lifecycleValue, "livingWorld_materializationLifecycle", 0);
        Scribe_Values.Look(ref purposeKey, "livingWorld_materializationPurposeKey", string.Empty);
        Scribe_Values.Look(ref failureReason, "livingWorld_materializationFailure", string.Empty);
        Scribe_Values.Look(ref rollbackPending, "livingWorld_materializationRollbackPending", false);
        Scribe_Values.Look(ref materializedPopulation, "livingWorld_materializedPopulation", 0);
        Scribe_Values.Look(ref materializedScore, "livingWorld_materializedScore", 0);
        Scribe_Values.Look(ref reconciled, "livingWorld_reconciled", false);
        Scribe_Values.Look(ref reconciledLayersValue, "livingWorld_reconciledLayers", 0);
        Scribe_Collections.Look(ref resources, "livingWorld_trackedResources", LookMode.Deep);
        Scribe_Collections.Look(ref facilityThings, "livingWorld_trackedFacilityThings", LookMode.Deep);
        Scribe_Collections.Look(ref floors, "livingWorld_trackedFloors", LookMode.Deep);
        Scribe_Collections.Look(ref animals, "livingWorld_trackedAnimals", LookMode.Deep);
        Scribe_Collections.Look(ref reconciledFacilityIds, "livingWorld_reconciledFacilityIds", LookMode.Value);
        Scribe_Collections.Look(ref warehouseManifest, "livingWorld_warehouseManifest", LookMode.Deep);
        Scribe_Values.Look(ref lastLiveSyncTick, "livingWorld_lastLiveSyncTick", -1);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            resources ??= new List<LivingWorldTrackedMapResource>();
            facilityThings ??= new List<LivingWorldTrackedMapThing>();
            floors ??= new List<LivingWorldTrackedMapFloor>();
            animals ??= new List<LivingWorldTrackedMapAnimal>();
            reconciledFacilityIds ??= new List<long>();
            warehouseManifest ??= new List<LivingWorldWarehouseManifestEntry>();
            resources.RemoveAll(entry => entry == null || entry.ThingId <= 0 || entry.ReturnOwnerValue <= 0);
            facilityThings.RemoveAll(entry => entry == null || entry.ThingId <= 0 || entry.FacilityIdValue <= 0);
            floors.RemoveAll(entry => entry == null || entry.FacilityIdValue <= 0);
            animals.RemoveAll(entry => entry == null || entry.PawnThingId <= 0 || entry.CohortIdValue <= 0);
            reconciledFacilityIds.RemoveAll(value => value <= 0);
            warehouseManifest.RemoveAll(entry => entry == null || string.IsNullOrWhiteSpace(entry.ResourceKey));

            if (reconciled)
            {
                reconciledLayersValue = (int)LivingWorldMapReconciliationLayer.All;
            }

            // Backward-compatible maps from before the lifecycle field were already materialized.
            if (materializedVersion > 0
                && (Lifecycle is LivingWorldMapMaterializationLifecycle.None
                    or LivingWorldMapMaterializationLifecycle.Materialized)
                && materializedPopulation <= 0)
            {
                var parentFaction = map.ParentFaction;
                var residentCount = parentFaction == null
                    ? 0
                    : map.mapPawns?.AllPawns.Count(pawn =>
                        pawn != null
                        && !pawn.Dead
                        && pawn.RaceProps?.Humanlike == true
                        && pawn.Faction == parentFaction
                        && pawn.Faction != Faction.OfPlayer) ?? 0;
                if (residentCount > 0)
                {
                    lifecycleValue = (int)LivingWorldMapMaterializationLifecycle.Materialized;
                    materializedPopulation = residentCount;
                    materializedScore = Math.Max(residentCount, materializedScore);
                }
                else
                {
                    materializedVersion = 0;
                    materializedPopulation = 0;
                    materializedScore = 0;
                    lifecycleValue = (int)LivingWorldMapMaterializationLifecycle.Failed;
                    failureReason = "Legacy settlement map had no recoverable NPC population and must be rematerialized.";
                    reconciled = false;
                    reconciledLayersValue = (int)LivingWorldMapReconciliationLayer.None;
                }
            }
            else if (materializedVersion > 0 && Lifecycle == LivingWorldMapMaterializationLifecycle.None)
            {
                lifecycleValue = (int)LivingWorldMapMaterializationLifecycle.Materialized;
            }
        }
    }

    public static LivingWorldSettlementVisitMapComponent? For(Map map)
    {
        return map?.GetComponent<LivingWorldSettlementVisitMapComponent>();
    }
}

public sealed class LivingWorldWarehouseManifestEntry : IExposable
{
    private string resourceKey = string.Empty;
    private int targetQuantity;
    private int materializedQuantity;

    public LivingWorldWarehouseManifestEntry()
    {
    }

    public LivingWorldWarehouseManifestEntry(string resourceKey, int targetQuantity, int materializedQuantity)
    {
        this.resourceKey = resourceKey ?? string.Empty;
        this.targetQuantity = Math.Max(0, targetQuantity);
        this.materializedQuantity = Math.Max(0, materializedQuantity);
    }

    public string ResourceKey => resourceKey;
    public int TargetQuantity => targetQuantity;
    public int MaterializedQuantity => materializedQuantity;

    public void SetTargetQuantity(int value) => targetQuantity = Math.Max(0, value);

    public void SetMaterializedQuantity(int value) => materializedQuantity = Math.Max(0, value);

    public void ExposeData()
    {
        Scribe_Values.Look(ref resourceKey, "resourceKey", string.Empty);
        Scribe_Values.Look(ref targetQuantity, "targetQuantity", 0);
        Scribe_Values.Look(ref materializedQuantity, "materializedQuantity", 0);
    }
}

public enum LivingWorldTrackedResourceState
{
    Pending,
    Crediting,
    Credited,
    Removed
}
public sealed class LivingWorldTrackedMapResource : IExposable
{
    private const int CurrentTrackingVersion = 1;

    private int thingId;
    private int returnOwnerKind;
    private long returnOwnerValue;
    private string resourceKey = string.Empty;
    // Retained for backward-compatible migration of pre-state-machine saves.
    private int reservedQuantity;
    private int physicalQuantity;
    private int remainingQuantity;
    private int reconciliationStateValue;
    private int creditOwnerKind;
    private long creditOwnerValue;
    private int creditLedgerQuantityBefore;
    private int creditQuantity;
    private int trackingVersion;

    public LivingWorldTrackedMapResource()
    {
    }

    public LivingWorldTrackedMapResource(
        int thingId,
        int returnOwnerKind,
        long returnOwnerValue,
        string resourceKey,
        int physicalQuantity,
        int remainingQuantity)
    {
        this.thingId = thingId;
        this.returnOwnerKind = returnOwnerKind;
        this.returnOwnerValue = returnOwnerValue;
        this.resourceKey = resourceKey;
        this.physicalQuantity = Math.Max(1, physicalQuantity);
        this.remainingQuantity = Math.Max(0, remainingQuantity);
        reservedQuantity = this.remainingQuantity;
        trackingVersion = CurrentTrackingVersion;
    }

    public int ThingId => thingId;
    public int ReturnOwnerKind => returnOwnerKind;
    public long ReturnOwnerValue => returnOwnerValue;
    public string ResourceKey => resourceKey;
    public int ReservedQuantity => remainingQuantity;
    public int PhysicalQuantity => physicalQuantity;
    public int RemainingQuantity => remainingQuantity;
    public LivingWorldTrackedResourceState ReconciliationState =>
        Enum.IsDefined(typeof(LivingWorldTrackedResourceState), reconciliationStateValue)
            ? (LivingWorldTrackedResourceState)reconciliationStateValue
            : LivingWorldTrackedResourceState.Pending;

    public EntityId ReturnOwnerId => EntityId.Create((EntityKind)returnOwnerKind, returnOwnerValue);
    public EntityId CreditOwnerId => EntityId.Create((EntityKind)creditOwnerKind, creditOwnerValue);
    public int CreditLedgerQuantityBefore => creditLedgerQuantityBefore;
    public int CreditQuantity => creditQuantity;

    public void SynchronizePhysicalQuantity(int currentPhysicalQuantity)
    {
        if (ReconciliationState != LivingWorldTrackedResourceState.Pending)
        {
            return;
        }

        var current = Math.Max(0, currentPhysicalQuantity);
        if (physicalQuantity <= 0)
        {
            physicalQuantity = current;
            if (remainingQuantity < 0)
            {
                // Legacy tracked entries used zero to mean "return the physical stack".
                remainingQuantity = current;
                reservedQuantity = remainingQuantity;
            }

            return;
        }

        if (current < physicalQuantity)
        {
            remainingQuantity = ScaleQuantity(remainingQuantity, current, physicalQuantity);
        }

        physicalQuantity = current;
        reservedQuantity = remainingQuantity;
    }

    public int SplitOff(int sourcePhysicalAfter, int splitPhysicalQuantity)
    {
        if (ReconciliationState != LivingWorldTrackedResourceState.Pending)
        {
            return 0;
        }

        var sourceAfter = Math.Max(0, sourcePhysicalAfter);
        var split = Math.Max(0, splitPhysicalQuantity);
        SynchronizePhysicalQuantity(sourceAfter + split);
        if (split <= 0 || physicalQuantity <= 0)
        {
            physicalQuantity = sourceAfter;
            return 0;
        }

        var transferred = ScaleQuantity(remainingQuantity, split, physicalQuantity);
        remainingQuantity -= transferred;
        physicalQuantity = sourceAfter;
        reservedQuantity = remainingQuantity;
        return transferred;
    }

    public int TransferPhysicalUnits(int movedPhysicalQuantity, int sourcePhysicalBefore, int sourcePhysicalAfter)
    {
        if (ReconciliationState != LivingWorldTrackedResourceState.Pending)
        {
            return 0;
        }

        SynchronizePhysicalQuantity(Math.Max(0, sourcePhysicalBefore));
        var moved = Math.Min(Math.Max(0, movedPhysicalQuantity), physicalQuantity);
        var transferred = physicalQuantity <= 0
            ? 0
            : ScaleQuantity(remainingQuantity, moved, physicalQuantity);
        remainingQuantity -= transferred;
        physicalQuantity = Math.Max(0, sourcePhysicalAfter);
        reservedQuantity = remainingQuantity;
        return transferred;
    }

    public void Absorb(int destinationPhysicalAfter, int transferredQuantity)
    {
        if (ReconciliationState != LivingWorldTrackedResourceState.Pending)
        {
            return;
        }

        remainingQuantity += Math.Max(0, transferredQuantity);
        physicalQuantity = Math.Max(0, destinationPhysicalAfter);
        reservedQuantity = remainingQuantity;
    }

    public void BeginCredit(EntityId ownerId, int ledgerQuantityBefore)
    {
        if (ReconciliationState != LivingWorldTrackedResourceState.Pending)
        {
            return;
        }

        creditOwnerKind = (int)ownerId.Kind;
        creditOwnerValue = ownerId.Value;
        creditLedgerQuantityBefore = Math.Max(0, ledgerQuantityBefore);
        creditQuantity = Math.Max(0, remainingQuantity);
        reconciliationStateValue = (int)LivingWorldTrackedResourceState.Crediting;
    }

    public void MarkCredited()
    {
        reconciliationStateValue = (int)LivingWorldTrackedResourceState.Credited;
    }

    public void MarkRemoved()
    {
        reconciliationStateValue = (int)LivingWorldTrackedResourceState.Removed;
        physicalQuantity = 0;
        remainingQuantity = 0;
        reservedQuantity = 0;
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref thingId, "thingId", 0);
        Scribe_Values.Look(ref returnOwnerKind, "returnOwnerKind", 0);
        Scribe_Values.Look(ref returnOwnerValue, "returnOwnerValue", 0L);
        Scribe_Values.Look(ref resourceKey, "resourceKey", string.Empty);
        Scribe_Values.Look(ref reservedQuantity, "reservedQuantity", 0);
        Scribe_Values.Look(ref physicalQuantity, "physicalQuantity", 0);
        Scribe_Values.Look(ref remainingQuantity, "remainingQuantity", 0);
        Scribe_Values.Look(ref reconciliationStateValue, "reconciliationState", 0);
        Scribe_Values.Look(ref creditOwnerKind, "creditOwnerKind", 0);
        Scribe_Values.Look(ref creditOwnerValue, "creditOwnerValue", 0L);
        Scribe_Values.Look(ref creditLedgerQuantityBefore, "creditLedgerQuantityBefore", 0);
        Scribe_Values.Look(ref creditQuantity, "creditQuantity", 0);
        Scribe_Values.Look(ref trackingVersion, "trackingVersion", 0);

        if (Scribe.mode == LoadSaveMode.PostLoadInit && trackingVersion <= 0)
        {
            remainingQuantity = reservedQuantity > 0 ? reservedQuantity : -1;
            physicalQuantity = 0;
            reconciliationStateValue = (int)LivingWorldTrackedResourceState.Pending;
            creditOwnerKind = 0;
            creditOwnerValue = 0;
            creditLedgerQuantityBefore = 0;
            creditQuantity = 0;
            trackingVersion = CurrentTrackingVersion;
        }
    }

    private static int ScaleQuantity(int quantity, int numerator, int denominator)
    {
        if (quantity <= 0 || numerator <= 0 || denominator <= 0)
        {
            return 0;
        }

        return (int)Math.Min(quantity, ((long)quantity * numerator) / denominator);
    }
}

public sealed class LivingWorldTrackedMapThing : IExposable
{
    private int thingId;
    private long facilityIdValue;

    public LivingWorldTrackedMapThing()
    {
    }

    public LivingWorldTrackedMapThing(int thingId, long facilityIdValue)
    {
        this.thingId = thingId;
        this.facilityIdValue = facilityIdValue;
    }

    public int ThingId => thingId;
    public long FacilityIdValue => facilityIdValue;
    public EntityId FacilityId => EntityId.Create(EntityKind.SettlementFacility, facilityIdValue);

    public void ExposeData()
    {
        Scribe_Values.Look(ref thingId, "thingId", 0);
        Scribe_Values.Look(ref facilityIdValue, "facilityIdValue", 0L);
    }
}

public sealed class LivingWorldTrackedMapFloor : IExposable
{
    private long facilityIdValue;
    private int x;
    private int z;
    private string terrainDefName = string.Empty;

    public LivingWorldTrackedMapFloor()
    {
    }

    public LivingWorldTrackedMapFloor(long facilityIdValue, int x, int z, string terrainDefName)
    {
        this.facilityIdValue = facilityIdValue;
        this.x = x;
        this.z = z;
        this.terrainDefName = terrainDefName;
    }

    public long FacilityIdValue => facilityIdValue;
    public EntityId FacilityId => EntityId.Create(EntityKind.SettlementFacility, facilityIdValue);
    public int X => x;
    public int Z => z;
    public string TerrainDefName => terrainDefName;

    public void ExposeData()
    {
        Scribe_Values.Look(ref facilityIdValue, "facilityIdValue", 0L);
        Scribe_Values.Look(ref x, "x", 0);
        Scribe_Values.Look(ref z, "z", 0);
        Scribe_Values.Look(ref terrainDefName, "terrainDefName", string.Empty);
    }
}

public sealed class LivingWorldTrackedMapAnimal : IExposable
{
    private int pawnThingId;
    private long cohortIdValue;
    private string animalKind = string.Empty;
    private int typeValue;

    public LivingWorldTrackedMapAnimal()
    {
    }

    public LivingWorldTrackedMapAnimal(int pawnThingId, long cohortIdValue, string animalKind, int typeValue)
    {
        this.pawnThingId = pawnThingId;
        this.cohortIdValue = cohortIdValue;
        this.animalKind = animalKind;
        this.typeValue = typeValue;
    }

    public int PawnThingId => pawnThingId;
    public long CohortIdValue => cohortIdValue;
    public EntityId CohortId => EntityId.Create(EntityKind.Animal, cohortIdValue);
    public string AnimalKind => animalKind;
    public AnimalCohortType Type => (AnimalCohortType)typeValue;

    public void ExposeData()
    {
        Scribe_Values.Look(ref pawnThingId, "pawnThingId", 0);
        Scribe_Values.Look(ref cohortIdValue, "cohortIdValue", 0L);
        Scribe_Values.Look(ref animalKind, "animalKind", string.Empty);
        Scribe_Values.Look(ref typeValue, "typeValue", 0);
    }
}
