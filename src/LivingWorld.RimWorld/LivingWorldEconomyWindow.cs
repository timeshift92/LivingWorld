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
    private const float ColTierPct = 0.60f;
    private const float ColWealthPct = 0.74f;

    private readonly List<EconomyRow> cachedRows = new();
    private Vector2 scrollPosition;
    private int cachedAtTick = -CacheRefreshIntervalTicks;
    private int cachedSettlementCount = -1;

    public override Vector2 InitialSize => new Vector2(760f, 560f);

    private readonly struct EconomyRow
    {
        public EconomyRow(string factionId, int settlements, int population, SettlementTier topTier, int wealth, float fill)
        {
            FactionId = factionId;
            Settlements = settlements;
            Population = population;
            TopTier = topTier;
            Wealth = wealth;
            Fill = fill;
        }

        public string FactionId { get; }
        public int Settlements { get; }
        public int Population { get; }
        public SettlementTier TopTier { get; }
        public int Wealth { get; }
        public float Fill { get; }
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
        var xTier = rect.x + (rect.width * ColTierPct);
        var xWealth = rect.x + (rect.width * ColWealthPct);

        if (isHeader)
        {
            Widgets.Label(new Rect(rect.x, rect.y, xSettlements - rect.x, rect.height), "LW_EconomyCol_Faction".Translate());
            Widgets.Label(new Rect(xSettlements, rect.y, xPopulation - xSettlements, rect.height), "LW_EconomyCol_Settlements".Translate());
            Widgets.Label(new Rect(xPopulation, rect.y, xTier - xPopulation, rect.height), "LW_EconomyCol_Population".Translate());
            Widgets.Label(new Rect(xTier, rect.y, xWealth - xTier, rect.height), "LW_EconomyCol_Tier".Translate());
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
        var population = debugExact ? data.Population.ToString() : PopulationBand(data.Population);
        var wealth = debugExact ? data.Wealth.ToString() : WealthBand(data.Wealth);

        Widgets.Label(new Rect(xSettlements, rect.y, xPopulation - xSettlements, rect.height), data.Settlements.ToString());
        Widgets.Label(new Rect(xPopulation, rect.y, xTier - xPopulation, rect.height), population);
        Widgets.Label(new Rect(xTier, rect.y, xWealth - xTier, rect.height), TierLabel(data.TopTier));

        // Wealth cell: comparative faction-coloured bar (share of the richest faction) + value.
        var wealthRect = new Rect(xWealth, rect.y, rect.xMax - xWealth, rect.height);
        if (data.Fill > 0f)
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

        var rows = state.Settlements
            .GroupBy(settlement => settlement.FactionId, System.StringComparer.Ordinal)
            .Select(group =>
            {
                var settlements = group.ToList();
                var population = settlements.Sum(settlement => state.GetSettlementPopulation(settlement.Id).Total);
                var topTier = settlements
                    .Select(settlement => SettlementDevelopmentService.GetTier(state, settlement.Id))
                    .DefaultIfEmpty(SettlementTier.Camp)
                    .Max();
                var wealth = FactionWealth(state, group.Key, settlements);
                return (FactionId: group.Key, Settlements: settlements.Count, Population: population, TopTier: topTier, Wealth: wealth);
            })
            .OrderByDescending(row => row.Wealth)
            .ThenByDescending(row => row.Population)
            .Take(MaxRows)
            .ToList();

        var maxWealth = rows.Count > 0 ? rows.Max(row => row.Wealth) : 0;
        foreach (var row in rows)
        {
            var fill = maxWealth > 0 ? (float)row.Wealth / maxWealth : 0f;
            cachedRows.Add(new EconomyRow(row.FactionId, row.Settlements, row.Population, row.TopTier, row.Wealth, fill));
        }
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
