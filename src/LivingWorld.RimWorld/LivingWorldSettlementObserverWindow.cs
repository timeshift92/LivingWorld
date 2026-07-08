using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Ledger observer for NPC settlements. This is intentionally not a generated RimWorld map: it lets
/// the player inspect the lightweight world simulation without materializing dozens of pawns/buildings.
/// </summary>
public sealed class LivingWorldSettlementObserverWindow : Window
{
    private const int TicksPerDay = 60_000;
    private const float LeftWidth = 240f;
    private const float RowHeight = 30f;
    private const int MaxSettlementRows = 120;
    private const int MaxEventRows = 12;

    private Vector2 settlementScroll;
    private Vector2 detailScroll;
    private EntityId? selectedSettlementId;

    public override Vector2 InitialSize => new Vector2(920f, 620f);

    public override void DoWindowContents(Rect inRect)
    {
        var component = LivingWorldWorldComponent.Instance;
        if (component == null)
        {
            Widgets.Label(inRect, "LW_WaitingForWorld".Translate());
            return;
        }

        var state = component.State;
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "LW_SettlementObserverTitle".Translate());
        Text.Font = GameFont.Small;

        var contentRect = new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 84f);
        var listRect = new Rect(contentRect.x, contentRect.y, LeftWidth, contentRect.height);
        var detailRect = new Rect(listRect.xMax + 12f, contentRect.y, contentRect.width - LeftWidth - 12f, contentRect.height);

        DrawSettlementList(listRect, state);
        DrawSettlementDetail(detailRect, state);

        var closeRect = new Rect(inRect.center.x - 80f, inRect.yMax - 38f, 160f, 34f);
        if (Widgets.ButtonText(closeRect, "CloseButton".Translate()))
        {
            Close();
        }
    }

    private void DrawSettlementList(Rect rect, WorldState state)
    {
        Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f), "LW_SettlementObserver_Settlements".Translate());
        Widgets.DrawLineHorizontal(rect.x, rect.y + 26f, rect.width);

        var settlements = state.Settlements
            .OrderBy(settlement => settlement.Name, StringComparer.Ordinal)
            .ThenBy(settlement => settlement.Id.Value)
            .Take(MaxSettlementRows)
            .ToList();

        selectedSettlementId ??= settlements.FirstOrDefault()?.Id;

        var scrollRect = new Rect(rect.x, rect.y + 32f, rect.width, rect.height - 32f);
        var viewRect = new Rect(0f, 0f, rect.width - 16f, settlements.Count * RowHeight);
        Widgets.BeginScrollView(scrollRect, ref settlementScroll, viewRect);

        var y = 0f;
        foreach (var settlement in settlements)
        {
            var row = new Rect(0f, y, viewRect.width, RowHeight - 2f);
            if (selectedSettlementId == settlement.Id)
            {
                Widgets.DrawHighlightSelected(row);
            }

            if (Widgets.ButtonText(row, settlement.Name))
            {
                selectedSettlementId = settlement.Id;
            }

            y += RowHeight;
        }

        Widgets.EndScrollView();
    }

    private void DrawSettlementDetail(Rect rect, WorldState state)
    {
        var settlement = selectedSettlementId.HasValue
            ? state.GetSettlement(selectedSettlementId.Value)
            : null;
        if (settlement == null)
        {
            Widgets.Label(rect, "LW_SettlementObserver_NoSettlement".Translate());
            return;
        }

        var lines = BuildDetailLines(state, settlement, Find.TickManager?.TicksGame ?? state.CurrentTick);
        var viewRect = new Rect(0f, 0f, rect.width - 16f, Math.Max(rect.height, lines.Sum(line => line.Height)));
        Widgets.BeginScrollView(rect, ref detailScroll, viewRect);

        var y = 0f;
        foreach (var line in lines)
        {
            if (line.IsHeader)
            {
                Text.Font = GameFont.Medium;
            }

            Widgets.Label(new Rect(0f, y, viewRect.width, line.Height), line.Text);
            Text.Font = GameFont.Small;
            y += line.Height;
        }

        Widgets.EndScrollView();
    }

    private static List<DetailLine> BuildDetailLines(WorldState state, WorldSettlement settlement, int currentTick)
    {
        var lines = new List<DetailLine>();
        var population = state.GetSettlementPopulation(settlement.Id);
        var production = state.GetSettlementProductionStatus(settlement.Id);
        var tier = SettlementDevelopmentService.GetTier(state, settlement.Id);
        var food = SettlementQueryService.GetFoodStatus(state, settlement.Id, "PackagedSurvivalMeal", 1);
        var migration = SettlementQueryService.GetMigrationStatus(state, settlement.Id, "PackagedSurvivalMeal", 1);
        var wealth = state.GetSettlementWealth(settlement.Id)?.TotalWealth ?? 0;

        lines.Add(Header($"{settlement.Name} [{settlement.FactionId}]"));
        lines.Add(Line("LW_SettlementObserver_Overview".Translate(
            settlement.Status.Named("status"),
            tier.Named("tier"),
            population.Total.Named("total"),
            population.Adults.Named("adults"),
            population.Children.Named("children"),
            population.Elderly.Named("elderly"),
            wealth.Named("wealth")).ToString()));
        lines.Add(Line("LW_SettlementObserver_Production".Translate(
            production.AdultWorkers.Named("workers"),
            production.FoodPerDay.Named("food"),
            production.SteelPerDay.Named("steel"),
            production.MedicinePerDay.Named("medicine"),
            production.ComponentsPerDay.Named("components"),
            production.Biome.Named("biome"),
            production.Hilliness.Named("hilliness"),
            production.TechLevel.Named("tech")).ToString(), 58f));
        lines.Add(Line("LW_SettlementObserver_FoodMigration".Translate(
            food.Food.Named("food"),
            food.DailyNeed.Named("need"),
            food.FoodDays.Named("days"),
            migration.Pressure.Named("pressure"),
            migration.PrimaryReason.Named("reason")).ToString()));

        lines.Add(Header("LW_SettlementObserver_Projects".Translate().ToString()));
        var activeProjects = state.SettlementProjects
            .Where(project => project.SettlementId == settlement.Id && project.Status == SettlementProjectStatus.Active)
            .OrderBy(project => project.CompletionTick)
            .ThenBy(project => project.Id.Value)
            .ToList();
        if (activeProjects.Count == 0)
        {
            lines.Add(Line("LW_SettlementObserver_NoActiveProject".Translate().ToString()));
        }
        else
        {
            foreach (var project in activeProjects)
            {
                lines.Add(Line(FormatProject(project, currentTick)));
            }
        }

        lines.Add(Header("LW_SettlementObserver_Facilities".Translate().ToString()));
        var facilities = state.GetSettlementFacilities(settlement.Id)
            .OrderBy(facility => facility.Kind)
            .ThenBy(facility => facility.Id.Value)
            .ToList();
        lines.AddRange(facilities.Count == 0
            ? new[] { Line("LW_SettlementObserver_NoFacilities".Translate().ToString()) }
            : facilities.Select(facility => Line("LW_SettlementObserver_FacilityLine".Translate(
                facility.Kind.Named("kind"),
                facility.Level.Named("level"),
                facility.ConditionPercent.Named("condition")).ToString())));

        lines.Add(Header("LW_SettlementObserver_Animals".Translate().ToString()));
        var animalCohorts = state.GetAnimalCohorts(settlement.Id)
            .OrderBy(cohort => cohort.Type)
            .ThenBy(cohort => cohort.AnimalKind, StringComparer.Ordinal)
            .ThenBy(cohort => cohort.Id.Value)
            .ToList();
        lines.AddRange(animalCohorts.Count == 0
            ? new[] { Line("LW_SettlementObserver_NoAnimals".Translate().ToString()) }
            : animalCohorts.Select(cohort => Line("LW_SettlementObserver_AnimalLine".Translate(
                cohort.Type.Named("type"),
                cohort.AnimalKind.Named("kind"),
                cohort.Count.Named("count"),
                cohort.HealthPercent.Named("health"),
                cohort.FertilityPercent.Named("fertility"),
                cohort.CarryingCapacity.Named("capacity")).ToString(), 36f)));

        lines.Add(Header("LW_SettlementObserver_Breeding".Translate().ToString()));
        var breedingProjects = state.AnimalBreedingProjects
            .Where(project => project.SettlementId == settlement.Id && project.Status == AnimalBreedingProjectStatus.Active)
            .OrderBy(project => project.CompletionTick)
            .ThenBy(project => project.Id.Value)
            .ToList();
        lines.AddRange(breedingProjects.Count == 0
            ? new[] { Line("LW_SettlementObserver_NoBreedingProject".Translate().ToString()) }
            : breedingProjects.Select(project => Line(FormatBreedingProject(state, project, currentTick), 36f)));

        lines.Add(Header("LW_SettlementObserver_Resources".Translate().ToString()));
        var resources = state.ResourcesForOwner(settlement.Id)
            .OrderByDescending(resource => resource.Quantity)
            .ThenBy(resource => resource.ResourceKey, StringComparer.Ordinal)
            .Take(12)
            .ToList();
        lines.AddRange(resources.Count == 0
            ? new[] { Line("LW_SettlementObserver_NoResources".Translate().ToString()) }
            : resources.Select(resource => Line("LW_SettlementObserver_ResourceLine".Translate(
                resource.ResourceKey.Named("resource"),
                resource.Quantity.Named("quantity")).ToString())));

        lines.Add(Header("LW_SettlementObserver_RecentEvents".Translate().ToString()));
        var relatedSubjectIds = new HashSet<EntityId>(animalCohorts.Select(cohort => cohort.Id))
        {
            settlement.Id
        };
        foreach (var project in state.AnimalBreedingProjects.Where(project => project.SettlementId == settlement.Id))
        {
            relatedSubjectIds.Add(project.Id);
        }

        var recentEvents = state.Events
            .Where(worldEvent =>
                worldEvent.SubjectId.HasValue
                && relatedSubjectIds.Contains(worldEvent.SubjectId.Value)
                && IsSettlementProcessEvent(worldEvent.Kind))
            .OrderByDescending(worldEvent => worldEvent.Tick)
            .ThenByDescending(worldEvent => worldEvent.Id.Value)
            .Take(MaxEventRows)
            .ToList();
        lines.AddRange(recentEvents.Count == 0
            ? new[] { Line("LW_SettlementObserver_NoRecentEvents".Translate().ToString()) }
            : recentEvents.Select(worldEvent => Line("LW_SettlementObserver_EventLine".Translate(
                worldEvent.Tick.Named("tick"),
                worldEvent.Kind.Named("kind"),
                worldEvent.Summary.Named("summary")).ToString(), 36f)));

        return lines;
    }

    private static string FormatProject(SettlementProject project, int currentTick)
    {
        var duration = Math.Max(1, project.CompletionTick - project.StartedTick);
        var elapsed = Math.Max(0, currentTick - project.StartedTick);
        var progress = Math.Min(100, elapsed * 100 / duration);
        var daysLeft = Math.Max(0, (int)Math.Ceiling((project.CompletionTick - currentTick) / (double)TicksPerDay));
        return "LW_SettlementObserver_ProjectProgress".Translate(
            project.Kind.Named("kind"),
            project.FacilityKind.Named("facility"),
            progress.Named("progress"),
            daysLeft.Named("days")).ToString();
    }

    private static string FormatBreedingProject(WorldState state, AnimalBreedingProject project, int currentTick)
    {
        var duration = Math.Max(1, project.CompletionTick - project.StartedTick);
        var elapsed = Math.Max(0, currentTick - project.StartedTick);
        var progress = Math.Min(100, elapsed * 100 / duration);
        var daysLeft = Math.Max(0, (int)Math.Ceiling((project.CompletionTick - currentTick) / (double)TicksPerDay));
        var cohort = state.GetAnimalCohort(project.SourceCohortId);
        var animalKind = cohort?.AnimalKind ?? project.SourceCohortId.ToString();
        return "LW_SettlementObserver_BreedingProgress".Translate(
            project.Kind.Named("kind"),
            project.Trait.Named("trait"),
            animalKind.Named("animal"),
            progress.Named("progress"),
            daysLeft.Named("days")).ToString();
    }

    private static bool IsSettlementProcessEvent(WorldEventKind kind)
    {
        return kind == WorldEventKind.SettlementProductionUpdated
            || kind == WorldEventKind.SettlementDeveloped
            || kind == WorldEventKind.SettlementProjectStarted
            || kind == WorldEventKind.SettlementProjectCompleted
            || kind == WorldEventKind.SettlementFacilityBuilt
            || kind == WorldEventKind.SettlementFacilityDamaged
            || kind == WorldEventKind.SettlementFacilityRepaired
            || kind == WorldEventKind.FoodShortage
            || kind == WorldEventKind.CitizenBorn
            || kind == WorldEventKind.MigrationStarted
            || kind == WorldEventKind.MigrationCompleted
            || kind == WorldEventKind.AnimalProductsHarvested
            || kind == WorldEventKind.AnimalHunted
            || kind == WorldEventKind.AnimalBreedingProjectStarted
            || kind == WorldEventKind.AnimalBreedingProjectCompleted
            || kind == WorldEventKind.AnimalCohortIncubated;
    }

    private static DetailLine Header(string text) => new(text, 30f, IsHeader: true);

    private static DetailLine Line(string text, float height = 24f) => new(text, height, IsHeader: false);

    private readonly record struct DetailLine(string Text, float Height, bool IsHeader);
}
