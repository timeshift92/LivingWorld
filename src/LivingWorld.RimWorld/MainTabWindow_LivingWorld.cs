using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace LivingWorld.RimWorld;

public sealed class MainTabWindow_LivingWorld : MainTabWindow
{
    private const int MaxSettlementRows = 40;
    private const int MaxArmyRows = 20;
    private const int MaxOutcomeRows = 12;
    private const int MaxFactionCollapseRows = 8;
    private const int MaxDrifterRows = 12;
    private const int MaxKnowledgeRows = 12;
    private const int MaxEventRows = 20;
    private const int MaxWarRows = 12;
    private const int CacheRefreshIntervalTicks = 120;
    private const int KnowledgeStaleAfterTicks = 1_800_000;

    private Vector2 scrollPosition;
    private string? lastActionResult;
    private int cachedAtTick = -CacheRefreshIntervalTicks;
    private int cachedSettlementCount = -1;
    private int cachedArmyCount = -1;
    private int cachedOutcomeCount = -1;
    private int cachedFactionRecordCount = -1;
    private int cachedDrifterCount = -1;
    private int cachedKnownInfoCount = -1;
    private int cachedEventCount = -1;
    private int cachedCitizenCount = -1;
    private int cachedProductionProfileCount = -1;
    private int cachedArmyMovementCount = -1;
    private List<string> cachedActiveWarbandRows = new();
    private List<string> cachedFactionStrengthRows = new();
    private List<string> cachedWarHistoryRows = new();
    private List<string> cachedFactionEconomyRows = new();
    private List<string> cachedSettlementRows = new();
    private List<string> cachedArmyRows = new();
    private List<string> cachedOutcomeRows = new();
    private List<string> cachedFactionCollapseRows = new();
    private List<string> cachedDrifterRows = new();
    private List<string> cachedKnownIntelRows = new();
    private List<string> cachedEventRows = new();

    public override Vector2 InitialSize => new Vector2(760f, 560f);

    public override void DoWindowContents(Rect inRect)
    {
        var component = LivingWorldWorldComponent.Instance;

        if (component == null)
        {
            Widgets.Label(inRect, "LW_WaitingForWorld".Translate());
            return;
        }

        var state = component.State;
        var listing = new Listing_Standard();
        listing.Begin(inRect);
        listing.Label("LW_MainTitle".Translate());
        listing.GapLine();
        listing.Label(component.GetSummary());
        listing.Label(component.GetDiagnosticSummary());

        if (state.Settlements.Count == 0)
        {
            listing.Label("LW_EmptyLedgerHint".Translate());
        }

        if (component.LastBootstrapStatus == "bootstrap-error")
        {
            listing.Label("LW_BootstrapErrorHint".Translate(component.LastBootstrapError.Named("error")));
        }

        var actionRect = listing.GetRect(32f);
        var retryRect = new Rect(actionRect.x, actionRect.y, (actionRect.width - 8f) / 2f, actionRect.height);
        var debugRect = new Rect(retryRect.xMax + 8f, actionRect.y, retryRect.width, actionRect.height);
        if (Widgets.ButtonText(retryRect, "LW_RetryBootstrapButton".Translate()))
        {
            component.RetryBootstrapFromRimWorldSettlements();
            state = component.State;
            lastActionResult = "LW_BootstrapRetried".Translate();
        }

        if (Widgets.ButtonText(debugRect, "LW_CreateDebugLedgerButton".Translate()))
        {
            component.CreateDebugLedger();
            state = component.State;
            lastActionResult = "LW_DebugLedgerCreated".Translate();
        }

        listing.Label(lastActionResult ?? "LW_RaidHookStatus".Translate());
        listing.Gap(8f);

        RefreshCachedRows(state);

        var scrollRect = listing.GetRect(inRect.height - 90f);
        var viewHeight = 140f
            + (cachedSettlementRows.Count * 88f)
            + (cachedKnownIntelRows.Count * 30f)
            + (cachedArmyRows.Count * 52f)
            + (cachedOutcomeRows.Count * 30f)
            + (cachedFactionCollapseRows.Count * 30f)
            + (cachedDrifterRows.Count * 30f)
            + 90f
            + (cachedActiveWarbandRows.Count * 26f)
            + (cachedFactionStrengthRows.Count * 26f)
            + (cachedWarHistoryRows.Count * 24f)
            + 30f
            + (cachedFactionEconomyRows.Count * 26f)
            + (cachedEventRows.Count * 24f);
        var viewRect = new Rect(0f, 0f, scrollRect.width - 16f, viewHeight);

        Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);

