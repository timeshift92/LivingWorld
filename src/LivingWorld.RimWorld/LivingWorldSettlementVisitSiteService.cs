using System;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Creates or reuses a controlled Living World proxy site for future settlement visits.
/// This deliberately does not enter or generate maps; caravan arrival wiring must call it first.
/// </summary>
public static class LivingWorldSettlementVisitSiteService
{
    public static bool TryCreateOrReuse(
        WorldObject sourceSettlement,
        EntityId settlementId,
        string visitKind,
        out WorldObject_LivingWorldSettlementVisitSite? site,
        out string failureReason)
    {
        site = null;
        failureReason = string.Empty;

        if (sourceSettlement == null)
        {
            failureReason = "LW_SettlementVisitFailure_MissingSource".Translate();
            return false;
        }

        if (settlementId.Kind != EntityKind.Settlement || settlementId.Value <= 0)
        {
            failureReason = "LW_SettlementVisitFailure_InvalidLedgerId".Translate();
            return false;
        }

        var worldObjects = Find.WorldObjects;
        if (worldObjects == null)
        {
            failureReason = "LW_SettlementVisitFailure_WorldUnavailable".Translate();
            return false;
        }

        if (FindExisting(sourceSettlement, settlementId, out site))
        {
            site!.RefreshSource(
                settlementId,
                sourceSettlement.ID,
                sourceSettlement.Tile,
                visitKind,
                sourceSettlement.Label);
            if (sourceSettlement.Faction != null && site.Faction != sourceSettlement.Faction)
            {
                site.SetFaction(sourceSettlement.Faction);
            }

            failureReason = string.Empty;
            return true;
        }

        var def = DefDatabase<WorldObjectDef>.GetNamedSilentFail("LivingWorld_SettlementVisitSite");
        if (def == null)
        {
            failureReason = "LW_SettlementVisitFailure_DefMissing".Translate();
            return false;
        }

        site = (WorldObject_LivingWorldSettlementVisitSite)WorldObjectMaker.MakeWorldObject(def);
        site.Configure(
            settlementId,
            sourceSettlement.ID,
            sourceSettlement.Tile,
            visitKind,
            sourceSettlement.Label);

        if (sourceSettlement.Faction != null)
        {
            site.SetFaction(sourceSettlement.Faction);
        }

        Find.WorldObjects.Add(site);
        return true;
    }

    internal static bool FindExisting(
        WorldObject sourceSettlement,
        EntityId settlementId,
        out WorldObject_LivingWorldSettlementVisitSite? site)
    {
        site = null;
        var worldObjects = Find.WorldObjects;
        if (worldObjects == null || sourceSettlement == null)
        {
            return false;
        }

        site = worldObjects.AllWorldObjects
            .OfType<WorldObject_LivingWorldSettlementVisitSite>()
            .FirstOrDefault(candidate =>
                candidate != null
                && !candidate.Destroyed
                && candidate.SettlementId == settlementId);

        return site != null;
    }

    internal static bool TryResolveSource(
        WorldObject_LivingWorldSettlementVisitSite site,
        out WorldObject source)
    {
        source = null!;
        var worldObjects = Find.WorldObjects?.AllWorldObjects;
        if (site == null || worldObjects == null || site.SourceRemoved)
        {
            return false;
        }

        source = worldObjects.FirstOrDefault(candidate =>
            candidate != null
            && !candidate.Destroyed
            && candidate.ID == site.SourceSettlementWorldObjectId)!;
        return source != null;
    }

    internal static void CloseProxiesForRemovedSource(WorldObject source)
    {
        var sites = Find.WorldObjects?.AllWorldObjects
            .OfType<WorldObject_LivingWorldSettlementVisitSite>()
            .Where(site => site != null && !site.Destroyed && site.SourceSettlementWorldObjectId == source.ID)
            .ToList() ?? new System.Collections.Generic.List<WorldObject_LivingWorldSettlementVisitSite>();
        foreach (var site in sites)
        {
            CloseOrphanedSite(site);
        }
    }

    internal static void CloseOrphanedSite(WorldObject_LivingWorldSettlementVisitSite site)
    {
        if (site == null || site.Destroyed)
        {
            return;
        }

        site.MarkSourceRemoved();
        if (site.HasMap)
        {
            var hasPlayerPawn = site.Map.mapPawns.PawnsInFaction(Faction.OfPlayer)
                .Any(pawn => pawn != null && pawn.Spawned && !pawn.Dead);
            if (!hasPlayerPawn)
            {
                Current.Game?.DeinitAndRemoveMap(site.Map, notifyPlayer: false);
            }

            return;
        }

        site.Destroy();
    }
}
