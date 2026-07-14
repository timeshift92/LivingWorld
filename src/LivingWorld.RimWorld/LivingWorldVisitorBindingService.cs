using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

public enum VisitorCompatibilityBindStatus
{
    NotHandled,
    Success,
    InvalidRequest,
    NoSourceSettlement,
    ReservationFailed,
    BindingFailed
}

public sealed record VisitorCompatibilityBindResult(
    VisitorCompatibilityBindStatus Status,
    string Reason,
    int BoundHumans,
    int MaterializedAnimals,
    int MaterializedCargo,
    PendingApproachingGroup? Manifest)
{
    public bool IsSuccess => Status == VisitorCompatibilityBindStatus.Success;
    public bool IsHandled => Status != VisitorCompatibilityBindStatus.NotHandled;
}

/// <summary>
/// Materializes an already-reserved neutral group. Vanilla may choose pawn kinds and apparel, but it
/// cannot increase the number of people, animals or recoverable inventory beyond the persisted ledger
/// manifest prepared at departure.
/// </summary>
public static class LivingWorldVisitorBindingService
{
    private const int VisitLeaseLifetimeTicks = 60_000 * 5;

    public static bool BindReservedVisitorPawns(
        WorldState state,
        PendingApproachingGroup group,
        List<Pawn> generatedPawns)
    {
        if (state == null || group == null || generatedPawns == null)
        {
            return false;
        }

        var leases = group.LeaseIdValues
            .Select(value => state.GetMaterializationLease(EntityId.Create(EntityKind.MaterializationLease, value)))
            .Where(lease => lease is { IsActive: true })
            .Select(lease => lease!)
            .OrderBy(lease => lease.Id.Value)
            .ToList();
        if (leases.Count == 0)
        {
            DestroyGenerated(generatedPawns);
            return false;
        }

        var humans = generatedPawns
            .Where(IsBindableHuman)
            .ToList();
        TrimPawns(generatedPawns, humans.Skip(leases.Count));
        humans = humans.Take(leases.Count).ToList();
        if (humans.Count == 0)
        {
            DestroyGenerated(generatedPawns);
            return false;
        }

        group.BoundPawnThingIds.Clear();
        var boundLeaseValues = new List<long>();
        for (var index = 0; index < humans.Count; index++)
        {
            var pawn = humans[index];
            var lease = leases[index];
            var bind = MaterializationLeaseService.BindPawn(state, lease.Id, pawn.thingIDNumber);
            if (bind.Status != MaterializationLeaseBindStatus.Success)
            {
                TrimPawns(generatedPawns, new[] { pawn });
                continue;
            }

            StampIdentity(pawn, lease.CitizenId);
            group.BoundPawnThingIds.Add(pawn.thingIDNumber);
            boundLeaseValues.Add(lease.Id.Value);
        }

        foreach (var unused in leases.Where(lease => !boundLeaseValues.Contains(lease.Id.Value)))
        {
            MaterializationLeaseService.Release(state, unused.Id, "neutral group generated fewer citizens than reserved");
        }

        group.LeaseIdValues = boundLeaseValues;
        if (group.BoundPawnThingIds.Count == 0)
        {
            DestroyGenerated(generatedPawns);
            return false;
        }

        ReplaceAnimalsWithReservedPayload(state, group, generatedPawns);
        ReplaceGeneratedInventoryWithReservedCargo(state, group, generatedPawns);
        ApproachingGroupRuntime.RecordGeneratedPawns(generatedPawns);
        return true;
    }

    // Compatibility wrapper retained for API consumers. Callers that can veto generation should use
    // BindVisitorPayload and honor its fail-close result.
    public static int BindVisitorPawns(WorldState state, string? factionDefName, IReadOnlyList<Pawn>? pawns)
    {
        return pawns is List<Pawn> mutable
            ? BindVisitorPayload(state, factionDefName, mutable).BoundHumans
            : 0;
    }

