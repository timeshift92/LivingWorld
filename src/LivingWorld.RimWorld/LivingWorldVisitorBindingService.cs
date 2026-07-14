using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

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

    // Compatibility path for explicitly disabled world travel. It still binds every human when the
    // source can support the full group; it never performs a partial identity assignment.
    public static int BindVisitorPawns(WorldState state, string? factionDefName, IReadOnlyList<Pawn>? pawns)
    {
        if (state == null
            || string.IsNullOrEmpty(factionDefName)
            || pawns == null
            || pawns.Count == 0
            || state.IsInitialWorldSeedingActive)
        {
            return 0;
        }

        var bindable = pawns.Where(IsBindableHuman).ToList();
        if (bindable.Count == 0)
        {
            return 0;
        }

        var source = state.Settlements
            .Where(settlement => settlement.IsActive
                && string.Equals(settlement.FactionId, factionDefName, StringComparison.Ordinal))
            .OrderByDescending(settlement => state.GetSettlementPopulation(settlement.Id).Total)
            .ThenBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
        if (source == null)
        {
            return 0;
        }

        var result = MaterializationLeaseService.CreateLeases(
            state,
            new MaterializationLeaseRequest(
                source.Id,
                source.Id,
                MaterializationPurpose.SettlementVisit,
                $"visit:{factionDefName}:{state.CurrentTick}",
                bindable.Count,
                VisitLeaseLifetimeTicks));
        if (result.Status != MaterializationLeaseStatus.Success)
        {
            return 0;
        }

        for (var index = 0; index < bindable.Count; index++)
        {
            var lease = result.Leases[index];
            var pawn = bindable[index];
            var bind = MaterializationLeaseService.BindPawn(state, lease.Id, pawn.thingIDNumber);
            if (bind.Status != MaterializationLeaseBindStatus.Success)
            {
                foreach (var created in result.Leases)
                {
                    if (state.GetMaterializationLease(created.Id)?.IsActive == true)
                    {
                        MaterializationLeaseService.Release(state, created.Id, "immediate neutral group binding failed");
                    }
                }

                return 0;
            }

            StampIdentity(pawn, lease.CitizenId);
        }

        return bindable.Count;
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

            state.AddResource(sourceId, resourceKey!, thing.stackCount);
            state.RecordEvent(
                WorldEventKind.SettlementTradeRecorded,
                sourceId,
                $"Neutral group returned {thing.stackCount} {resourceKey}: {reason}.");
            returned += thing.stackCount;
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
