using System;
using System.Linq;
using HarmonyLib;
using LivingWorld.Core;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(Settlement), "GetInspectString")]
public static class LivingWorldSettlementInspectPatch
{
    private const int CacheRefreshIntervalTicks = 120;
    private const int KnowledgeStaleAfterTicks = 1_800_000;
    private static string? cachedStableKey;
    private static int cachedAtTick = -CacheRefreshIntervalTicks;
    private static string cachedLine = string.Empty;

    public static void Postfix(Settlement __instance, ref string __result)
    {
        var component = LivingWorldWorldComponent.Instance;
        if (component == null)
        {
            return;
        }

        var stableKey = BuildStableKey(__instance);
        var line = GetCachedInspectLine(component.State, __instance, stableKey);
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        __result = string.IsNullOrWhiteSpace(__result)
            ? line
            : $"{line}\n{__result}";
    }

    private static string GetCachedInspectLine(WorldState state, Settlement worldObject, string stableKey)
    {
        var currentTick = Find.TickManager?.TicksGame ?? 0;
        var visiblePawns = CountVisibleSettlementPawns(worldObject);
        var cacheKey = $"{stableKey}:visible:{visiblePawns}";
        if (cachedStableKey == cacheKey
            && currentTick - cachedAtTick < CacheRefreshIntervalTicks)
        {
            return cachedLine;
        }

        cachedStableKey = cacheKey;
        cachedAtTick = currentTick;
        cachedLine = BuildInspectLine(state, worldObject, stableKey, visiblePawns);
        return cachedLine;
    }

    private static string BuildInspectLine(
        WorldState state,
        Settlement worldObject,
        string stableKey,
        int visiblePawns)
    {
        var settlement = FindSettlementForWorldObject(state, worldObject, stableKey);
        if (settlement == null)
        {
            var missingLine = "LW_InspectPopulationMissingLine".Translate(
                state.Settlements.Count.Named("settlements")).ToString();
            return AppendVisiblePawnsLine(missingLine, visiblePawns);
        }

        var known = state.GetKnownSettlementInfo(settlement.Id);
        if (worldObject.HasMap
            && (known?.ExactValuesVisible != true
                || PlayerKnowledgeService.GetFreshness(
                    known,
                    Find.TickManager?.TicksGame ?? 0,
                    KnowledgeStaleAfterTicks).IsStale))
        {
            known = PlayerKnowledgeService.RecordDirectVisitSettlementInfo(
                state,
                settlement.Id,
                "settlement map is loaded");
        }

        var freshness = known == null
            ? default
            : PlayerKnowledgeService.GetFreshness(
                known,
                Find.TickManager?.TicksGame ?? 0,
                KnowledgeStaleAfterTicks);
        if (known?.ExactValuesVisible == true && freshness.IsStale)
        {
            known = known with { ExactValuesVisible = false };
        }

        var knowledgeLine = known == null
            ? "LW_KnowledgeUnknown".Translate()
            : "LW_KnowledgeLine".Translate(
                known.SourceKind.Named("source"),
                known.Confidence.Named("confidence"),
                known.Tick.Named("tick"),
                freshness.AgeDays.Named("ageDays"),
                freshness.IsStale.Named("stale"),
                known.PopulationBand.Named("populationBand"),
                known.Food.Named("food"),
                known.Migration.Named("migration"),
                known.Production.Named("production"));

        var inspectLine = "LW_InspectPopulationLine".Translate(
            knowledgeLine.Named("knowledge")).ToString();
        var productionLine = known?.ExactValuesVisible == true
            ? FormatProductionLine(state.GetSettlementProductionStatus(settlement.Id))
            : "LW_ProductionHiddenLine".Translate().ToString();
        inspectLine = $"{inspectLine}\n{productionLine}";

        var facilitiesLine = BuildFacilitiesLine(state, settlement.Id, known);
        if (facilitiesLine != null)
        {
            inspectLine = $"{inspectLine}\n{facilitiesLine}";
        }

        var animalsLine = BuildAnimalsLine(state, settlement.Id, known);
        if (animalsLine != null)
        {
            inspectLine = $"{inspectLine}\n{animalsLine}";
        }

        // Threat header: if a world-war army is marching on this settlement it is already visible
        // on the map (the warband marker), so surfacing it here is consistent with fog-of-war and
        // ties the map object to the settlement. Famine/growth stay inside the knowledge line so we
        // never leak exact ledger state the player has not scouted.
        var threatLine = BuildThreatLine(state, settlement.Id);
        if (threatLine != null)
        {
            inspectLine = $"{threatLine}\n{inspectLine}";
        }

        return AppendVisiblePawnsLine(inspectLine, visiblePawns);
    }