        var y = 0f;
        Widgets.Label(new Rect(0f, y, viewRect.width, 28f), "LW_SettlementsHeader".Translate());
        y += 30f;
        if (state.Settlements.Count > cachedSettlementRows.Count)
        {
            Widgets.Label(
                new Rect(0f, y, viewRect.width, 24f),
                "LW_ListLimited".Translate(
                    cachedSettlementRows.Count.Named("shown"),
                    state.Settlements.Count.Named("total")));
            y += 26f;
        }

        foreach (var row in cachedSettlementRows)
        {
            Widgets.Label(new Rect(0f, y, viewRect.width, 80f), row);
            y += 88f;
        }

        Widgets.Label(new Rect(0f, y, viewRect.width, 28f), "LW_KnownIntelFreshnessHeader".Translate());
        y += 30f;

        foreach (var row in cachedKnownIntelRows)
        {
            Widgets.Label(new Rect(0f, y, viewRect.width, 24f), row);
            y += 30f;
        }

        Widgets.Label(new Rect(0f, y, viewRect.width, 28f), "LW_ArmiesHeader".Translate());
        y += 30f;

        foreach (var row in cachedArmyRows)
        {
            Widgets.Label(new Rect(0f, y, viewRect.width, 44f), row);
            y += 52f;
        }

        Widgets.Label(new Rect(0f, y, viewRect.width, 28f), "LW_RaidOutcomesHeader".Translate());
        y += 30f;

        foreach (var row in cachedOutcomeRows)
        {
            Widgets.Label(new Rect(0f, y, viewRect.width, 24f), row);
            y += 30f;
        }

        Widgets.Label(new Rect(0f, y, viewRect.width, 28f), "LW_FactionCollapsesHeader".Translate());
        y += 30f;

        foreach (var row in cachedFactionCollapseRows)
        {
            Widgets.Label(new Rect(0f, y, viewRect.width, 24f), row);
            y += 30f;
        }

        Widgets.Label(new Rect(0f, y, viewRect.width, 28f), "LW_DriftersHeader".Translate());
        y += 30f;

        foreach (var row in cachedDrifterRows)
        {
            Widgets.Label(new Rect(0f, y, viewRect.width, 24f), row);
            y += 30f;
        }

        Widgets.Label(new Rect(0f, y, viewRect.width, 28f), "LW_WorldWarHeader".Translate());
        y += 30f;
        if (component.IsRimWarActive)
        {
            Widgets.Label(new Rect(0f, y, viewRect.width, 24f), "LW_WorldWarDisabledByRimWar".Translate());
            y += 26f;
        }

        if (component.IsEmpireActive)
        {
            Widgets.Label(new Rect(0f, y, viewRect.width, 24f), "LW_EmpireActiveNote".Translate());
            y += 26f;
        }

        foreach (var row in cachedActiveWarbandRows)
        {
            Widgets.Label(new Rect(0f, y, viewRect.width, 24f), row);
            y += 26f;
        }

        foreach (var row in cachedFactionStrengthRows)
        {
            Widgets.Label(new Rect(0f, y, viewRect.width, 24f), row);
            y += 26f;
        }

        foreach (var row in cachedWarHistoryRows)
        {
            Widgets.Label(new Rect(0f, y, viewRect.width, 22f), row);
            y += 24f;
        }

        Widgets.Label(new Rect(0f, y, viewRect.width, 28f), "LW_WorldEconomyHeader".Translate());
        y += 30f;

        foreach (var row in cachedFactionEconomyRows)
        {
            Widgets.Label(new Rect(0f, y, viewRect.width, 24f), row);
            y += 26f;
        }

