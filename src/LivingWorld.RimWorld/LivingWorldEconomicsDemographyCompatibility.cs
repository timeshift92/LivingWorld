using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LivingWorld.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

internal static class LivingWorldEconomicsDemographyCompatibility
{
    internal const string PackageId = "helldan.economicsdemography";
    private const string ManagerTypeName = "EconomicsDemography.WorldPopulationManager";

    internal static bool IsActive => ModsConfig.IsActive(PackageId);

    internal static bool LivingWorldOwnsSimulation =>
        IsActive
        && LivingWorldWorldComponent.Instance?.IsBootstrapped == true;

    internal static MethodBase? ManagerMethod(string methodName) =>
        AccessTools.Method(ManagerTypeName + ":" + methodName);

    /// <summary>
    /// E&amp;D records every ruin it creates in its private ruinsExpiration dictionary. Odyssey's
    /// DestroyedSettlement may contain a grav-core, so those compatibility ruins must be removed
    /// before their world-object comps tick. Matching by E&amp;D's persisted IDs keeps genuine
    /// vanilla and Living World ruins untouched.
    /// </summary>
    internal static int RemoveOwnedLegacyRuins()
    {
        if (!IsActive || Find.WorldObjects == null)
        {
            return 0;
        }

        try
        {
            var managerType = AccessTools.TypeByName(ManagerTypeName);
            var manager = AccessTools.Field(managerType, "Instance")?.GetValue(null);
            var trackedRuins = AccessTools.Field(managerType, "ruinsExpiration")?.GetValue(manager) as IDictionary;
            if (trackedRuins == null || trackedRuins.Count == 0)
            {
                return 0;
            }

            var ids = trackedRuins.Keys.Cast<object>()
                .OfType<int>()
                .OrderBy(id => id)
                .ToList();
            var removed = 0;
            foreach (var id in ids)
            {
                var ruin = Find.WorldObjects.AllWorldObjects.FirstOrDefault(worldObject =>
                    worldObject.ID == id
                    && worldObject.def == WorldObjectDefOf.DestroyedSettlement);
                if (ruin == null || ruin.Destroyed)
                {
                    continue;
                }

                Find.WorldObjects.Remove(ruin);
                removed++;
            }

            trackedRuins.Clear();
            if (removed > 0)
            {
                Log.Warning($"[LivingWorld] Removed {removed} legacy abandoned settlement(s) created by Economics & Demography before Odyssey grav-core comps could activate.");
            }

            return removed;
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] E&D legacy ruin cleanup failed safely: {ex.GetType().Name}: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// E&amp;D can destroy every physical base and mark its faction defeated when its separate
    /// population pool reaches zero. Living World owns those ledgers, so on legacy saves restore
    /// missing physical bases before the normal destructive reconciliation sees them as ruins.
    /// </summary>
    internal static int RepairMissingPhysicalSettlements(WorldState state)
    {
        if (!IsActive || state == null || Find.WorldObjects == null || Find.WorldGrid == null)
        {
            return 0;
        }

        var factions = Find.FactionManager?.AllFactionsListForReading
            .Where(faction => faction?.def?.humanlikeFaction == true && !faction.IsPlayer && !faction.temporary)
            .ToDictionary(faction => faction.def.defName, StringComparer.Ordinal);
        if (factions == null || factions.Count == 0)
        {
            return 0;
        }

        var physicalKeys = new HashSet<string>(
            Find.WorldObjects.Settlements
                .Where(settlement => settlement?.Faction?.def != null && !settlement.Faction.IsPlayer)
                .Select(settlement => BuildPhysicalKey(settlement)),
            StringComparer.Ordinal);
        var occupiedTiles = new HashSet<int>(Find.WorldObjects.AllWorldObjects.Select(worldObject => (int)worldObject.Tile));
        var candidates = state.Settlements
            .Where(settlement =>
                settlement.IsActive
                && factions.ContainsKey(settlement.FactionId)
                && state.GetSettlementPopulation(settlement.Id).Total > 0
                && !physicalKeys.Contains(settlement.Slug))
            .OrderBy(settlement => settlement.FactionId, StringComparer.Ordinal)
            .ThenBy(settlement => settlement.Id.Value)
            .ToList();
        if (candidates.Count == 0)
        {
            return 0;
        }

        var restored = 0;
        foreach (var ledger in candidates)
        {
            if (!TrySelectRecoveryTile(state.WorldSeed, ledger, occupiedTiles, out var tile))
            {
                Log.Warning($"[LivingWorld] Could not recover a physical tile for E&D-orphaned settlement {ledger.Id} ({ledger.Name}).");
                continue;
            }

            var faction = factions[ledger.FactionId];
            var oldSlug = ledger.Slug;
            var stableKey = LivingWorldSettlementExpansionWorldBridge.BuildStableKey(tile, ledger.FactionId);
            Settlement? physical = null;
            var oldDefeated = faction.defeated;
            var oldHidden = faction.hidden;
            try
            {
                SettlementLifecycleService.RebindPhysicalStableKey(state, ledger.Id, stableKey);
                faction.defeated = false;
                faction.hidden = faction.def.hidden;

                physical = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                physical.Tile = tile;
                physical.SetFaction(faction);
                physical.Name = ledger.Name;
                Find.WorldObjects.Add(physical);

                occupiedTiles.Add(tile);
                physicalKeys.Add(stableKey);
                restored++;
            }
            catch (Exception ex)
            {
                if (physical != null && !physical.Destroyed)
                {
                    Find.WorldObjects.Remove(physical);
                }

                SettlementLifecycleService.RebindPhysicalStableKey(state, ledger.Id, oldSlug);
                faction.defeated = oldDefeated;
                faction.hidden = oldHidden;
                Log.Warning($"[LivingWorld] E&D settlement recovery failed safely for {ledger.Id}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        if (restored > 0)
        {
            Log.Warning($"[LivingWorld] Recovered {restored} physical NPC settlement(s) removed by Economics & Demography; Living World now owns faction lifecycle.");
        }

        return restored;
    }

    private static string BuildPhysicalKey(Settlement settlement) =>
        $"worldobject:{settlement.def?.defName ?? "Settlement"}:{(int)settlement.Tile}:{settlement.Faction?.def?.defName ?? "none"}";

    private static bool TrySelectRecoveryTile(
        int worldSeed,
        WorldSettlement settlement,
        HashSet<int> occupiedTiles,
        out int selectedTile)
    {
        selectedTile = -1;
        var grid = Find.WorldGrid;
        if (grid == null || grid.TilesCount <= 0)
        {
            return false;
        }

        var hash = StableHash($"{worldSeed}|{settlement.FactionId}|{settlement.Id.Value}|{settlement.Slug}");
        var start = (int)(hash % (uint)grid.TilesCount);
        for (var offset = 0; offset < grid.TilesCount; offset++)
        {
            var candidate = (start + offset) % grid.TilesCount;
            if (occupiedTiles.Contains(candidate))
            {
                continue;
            }

            try
            {
                PlanetTile planetTile = candidate;
                var tile = grid[planetTile];
                var biome = tile?.PrimaryBiome;
                if (tile == null || biome == null || tile.WaterCovered || biome.impassable || !biome.canBuildBase)
                {
                    continue;
                }

                if (Find.WorldObjects.AnyWorldObjectAt(planetTile) || !TileFinder.IsValidTileForNewSettlement(planetTile))
                {
                    continue;
                }

                selectedTile = candidate;
                return true;
            }
            catch
            {
                // Modded world layers may reject a candidate. Continue deterministically.
            }
        }

        return false;
    }

    private static uint StableHash(string value)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var character in value)
            {
                hash ^= character;
                hash *= 16777619u;
            }

            return hash;
        }
    }
}

[HarmonyPatch]
internal static class LivingWorldEconomicsDemographyWorldTickPatch
{
    private static bool Prepare() => LivingWorldEconomicsDemographyCompatibility.ManagerMethod("WorldComponentTick") != null;

    private static MethodBase TargetMethod() =>
        LivingWorldEconomicsDemographyCompatibility.ManagerMethod("WorldComponentTick")!;

    private static bool Prefix() => !LivingWorldEconomicsDemographyCompatibility.LivingWorldOwnsSimulation;
}

[HarmonyPatch]
internal static class LivingWorldEconomicsDemographyPopulationMutationPatch
{
    private static bool Prepare() => LivingWorldEconomicsDemographyCompatibility.ManagerMethod("ModifyPopulation") != null;

    private static MethodBase TargetMethod() =>
        LivingWorldEconomicsDemographyCompatibility.ManagerMethod("ModifyPopulation")!;

    private static bool Prefix() => !LivingWorldEconomicsDemographyCompatibility.LivingWorldOwnsSimulation;
}
