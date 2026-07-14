using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// A columnar Population/Economy table for the whole NPC world — one row per faction with its
/// settlement count, population, top settlement tier and material wealth, drawn as an aligned grid
/// with faction icons and comparative wealth bars. This is the "at a glance" companion to the
/// scrolling label lists on the main tab (inspired by Economics-and-Demography's population tab).
///
/// It derives bands from live ledger data: population from <c>GetSettlementPopulation</c>, tier from
/// <c>SettlementDevelopmentService.GetTier</c>, and wealth from the faction wealth snapshot when the
/// Core sim has recorded one, otherwise a live material-stock fallback. Exact population and wealth
/// values are shown only while debug logging is enabled. Rows are cached on a tick throttle so the
/// popup never recomputes aggregates every frame.
/// </summary>
public sealed class LivingWorldEconomyWindow : Window
{
    private const int MaxRows = 30;
    private const int CacheRefreshIntervalTicks = 120;
    private const float RowHeight = 26f;
    private const float IconSize = 20f;

    // Column left edges as a fraction of the table width, plus the wealth column that fills the rest.
    private const float ColSettlementsPct = 0.34f;
    private const float ColPopulationPct = 0.46f;
    private const float ColPopTrendPct = 0.55f;
    private const float ColTierPct = 0.63f;
    private const float ColOutputPct = 0.72f;
    private const float ColChangePct = 0.81f;
    private const float ColWealthPct = 0.90f;

    private readonly List<EconomyRow> cachedRows = new();
    private Vector2 scrollPosition;
    private int cachedAtTick = -CacheRefreshIntervalTicks;
    private int cachedSettlementCount = -1;

    public override Vector2 InitialSize => new Vector2(760f, 560f);