    private static string? BuildThreatLine(WorldState state, EntityId settlementId)
    {
        var currentTick = Find.TickManager?.TicksGame ?? 0;
        var soonestArrival = int.MaxValue;
        foreach (var movement in state.ArmyMovements)
        {
            if (movement.Status == ArmyMovementStatus.Traveling
                && movement.TargetSettlementId == settlementId
                && movement.ArrivalTick < soonestArrival)
            {
                soonestArrival = movement.ArrivalTick;
            }
        }

        if (soonestArrival == int.MaxValue)
        {
            return null;
        }

        var days = Math.Max(0, (int)Math.Round((soonestArrival - currentTick) / 60000f));
        return "LW_InspectThreatLine".Translate(days.Named("days")).ToString();
    }

    private static string AppendVisiblePawnsLine(string inspectLine, int visiblePawns)
    {
        if (visiblePawns < 0)
        {
            return inspectLine;
        }

        var visibleLine = "LW_MapVisiblePawnsLine".Translate(
            visiblePawns.Named("visiblePawns")).ToString();
        return $"{inspectLine}\n{visibleLine}";
    }

    private static int CountVisibleSettlementPawns(Settlement worldObject)
    {
        if (!worldObject.HasMap || worldObject.Map?.mapPawns == null)
        {
            return -1;
        }

        var faction = worldObject.Faction;
        return worldObject.Map.mapPawns.AllPawnsSpawned.Count(pawn =>
            pawn?.RaceProps?.Humanlike == true
            && !pawn.Dead
            && (faction == null || pawn.Faction == faction));
    }

    private static string FormatProductionLine(SettlementProductionStatus production)
    {
        return "LW_ProductionLine".Translate(
            production.AdultWorkers.Named("workers"),
            production.FoodPerDay.Named("food"),
            production.SteelPerDay.Named("steel"),
            production.MedicinePerDay.Named("medicine"),
            production.ComponentsPerDay.Named("components"),
            production.Biome.Named("biome"),
            production.Hilliness.Named("hilliness"),
            production.TechLevel.Named("tech")).ToString();
    }

    // Surface the settlement's facilities and any in-flight build/repair project. Fog-of-war: only
    // shown for settlements the player already knows something about (same rule the production line
    // uses). Exact levels/condition/ETA appear only when the player has directly-known intel;
    // otherwise the settlement's development is a coarse band and its project a generic note, so we
    // never leak the exact ledger the player has not scouted.
    private static string? BuildFacilitiesLine(WorldState state, EntityId settlementId, KnownSettlementInfo? known)
    {
        if (known == null)
        {
            return null;
        }

        var facilities = state.GetSettlementFacilities(settlementId);
        SettlementProject? activeProject = null;
        foreach (var project in state.SettlementProjects)
        {
            if (project.SettlementId == settlementId
                && project.Status == SettlementProjectStatus.Active
                && (activeProject == null || project.CompletionTick < activeProject.CompletionTick))
            {
                activeProject = project;
            }
        }

        if (facilities.Count == 0 && activeProject == null)
        {
            return null;
        }

        string line;
        if (known.ExactValuesVisible)
        {
            var facilityText = facilities.Count == 0
                ? "LW_InspectFacilitiesNone".Translate().ToString()
                : string.Join(", ", facilities
                    .OrderBy(facility => facility.Kind)
                    .ThenBy(facility => facility.Id.Value)
                    .Select(facility => "LW_InspectFacilityItem".Translate(
                        FacilityKindLabel(facility.Kind).Named("kind"),
                        facility.Level.Named("level"),
                        facility.ConditionPercent.Named("condition")).ToString()));
            line = "LW_InspectFacilitiesExactLine".Translate(facilityText.Named("facilities")).ToString();
        }
        else
        {
            line = "LW_InspectFacilitiesBandLine".Translate(
                FacilityDevelopmentBand(facilities.Count).Named("band")).ToString();
        }

        if (activeProject != null)
        {
            line = $"{line}\n{FormatProjectLine(activeProject, known.ExactValuesVisible)}";
        }

        return line;
    }

    private static string FormatProjectLine(SettlementProject project, bool exactVisible)
    {
        if (!exactVisible)
        {
            return "LW_InspectFacilityProjectCoarseLine".Translate().ToString();
        }

        var currentTick = Find.TickManager?.TicksGame ?? 0;
        var days = Math.Max(0, (int)Math.Round((project.CompletionTick - currentTick) / 60000f));
        var key = project.Kind == SettlementProjectKind.RepairFacility
            ? "LW_InspectFacilityProjectRepairExact"
            : "LW_InspectFacilityProjectBuildExact";
        return key.Translate(
            FacilityKindLabel(project.FacilityKind).Named("kind"),
            days.Named("days")).ToString();
    }

