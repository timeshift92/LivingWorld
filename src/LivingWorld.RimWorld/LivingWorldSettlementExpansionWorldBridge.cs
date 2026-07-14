using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

internal sealed class LivingWorldSettlementExpansionWorldBinding : IExposable
{
    public long GroupId;
    public string LocationToken = string.Empty;
    public string FactionId = string.Empty;
    public string PlannedSettlementSlug = string.Empty;
    public string PlannedSettlementName = string.Empty;
    public int OriginTile = -1;
    public int TargetTile = -1;
    public long LedgerSettlementId;
    public int WorldObjectId = -1;
    public string WorldObjectStableKey = string.Empty;
    public bool Completed;

    public string MarkerKey => LivingWorldSettlementExpansionWorldBridge.MarkerKeyPrefix + GroupId;

    public void ExposeData()
    {
        Scribe_Values.Look(ref GroupId, "groupId", 0L);
        Scribe_Values.Look(ref LocationToken, "locationToken", string.Empty);
        Scribe_Values.Look(ref FactionId, "factionId", string.Empty);
        Scribe_Values.Look(ref PlannedSettlementSlug, "plannedSettlementSlug", string.Empty);
        Scribe_Values.Look(ref PlannedSettlementName, "plannedSettlementName", string.Empty);
        Scribe_Values.Look(ref OriginTile, "originTile", -1);
        Scribe_Values.Look(ref TargetTile, "targetTile", -1);
        Scribe_Values.Look(ref LedgerSettlementId, "ledgerSettlementId", 0L);
        Scribe_Values.Look(ref WorldObjectId, "worldObjectId", -1);
        Scribe_Values.Look(ref WorldObjectStableKey, "worldObjectStableKey", string.Empty);
        Scribe_Values.Look(ref Completed, "completed", false);
    }
}

/// <summary>
/// Binds Core settlement expeditions to deterministic RimWorld tiles and world objects. Core owns
/// the population and resource transfer; this bridge owns only the persisted tile reservation,
/// visible travel marker, and the vanilla settlement created after a successful arrival.
/// </summary>
internal static class LivingWorldSettlementExpansionWorldBridge
{
    internal const string MarkerKeyPrefix = "settler:";

    private const int PreferredMinDistance = 5;
    private const int PreferredMaxDistance = 14;
    private const int FallbackMaxDistance = 30;

