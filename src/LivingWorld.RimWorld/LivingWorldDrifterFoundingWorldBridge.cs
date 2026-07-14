using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Binds persisted drifter founding expeditions to real RimWorld tiles, markers and settlements.
/// The ledger reserves founders and supplies at departure; a settlement is created only on arrival.
/// </summary>
internal static class LivingWorldDrifterFoundingWorldBridge
{
    internal const string MarkerKeyPrefix = "drifterfounding:";
    private const int RuntimeTravelTicks = 2 * 60_000;

    public static DrifterFoundingResult Simulate(
        WorldState state,
        DrifterFoundingRequest request,
        IReadOnlyCollection<LivingWorldSettlementExpansionWorldBinding> expansionBindings)
    {
        state.AdvanceToTick(request.Tick);
        var completed = CompleteReadyJourney(state, request.Tick, expansionBindings);
        if (completed != null)
        {
            SyncMarkers(state);
            return completed;
        }

        if (state.DrifterFoundingJourneys.Any(journey =>
            journey.Status == DrifterFoundingJourneyStatus.Traveling))
        {
            SyncMarkers(state);
            return new DrifterFoundingResult(false, "A drifter founding expedition is already traveling.", null, null, 0, false);
        }

        if (state.Drifters.Count(drifter => !state.IsDrifterReserved(drifter.Id)) < Math.Max(1, request.MinFounders))
        {
            SyncMarkers(state);
            return DrifterFoundingService.SimulateFounding(
                state,
                request with { EligibleFactionIds = Array.Empty<string>() });
        }

        var leader = state.Drifters
            .Where(drifter => !state.IsDrifterReserved(drifter.Id))
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
        var factionId = DrifterFoundingService.SelectFaction(state, physicalFactions, leader.LeadsRaiderBand);
        if (string.IsNullOrWhiteSpace(factionId))
        {
            SyncMarkers(state);
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
            SyncMarkers(state);
            return DrifterFoundingService.SimulateFounding(
                state,
                request with { EligibleFactionIds = Array.Empty<string>() });
        }

        var reservedTiles = ReservedTiles(state, expansionBindings, null);
        var locationToken = $"drifter|{leader.Id.Value}|{factionId}|{request.Tick}";
        if (!LivingWorldSettlementExpansionWorldBridge.TrySelectFreeTile(
                locationToken,
                SettlementSlug.ParseTile(sponsor.Slug),
                reservedTiles,
                out var tile))
        {
            SyncMarkers(state);
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
                TravelDurationTicks = RuntimeTravelTicks,
            });
        SyncMarkers(state);
        return result;
    }

    public static void SyncMarkers(WorldState state)
    {
        var worldObjects = Find.WorldObjects;
        if (worldObjects == null)
        {
            return;
        }

        var markerDef = DefDatabase<WorldObjectDef>.GetNamedSilentFail("LivingWorld_ArmyMarker");
        var existing = worldObjects.AllWorldObjects
            .OfType<WorldObject_LivingWorldArmy>()
            .Where(marker => marker.MarkerKey.StartsWith(MarkerKeyPrefix, StringComparison.Ordinal))
            .ToDictionary(marker => marker.MarkerKey, StringComparer.Ordinal);
        var live = new HashSet<string>(StringComparer.Ordinal);
        if (markerDef != null)
        {
            foreach (var journey in state.DrifterFoundingJourneys
                .Where(candidate => candidate.Status == DrifterFoundingJourneyStatus.Traveling)
                .OrderBy(candidate => candidate.Id.Value))
            {
                var originTile = SettlementSlug.ParseTile(state.GetSettlement(journey.SponsorSettlementId)?.Slug);
                var targetTile = SettlementSlug.ParseTile(journey.PhysicalStableKey);
                if (originTile < 0 || targetTile < 0)
                {
                    continue;
                }


                var key = MarkerKeyPrefix + journey.Id.Value;
                live.Add(key);
                var markerIsNew = !existing.TryGetValue(key, out var marker);
                marker ??= (WorldObject_LivingWorldArmy)WorldObjectMaker.MakeWorldObject(markerDef);
                marker.Tile = originTile;
                var faction = ResolveFaction(journey.FactionId);
                if (faction != null)
                {
                    marker.SetFaction(faction);
                }

                marker.Configure(
                    key,
                    "World/LivingWorld_Settler",
                    "LW_MissionKind_Settler".Translate(),
                    originTile,
                    targetTile,
                    journey.CreatedTick,
                    journey.ArrivalTick,
                    faction?.Name ?? journey.FactionId,
                    LivingWorldTransitVisibility.CanRevealSettlement(state, journey.SponsorSettlementId)
                        ? journey.PlannedName
                        : "LW_UnknownDestination".Translate(),
                    journey.FounderDrifterIds.Count,
                    journey.FounderDrifterIds.Count,
                    $"PackagedSurvivalMeal {journey.FoodQuantity}, Steel {journey.SteelQuantity}, ComponentIndustrial {journey.ComponentQuantity}",
                    "LW_MissionReason_DrifterFounding".Translate());
                marker.SetStrategicVisibility(LivingWorldTransitVisibility.IsKnown(
                    state,
                    journey.FactionId,
                    journey.SponsorSettlementId,
                    journey.SponsorSettlementId,
                    originTile,
                    targetTile,
                    journey.CreatedTick,
                    journey.ArrivalTick));
                if (markerIsNew)
                {
                    worldObjects.Add(marker);
                }
            }
        }

        foreach (var pair in existing)
        {
            if (!live.Contains(pair.Key))
            {
                worldObjects.Remove(pair.Value);
            }
        }
    }

    private static DrifterFoundingResult? CompleteReadyJourney(
        WorldState state,
        int tick,
        IReadOnlyCollection<LivingWorldSettlementExpansionWorldBinding> expansionBindings)
    {
        var journey = state.DrifterFoundingJourneys
            .Where(candidate => candidate.Status == DrifterFoundingJourneyStatus.Traveling)
            .Where(candidate => candidate.ArrivalTick <= tick)
            .OrderBy(candidate => candidate.ArrivalTick)
            .ThenBy(candidate => candidate.Id.Value)
            .FirstOrDefault();
        if (journey == null)
        {
            return null;
        }

        var faction = ResolveFaction(journey.FactionId);
        if (faction == null || faction.IsPlayer)
        {
            state.CancelDrifterFoundingJourney(journey.Id, "physical faction unavailable on arrival");
            return new DrifterFoundingResult(false, "The founding faction disappeared before arrival.", null, journey.FactionId, journey.FounderDrifterIds.Count, journey.IsRaiderBand);
        }

        var originTile = SettlementSlug.ParseTile(state.GetSettlement(journey.SponsorSettlementId)?.Slug);
        var targetTile = SettlementSlug.ParseTile(journey.PhysicalStableKey);
        var reservedTiles = ReservedTiles(state, expansionBindings, journey.Id);
        if (!LivingWorldSettlementExpansionWorldBridge.IsFreeFoundingTile(targetTile, originTile, reservedTiles))
        {
            if (!LivingWorldSettlementExpansionWorldBridge.TrySelectFreeTile(
                    $"reroute|{journey.Id.Value}|{journey.PhysicalStableKey}",
                    originTile,
                    reservedTiles,
                    out targetTile))
            {
                state.CancelDrifterFoundingJourney(journey.Id, "no reachable destination remained");
                return new DrifterFoundingResult(false, "No reachable founding destination remained; supplies returned.", null, journey.FactionId, journey.FounderDrifterIds.Count, journey.IsRaiderBand);
            }

            var stableKey = LivingWorldSettlementExpansionWorldBridge.BuildStableKey(targetTile, journey.FactionId);
            journey = state.RebindDrifterFoundingDestination(journey.Id, stableKey);
        }

        Settlement? worldSettlement = null;
        WorldSettlement? settlement = null;
        try
        {
            worldSettlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
            worldSettlement.Tile = targetTile;
            worldSettlement.SetFaction(faction);
            worldSettlement.Name = journey.PlannedName;
            Find.WorldObjects.Add(worldSettlement);

            settlement = state.CompleteDrifterFoundingJourney(journey.Id);
        }
        catch (Exception ex)
        {
            if (worldSettlement != null && !worldSettlement.Destroyed)
            {
                Find.WorldObjects.Remove(worldSettlement);
            }

            if (state.GetDrifterFoundingJourney(journey.Id)?.Status == DrifterFoundingJourneyStatus.Traveling)
            {
                state.CancelDrifterFoundingJourney(journey.Id, $"physical arrival failed: {ex.GetType().Name}");
            }

            Log.Error($"[LivingWorld] Drifter founding journey {journey.Id} failed safely: {ex.GetType().Name}: {ex.Message}");
            return new DrifterFoundingResult(false, "The physical settlement could not be created; founders and supplies were released.", null, journey.FactionId, journey.FounderDrifterIds.Count, journey.IsRaiderBand);
        }

        // The physical object and Core settlement are now committed together. Profile seeding is
        // recoverable metadata: a failure here must not delete the real world object and leave a
        // ledger-only ghost settlement.
        try
        {
            var candidate = new WorldObjectSettlementCandidate(
                journey.PhysicalStableKey,
                worldSettlement!.Name,
                journey.FactionId,
                targetTile,
                worldSettlement.def?.defName ?? "Settlement",
                true);
            state.RecordSettlementProductionProfile(
                RimWorldSettlementProductionProfileFactory.Create(candidate, settlement!.Id, faction));
            SettlementBootstrapPrimer.PrimeSettlement(
                state,
                new SettlementBootstrapPrimerRequest(
                    tick,
                    settlement.Id,
                    "PackagedSurvivalMeal",
                    "Steel",
                    "ComponentIndustrial"));
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Founded settlement {settlement!.Id}, but profile priming will be repaired later: {ex.GetType().Name}: {ex.Message}");
        }

        return new DrifterFoundingResult(
            true,
            $"Founded {journey.PlannedName} after a physical expedition.",
            settlement!.Id,
            journey.FactionId,
            journey.FounderDrifterIds.Count,
            journey.IsRaiderBand)
        {
            JourneyId = journey.Id
        };
    }

    private static HashSet<int> ReservedTiles(
        WorldState state,
        IReadOnlyCollection<LivingWorldSettlementExpansionWorldBinding> expansionBindings,
        EntityId? excludingJourneyId)
    {
        var reserved = new HashSet<int>(expansionBindings
            .Where(binding => !binding.Completed && binding.TargetTile >= 0)
            .Select(binding => binding.TargetTile));
        foreach (var journey in state.DrifterFoundingJourneys
            .Where(candidate => candidate.Status == DrifterFoundingJourneyStatus.Traveling)
            .Where(candidate => !excludingJourneyId.HasValue || candidate.Id != excludingJourneyId.Value))
        {
            var tile = SettlementSlug.ParseTile(journey.PhysicalStableKey);
            if (tile >= 0)
            {
                reserved.Add(tile);
            }
        }

        return reserved;
    }

    private static Faction? ResolveFaction(string factionId)
    {
        return Find.FactionManager?.AllFactionsListForReading
            .FirstOrDefault(candidate => candidate.def?.defName == factionId);
    }
}
