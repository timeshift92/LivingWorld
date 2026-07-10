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
            failureReason = "missing source settlement";
            return false;
        }

        if (settlementId.Kind != EntityKind.Settlement || settlementId.Value <= 0)
        {
            failureReason = "invalid Living World settlement id";
            return false;
        }

        var worldObjects = Find.WorldObjects;
        if (worldObjects == null)
        {
            failureReason = "world objects are not available";
            return false;
        }

        if (FindExisting(sourceSettlement, settlementId, out site))
        {
            failureReason = string.Empty;
            return true;
        }

        var def = DefDatabase<WorldObjectDef>.GetNamedSilentFail("LivingWorld_SettlementVisitSite");
        if (def == null)
        {
            failureReason = "LivingWorld_SettlementVisitSite def is missing";
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
                && (candidate.SettlementId == settlementId
                    || candidate.SourceSettlementWorldObjectId == sourceSettlement.ID));

        return site != null;
    }
}
