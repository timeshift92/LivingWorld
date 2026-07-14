using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using LivingWorld.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Keeps RimWorld's settlement world objects aligned with the Living World ledger after
/// simulations capture or destroy an NPC settlement. Ledger slugs retain their original faction,
/// so identity is resolved by the embedded world tile and ownership comes from FactionId.
/// </summary>
[HarmonyPatch(typeof(LivingWorldWorldComponent), nameof(LivingWorldWorldComponent.WorldComponentTick))]
public static class LivingWorldSettlementFactionBridge
{
    private const int SyncIntervalTicks = 250;
    private const string WorldObjectSlugPrefix = "worldobject:";
    private static readonly HashSet<string> LoggedSkipKeys = new(StringComparer.Ordinal);

    [HarmonyPostfix]
    public static void Postfix(LivingWorldWorldComponent __instance)
    {
        var ticksGame = Find.TickManager?.TicksGame ?? -1;
        if (ticksGame < 0
            || ticksGame % SyncIntervalTicks != 0
            || !__instance.IsBootstrapped)
        {
            return;
        }

        Synchronize(__instance.State);
    }

    private static void Synchronize(WorldState state)
    {
        var worldObjects = Find.WorldObjects;
        var factionManager = Find.FactionManager;
        if (worldObjects == null || factionManager == null)
        {
            return;
        }

        var ledgerByTile = IndexLedgerSettlementsByTile(state.Settlements);
        if (ledgerByTile.Count == 0)
        {
            return;
        }

        var factionsByDefName = factionManager.AllFactionsListForReading
            .Where(faction => faction?.def?.defName != null)
            .GroupBy(faction => faction.def.defName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        // Snapshot the list because destroying a settlement mutates WorldObjectsHolder.
        foreach (var vanillaSettlement in worldObjects.Settlements.ToList())
        {
            if (vanillaSettlement == null
                || vanillaSettlement.Destroyed
                || vanillaSettlement.Faction?.IsPlayer == true
                || !ledgerByTile.TryGetValue(vanillaSettlement.Tile, out var ledgerSettlements))
            {
                continue;
            }

            try
            {
                SynchronizeSettlement(vanillaSettlement, ledgerSettlements, factionsByDefName);
            }
            catch (Exception ex)
            {
                DebugLogOnce(
                    $"exception:{vanillaSettlement.Tile}:{ex.GetType().FullName}",
                    $"settlement bridge skipped tile {vanillaSettlement.Tile}: "
                    + $"{ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static void SynchronizeSettlement(
        Settlement vanillaSettlement,
        IReadOnlyList<WorldSettlement> ledgerSettlements,
        IReadOnlyDictionary<string, Faction> factionsByDefName)
    {
        var activeSettlement = ledgerSettlements
            .Where(settlement => settlement.IsActive)
            .OrderByDescending(settlement => settlement.Id.Value)
            .FirstOrDefault();

        if (activeSettlement == null)
        {
            if (vanillaSettlement.HasMap)
            {
                DebugLogOnce(
                    $"active-map:{vanillaSettlement.ID}",
                    $"deferred removal of inactive settlement at tile {vanillaSettlement.Tile}: map is active");
                return;
            }

            var label = vanillaSettlement.LabelCap.ToString();
            var tile = vanillaSettlement.Tile;
            vanillaSettlement.Destroy();
            DebugLog($"removed inactive settlement '{label}' at tile {tile}");
            return;
        }

        if (!factionsByDefName.TryGetValue(activeSettlement.FactionId, out var targetFaction)
            || targetFaction.IsPlayer)
        {
            DebugLogOnce(
                $"unknown-faction:{activeSettlement.Id}:{activeSettlement.FactionId}",
                $"could not resolve non-player faction '{activeSettlement.FactionId}' "
                + $"for settlement {activeSettlement.Id} at tile {vanillaSettlement.Tile}");
            return;
        }

        if (ReferenceEquals(vanillaSettlement.Faction, targetFaction))
        {
            return;
        }

        var previousFaction = vanillaSettlement.Faction?.def?.defName ?? "none";
        var targetFactionName = targetFaction.def?.defName ?? activeSettlement.FactionId;
        vanillaSettlement.SetFaction(targetFaction);
        DebugLog(
            $"changed settlement at tile {vanillaSettlement.Tile} from {previousFaction} "
            + $"to {targetFactionName}");
    }

    private static Dictionary<int, List<WorldSettlement>> IndexLedgerSettlementsByTile(
        IEnumerable<WorldSettlement> settlements)
    {
        var result = new Dictionary<int, List<WorldSettlement>>();
        foreach (var settlement in settlements)
        {
            if (!TryParseTile(settlement.Slug, out var tile))
            {
                continue;
            }

            if (!result.TryGetValue(tile, out var atTile))
            {
                atTile = new List<WorldSettlement>();
                result.Add(tile, atTile);
            }

            atTile.Add(settlement);
        }

        return result;
    }

    private static bool TryParseTile(string? slug, out int tile)
    {
        tile = -1;
        if (string.IsNullOrWhiteSpace(slug))
        {
            return false;
        }

        var value = slug!;
        if (!value.StartsWith(WorldObjectSlugPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = value.Split(':');
        return parts.Length >= 4
            && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out tile)
            && tile >= 0;
    }

    private static void DebugLog(string message)
    {
        if ((LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging)
        {
            Log.Message($"[LivingWorld] {message}");
        }
    }

    private static void DebugLogOnce(string key, string message)
    {
        if ((LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging
            && LoggedSkipKeys.Add(key))
        {
            Log.Message($"[LivingWorld] {message}");
        }
    }
}