    private static string FacilityKindLabel(SettlementFacilityKind kind)
    {
        return kind switch
        {
            SettlementFacilityKind.Farm => "LW_FacilityKind_Farm".Translate().ToString(),
            SettlementFacilityKind.Workshop => "LW_FacilityKind_Workshop".Translate().ToString(),
            SettlementFacilityKind.Clinic => "LW_FacilityKind_Clinic".Translate().ToString(),
            SettlementFacilityKind.PowerPlant => "LW_FacilityKind_PowerPlant".Translate().ToString(),
            SettlementFacilityKind.Storage => "LW_FacilityKind_Storage".Translate().ToString(),
            _ => kind.ToString(),
        };
    }

    private static string FacilityDevelopmentBand(int facilityCount)
    {
        if (facilityCount >= 5)
        {
            return "LW_FacilityDevBand_Advanced".Translate().ToString();
        }

        if (facilityCount >= 3)
        {
            return "LW_FacilityDevBand_Developed".Translate().ToString();
        }

        if (facilityCount >= 1)
        {
            return "LW_FacilityDevBand_Basic".Translate().ToString();
        }

        return "LW_FacilityDevBand_None".Translate().ToString();
    }

    // Surface the settlement's animal cohorts (Task 7). Same fog-of-war rule as production/facilities:
    // exact herd sizes only when the player has directly-known intel, otherwise a coarse abundance band.
    private static string? BuildAnimalsLine(WorldState state, EntityId settlementId, KnownSettlementInfo? known)
    {
        if (known == null)
        {
            return null;
        }

        var cohorts = state.AnimalCohorts
            .Where(cohort => cohort.OwnerId == settlementId && cohort.Count > 0)
            .ToList();
        if (cohorts.Count == 0)
        {
            return null;
        }

        if (known.ExactValuesVisible)
        {
            var text = string.Join(", ", cohorts
                .OrderByDescending(cohort => cohort.Count)
                .ThenBy(cohort => cohort.Id.Value)
                .Take(6)
                .Select(cohort => "LW_InspectAnimalItem".Translate(
                    AnimalKindLabel(cohort.AnimalKind).Named("kind"),
                    cohort.Count.Named("count")).ToString()));
            return "LW_InspectAnimalsExactLine".Translate(text.Named("animals")).ToString();
        }

        return "LW_InspectAnimalsBandLine".Translate(
            AnimalAbundanceBand(cohorts.Sum(cohort => cohort.Count)).Named("band")).ToString();
    }

    private static string AnimalKindLabel(string kind)
    {
        if (string.IsNullOrEmpty(kind))
        {
            return kind ?? string.Empty;
        }

        var def = DefDatabase<PawnKindDef>.GetNamedSilentFail(kind);
        return !string.IsNullOrEmpty(def?.label) ? def!.label.CapitalizeFirst() : kind;
    }

    private static string AnimalAbundanceBand(int total)
    {
        if (total >= 120)
        {
            return "LW_AnimalBand_Teeming".Translate();
        }

        if (total >= 40)
        {
            return "LW_AnimalBand_Abundant".Translate();
        }

        if (total >= 10)
        {
            return "LW_AnimalBand_Moderate".Translate();
        }

        return "LW_AnimalBand_Sparse".Translate();
    }

    private static WorldSettlement? FindSettlementForWorldObject(
        WorldState state,
        Settlement worldObject,
        string stableKey)
    {
        var direct = state.Settlements.FirstOrDefault(candidate => candidate.Slug == stableKey);
        if (direct != null)
        {
            return direct;
        }

        var factionId = worldObject.Faction?.def?.defName ?? "UnknownFaction";
        var label = worldObject.LabelCap;
        if (!string.IsNullOrWhiteSpace(label))
        {
            var byNameAndFaction = state.Settlements.FirstOrDefault(candidate =>
                string.Equals(candidate.FactionId, factionId, StringComparison.Ordinal)
                && string.Equals(candidate.Name, label, StringComparison.Ordinal));
            if (byNameAndFaction != null)
            {
                return byNameAndFaction;
            }
        }

        var tileToken = $":{worldObject.Tile}:";
        return state.Settlements.FirstOrDefault(candidate =>
            string.Equals(candidate.FactionId, factionId, StringComparison.Ordinal)
            && candidate.Slug.Contains(tileToken));
    }

    private static string BuildStableKey(WorldObject obj)
    {
        var defName = obj.def?.defName ?? obj.GetType().Name;
        var factionId = obj.Faction?.def?.defName ?? "UnknownFaction";
        return $"worldobject:{defName}:{obj.Tile}:{factionId}";
    }
}