    public static VisitorCompatibilityBindResult BindVisitorPayload(
        WorldState state,
        string? factionDefName,
        List<Pawn>? generatedPawns)
    {
        if (state == null
            || string.IsNullOrEmpty(factionDefName)
            || generatedPawns == null
            || generatedPawns.Count == 0)
        {
            return CompatibilityResult(
                VisitorCompatibilityBindStatus.InvalidRequest,
                "Compatibility visitor binding requires state, faction and generated pawns.");
        }

        if (state.IsInitialWorldSeedingActive)
        {
            return CompatibilityResult(
                VisitorCompatibilityBindStatus.NotHandled,
                "Initial world seeding remains owned by RimWorld.");
        }

        var bindable = generatedPawns.Where(IsBindableHuman).ToList();
        if (bindable.Count == 0)
        {
            return CompatibilityResult(
                VisitorCompatibilityBindStatus.InvalidRequest,
                "Generated neutral group has no bindable humans.");
        }

        var source = state.Settlements
            .Where(settlement => settlement.IsActive
                && string.Equals(settlement.FactionId, factionDefName, StringComparison.Ordinal))
            .OrderByDescending(settlement => state.GetSettlementPopulation(settlement.Id).Total)
            .ThenBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
        if (source == null)
        {
            return CompatibilityResult(
                VisitorCompatibilityBindStatus.NoSourceSettlement,
                $"No active Living World settlement owns faction {factionDefName}.");
        }

        var purposeKey = $"compat-visit:{factionDefName}:{state.CurrentTick}:{bindable[0].thingIDNumber}";
        var requestedResources = generatedPawns
            .Where(pawn => pawn?.inventory?.innerContainer != null)
            .SelectMany(pawn => pawn.inventory.innerContainer.InnerListForReading)
            .Where(thing => thing != null && !thing.Destroyed)
            .Select(thing => new
            {
                ResourceKey = thing.GetInnerIfMinified()?.def?.defName,
                Quantity = Math.Max(0, thing.stackCount)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.ResourceKey) && item.Quantity > 0)
            .GroupBy(item => item.ResourceKey!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity), StringComparer.Ordinal);
        var requestedAnimals = generatedPawns.Count(pawn => pawn?.RaceProps?.Animal == true && !pawn.Dead);
        var reservation = TravelingGroupReservationService.Reserve(
            state,
            new TravelingGroupReservationRequest(
                source.Id,
                MaterializationPurpose.SettlementVisit,
                purposeKey,
                bindable.Count,
                VisitLeaseLifetimeTicks,
                requestedResources,
                requestedAnimals,
                state.CurrentTick));
        if (reservation.Status != TravelingGroupReservationStatus.Success
            || !reservation.ResourceOwnerId.HasValue)
        {
            return CompatibilityResult(
                VisitorCompatibilityBindStatus.ReservationFailed,
                reservation.Reason);
        }