    public static void Synchronize(
        WorldState state,
        List<LivingWorldSettlementExpansionWorldBinding> bindings)
    {
        if (state == null || bindings == null || Find.WorldObjects == null || Find.WorldGrid == null)
        {
            return;
        }

        try
        {
            RemoveDuplicateBindings(bindings);

            var foundingGroups = state.MigrationGroups
                .Where(group => string.Equals(
                    group.Reason,
                    MigrationService.ReasonSettlementFounding,
                    StringComparison.Ordinal))
                .OrderBy(group => group.Id.Value)
                .ToList();
            var groupsById = foundingGroups.ToDictionary(group => group.Id.Value);

            foreach (var group in foundingGroups)
            {
                SynchronizeGroup(state, bindings, group);
            }

            RemoveOrphanedBindings(bindings, groupsById);
            RemoveStaleMarkers(bindings, groupsById);
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Settlement-expansion world bridge failed safely: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void SynchronizeGroup(
        WorldState state,
        List<LivingWorldSettlementExpansionWorldBinding> bindings,
        WorldMigrationGroup group)
    {
        var faction = ResolveFaction(group.FactionId);
        if (faction == null || faction.IsPlayer)
        {
            RemoveMarker(MarkerKeyPrefix + group.Id.Value);
            bindings.RemoveAll(binding => binding.GroupId == group.Id.Value && !binding.Completed);
            DebugLog($"ignored invalid/player settlement expedition {group.Id} ({group.FactionId}).");
            return;
        }

        if (group.Status == MigrationGroupStatus.Lost)
        {
            RemoveMarker(MarkerKeyPrefix + group.Id.Value);
            bindings.RemoveAll(binding => binding.GroupId == group.Id.Value && !binding.Completed);
            DebugLog($"released lost settlement expedition {group.Id} and its tile reservation.");
            return;
        }

        var binding = bindings.FirstOrDefault(candidate => candidate.GroupId == group.Id.Value);
        if (binding == null)
        {
            if (!TryCreateBinding(state, bindings, group, out binding))
            {
                RemoveMarker(MarkerKeyPrefix + group.Id.Value);
                return;
            }

            bindings.Add(binding);
            DebugLog(
                $"reserved tile {binding.TargetTile} for settlement expedition {group.Id} " +
                $"({binding.LocationToken}) from tile {binding.OriginTile}.");
        }
        else
        {
            RefreshBindingIdentity(binding, group);
        }

        if (group.Status == MigrationGroupStatus.Traveling)
        {
            EnsureTravelMarker(state, group, binding, faction);
            return;
        }

        RemoveMarker(binding.MarkerKey);
        if (group.Status != MigrationGroupStatus.Arrived || binding.Completed)
        {
            return;
        }

        CompleteArrival(state, bindings, group, binding, faction);
    }

    private static bool TryCreateBinding(
        WorldState state,
        IReadOnlyCollection<LivingWorldSettlementExpansionWorldBinding> bindings,
        WorldMigrationGroup group,
        out LivingWorldSettlementExpansionWorldBinding binding)
    {
        binding = null!;
        if (!TryResolveOriginTile(state, bindings, group.SourceSettlementId, group.FactionId, out var originTile))
        {
            Log.Warning(
                $"[LivingWorld] Settlement expedition {group.Id} has no resolvable non-player origin tile; " +
                "no marker or tile reservation was created.");
            return false;
        }

        var reservedTiles = new HashSet<int>(bindings
            .Where(candidate => !candidate.Completed && candidate.TargetTile >= 0)
            .Select(candidate => candidate.TargetTile));
        if (!TrySelectFreeTile(group.PlannedLocationToken, originTile, reservedTiles, out var targetTile))
        {
            Log.Warning(
                $"[LivingWorld] Settlement expedition {group.Id} could not reserve a reachable free tile " +
                $"from origin {originTile}; it remains ledger-only until a tile becomes available.");
            return false;
        }

        binding = new LivingWorldSettlementExpansionWorldBinding
        {
            GroupId = group.Id.Value,
            LocationToken = group.PlannedLocationToken,
            FactionId = group.FactionId,
            PlannedSettlementSlug = group.PlannedSettlementSlug,
            PlannedSettlementName = group.PlannedSettlementName,
            OriginTile = originTile,
            TargetTile = targetTile,
        };
        state.BindSettlementExpeditionLocation(
            group.Id,
            BuildStableKey(targetTile, group.FactionId));
        return true;
    }

    private static void RefreshBindingIdentity(
        LivingWorldSettlementExpansionWorldBinding binding,
        WorldMigrationGroup group)
    {
        binding.LocationToken = group.PlannedLocationToken;
        binding.FactionId = group.FactionId;
        binding.PlannedSettlementSlug = group.PlannedSettlementSlug;
        binding.PlannedSettlementName = group.PlannedSettlementName;
    }

    private static void EnsureTravelMarker(
        WorldState state,
        WorldMigrationGroup group,
        LivingWorldSettlementExpansionWorldBinding binding,
        Faction faction)
    {
        var worldObjects = Find.WorldObjects;
        var markerDef = DefDatabase<WorldObjectDef>.GetNamedSilentFail("LivingWorld_ArmyMarker");
        if (worldObjects == null || markerDef == null || binding.OriginTile < 0 || binding.TargetTile < 0)
        {
            return;
        }

        var marker = worldObjects.AllWorldObjects
            .OfType<WorldObject_LivingWorldArmy>()
            .FirstOrDefault(candidate => string.Equals(candidate.MarkerKey, binding.MarkerKey, StringComparison.Ordinal));
        var isNew = marker == null;
        marker ??= (WorldObject_LivingWorldArmy)WorldObjectMaker.MakeWorldObject(markerDef);
        marker.Tile = binding.OriginTile;
        marker.SetFaction(faction);

        var settlerCount = state.Citizens.Count(citizen =>
            citizen.Status == CitizenStatus.Migrating && state.GetOwner(citizen.Id) == group.Id);
        var resources = string.Join(
            ", ",
            state.ResourcesForOwner(group.Id)
                .Where(resource => resource.Quantity > 0)
                .OrderBy(resource => resource.ResourceKey, StringComparer.Ordinal)
                .Select(resource => $"{resource.ResourceKey} {resource.Quantity}")
                .ToArray());
        marker.Configure(
            binding.MarkerKey,
            "World/LivingWorld_Settler",
            "LW_MissionKind_Settler".Translate(),
            binding.OriginTile,
            binding.TargetTile,
            group.CreatedTick,
            group.ArrivalTick,
            faction.Name,
            CleanName(group.PlannedSettlementName, group.PlannedSettlementSlug),
            settlerCount,
            settlerCount,
            resources,
            group.Reason);

        if (isNew)
        {
            worldObjects.Add(marker);
            DebugLog(
                $"created settler marker {binding.MarkerKey}: {binding.OriginTile} -> {binding.TargetTile}, " +
                $"arrival {group.ArrivalTick}.");
        }
    }

    private static void CompleteArrival(
        WorldState state,
        List<LivingWorldSettlementExpansionWorldBinding> bindings,
        WorldMigrationGroup group,
        LivingWorldSettlementExpansionWorldBinding binding,
        Faction faction)
    {
        var colony = state.Settlements
            .Where(settlement => string.Equals(settlement.FactionId, group.FactionId, StringComparison.Ordinal))
            .FirstOrDefault(settlement =>
                string.Equals(settlement.Slug, group.PlannedSettlementSlug, StringComparison.Ordinal)
                || (!string.IsNullOrWhiteSpace(group.PhysicalStableKey)
                    && string.Equals(settlement.Slug, group.PhysicalStableKey, StringComparison.Ordinal)));
        if (colony == null || !colony.IsActive)
        {
            Log.Warning(
                $"[LivingWorld] Arrived settlement expedition {group.Id} has no active ledger colony " +
                $"for slug '{group.PlannedSettlementSlug}'; its tile reservation was released.");
            bindings.Remove(binding);
            return;
        }

        var existing = ResolveExistingSettlement(binding);
        if (existing != null)
        {
            FinishLink(state, group, binding, colony, existing, faction);
            return;
        }

        var otherReservations = new HashSet<int>(bindings
            .Where(candidate => candidate.GroupId != binding.GroupId && !candidate.Completed && candidate.TargetTile >= 0)
            .Select(candidate => candidate.TargetTile));
        if (!IsFreeFoundingTile(binding.TargetTile, binding.OriginTile, otherReservations))
        {
            var oldTile = binding.TargetTile;
            if (!TrySelectFreeTile(binding.LocationToken, binding.OriginTile, otherReservations, out var replacementTile))
            {
                Log.Warning(
                    $"[LivingWorld] Arrived settlement expedition {group.Id} found tile {oldTile} occupied " +
                    "and no deterministic replacement was available; creation is deferred.");
                return;
            }

            binding.TargetTile = replacementTile;
            DebugLog($"rerouted arrived settlement expedition {group.Id} from occupied tile {oldTile} to {replacementTile}.");
        }

        // Recheck immediately before construction. A quest/site can occupy the replacement between
        // selection and this point during another component's tick; in that case defer safely.
        if (!IsFreeFoundingTile(binding.TargetTile, binding.OriginTile, otherReservations))
        {
            Log.Warning(
                $"[LivingWorld] Settlement expedition {group.Id} target tile {binding.TargetTile} became occupied " +
                "during arrival; vanilla settlement creation was deferred.");
            return;
        }

        try
        {
            var worldSettlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
            worldSettlement.Tile = binding.TargetTile;
            worldSettlement.SetFaction(faction);
            worldSettlement.Name = CleanName(colony.Name, group.PlannedSettlementName);
            var stableKey = BuildStableKey(binding.TargetTile, faction.def?.defName ?? group.FactionId);
            SettlementLifecycleService.BindPhysicalLocation(
                state,
                colony.Id,
                stableKey,
                Find.TickManager?.TicksGame ?? state.CurrentTick,
                "settler expedition reached its physical destination");
            Find.WorldObjects.Add(worldSettlement);
            FinishLink(state, group, binding, colony, worldSettlement, faction);
            DebugLog(
                $"created vanilla settlement {worldSettlement.ID} for ledger settlement {colony.Id} " +
                $"from expedition {group.Id} at tile {binding.TargetTile}.");
        }
        catch (Exception ex)
        {
            Log.Warning(
                $"[LivingWorld] Failed to create vanilla settlement for expedition {group.Id} at tile " +
                $"{binding.TargetTile}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void FinishLink(
        WorldState state,
        WorldMigrationGroup group,
        LivingWorldSettlementExpansionWorldBinding binding,
        WorldSettlement colony,
        Settlement worldSettlement,
        Faction faction)
    {
        binding.Completed = true;
        binding.LedgerSettlementId = colony.Id.Value;
        binding.WorldObjectId = worldSettlement.ID;
        binding.TargetTile = worldSettlement.Tile;
        binding.WorldObjectStableKey = BuildStableKey(worldSettlement.Tile, faction.def?.defName ?? group.FactionId);

        colony = SettlementLifecycleService.BindPhysicalLocation(
            state,
            colony.Id,
            binding.WorldObjectStableKey,
            Find.TickManager?.TicksGame ?? state.CurrentTick,
            "settler expedition linked to vanilla settlement");

        // Replace only environmental production fields with the actual destination tile while
        // retaining the colony's inherited economic character.
        var currentProfile = state.GetSettlementProductionProfile(colony.Id);
        var candidate = new WorldObjectSettlementCandidate(
            binding.WorldObjectStableKey,
            worldSettlement.Name,
            group.FactionId,
            worldSettlement.Tile,
            worldSettlement.def?.defName ?? "Settlement",
            true);
        var actualProfile = RimWorldSettlementProductionProfileFactory.Create(candidate, colony.Id, faction);
        if (currentProfile != null)
        {
            actualProfile = actualProfile with
            {
                Archetype = currentProfile.Archetype,
                LaborEfficiencyPercent = currentProfile.LaborEfficiencyPercent,
                EconomyScalePercent = currentProfile.EconomyScalePercent,
                ComplexityPenaltyPercent = currentProfile.ComplexityPenaltyPercent,
            };
        }

        state.RecordSettlementProductionProfile(actualProfile);
    }

    private static Settlement? ResolveExistingSettlement(LivingWorldSettlementExpansionWorldBinding binding)
    {
        if (binding.WorldObjectId < 0 || Find.WorldObjects == null)
        {
            return null;
        }

        return Find.WorldObjects.AllWorldObjects
            .OfType<Settlement>()
            .FirstOrDefault(candidate => candidate.ID == binding.WorldObjectId && !candidate.Destroyed);
    }

    private static bool TryResolveOriginTile(
        WorldState state,
        IReadOnlyCollection<LivingWorldSettlementExpansionWorldBinding> bindings,
        EntityId sourceSettlementId,
        string factionId,
        out int originTile)
    {
        var source = state.GetSettlement(sourceSettlementId);
        originTile = ParseSettlementTile(source?.Slug);
        if (originTile >= 0)
        {
            return true;
        }

        var linked = bindings.FirstOrDefault(candidate =>
            candidate.Completed && candidate.LedgerSettlementId == sourceSettlementId.Value);
        if (linked != null && linked.TargetTile >= 0)
        {
            originTile = linked.TargetTile;
            return true;
        }

        var factionSettlement = Find.WorldObjects?.Settlements
            .Where(settlement => settlement.Faction?.def?.defName == factionId && !settlement.Faction.IsPlayer)
            .OrderBy(settlement => (int)settlement.Tile)
            .FirstOrDefault();
        if (factionSettlement == null)
        {
            return false;
        }

        originTile = factionSettlement.Tile;
        return originTile >= 0;
    }

    internal static bool TrySelectFreeTile(
        string locationToken,
        int originTile,
        HashSet<int> reservedTiles,
        out int selectedTile)
    {
        selectedTile = -1;
        var grid = Find.WorldGrid;
        if (grid == null || originTile < 0 || grid.TilesCount <= 0)
        {
            return false;
        }

        var start = (int)(StableHash(locationToken) % (uint)grid.TilesCount);
        if (TryScanDistanceBand(start, originTile, PreferredMinDistance, PreferredMaxDistance, reservedTiles, out selectedTile))
        {
            return true;
        }

        return TryScanDistanceBand(start, originTile, 1, FallbackMaxDistance, reservedTiles, out selectedTile);
    }

    private static bool TryScanDistanceBand(
        int start,
        int originTile,
        int minDistance,
        int maxDistance,
        HashSet<int> reservedTiles,
        out int selectedTile)
    {
        selectedTile = -1;
        var grid = Find.WorldGrid;
        if (grid == null)
        {
            return false;
        }

        for (var offset = 0; offset < grid.TilesCount; offset++)
        {
            var candidate = (start + offset) % grid.TilesCount;
            var distance = grid.ApproxDistanceInTiles(originTile, candidate);
            if (distance < minDistance || distance > maxDistance)
            {
                continue;
            }

            if (IsFreeFoundingTile(candidate, originTile, reservedTiles))
            {
                selectedTile = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool IsFreeFoundingTile(int candidate, int originTile, HashSet<int> reservedTiles)
    {
        var grid = Find.WorldGrid;
        var worldObjects = Find.WorldObjects;
        if (grid == null || worldObjects == null || candidate < 0 || candidate == originTile || reservedTiles.Contains(candidate))
        {
            return false;
        }

        try
        {
            PlanetTile candidateTile = candidate;
            PlanetTile originPlanetTile = originTile;
            if (candidateTile.Layer != originPlanetTile.Layer)
            {
                return false;
            }

            var tile = grid[candidateTile];
            var biome = tile?.PrimaryBiome;
            if (tile == null || biome == null || tile.WaterCovered || biome.impassable || !biome.canBuildBase)
            {
                return false;
            }

            if (worldObjects.AnyWorldObjectAt(candidateTile)
                || !TileFinder.IsValidTileForNewSettlement(candidateTile)
                || Find.WorldReachability?.CanReach(originPlanetTile, candidateTile) != true)
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void RemoveOrphanedBindings(
        List<LivingWorldSettlementExpansionWorldBinding> bindings,
        IReadOnlyDictionary<long, WorldMigrationGroup> groupsById)
    {
        for (var index = bindings.Count - 1; index >= 0; index--)
        {
            var binding = bindings[index];
            if (binding.Completed || groupsById.ContainsKey(binding.GroupId))
            {
                continue;
            }

            RemoveMarker(binding.MarkerKey);
            bindings.RemoveAt(index);
            DebugLog($"removed orphaned settlement-expedition reservation {binding.GroupId}.");
        }
    }

    private static void RemoveStaleMarkers(
        IReadOnlyCollection<LivingWorldSettlementExpansionWorldBinding> bindings,
        IReadOnlyDictionary<long, WorldMigrationGroup> groupsById)
    {
        var worldObjects = Find.WorldObjects;
        if (worldObjects == null)
        {
            return;
        }

        var liveKeys = new HashSet<string>(
            bindings
                .Where(binding =>
                    !binding.Completed
                    && groupsById.TryGetValue(binding.GroupId, out var group)
                    && group.Status == MigrationGroupStatus.Traveling)
                .Select(binding => binding.MarkerKey),
            StringComparer.Ordinal);
        var stale = worldObjects.AllWorldObjects
            .OfType<WorldObject_LivingWorldArmy>()
            .Where(marker => marker.MarkerKey.StartsWith(MarkerKeyPrefix, StringComparison.Ordinal))
            .Where(marker => !liveKeys.Contains(marker.MarkerKey))
            .ToList();
        foreach (var marker in stale)
        {
            worldObjects.Remove(marker);
        }
    }

    private static void RemoveDuplicateBindings(List<LivingWorldSettlementExpansionWorldBinding> bindings)
    {
        var seen = new HashSet<long>();
        for (var index = bindings.Count - 1; index >= 0; index--)
        {
            var binding = bindings[index];
            if (binding == null || binding.GroupId <= 0 || !seen.Add(binding.GroupId))
            {
                bindings.RemoveAt(index);
            }
        }
    }

    private static void RemoveMarker(string markerKey)
    {
        var worldObjects = Find.WorldObjects;
        var marker = worldObjects?.AllWorldObjects
            .OfType<WorldObject_LivingWorldArmy>()
            .FirstOrDefault(candidate => string.Equals(candidate.MarkerKey, markerKey, StringComparison.Ordinal));
        if (worldObjects != null && marker != null)
        {
            worldObjects.Remove(marker);
        }
    }

    private static Faction? ResolveFaction(string factionId)
    {
        return Find.FactionManager?.AllFactionsListForReading
            .FirstOrDefault(candidate => candidate.def?.defName == factionId);
    }

    private static int ParseSettlementTile(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return -1;
        }

        var parts = slug!.Split(':');
        return parts.Length >= 3 && int.TryParse(parts[2], out var tile) ? tile : -1;
    }

    internal static string BuildStableKey(int tile, string factionDefName)
    {
        return $"worldobject:Settlement:{tile}:{factionDefName}";
    }

    private static string CleanName(string? primary, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(primary))
        {
            return primary!.Trim();
        }

        return string.IsNullOrWhiteSpace(fallback) ? "Living World settlement" : fallback!.Trim();
    }

    private static uint StableHash(string? value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in value ?? string.Empty)
            {
                hash ^= character;
                hash *= 16777619u;
            }

            hash ^= hash >> 13;
            hash *= 2246822519u;
            hash ^= hash >> 15;
            return hash;
        }
    }

    private static void DebugLog(string message)
    {
        if ((LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging)
        {
            Log.Message("[LivingWorld] Settlement expansion bridge: " + message);
        }
    }

}
