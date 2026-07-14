using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LivingWorld.Core;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Persists manifests for neutral groups generated through the compatibility path where world travel is
/// disabled. It deliberately does not depend on LivingWorldWorldComponent: the same conservation contract
/// applies even when presentation and travel markers are ceded to vanilla or another mod.
/// </summary>
public sealed class LivingWorldCompatibilityVisitorComponent : GameComponent
{
    private List<PendingApproachingGroup> manifests = new();

    public LivingWorldCompatibilityVisitorComponent(Game game)
    {
    }

    public static LivingWorldCompatibilityVisitorComponent? Instance =>
        Current.Game?.GetComponent<LivingWorldCompatibilityVisitorComponent>();

    public void Register(PendingApproachingGroup manifest)
    {
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.PurposeKey))
        {
            return;
        }

        manifests.RemoveAll(candidate => string.Equals(candidate.PurposeKey, manifest.PurposeKey, StringComparison.Ordinal));
        manifest.Status = PendingApproachingGroupStatus.Materialized;
        manifest.MaterializedTick = Find.TickManager?.TicksGame ?? 0;
        manifests.Add(manifest);
    }

    public void NotifySpawned(Pawn pawn)
    {
        if (pawn == null || pawn.RaceProps?.Animal != true)
        {
            return;
        }

        var group = FindForPawn(pawn.thingIDNumber);
        var animal = group?.Animals.FirstOrDefault(candidate => candidate.PawnThingId == pawn.thingIDNumber);
        if (animal == null || animal.Resolved)
        {
            return;
        }

        LivingWorldAnimalMapPawnTracker.Track(
            pawn,
            new MaterializedAnimalStack(
                EntityId.Create(EntityKind.Animal, animal.CohortIdValue),
                animal.AnimalKind,
                animal.Type,
                1));
    }

    public bool NotifyReturned(WorldState state, Pawn pawn, string reason)
    {
        var group = pawn == null ? null : FindForPawn(pawn.thingIDNumber);
        if (state == null || pawn == null || group == null
            || group.ResolvedCarrierThingIds.Contains(pawn.thingIDNumber))
        {
            return false;
        }

        LivingWorldVisitorBindingService.ReturnCarrierInventory(state, group, pawn, reason);
        group.ResolvedCarrierThingIds.Add(pawn.thingIDNumber);
        var animal = group.Animals.FirstOrDefault(candidate => candidate.PawnThingId == pawn.thingIDNumber);
        if (animal != null && !animal.Resolved)
        {
            if (!LivingWorldAnimalMapPawnTracker.TryMarkReturned(pawn, reason))
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
                    Find.TickManager?.TicksGame ?? state.CurrentTick,
                    reason);
            }

            animal.Resolved = true;
        }

        PruneResolved();
        return true;
    }

    public bool NotifyLost(Pawn pawn)
    {
        var group = pawn == null ? null : FindForPawn(pawn.thingIDNumber);
        if (pawn == null || group == null
            || group.ResolvedCarrierThingIds.Contains(pawn.thingIDNumber))
        {
            return false;
        }

        // Inventory remains physical on the corpse and is deliberately not credited. Marking the
        // carrier resolved only closes the lease manifest; pawn fate and animal loss are handled by
        // their existing authoritative trackers.
        group.ResolvedCarrierThingIds.Add(pawn.thingIDNumber);
        var animal = group.Animals.FirstOrDefault(candidate => candidate.PawnThingId == pawn.thingIDNumber);
        if (animal != null)
        {
            animal.Resolved = true;
        }

        PruneResolved();
        return true;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref manifests, "livingWorld_compatibilityVisitorManifests", LookMode.Deep);
        manifests ??= new List<PendingApproachingGroup>();
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            manifests.RemoveAll(candidate => candidate == null || string.IsNullOrWhiteSpace(candidate.PurposeKey));
        }
    }

    private PendingApproachingGroup? FindForPawn(int pawnThingId)
    {
        return manifests.FirstOrDefault(group =>
            group.BoundPawnThingIds.Contains(pawnThingId)
            || group.Animals.Any(animal => animal.PawnThingId == pawnThingId));
    }

    private void PruneResolved()
    {
        manifests.RemoveAll(group =>
            group.BoundPawnThingIds.All(group.ResolvedCarrierThingIds.Contains)
            && group.Animals.All(animal => animal.Resolved));
    }
}

[HarmonyPatch]
public static class LivingWorldCompatibilityVisitorSpawnPatch
{
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(
            typeof(GenSpawn),
            nameof(GenSpawn.Spawn),
            new[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(WipeMode) });
        yield return AccessTools.Method(
            typeof(GenSpawn),
            nameof(GenSpawn.Spawn),
            new[]
            {
                typeof(Thing),
                typeof(IntVec3),
                typeof(Map),
                typeof(Rot4),
                typeof(WipeMode),
                typeof(bool),
                typeof(bool)
            });
    }

    public static void Postfix(Thing __result)
    {
        if (__result is Pawn pawn)
        {
            LivingWorldCompatibilityVisitorComponent.Instance?.NotifySpawned(pawn);
        }
    }
}
