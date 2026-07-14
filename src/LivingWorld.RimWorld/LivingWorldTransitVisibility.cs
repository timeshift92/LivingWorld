using System;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Applies player knowledge to strategic traffic. Public knowledge that a settlement exists is not
/// enough to reveal every group it dispatches: traffic is visible only when it belongs to the player,
/// is heading for the player, was reported recently by a scout/trader/visit, or is physically close
/// enough to a player world object to be observed.
/// </summary>
internal static class LivingWorldTransitVisibility
{
    private const int TrafficIntelLifetimeTicks = 5 * 60_000;
    private const float PhysicalDetectionRadiusTiles = 6f;

    public static bool IsKnown(
        WorldState state,
        string factionId,
        EntityId originSettlementId,
        EntityId targetSettlementId,
        int originTile,
        int targetTile,
        int departTick,
        int arrivalTick)
    {
        if (state == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(state.PlayerFactionId)
            && string.Equals(state.PlayerFactionId, factionId, StringComparison.Ordinal))
        {
            return true;
        }

        var target = state.GetSettlement(targetSettlementId);
        if (target != null
            && !string.IsNullOrWhiteSpace(state.PlayerFactionId)
            && string.Equals(target.FactionId, state.PlayerFactionId, StringComparison.Ordinal))
        {
            return true;
        }

        if (HasFreshTrafficIntel(state, originSettlementId)
            || HasFreshTrafficIntel(state, targetSettlementId))
        {
            return true;
        }

        return IsPhysicallyObservedOnly(originTile, targetTile, departTick, arrivalTick);
    }

    public static bool IsKnownFromSource(WorldState state, string factionId, EntityId sourceSettlementId)
    {
        if (!string.IsNullOrWhiteSpace(state.PlayerFactionId)
            && string.Equals(state.PlayerFactionId, factionId, StringComparison.Ordinal))
        {
            return true;
        }

        return HasFreshTrafficIntel(state, sourceSettlementId);
    }

    public static bool CanRevealSettlement(WorldState state, EntityId settlementId)
    {
        var settlement = state.GetSettlement(settlementId);
        if (settlement != null
            && !string.IsNullOrWhiteSpace(state.PlayerFactionId)
            && string.Equals(settlement.FactionId, state.PlayerFactionId, StringComparison.Ordinal))
        {
            return true;
        }

        return HasFreshTrafficIntel(state, settlementId);
    }

    private static bool HasFreshTrafficIntel(WorldState state, EntityId settlementId)
    {
        var info = state.GetKnownSettlementInfo(settlementId);
        return info != null
            && info.SourceKind != IntelSourceKind.Public
            && state.CurrentTick - info.Tick >= 0
            && state.CurrentTick - info.Tick <= TrafficIntelLifetimeTicks;
    }

    public static bool IsPhysicallyObservedOnly(int originTile, int targetTile, int departTick, int arrivalTick)
    {
        var grid = Find.WorldGrid;
        var worldObjects = Find.WorldObjects;
        if (grid == null || worldObjects == null || originTile < 0 || targetTile < 0)
        {
            return false;
        }

        var span = Math.Max(1, arrivalTick - departTick);
        var now = Find.TickManager?.TicksGame ?? departTick;
        var progress = Mathf.Clamp01((now - departTick) / (float)span);
        var position = Vector3.Slerp(grid.GetTileCenter(originTile), grid.GetTileCenter(targetTile), progress);
        return worldObjects.AllWorldObjects.Any(worldObject =>
        {
            if (worldObject?.Faction != Faction.OfPlayer || worldObject.Tile < 0)
            {
                return false;
            }

            var detectionRadius = worldObject.Tile.Layer.AverageTileSize * PhysicalDetectionRadiusTiles;
            return Vector3.Distance(position, grid.GetTileCenter(worldObject.Tile)) <= detectionRadius;
        });
    }
}