        var manifest = CreateCompatibilityManifest(
            source,
            factionDefName!,
            purposeKey,
            reservation,
            generatedPawns.Count);
        try
        {
            if (!BindReservedVisitorPawns(state, manifest, generatedPawns)
                || manifest.BoundPawnThingIds.Count != bindable.Count)
            {
                RollbackFailedArrival(
                    state,
                    manifest,
                    generatedPawns,
                    state.CurrentTick,
                    "compatibility neutral group binding failed");
                return CompatibilityResult(
                    VisitorCompatibilityBindStatus.BindingFailed,
                    "Generated neutral group could not be bound completely to its reserved manifest.");
            }

            return new VisitorCompatibilityBindResult(
                VisitorCompatibilityBindStatus.Success,
                "Compatibility neutral group is fully ledger-backed.",
                manifest.BoundPawnThingIds.Count,
                manifest.Animals.Count(animal => animal.PawnThingId > 0 && !animal.Resolved),
                manifest.Cargo.Sum(cargo => cargo.MaterializedQuantity),
                manifest);
        }
        catch (Exception error)
        {
            RollbackFailedArrival(
                state,
                manifest,
                generatedPawns,
                state.CurrentTick,
                "compatibility neutral group binding threw");
            return CompatibilityResult(
                VisitorCompatibilityBindStatus.BindingFailed,
                $"Compatibility neutral group binding failed: {error.GetType().Name}: {error.Message}");
        }
    }

    public static void RollbackFailedArrival(
        WorldState state,
        PendingApproachingGroup group,
        IReadOnlyList<Pawn> generatedPawns,
        int tick,
        string reason)
    {
        foreach (var cargo in group.Cargo)
        {
            if (cargo.MaterializedQuantity > 0)
            {
                state.AddResource(
                    EntityId.Create(EntityKind.Settlement, group.SourceSettlementIdValue),
                    cargo.ResourceKey,
                    cargo.MaterializedQuantity);
                cargo.MaterializedQuantity = 0;
            }
        }

        DestroyGenerated(generatedPawns);
        TravelingGroupReservationService.Rollback(
            state,
            group.PurposeKey,
            group.Animals
                .Where(animal => !animal.Resolved)
                .GroupBy(animal => new { animal.CohortIdValue, animal.AnimalKind, animal.Type })
                .Select(grouping => new MaterializedAnimalStack(
                    EntityId.Create(EntityKind.Animal, grouping.Key.CohortIdValue),
                    grouping.Key.AnimalKind,
                    grouping.Key.Type,
                    grouping.Count()))
                .ToList(),
            tick,
            reason);
    }

    public static int ReturnCarrierInventory(
        WorldState state,
        PendingApproachingGroup group,
        Pawn pawn,
        string reason)
    {
        if (state == null || group == null || pawn?.inventory?.innerContainer == null)
        {
            return 0;
        }

        var supported = new HashSet<string>(group.Cargo.Select(cargo => cargo.ResourceKey), StringComparer.Ordinal);
        var sourceId = EntityId.Create(EntityKind.Settlement, group.SourceSettlementIdValue);
        var returned = 0;
        foreach (var thing in pawn.inventory.innerContainer.ToList())
        {
            var resourceKey = thing?.GetInnerIfMinified()?.def?.defName;
            if (thing == null || string.IsNullOrWhiteSpace(resourceKey) || !supported.Contains(resourceKey!))
            {
                continue;
            }

            var quantity = thing.stackCount;
            var ledgerBefore = state.GetOwnedResourceQuantity(sourceId, resourceKey!);
            pawn.inventory.innerContainer.Remove(thing);
            var credited = false;
            try
            {
                state.AddResource(sourceId, resourceKey!, quantity);
                credited = state.GetOwnedResourceQuantity(sourceId, resourceKey!) == ledgerBefore + quantity;
                if (!credited)
                {
                    throw new InvalidOperationException(
                        $"Neutral group inventory credit mismatch for {resourceKey}: {ledgerBefore} + {quantity}.");
                }

                if (!thing.Destroyed)
                {
                    thing.Destroy(DestroyMode.Vanish);
                }
            }
            catch (Exception error)
            {
                var current = state.GetOwnedResourceQuantity(sourceId, resourceKey!);
                credited = current == ledgerBefore + quantity;
                if (credited)
                {
                    if (!thing.Destroyed)
                    {
                        try
                        {
                            thing.Destroy(DestroyMode.Vanish);
                        }
                        catch (Exception destroyError)
                        {
                            Log.Warning(
                                $"[LivingWorld] Credited visitor cargo could not be destroyed: "
                                + $"{destroyError.GetType().Name}: {destroyError.Message}");
                        }
                    }
                }
                else if (!thing.Destroyed)
                {
                    pawn.inventory.innerContainer.TryAdd(thing, false);
                }

                Log.Warning(
                    $"[LivingWorld] Visitor cargo return recovered safely: "
                    + $"{error.GetType().Name}: {error.Message}");
            }

            if (!credited)
            {
                continue;
            }

            foreach (var cargo in group.Cargo.Where(candidate =>
                         string.Equals(candidate.ResourceKey, resourceKey, StringComparison.Ordinal)))
            {
                var applied = Math.Min(quantity, Math.Max(0, cargo.MaterializedQuantity));
                cargo.MaterializedQuantity -= applied;
                break;
            }

            try
            {
                state.RecordEvent(
                    WorldEventKind.SettlementTradeRecorded,
                    sourceId,
                    $"Neutral group returned {quantity} {resourceKey}: {reason}.");
            }
            catch (Exception eventError)
            {
                // Resource transfer already committed and the physical stack is gone. Event logging
                // is diagnostic and must not make the caller retry the economic mutation.
                Log.Warning(
                    $"[LivingWorld] Visitor cargo return event was skipped: "
                    + $"{eventError.GetType().Name}: {eventError.Message}");
            }

            returned += quantity;
        }

        return returned;
    }

    private static void ReplaceAnimalsWithReservedPayload(
        WorldState state,
        PendingApproachingGroup group,
        List<Pawn> generatedPawns)
    {
        TrimPawns(generatedPawns, generatedPawns.Where(pawn => pawn?.RaceProps?.Animal == true).ToList());

        var faction = generatedPawns.FirstOrDefault(pawn => pawn?.Faction != null)?.Faction;
        foreach (var animal in group.Animals)
        {
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(animal.AnimalKind);
            if (kind == null)
            {
                ReturnReservedAnimal(state, animal, group, "animal kind unavailable during neutral group arrival");
                continue;
            }

            try
            {
                var pawn = PawnGenerator.GeneratePawn(kind, faction, null);
                animal.PawnThingId = pawn.thingIDNumber;
                generatedPawns.Add(pawn);
            }
            catch
            {
                ReturnReservedAnimal(state, animal, group, "animal generation failed during neutral group arrival");
            }
        }
    }

    private static void ReplaceGeneratedInventoryWithReservedCargo(
        WorldState state,
        PendingApproachingGroup group,
        List<Pawn> generatedPawns)
    {
        var carriers = generatedPawns.Where(pawn => pawn?.inventory?.innerContainer != null).ToList();
        foreach (var carrier in carriers)
        {
            carrier.inventory.innerContainer.ClearAndDestroyContents(DestroyMode.Vanish);
        }

        if (group.ResourceOwnerLeaseIdValue <= 0 || carriers.Count == 0)
        {
            ReturnUnmaterializedCargo(state, group);
            group.CargoCommitted = true;
            return;
        }

        var ownerId = EntityId.Create(EntityKind.MaterializationLease, group.ResourceOwnerLeaseIdValue);
        foreach (var cargo in group.Cargo.OrderBy(cargo => cargo.ResourceKey, StringComparer.Ordinal))
        {
            var thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(cargo.ResourceKey);
            var available = state.GetOwnedResourceQuantity(ownerId, cargo.ResourceKey);
            if (thingDef == null || available <= 0)
            {
                continue;
            }

            var remaining = Math.Min(cargo.ReservedQuantity, available);
            while (remaining > 0)
            {
                var stack = ThingMaker.MakeThing(thingDef);
                stack.stackCount = Math.Min(remaining, Math.Max(1, thingDef.stackLimit));
                var added = false;
                foreach (var carrier in carriers)
                {
                    if (carrier.inventory.innerContainer.TryAdd(stack, false))
                    {
                        added = true;
                        break;
                    }
                }

                if (!added)
                {
                    stack.Destroy(DestroyMode.Vanish);
                    break;
                }

                var consumed = state.ConsumeResource(
                    ownerId,
                    cargo.ResourceKey,
                    stack.stackCount,
                    "neutral group cargo materialized into pawn inventory");
                if (consumed != stack.stackCount)
                {
                    throw new InvalidOperationException(
                        $"Neutral group cargo ledger mismatch for {cargo.ResourceKey}: {consumed}/{stack.stackCount}.");
                }

                cargo.MaterializedQuantity += stack.stackCount;
                remaining -= stack.stackCount;
            }
        }

        ReturnUnmaterializedCargo(state, group);
        group.CargoCommitted = true;
    }

    private static void ReturnUnmaterializedCargo(WorldState state, PendingApproachingGroup group)
    {
        if (group.ResourceOwnerLeaseIdValue <= 0)
        {
            return;
        }

        var ownerId = EntityId.Create(EntityKind.MaterializationLease, group.ResourceOwnerLeaseIdValue);
        var sourceId = EntityId.Create(EntityKind.Settlement, group.SourceSettlementIdValue);
        foreach (var resource in state.ResourcesForOwner(ownerId).Where(resource => resource.Quantity > 0))
        {
            var transfer = state.TransferResource(
                ownerId,
                sourceId,
                resource.ResourceKey,
                resource.Quantity,
                "neutral group cargo could not be physically materialized");
            if (transfer.Status != OwnershipTransferStatus.Success)
            {
                throw new InvalidOperationException(transfer.Reason);
            }
        }
    }

    private static void ReturnReservedAnimal(
        WorldState state,
        PendingApproachingGroupAnimal animal,
        PendingApproachingGroup group,
        string reason)
    {
        AnimalMapMaterializationService.ReturnToCohorts(
            state,
            new[]
            {
                new MaterializedAnimalStack(
                    EntityId.Create(EntityKind.Animal, animal.CohortIdValue),
                    animal.AnimalKind,
                    animal.Type,
                    1)
            },
            state.CurrentTick,
            reason);
        animal.Resolved = true;
    }

    private static bool IsBindableHuman(Pawn pawn)
    {
        return pawn != null
            && !pawn.Dead
            && pawn.RaceProps?.Humanlike == true
            && pawn.GetComp<CompLivingWorldIdentity>()?.HasLedgerId != true;
    }

    private static PendingApproachingGroup CreateCompatibilityManifest(
        WorldSettlement source,
        string factionDefName,
        string purposeKey,
        TravelingGroupReservationResult reservation,
        int generatedPawnCount)
    {
        var manifest = new PendingApproachingGroup
        {
            FactionDefName = factionDefName,
            SourceSettlementIdValue = source.Id.Value,
            PurposeKey = purposeKey,
            LeaseIdValues = reservation.CitizenLeases.Select(lease => lease.Id.Value).ToList(),
            ResourceOwnerLeaseIdValue = reservation.ResourceOwnerId?.Value ?? 0L,
            PawnCount = generatedPawnCount,
            Status = PendingApproachingGroupStatus.Materializing
        };
        manifest.Cargo.AddRange(reservation.Resources.Select(resource => new PendingApproachingGroupCargo
        {
            ResourceKey = resource.ResourceKey,
            ReservedQuantity = resource.Quantity
        }));
        foreach (var animal in reservation.Animals)
        {
            for (var index = 0; index < animal.Count; index++)
            {
                manifest.Animals.Add(new PendingApproachingGroupAnimal
                {
                    CohortIdValue = animal.CohortId.Value,
                    AnimalKind = animal.AnimalKind,
                    Type = animal.Type
                });
            }
        }

        return manifest;
    }

    private static VisitorCompatibilityBindResult CompatibilityResult(
        VisitorCompatibilityBindStatus status,
        string reason)
    {
        return new VisitorCompatibilityBindResult(status, reason, 0, 0, 0, null);
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

    private static void TrimPawns(List<Pawn> generatedPawns, IEnumerable<Pawn> removals)
    {
        foreach (var pawn in removals.Where(pawn => pawn != null).Distinct().ToList())
        {
            generatedPawns.Remove(pawn);
            if (!pawn.Destroyed)
            {
                pawn.Destroy(DestroyMode.Vanish);
            }
        }
    }

    private static void DestroyGenerated(IEnumerable<Pawn> pawns)
    {
        foreach (var pawn in pawns?.Where(pawn => pawn != null).Distinct().ToList() ?? new List<Pawn>())
        {
            if (!pawn.Destroyed)
            {
                pawn.Destroy(DestroyMode.Vanish);
            }
        }
    }
}