        Widgets.Label(new Rect(0f, y, viewRect.width, 28f), "LW_EventsHeader".Translate());
        y += 30f;

        foreach (var row in cachedEventRows)
        {
            Widgets.Label(new Rect(0f, y, viewRect.width, 22f), row);
            y += 24f;
        }

        Widgets.EndScrollView();
        listing.End();
    }

    private void RefreshCachedRows(WorldState state)
    {
        var currentTick = Find.TickManager?.TicksGame ?? 0;
        if (cachedSettlementCount == state.Settlements.Count
            && cachedArmyCount == state.Armies.Count
            && cachedOutcomeCount == state.RaidOutcomes.Count
            && cachedFactionRecordCount == state.FactionRecords.Count
            && cachedDrifterCount == state.Drifters.Count
            && cachedKnownInfoCount == state.KnownSettlementInfos.Count
            && cachedEventCount == state.Events.Count
            && cachedCitizenCount == state.Citizens.Count
            && cachedProductionProfileCount == state.ProductionProfiles.Count
            && cachedArmyMovementCount == state.ArmyMovements.Count
            && currentTick - cachedAtTick < CacheRefreshIntervalTicks)
        {
            return;
        }

        cachedAtTick = currentTick;
        cachedSettlementCount = state.Settlements.Count;
        cachedArmyCount = state.Armies.Count;
        cachedOutcomeCount = state.RaidOutcomes.Count;
        cachedFactionRecordCount = state.FactionRecords.Count;
        cachedDrifterCount = state.Drifters.Count;
        cachedKnownInfoCount = state.KnownSettlementInfos.Count;
        cachedEventCount = state.Events.Count;
        cachedCitizenCount = state.Citizens.Count;
        cachedProductionProfileCount = state.ProductionProfiles.Count;
        cachedSettlementRows = state.Settlements
            .OrderBy(settlement => settlement.Id.Value)
            .Take(MaxSettlementRows)
            .Select(settlement =>
            {
                var known = state.GetKnownSettlementInfo(settlement.Id);
                var knowledgeLine = FormatKnowledgeLine(known, currentTick);
                var productionLine = known?.ExactValuesVisible == true
                    ? FormatProductionLine(state.GetSettlementProductionStatus(settlement.Id))
                    : "LW_ProductionHiddenLine".Translate().ToString();
                return "LW_SettlementLine".Translate(
                    settlement.Name.Named("name"),
                    settlement.FactionId.Named("faction"),
                    knowledgeLine.Named("knowledge"),
                    productionLine.Named("production")).ToString();
            })
            .ToList();
        cachedKnownIntelRows = state.KnownSettlementInfos
            .OrderByDescending(info => info.Tick)
            .ThenBy(info => info.SettlementId.Value)
            .Take(MaxKnowledgeRows)
            .Select(info =>
            {
                var settlement = state.GetSettlement(info.SettlementId);
                var freshness = PlayerKnowledgeService.GetFreshness(info, currentTick, KnowledgeStaleAfterTicks);
                return "LW_KnownIntelFreshnessLine".Translate(
                    (settlement?.Name ?? info.SettlementId.ToString()).Named("name"),
                    info.SourceKind.Named("source"),
                    info.Confidence.Named("confidence"),
                    freshness.AgeDays.Named("ageDays"),
                    freshness.IsStale.Named("stale"),
                    info.Summary.Named("summary")).ToString();
            })
            .ToList();
        cachedArmyRows = state.Armies
            .OrderBy(army => army.Id.Value)
            .Take(MaxArmyRows)
            .Select(army =>
            {
                var aliveCitizens = state.Citizens.Count(citizen =>
                    state.GetOwner(citizen.Id) == army.Id
                    && citizen.Status == CitizenStatus.Alive);
                var deadCitizens = state.Citizens.Count(citizen =>
                    state.GetOwner(citizen.Id) == army.Id
                    && citizen.Status == CitizenStatus.Dead);
                var returnedCitizens = state.RaidPawnLinks.Count(link =>
                    link.ArmyId == army.Id
                    && link.Status == RaidPawnLinkStatus.Returned);
                var prisonerCitizens = state.RaidPawnLinks.Count(link =>
                    link.ArmyId == army.Id
                    && link.Status == RaidPawnLinkStatus.Prisoner);
                var meals = state.GetOwnedResourceQuantity(army.Id, "PackagedSurvivalMeal");
                var steel = state.GetOwnedResourceQuantity(army.Id, "Steel");
                return "LW_ArmyLine".Translate(
                    army.Name.Named("name"),
                    army.FactionId.Named("faction"),
                    aliveCitizens.Named("citizens"),
                    deadCitizens.Named("deadCitizens"),
                    returnedCitizens.Named("returnedCitizens"),
                    prisonerCitizens.Named("prisonerCitizens"),
                    meals.Named("meals"),
                    steel.Named("steel")).ToString();
            })
            .ToList();
        cachedOutcomeRows = state.RaidOutcomes
            .OrderByDescending(outcome => outcome.Tick)
            .ThenByDescending(outcome => outcome.ArmyId.Value)
            .Take(MaxOutcomeRows)
            .Select(outcome =>
            {
                var army = state.GetArmy(outcome.ArmyId);
                return "LW_RaidOutcomeLine".Translate(
                    (army?.Name ?? outcome.ArmyId.ToString()).Named("name"),
                    outcome.FactionId.Named("faction"),
                    outcome.Sent.Named("sent"),
                    outcome.Dead.Named("dead"),
                    outcome.Returned.Named("returned"),
                    outcome.Prisoner.Named("prisoner"),
                    outcome.Missing.Named("missing")).ToString();
            })
            .ToList();
        cachedFactionCollapseRows = state.FactionRecords
            .Where(record => record.Status == WorldFactionStatus.Collapsed)
            .OrderByDescending(record => record.Tick)
            .ThenBy(record => record.FactionId, System.StringComparer.Ordinal)
            .Take(MaxFactionCollapseRows)
            .Select(record =>
                "LW_FactionCollapseLine".Translate(
                    record.FactionId.Named("faction"),
                    record.Tick.Named("tick"),
                    record.Reason.Named("reason")).ToString())
            .ToList();
        cachedDrifterRows = state.Drifters
            .OrderBy(drifter => drifter.ArrivalTick)
            .ThenBy(drifter => drifter.Id.Value)
            .Take(MaxDrifterRows)
            .Select(drifter =>
                "LW_DrifterLine".Translate(
                    drifter.Name.Named("name"),
                    drifter.Age.Named("age"),
                    drifter.Sex.Named("sex"),
                    drifter.CombatAptitude.Named("combat"),
                    drifter.OrganizationAptitude.Named("organization")).ToString())
            .ToList();
        cachedEventRows = state.Events
            .Skip(System.Math.Max(0, state.Events.Count - MaxEventRows))
            .Take(MaxEventRows)
            .Select(worldEvent =>
                "LW_EventLine".Translate(
                    worldEvent.Tick.Named("tick"),
                    worldEvent.Kind.Named("kind"),
                    worldEvent.Summary.Named("summary")).ToString())
            .ToList();

        cachedArmyMovementCount = state.ArmyMovements.Count;
        cachedActiveWarbandRows = state.ArmyMovements
            .Where(movement => movement.Status == ArmyMovementStatus.Traveling)
            .OrderBy(movement => movement.ArrivalTick)
            .ThenBy(movement => movement.ArmyId.Value)
            .Take(MaxWarRows)
            .Select(movement =>
            {
                var army = state.GetArmy(movement.ArmyId);
                var target = state.GetSettlement(movement.TargetSettlementId);
                return "LW_WarbandMovementLine".Translate(
                    (army?.FactionId ?? "?").Named("faction"),
                    (target?.Name ?? "?").Named("target"),
                    movement.ArrivalTick.Named("eta")).ToString();
            })
            .ToList();

        var debugExact = (LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging;
        cachedFactionStrengthRows = state.Settlements
            .Select(settlement => settlement.FactionId)
            .Distinct(System.StringComparer.Ordinal)
            .OrderBy(factionId => factionId, System.StringComparer.Ordinal)
            .Take(MaxWarRows)
            .Select(factionId =>
            {
                var power = state.Settlements
                    .Where(settlement => string.Equals(settlement.FactionId, factionId, System.StringComparison.Ordinal))
                    .Sum(settlement => SettlementPowerService.GetSettlementPower(state, settlement.Id).CombatPower);
                var strength = debugExact ? power.ToString() : StrengthBand(power);
                return "LW_FactionStrengthLine".Translate(
                    factionId.Named("faction"),
                    strength.Named("strength")).ToString();
            })
            .ToList();

        cachedWarHistoryRows = state.Events
            .Where(worldEvent =>
                worldEvent.Kind == WorldEventKind.SettlementCaptured
                || worldEvent.Kind == WorldEventKind.WarbandLaunched
                || worldEvent.Kind == WorldEventKind.FactionCollapsed)
            .OrderByDescending(worldEvent => worldEvent.Tick)
            .ThenByDescending(worldEvent => worldEvent.Id.Value)
            .Take(MaxWarRows)
            .Select(worldEvent =>
                "LW_WarHistoryLine".Translate(
                    worldEvent.Tick.Named("tick"),
                    worldEvent.Kind.Named("kind"),
                    worldEvent.Summary.Named("summary")).ToString())
            .ToList();

        cachedFactionEconomyRows = state.Settlements
            .Select(settlement => settlement.FactionId)
            .Distinct(System.StringComparer.Ordinal)
            .OrderBy(factionId => factionId, System.StringComparer.Ordinal)
            .Take(MaxWarRows)
            .Select(factionId =>
            {
                var stock = state.Settlements
                    .Where(settlement => string.Equals(settlement.FactionId, factionId, System.StringComparison.Ordinal))
                    .Sum(settlement => FactionMaterialStock(state, settlement.Id));
                var wealth = debugExact ? stock.ToString() : WealthBand(stock);
                return "LW_FactionEconomyLine".Translate(
                    factionId.Named("faction"),
                    wealth.Named("wealth")).ToString();
            })
            .ToList();
    }

    // A settlement's material stockpile across the tracked resources — a proxy until Codex's
    // ledger wealth (E1) lands; shown as a band so the player is not omniscient.
    private static int FactionMaterialStock(WorldState state, EntityId settlementId)
    {
        return state.GetOwnedResourceQuantity(settlementId, "PackagedSurvivalMeal")
            + state.GetOwnedResourceQuantity(settlementId, "Steel")
            + state.GetOwnedResourceQuantity(settlementId, "MedicineIndustrial")
            + state.GetOwnedResourceQuantity(settlementId, "ComponentIndustrial");
    }

    private static string WealthBand(int stock)
    {
        if (stock < 100)
        {
            return "LW_WealthBandPoor".Translate();
        }

        return stock < 500
            ? "LW_WealthBandModest".Translate()
            : "LW_WealthBandWealthy".Translate();
    }

    // Player-facing strength is a coarse band, not an omniscient exact value (debug logging
    // reveals the raw number instead).
    private static string StrengthBand(int power)
    {
        if (power < 500)
        {
            return "LW_FactionStrengthBandWeak".Translate();
        }

        return power < 2000
            ? "LW_FactionStrengthBandModerate".Translate()
            : "LW_FactionStrengthBandStrong".Translate();
    }

    private static string FormatKnowledgeLine(KnownSettlementInfo? known, int currentTick)
    {
        if (known == null)
        {
            return "LW_KnowledgeUnknown".Translate().ToString();
        }

        var freshness = PlayerKnowledgeService.GetFreshness(known, currentTick, KnowledgeStaleAfterTicks);
        return "LW_KnowledgeLine".Translate(
            known.SourceKind.Named("source"),
            known.Confidence.Named("confidence"),
            known.Tick.Named("tick"),
            freshness.AgeDays.Named("ageDays"),
            freshness.IsStale.Named("stale"),
            known.PopulationBand.Named("populationBand"),
            known.Food.Named("food"),
            known.Migration.Named("migration"),
            known.Production.Named("production")).ToString();
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
}
