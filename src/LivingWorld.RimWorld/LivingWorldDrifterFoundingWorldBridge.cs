using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Commits drifter founding as one physical operation: a real sponsor, real supplies, a reachable
/// reserved tile, a ledger settlement keyed to that tile, and a vanilla world settlement.
/// </summary>
internal static class LivingWorldDrifterFoundingWorldBridge
{
    public static DrifterFoundingResult Simulate(
        WorldState state,
        DrifterFoundingRequest request,
        IReadOnlyCollection<LivingWorldSettlementExpansionWorldBinding> expansionBindings)
    {
        if (state.Drifters.Count < Math.Max(1, request.MinFounders))
        {
            return DrifterFoundingService.SimulateFounding(
                state,
                request with { EligibleFactionIds = Array.Empty<string>() });
        }

        var leader = state.Drifters
            .OrderByDescending(drifter => drifter.LeadershipAptitude)
            .ThenBy(drifter => drifter.Id.Value)
            .First();
        var physicalFactions = Find.WorldObjects?.Settlements
            .Where(settlement => settlement.Faction != null && !settlement.Faction.IsPlayer)
            .Where(settlement => settlement.Tile >= 0 && !settlement.Destroyed)
            .Select(settlement => settlement.Faction.def?.defName)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();
        var factionId = DrifterFoundingService.SelectFaction(
            state,
            physicalFactions,
            leader.LeadsRaiderBand);
        if (string.IsNullOrWhiteSpace(factionId))
        {
            return DrifterFoundingService.SimulateFounding(
                state,
                request with { EligibleFactionIds = physicalFactions });
        }

        var sponsor = state.Settlements
            .Where(settlement => settlement.IsActive)
            .Where(settlement => string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal))
            .Where(settlement => SettlementSlug.ParseTile(settlement.Slug) >= 0)
            .OrderByDescending(settlement => state.GetOwnedResourceQuantity(settlement.Id, "PackagedSurvivalMeal"))
            .ThenByDescending(settlement => state.GetOwnedResourceQuantity(settlement.Id, "Steel"))
            .ThenBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
        if (sponsor == null)
        {
            return DrifterFoundingService.SimulateFounding(
                state,
                request with { EligibleFactionIds = Array.Empty<string>() });
        }

        var reservedTiles = new HashSet<int>(expansionBindings
            .Where(binding => !binding.Completed && binding.TargetTile >= 0)
            .Select(binding => binding.TargetTile));
        var locationToken = $"drifter|{leader.Id.Value}|{factionId}|{request.Tick}";
        if (!LivingWorldSettlementExpansionWorldBridge.TrySelectFreeTile(
                locationToken,
                SettlementSlug.ParseTile(sponsor.Slug),
                reservedTiles,
                out var tile))
        {
            return DrifterFoundingService.SimulateFounding(
                state,
                request with { EligibleFactionIds = Array.Empty<string>() });
        }

        var stableKey = LivingWorldSettlementExpansionWorldBridge.BuildStableKey(tile, factionId!);
        var result = DrifterFoundingService.SimulateFounding(
            state,
            request with
            {
                EligibleFactionIds = physicalFactions,
                PreferredFactionId = factionId,
                PhysicalStableKey = stableKey,
                SponsorSettlementId = sponsor.Id,
            });
        if (!result.Founded || !result.SettlementId.HasValue)
        {
            return result;
        }

        var settlement = state.GetSettlement(result.SettlementId.Value)!;
        var faction = Find.FactionManager?.AllFactionsListForReading
            .FirstOrDefault(candidate => candidate.def?.defName == factionId);
        try
        {
            if (faction == null || faction.IsPlayer)
            {
                throw new InvalidOperationException($"Faction {factionId} is no longer a physical NPC faction.");
            }

            var worldSettlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
            worldSettlement.Tile = tile;
            worldSettlement.SetFaction(faction);
            worldSettlement.Name = settlement.Name;
            var worldObjects = Find.WorldObjects
                ?? throw new InvalidOperationException("RimWorld world-object holder is unavailable.");
            worldObjects.Add(worldSettlement);

            var candidate = new WorldObjectSettlementCandidate(
                stableKey,
                worldSettlement.Name,
                factionId!,
                tile,
                worldSettlement.def?.defName ?? "Settlement",
                true);
            state.RecordSettlementProductionProfile(
                RimWorldSettlementProductionProfileFactory.Create(candidate, settlement.Id, faction));
            SettlementBootstrapPrimer.PrimeSettlement(
                state,
                new SettlementBootstrapPrimerRequest(
                    request.Tick,
                    settlement.Id,
                    "PackagedSurvivalMeal",
                    "Steel",
                    "ComponentIndustrial"));
            return result;
        }
        catch (Exception ex)
        {
            SettlementLifecycleService.DestroySettlement(
                state,
                settlement.Id,
                request.Tick,
                $"physical drifter founding failed: {ex.GetType().Name}");
            Log.Error(
                $"[LivingWorld] Drifter settlement {settlement.Id} could not be materialized at tile {tile}: " +
                $"{ex.GetType().Name}: {ex.Message}");
            return result with
            {
                Founded = false,
                Reason = "The physical RimWorld settlement could not be created; the failed founding was retired.",
            };
        }
    }
}