    private readonly struct EconomyRow
    {
        public EconomyRow(
            string factionId,
            int settlements,
            int population,
            int dailyPopulationChange,
            SettlementTier topTier,
            int dailyOutputValue,
            int dailyWealthChange,
            int wealth,
            float fill,
            bool exactVisible,
            SettlementPopulationBand populationBand,
            SettlementMigrationKnowledge migrationKnowledge,
            SettlementProductionKnowledge productionKnowledge)
        {
            FactionId = factionId;
            Settlements = settlements;
            Population = population;
            DailyPopulationChange = dailyPopulationChange;
            TopTier = topTier;
            DailyOutputValue = dailyOutputValue;
            DailyWealthChange = dailyWealthChange;
            Wealth = wealth;
            Fill = fill;
            ExactVisible = exactVisible;
            PopulationBand = populationBand;
            MigrationKnowledge = migrationKnowledge;
            ProductionKnowledge = productionKnowledge;
        }

        public string FactionId { get; }
        public int Settlements { get; }
        public int Population { get; }
        public int DailyPopulationChange { get; }
        public SettlementTier TopTier { get; }
        public int DailyOutputValue { get; }
        public int DailyWealthChange { get; }
        public int Wealth { get; }
        public float Fill { get; }
        public bool ExactVisible { get; }
        public SettlementPopulationBand PopulationBand { get; }
        public SettlementMigrationKnowledge MigrationKnowledge { get; }
        public SettlementProductionKnowledge ProductionKnowledge { get; }
    }

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
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "LW_EconomyWindowTitle".Translate());
        Text.Font = GameFont.Small;

        var headerRect = new Rect(inRect.x, inRect.y + 40f, inRect.width, 24f);
        DrawColumns(headerRect, isHeader: true, null, state);
        Widgets.DrawLineHorizontal(inRect.x, headerRect.yMax + 2f, inRect.width);

        RefreshRows(state);

        var bodyRect = new Rect(inRect.x, headerRect.yMax + 6f, inRect.width, inRect.height - (headerRect.yMax + 6f - inRect.y) - 44f);
        var viewRect = new Rect(0f, 0f, bodyRect.width - 16f, cachedRows.Count * RowHeight);
        Widgets.BeginScrollView(bodyRect, ref scrollPosition, viewRect);
        var y = 0f;
        foreach (var row in cachedRows)
        {
            DrawColumns(new Rect(0f, y, viewRect.width, RowHeight - 2f), isHeader: false, row, state);
            y += RowHeight;
        }

        Widgets.EndScrollView();

        var closeRect = new Rect(inRect.center.x - 80f, inRect.yMax - 38f, 160f, 34f);
        if (Widgets.ButtonText(closeRect, "CloseButton".Translate()))
        {
            Close();
        }
    }

    private void DrawColumns(Rect rect, bool isHeader, EconomyRow? row, WorldState state)
    {
        var xSettlements = rect.x + (rect.width * ColSettlementsPct);
        var xPopulation = rect.x + (rect.width * ColPopulationPct);
        var xPopTrend = rect.x + (rect.width * ColPopTrendPct);
        var xTier = rect.x + (rect.width * ColTierPct);
        var xOutput = rect.x + (rect.width * ColOutputPct);
        var xChange = rect.x + (rect.width * ColChangePct);
        var xWealth = rect.x + (rect.width * ColWealthPct);

        if (isHeader)
        {
            Widgets.Label(new Rect(rect.x, rect.y, xSettlements - rect.x, rect.height), "LW_EconomyCol_Faction".Translate());
            Widgets.Label(new Rect(xSettlements, rect.y, xPopulation - xSettlements, rect.height), "LW_EconomyCol_Settlements".Translate());
            Widgets.Label(new Rect(xPopulation, rect.y, xPopTrend - xPopulation, rect.height), "LW_EconomyCol_Population".Translate());
            Widgets.Label(new Rect(xPopTrend, rect.y, xTier - xPopTrend, rect.height), "LW_EconomyCol_PopTrend".Translate());
            Widgets.Label(new Rect(xTier, rect.y, xOutput - xTier, rect.height), "LW_EconomyCol_Tier".Translate());
            Widgets.Label(new Rect(xOutput, rect.y, xChange - xOutput, rect.height), "LW_EconomyCol_Output".Translate());
            Widgets.Label(new Rect(xChange, rect.y, xWealth - xChange, rect.height), "LW_EconomyCol_Change".Translate());
            Widgets.Label(new Rect(xWealth, rect.y, rect.xMax - xWealth, rect.height), "LW_EconomyCol_Wealth".Translate());
            return;
        }

        var data = row!.Value;

        // Faction cell: native icon + colour + name (same treatment as the main-tab lists).
        var faction = Find.FactionManager?.AllFactionsListForReading
            .FirstOrDefault(candidate => candidate.def != null
                && string.Equals(candidate.def.defName, data.FactionId, System.StringComparison.Ordinal));
        var nameX = rect.x;
        if (faction?.def?.FactionIcon != null)
        {
            var iconRect = new Rect(rect.x, rect.y + ((rect.height - IconSize) / 2f), IconSize, IconSize);
            var previous = GUI.color;
            GUI.color = faction.Color;
            GUI.DrawTexture(iconRect, faction.def.FactionIcon);
            GUI.color = previous;
            nameX += IconSize + 4f;
        }

        var factionName = faction?.Name ?? data.FactionId;
        Widgets.Label(new Rect(nameX, rect.y, xSettlements - nameX, rect.height), factionName);
        var debugExact = (LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging;
        var exactVisible = debugExact || data.ExactVisible;
        var population = exactVisible ? data.Population.ToString() : data.PopulationBand.ToString();
        var wealth = exactVisible ? data.Wealth.ToString() : "?";

        Widgets.Label(new Rect(xSettlements, rect.y, xPopulation - xSettlements, rect.height), data.Settlements.ToString());
        Widgets.Label(new Rect(xPopulation, rect.y, xPopTrend - xPopulation, rect.height), population);
        Widgets.Label(new Rect(xPopTrend, rect.y, xTier - xPopTrend, rect.height),
            exactVisible ? FormatSigned(data.DailyPopulationChange) : data.MigrationKnowledge.ToString());
        Widgets.Label(new Rect(xTier, rect.y, xOutput - xTier, rect.height), exactVisible ? TierLabel(data.TopTier) : "?");
        Widgets.Label(new Rect(xOutput, rect.y, xChange - xOutput, rect.height),
            exactVisible ? FormatSigned(data.DailyOutputValue) : data.ProductionKnowledge.ToString());
        Widgets.Label(new Rect(xChange, rect.y, xWealth - xChange, rect.height), exactVisible ? FormatSigned(data.DailyWealthChange) : "?");

        // Wealth cell: comparative faction-coloured bar (share of the richest faction) + value.
        var wealthRect = new Rect(xWealth, rect.y, rect.xMax - xWealth, rect.height);
        if (exactVisible && data.Fill > 0f)
        {
            var barColor = faction != null ? faction.Color : new Color(0.5f, 0.5f, 0.55f);
            barColor.a = 0.3f;
            var previous = GUI.color;
            GUI.color = barColor;
            GUI.DrawTexture(new Rect(wealthRect.x, wealthRect.y, wealthRect.width * Mathf.Clamp01(data.Fill), wealthRect.height), BaseContent.WhiteTex);
            GUI.color = previous;
        }

        Widgets.Label(wealthRect, wealth);
    }

    private void RefreshRows(WorldState state)
    {
        var currentTick = Find.TickManager?.TicksGame ?? 0;
        if (cachedSettlementCount == state.Settlements.Count
            && currentTick - cachedAtTick < CacheRefreshIntervalTicks)
        {
            return;
        }

        cachedAtTick = currentTick;
        cachedSettlementCount = state.Settlements.Count;
        cachedRows.Clear();

        var debugExact = (LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging;
        var visibleSettlements = debugExact ? state.Settlements.Where(settlement => settlement.IsActive) : KnownSettlementsForPlayer(state);
        var knownBySettlement = state.KnownSettlementInfos.ToDictionary(info => info.SettlementId);
        var activeSettlementCounts = state.Settlements
            .Where(settlement => settlement.IsActive)
            .GroupBy(settlement => settlement.FactionId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var rows = visibleSettlements
            .GroupBy(settlement => settlement.FactionId, System.StringComparer.Ordinal)
            .Select(group =>
            {
                var settlements = group.ToList();
                var population = settlements.Sum(settlement => state.GetSettlementPopulation(settlement.Id).Total);
                var dailyPopulationChange = DailyPopulationChange(state, settlements, currentTick);
                var topTier = settlements
                    .Select(settlement => SettlementDevelopmentService.GetTier(state, settlement.Id))
                    .DefaultIfEmpty(SettlementTier.Camp)
                    .Max();
                var dailyOutputValue = settlements.Sum(settlement => DailyOutputValue(state.GetSettlementProductionStatus(settlement.Id)));
                var dailyFoodCost = DailyFoodCost(state, settlements);
                var wealth = FactionWealth(state, group.Key, settlements);
                var known = settlements
                    .Select(settlement => knownBySettlement.TryGetValue(settlement.Id, out var info) ? info : null)
                    .Where(info => info != null)
                    .Cast<KnownSettlementInfo>()
                    .ToList();
                var exactVisible = debugExact
                    || (activeSettlementCounts.TryGetValue(group.Key, out var activeCount)
                        && activeCount == settlements.Count
                        && known.Count == settlements.Count
                        && known.All(info =>
                            info.ExactValuesVisible
                            && !PlayerKnowledgeService.GetFreshness(info, currentTick, 1_800_000).IsStale));
                return (
                    FactionId: group.Key,
                    Settlements: settlements.Count,
                    Population: population,
                    DailyPopulationChange: dailyPopulationChange,
                    TopTier: topTier,
                    DailyOutputValue: dailyOutputValue,
                    DailyWealthChange: dailyOutputValue - dailyFoodCost,
                    Wealth: wealth,
                    ExactVisible: exactVisible,
                    PopulationBand: known.Select(info => info.PopulationBand).DefaultIfEmpty(SettlementPopulationBand.Unknown).Max(),
                    MigrationKnowledge: known.Select(info => info.Migration).DefaultIfEmpty(SettlementMigrationKnowledge.Unknown).Max(),
                    ProductionKnowledge: known.Select(info => info.Production).DefaultIfEmpty(SettlementProductionKnowledge.Unknown).Max());
            })
            .OrderByDescending(row => row.Wealth)
            .ThenByDescending(row => row.Population)
            .Take(MaxRows)
            .ToList();

        var maxWealth = rows.Count > 0 ? rows.Max(row => row.Wealth) : 0;
        foreach (var row in rows)
        {
            var fill = row.ExactVisible && maxWealth > 0 ? (float)row.Wealth / maxWealth : 0f;
            cachedRows.Add(new EconomyRow(
                row.FactionId,
                row.Settlements,
                row.Population,
                row.DailyPopulationChange,
                row.TopTier,
                row.DailyOutputValue,
                row.DailyWealthChange,
                row.Wealth,
                fill,
                row.ExactVisible,
                row.PopulationBand,
                row.MigrationKnowledge,
                row.ProductionKnowledge));
        }
    }

    private static IEnumerable<WorldSettlement> KnownSettlementsForPlayer(WorldState state)
    {
        var knownSettlementIds = new HashSet<EntityId>(state.KnownSettlementInfos.Select(info => info.SettlementId));
        return state.Settlements.Where(settlement => settlement.IsActive && knownSettlementIds.Contains(settlement.Id));
    }

    private static int DailyOutputValue(SettlementProductionStatus production)
    {
        var prices = SettlementWealthService.DefaultPriceBook;
        return (production.FoodPerDay * prices.PriceOf("PackagedSurvivalMeal"))
            + (production.SteelPerDay * prices.PriceOf("Steel"))
            + (production.MedicinePerDay * prices.PriceOf("MedicineIndustrial"))
            + (production.ComponentsPerDay * prices.PriceOf("ComponentIndustrial"));
    }

    private static int DailyPopulationChange(WorldState state, List<WorldSettlement> settlements, int currentTick)
    {
        var effectiveTick = Math.Max(currentTick, state.CurrentTick);
        var cutoff = Math.Max(0, effectiveTick - 60_000);
        var settlementIds = new HashSet<EntityId>(settlements.Select(settlement => settlement.Id));
        var change = 0;
        foreach (var worldEvent in state.Events.Where(worldEvent => worldEvent.Tick >= cutoff))
        {
            if (!worldEvent.SubjectId.HasValue)
            {
                continue;
            }

            var citizen = state.GetCitizen(worldEvent.SubjectId.Value);
            if (citizen == null || !settlementIds.Contains(citizen.SettlementId))
            {
                continue;
            }

            change += worldEvent.Kind switch
            {
                WorldEventKind.CitizenBorn => 1,
                WorldEventKind.MigrationCompleted => 1,
                WorldEventKind.DrifterAssimilated => 1,
                WorldEventKind.CitizenDied => -1,
                WorldEventKind.RefugeeCreated => -1,
                WorldEventKind.RaidPawnCaptured => -1,
                WorldEventKind.RaidPawnMissing => -1,
                _ => 0,
            };
        }

        return change;
    }

    private static int DailyFoodCost(WorldState state, List<WorldSettlement> settlements)
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        if (settings.foodPerCitizen <= 0)
        {
            return 0;
        }

        var mealPrice = SettlementWealthService.DefaultPriceBook.PriceOf("PackagedSurvivalMeal");
        return settlements.Sum(settlement => state.GetSettlementPopulation(settlement.Id).Total) * mealPrice;
    }

    // Prefer the Core wealth snapshot when the economy sim has recorded one; fall back to a live
    // material-stock sum so the table is never empty before that wiring lands.
    private static int FactionWealth(WorldState state, string factionId, List<WorldSettlement> settlements)
    {
        var snapshot = state.GetFactionWealth(factionId);
        if (snapshot != null && snapshot.TotalWealth > 0)
        {
            return snapshot.TotalWealth;
        }

        return settlements.Sum(settlement =>
            state.GetOwnedResourceQuantity(settlement.Id, "PackagedSurvivalMeal")
            + state.GetOwnedResourceQuantity(settlement.Id, "Steel")
            + state.GetOwnedResourceQuantity(settlement.Id, "MedicineIndustrial")
            + state.GetOwnedResourceQuantity(settlement.Id, "ComponentIndustrial"));
    }

    private static string PopulationBand(int population)
    {
        if (population < 8)
        {
            return "LW_PopulationBandTiny".Translate();
        }

        if (population < 20)
        {
            return "LW_PopulationBandSmall".Translate();
        }

        if (population < 60)
        {
            return "LW_PopulationBandMedium".Translate();
        }

        return "LW_PopulationBandLarge".Translate();
    }

    private static string WealthBand(int wealth)
    {
        if (wealth < 1000)
        {
            return "LW_WealthBandPoor".Translate();
        }

        return wealth < 8000
            ? "LW_WealthBandModest".Translate()
            : "LW_WealthBandWealthy".Translate();
    }

    private static string FormatSigned(int value)
    {
        return value switch
        {
            > 0 => "+" + value,
            < 0 => value.ToString(),
            _ => "0",
        };
    }

    private static string TierLabel(SettlementTier tier)
    {
        return tier switch
        {
            SettlementTier.City => "LW_Tier_City".Translate(),
            SettlementTier.Town => "LW_Tier_Town".Translate(),
            SettlementTier.Village => "LW_Tier_Village".Translate(),
            _ => "LW_Tier_Camp".Translate(),
        };
    }
}
