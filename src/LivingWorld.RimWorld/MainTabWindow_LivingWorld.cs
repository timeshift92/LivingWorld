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
    private int cachedCaravanCount = -1;
    private int cachedMissionCount = -1;
    private List<string> cachedActiveWarbandRows = new();
    private List<string> cachedWorldActionRows = new();
    private List<string> cachedActionAttemptRows = new();
    private List<(string FactionId, string Text, float Fill)> cachedFactionStrengthRows = new();
    private List<string> cachedWarHistoryRows = new();
    private List<(string FactionId, string Text, float Fill)> cachedFactionEconomyRows = new();
    private List<(string FactionId, string Text, float Fill)> cachedWatcherRows = new();
    private List<string> cachedSettlementRows = new();
    private List<string> cachedArmyRows = new();
    private List<string> cachedOutcomeRows = new();
    private List<string> cachedFactionCollapseRows = new();
    private List<string> cachedDrifterRows = new();
    private List<string> cachedKnownIntelRows = new();
    private List<string> cachedConflictRows = new();
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
        listing.Gap(4f);

        // Opens the columnar Population/Economy table (F-1) — the at-a-glance companion to the
        // scrolling lists below.
        if (listing.ButtonText("LW_OpenEconomyWindow".Translate()))
        {
            Find.WindowStack.Add(new LivingWorldEconomyWindow());
        }

        if (listing.ButtonText("LW_OpenSettlementObserverWindow".Translate()))
        {
            Find.WindowStack.Add(new LivingWorldSettlementObserverWindow());
        }

        listing.Gap(6f);

        RefreshCachedRows(state);

        var scrollRect = listing.GetRect(inRect.height - 130f);
        var viewHeight = 140f
            + (cachedSettlementRows.Count * 88f)
            + (cachedKnownIntelRows.Count * 30f)
            + (cachedArmyRows.Count * 52f)
            + (cachedOutcomeRows.Count * 30f)
            + (cachedFactionCollapseRows.Count * 30f)
            + (cachedDrifterRows.Count * 30f)
            + 90f
            + 30f
            + (cachedWorldActionRows.Count * 26f)
            + (cachedActionAttemptRows.Count > 0 ? 30f + (cachedActionAttemptRows.Count * 24f) : 0f)
            + (cachedActiveWarbandRows.Count * 26f)
            + (cachedFactionStrengthRows.Count * 26f)
            + (cachedWarHistoryRows.Count * 24f)
            + 30f
            + (cachedFactionEconomyRows.Count * 26f)
            + 30f
            + (cachedWatcherRows.Count * 26f)
            + 30f
            + (cachedConflictRows.Count * 24f)
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

        Widgets.Label(new Rect(0f, y, viewRect.width, 28f), "LW_WorldActionsHeader".Translate());
        y += 30f;

        foreach (var row in cachedWorldActionRows)
        {
            Widgets.Label(new Rect(0f, y, viewRect.width, 24f), row);
            y += 26f;
        }

        if (cachedActionAttemptRows.Count > 0)
        {
            Widgets.Label(new Rect(0f, y, viewRect.width, 28f), "LW_ActionAttemptsHeader".Translate());
            y += 30f;
            foreach (var row in cachedActionAttemptRows)
            {
                Widgets.Label(new Rect(0f, y, viewRect.width, 22f), row);
                y += 24f;
            }
        }

        foreach (var row in cachedActiveWarbandRows)
        {
            Widgets.Label(new Rect(0f, y, viewRect.width, 24f), row);
            y += 26f;
        }

        foreach (var row in cachedFactionStrengthRows)
        {
            DrawFactionRow(new Rect(0f, y, viewRect.width, 24f), row.FactionId, row.Text, row.Fill);
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
            DrawFactionRow(new Rect(0f, y, viewRect.width, 24f), row.FactionId, row.Text, row.Fill);
            y += 26f;
        }

        Widgets.Label(new Rect(0f, y, viewRect.width, 28f), "LW_WatchersHeader".Translate());
        y += 30f;

        foreach (var row in cachedWatcherRows)
        {
            DrawFactionRow(new Rect(0f, y, viewRect.width, 24f), row.FactionId, row.Text, row.Fill);
            y += 26f;
        }

        Widgets.Label(new Rect(0f, y, viewRect.width, 28f), "LW_WorldConflictsHeader".Translate());
        y += 30f;

        foreach (var row in cachedConflictRows)
        {
            Widgets.Label(new Rect(0f, y, viewRect.width, 22f), row);
            y += 24f;
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
            && cachedCaravanCount == state.Caravans.Count
            && cachedMissionCount == state.Missions.Count
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
        cachedCaravanCount = state.Caravans.Count;
        cachedMissionCount = state.Missions.Count;
        cachedSettlementRows = state.Settlements
            .OrderBy(settlement => settlement.Id.Value)
            .Take(MaxSettlementRows)
            .Select(settlement =>
            {
                var known = state.GetKnownSettlementInfo(settlement.Id);
                var knowledgeLine = FormatKnowledgeLine(known, currentTick);
                var productionLine = PlayerKnowledgeService.HasFreshExactSnapshot(known, currentTick)
                    ? FormatProductionLine(known!.ExactSnapshot!)
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
        var debugExact = (LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging;
        var travelingArmyIds = new HashSet<EntityId>(state.ArmyMovements
            .Where(movement => movement.Status == ArmyMovementStatus.Traveling)
            .Select(movement => movement.ArmyId));
        cachedArmyRows = state.Armies
            .Where(army => debugExact
                || (travelingArmyIds.Contains(army.Id)
                    && LivingWorldTransitVisibility.IsKnownFromSource(
                        state, army.FactionId, army.SourceSettlementId)))
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
                if (!debugExact)
                {
                    return "LW_ArmyBandLine".Translate(
                        army.Name.Named("name"),
                        army.FactionId.Named("faction"),
                        StrengthBand((aliveCitizens * 100) + (deadCitizens * 20)).Named("strength"),
                        CargoBand(meals + steel).Named("cargo")).ToString();
                }

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
            .Where(record => debugExact || state.Settlements.Any(settlement =>
                string.Equals(settlement.FactionId, record.FactionId, System.StringComparison.Ordinal)
                && LivingWorldTransitVisibility.CanRevealSettlement(state, settlement.Id)))
            .OrderByDescending(record => record.Tick)
            .ThenBy(record => record.FactionId, System.StringComparer.Ordinal)
            .Take(MaxFactionCollapseRows)
            .Select(record =>
                "LW_FactionCollapseLine".Translate(
                    record.FactionId.Named("faction"),
                    record.Tick.Named("tick"),
                    record.Reason.Named("reason")).ToString())
            .ToList();
        cachedDrifterRows = (debugExact ? state.Drifters : System.Array.Empty<Drifter>())
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
        var knownSettlementIds = new HashSet<EntityId>(state.Settlements
            .Where(settlement => LivingWorldTransitVisibility.CanRevealSettlement(state, settlement.Id))
            .Select(settlement => settlement.Id));
        cachedEventRows = state.Events
            .Where(worldEvent => debugExact
                || (worldEvent.SettlementId.HasValue
                    && LivingWorldTransitVisibility.CanRevealSettlement(state, worldEvent.SettlementId.Value)))
            .Skip(System.Math.Max(0, state.Events.Count - MaxEventRows))
            .Take(MaxEventRows)
            .Select(worldEvent =>
                "LW_EventLine".Translate(
                    worldEvent.Tick.Named("tick"),
                    worldEvent.Kind.Named("kind"),
                    worldEvent.Summary.Named("summary")).ToString())
            .ToList();

        cachedArmyMovementCount = state.ArmyMovements.Count;
        cachedWorldActionRows = BuildWorldActionRows(state);
        cachedActionAttemptRows = debugExact
            ? state.RecentActionAttempts
                .OrderByDescending(attempt => attempt.Tick)
                .ThenBy(attempt => attempt.FactionId, System.StringComparer.Ordinal)
                .Take(12)
                .Select(attempt => "LW_ActionAttemptRow".Translate(
                    attempt.FactionId.Named("faction"),
                    attempt.Action.Named("action"),
                    attempt.Reason.Named("reason"),
                    attempt.Detail.Named("detail")).ToString())
                .ToList()
            : new List<string>();
        cachedActiveWarbandRows = state.ArmyMovements
            .Where(movement => movement.Status == ArmyMovementStatus.Traveling)
            .Where(movement =>
            {
                var army = state.GetArmy(movement.ArmyId);
                return army != null && LivingWorldTransitVisibility.IsKnownFromSource(
                    state, army.FactionId, army.SourceSettlementId);
            })
            .OrderBy(movement => movement.ArrivalTick)
            .ThenBy(movement => movement.ArmyId.Value)
            .Take(MaxWarRows)
            .Select(movement =>
            {
                var army = state.GetArmy(movement.ArmyId);
                var target = state.GetSettlement(movement.TargetSettlementId);
                return "LW_WarbandMovementLine".Translate(
                    (army?.FactionId ?? "?").Named("faction"),
                    (target != null && LivingWorldTransitVisibility.CanRevealSettlement(state, target.Id)
                        ? target.Name
                        : "LW_UnknownDestination".Translate().ToString()).Named("target"),
                    movement.ArrivalTick.Named("eta")).ToString();
            })
            .ToList();

        var strengthSettlements = debugExact
            ? state.Settlements.Where(settlement => settlement.IsActive)
            : state.Settlements.Where(settlement => settlement.IsActive && knownSettlementIds.Contains(settlement.Id));
        var strengthRaw = strengthSettlements
            .Select(settlement => settlement.FactionId)
            .Distinct(System.StringComparer.Ordinal)
            .OrderBy(factionId => factionId, System.StringComparer.Ordinal)
            .Take(MaxWarRows)
            .Select(factionId =>
            {
                var power = debugExact
                    ? state.Settlements
                        .Where(settlement => string.Equals(settlement.FactionId, factionId, System.StringComparison.Ordinal))
                        .Sum(settlement => SettlementPowerService.GetSettlementPower(state, settlement.Id).CombatPower)
                    : state.KnownSettlementInfos
                        .Where(info => state.GetSettlement(info.SettlementId)?.FactionId == factionId)
                        .Sum(info => PopulationBandMinimum(info.PopulationBand) * 100);
                var strength = debugExact ? power.ToString() : StrengthBand(power);
                var strengthKey = debugExact ? "LW_FactionStrengthLine" : "LW_FactionStrengthKnownLine";
                return (FactionId: factionId, Text: strengthKey.Translate(
                    factionId.Named("faction"),
                    strength.Named("strength")).ToString(), Value: power);
            })
            .ToList();
        var maxStrength = strengthRaw.Count > 0 ? strengthRaw.Max(row => row.Value) : 0;
        cachedFactionStrengthRows = strengthRaw
            .Select(row => (row.FactionId, row.Text, maxStrength > 0 ? (float)row.Value / maxStrength : 0f))
            .ToList();

        cachedWarHistoryRows = state.Events
            .Where(worldEvent =>
                (worldEvent.Kind == WorldEventKind.SettlementCaptured
                    || worldEvent.Kind == WorldEventKind.WarbandLaunched
                    || worldEvent.Kind == WorldEventKind.FactionCollapsed)
                && (debugExact
                    || (worldEvent.SettlementId.HasValue && knownSettlementIds.Contains(worldEvent.SettlementId.Value))))
            .OrderByDescending(worldEvent => worldEvent.Tick)
            .ThenByDescending(worldEvent => worldEvent.Id.Value)
            .Take(MaxWarRows)
            .Select(worldEvent =>
                "LW_WarHistoryLine".Translate(
                    worldEvent.Tick.Named("tick"),
                    worldEvent.Kind.Named("kind"),
                    worldEvent.Summary.Named("summary")).ToString())
            .ToList();

        var economyRaw = strengthSettlements
            .Select(settlement => settlement.FactionId)
            .Distinct(System.StringComparer.Ordinal)
            .OrderBy(factionId => factionId, System.StringComparer.Ordinal)
            .Take(MaxWarRows)
            .Select(factionId =>
            {
                // E4b: read the priced ledger wealth (silver + valued material) the economy sim
                // records each day; fall back to a raw material sum only before the first daily
                // refresh has run.
                var known = state.KnownSettlementInfos
                    .Where(info => state.GetSettlement(info.SettlementId)?.FactionId == factionId)
                    .ToList();
                var freshSnapshots = known
                    .Where(info => PlayerKnowledgeService.HasFreshExactSnapshot(info, currentTick))
                    .Select(info => info.ExactSnapshot!)
                    .ToList();
                var stock = debugExact
                    ? state.GetFactionWealth(factionId)?.TotalWealth
                        ?? state.Settlements
                            .Where(settlement => string.Equals(settlement.FactionId, factionId, System.StringComparison.Ordinal))
                            .Sum(settlement => FactionMaterialStock(state, settlement.Id))
                    : freshSnapshots.Sum(snapshot => snapshot.Wealth);
                var wealth = debugExact || freshSnapshots.Count == known.Count
                    ? (debugExact ? stock.ToString() : WealthBand(stock))
                    : known.Select(info => info.Production).DefaultIfEmpty(SettlementProductionKnowledge.Unknown).Max().ToString();
                var economyKey = debugExact ? "LW_FactionEconomyLine" : "LW_FactionEconomyKnownLine";
                return (FactionId: factionId, Text: economyKey.Translate(
                    factionId.Named("faction"),
                    wealth.Named("wealth")).ToString(), Value: stock);
            })
            .ToList();
        var maxStock = debugExact && economyRaw.Count > 0 ? economyRaw.Max(row => row.Value) : 0;
        cachedFactionEconomyRows = economyRaw
            .Select(row => (row.FactionId, row.Text, maxStock > 0 ? (float)row.Value / maxStock : 0f))
            .ToList();

        // Watchers: factions that have raid intel about the player's colony (from trade/scouting).
        // One row per faction (its strongest active fact), showing how much they know as a band and
        // how fresh it is — the persistent companion to the source-labelled warning letters.
        cachedWatcherRows = state.RaidIntelFacts
            .Where(fact => fact.TargetKind == RaidIntelTargetKind.PlayerColony && !fact.IsExpired(currentTick))
            .GroupBy(fact => fact.FactionId, System.StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(fact => fact.ValueBand)
                .ThenByDescending(fact => fact.Confidence)
                .First())
            .OrderByDescending(fact => fact.ValueBand)
            .ThenByDescending(fact => fact.Confidence)
            .ThenBy(fact => fact.FactionId, System.StringComparer.Ordinal)
            .Take(MaxWarRows)
            .Select(fact =>
            {
                var ageDays = System.Math.Max(0, (currentTick - fact.CreatedTick) / 60_000);
                var text = "LW_WatcherLine".Translate(
                    fact.FactionId.Named("faction"),
                    IntelBand(fact.ValueBand).Named("band"),
                    ageDays.Named("days")).ToString();
                return (fact.FactionId, text, Mathf.Clamp01(fact.Confidence / 100f));
            })
            .ToList();

        // World conflicts: ongoing NPC-vs-NPC wars and truces (Task 6). Resolved conflicts drop off;
        // the rest are shown with a coarse intensity band from cumulative war exhaustion so the tab
        // never implies false precision about battles the player has not witnessed.
        cachedConflictRows = state.Conflicts
            .Where(conflict => conflict.Status != WorldConflictStatus.Resolved)
            .Where(conflict => debugExact || IsConflictKnownToPlayer(state, conflict))
            .OrderByDescending(conflict => conflict.WarExhaustionA + conflict.WarExhaustionB)
            .ThenBy(conflict => conflict.Id.Value)
            .Take(MaxWarRows)
            .Select(conflict =>
            {
                var days = System.Math.Max(0, (currentTick - conflict.StartedTick) / 60_000);
                return "LW_WorldConflictLine".Translate(
                    ResolveFactionName(conflict.FactionA).Named("factionA"),
                    ResolveFactionName(conflict.FactionB).Named("factionB"),
                    ConflictStatusLabel(conflict.Status).Named("status"),
                    days.Named("days"),
                    ConflictIntensityBand(conflict.WarExhaustionA + conflict.WarExhaustionB).Named("intensity"),
                    conflict.RefugeesCreated.Named("displaced")).ToString();
            })
            .ToList();

    }

    private static List<string> BuildWorldActionRows(WorldState state)
    {
        var warbands = state.ArmyMovements
            .Where(movement => movement.Status == ArmyMovementStatus.Traveling)
            .Where(movement =>
            {
                var army = state.GetArmy(movement.ArmyId);
                return army != null && LivingWorldTransitVisibility.IsKnownFromSource(
                    state, army.FactionId, army.SourceSettlementId);
            })
            .OrderBy(movement => movement.ArrivalTick)
            .ThenBy(movement => movement.ArmyId.Value)
            .ToList();
        var caravans = state.Caravans
            .Where(caravan => caravan.Status == CaravanStatus.Traveling)
            .Where(caravan => LivingWorldTransitVisibility.IsKnownFromSource(
                state, caravan.FactionId, caravan.SourceSettlementId))
            .OrderBy(caravan => caravan.ArrivalTick)
            .ThenBy(caravan => caravan.Id.Value)
            .ToList();
        var scouts = state.Missions
            .Where(mission => mission.Status == WorldMissionStatus.Traveling && mission.Kind == WorldMissionKind.Scout)
            .Where(mission => LivingWorldTransitVisibility.IsKnownFromSource(
                state, mission.FactionId, mission.OriginSettlementId))
            .OrderBy(mission => mission.ArrivalTick)
            .ThenBy(mission => mission.Id.Value)
            .ToList();
        var diplomats = state.Missions
            .Where(mission => mission.Status == WorldMissionStatus.Traveling && mission.Kind == WorldMissionKind.Diplomat)
            .Where(mission => LivingWorldTransitVisibility.IsKnownFromSource(
                state, mission.FactionId, mission.OriginSettlementId))
            .OrderBy(mission => mission.ArrivalTick)
            .ThenBy(mission => mission.Id.Value)
            .ToList();
        var settlers = state.MigrationGroups
            .Where(group => group.Status == MigrationGroupStatus.Traveling
                && string.Equals(group.Reason, MigrationService.ReasonSettlementFounding, System.StringComparison.Ordinal))
            .Where(group => LivingWorldTransitVisibility.IsKnownFromSource(
                state, group.FactionId, group.SourceSettlementId))
            .OrderBy(group => group.ArrivalTick)
            .ThenBy(group => group.Id.Value)
            .ToList();
        var migrations = state.MigrationGroups
            .Where(group => group.Status == MigrationGroupStatus.Traveling
                && !string.Equals(group.Reason, MigrationService.ReasonSettlementFounding, System.StringComparison.Ordinal)
                && group.TargetSettlementId.HasValue)
            .Where(group => LivingWorldTransitVisibility.IsKnownFromSource(
                state, group.FactionId, group.SourceSettlementId))
            .OrderBy(group => group.ArrivalTick)
            .ThenBy(group => group.Id.Value)
            .ToList();
        var developments = state.Events
            .Where(worldEvent => worldEvent.Kind == WorldEventKind.SettlementDeveloped
                && worldEvent.Tick >= System.Math.Max(0, state.CurrentTick - 60_000)
                && worldEvent.SettlementId.HasValue)
            .Where(worldEvent => LivingWorldTransitVisibility.CanRevealSettlement(
                state, worldEvent.SettlementId!.Value))
            .OrderByDescending(worldEvent => worldEvent.Tick)
            .ThenByDescending(worldEvent => worldEvent.Id.Value)
            .ToList();

        var rows = new List<string>
        {
            "LW_WorldActionLegendLine".Translate(
                warbands.Count.Named("warbands"),
                caravans.Count.Named("caravans"),
                scouts.Count.Named("scouts"),
                diplomats.Count.Named("diplomats"),
                settlers.Count.Named("settlers"),
                migrations.Count.Named("migrations"),
                developments.Count.Named("developments")).ToString(),
        };

        rows.AddRange(warbands.Take(4).Select(movement =>
        {
            var army = state.GetArmy(movement.ArmyId);
            var target = state.GetSettlement(movement.TargetSettlementId);
            return "LW_WorldActionRow".Translate(
                "LW_MissionKind_Warband".Translate().Named("kind"),
                (army?.FactionId ?? "?").Named("faction"),
                (target != null && LivingWorldTransitVisibility.CanRevealSettlement(state, target.Id)
                    ? target.Name
                    : "LW_UnknownDestination".Translate().ToString()).Named("target"),
                DaysUntil(movement.ArrivalTick, state.CurrentTick).Named("days")).ToString();
        }));

        rows.AddRange(caravans.Take(4).Select(caravan =>
        {
            var target = state.GetSettlement(caravan.TargetSettlementId);
            return "LW_WorldActionRow".Translate(
                "LW_MissionKind_Trader".Translate().Named("kind"),
                caravan.FactionId.Named("faction"),
                (target != null && LivingWorldTransitVisibility.CanRevealSettlement(state, target.Id)
                    ? target.Name
                    : "LW_UnknownDestination".Translate().ToString()).Named("target"),
                DaysUntil(caravan.ArrivalTick, state.CurrentTick).Named("days")).ToString();
        }));

        rows.AddRange(scouts.Take(3).Select(mission =>
        {
            var target = mission.TargetSettlementId.HasValue
                ? state.GetSettlement(mission.TargetSettlementId.Value)
                : null;
            return "LW_WorldActionRow".Translate(
                "LW_MissionKind_Scout".Translate().Named("kind"),
                mission.FactionId.Named("faction"),
                (target != null && LivingWorldTransitVisibility.CanRevealSettlement(state, target.Id)
                    ? target.Name
                    : "LW_UnknownDestination".Translate().ToString()).Named("target"),
                DaysUntil(mission.ArrivalTick, state.CurrentTick).Named("days")).ToString();
        }));

        rows.AddRange(diplomats.Take(3).Select(mission =>
        {
            var target = mission.TargetSettlementId.HasValue
                ? state.GetSettlement(mission.TargetSettlementId.Value)
                : null;
            return "LW_WorldActionRow".Translate(
                "LW_MissionKind_Diplomat".Translate().Named("kind"),
                mission.FactionId.Named("faction"),
                (mission.TargetsPlayerContact
                    ? "LW_PlayerContactDestination".Translate().ToString()
                    : target != null && LivingWorldTransitVisibility.CanRevealSettlement(state, target.Id)
                        ? target.Name
                        : "LW_UnknownDestination".Translate().ToString()).Named("target"),
                DaysUntil(mission.ArrivalTick, state.CurrentTick).Named("days")).ToString();
        }));

        rows.AddRange(settlers.Take(3).Select(group =>
            "LW_WorldActionRow".Translate(
                "LW_MissionKind_Settler".Translate().Named("kind"),
                group.FactionId.Named("faction"),
                (string.IsNullOrWhiteSpace(group.PlannedSettlementName)
                    ? group.PlannedSettlementSlug
                    : group.PlannedSettlementName).Named("target"),
                DaysUntil(group.ArrivalTick, state.CurrentTick).Named("days")).ToString()));

        rows.AddRange(migrations.Take(3).Select(group =>
        {
            var target = group.TargetSettlementId.HasValue
                ? state.GetSettlement(group.TargetSettlementId.Value)
                : null;
            return "LW_WorldActionRow".Translate(
                "LW_MissionKind_Refugees".Translate().Named("kind"),
                group.FactionId.Named("faction"),
                (target?.Name ?? "LW_UnknownDestination".Translate().ToString()).Named("target"),
                DaysUntil(group.ArrivalTick, state.CurrentTick).Named("days")).ToString();
        }));

        rows.AddRange(developments.Take(3).Select(worldEvent =>
        {
            var settlement = worldEvent.SettlementId.HasValue
                ? state.GetSettlement(worldEvent.SettlementId.Value)
                : null;
            return "LW_WorldActionCompletedRow".Translate(
                "LW_MissionKind_Develop".Translate().Named("kind"),
                (settlement?.FactionId ?? "?").Named("faction"),
                (settlement?.Name ?? "?").Named("target")).ToString();
        }));

        return rows;
    }

    private static bool IsConflictKnownToPlayer(WorldState state, WorldConflict conflict)
    {
        var playerId = state.PlayerFactionId;
        if (!string.IsNullOrWhiteSpace(playerId) && conflict.Involves(playerId!))
        {
            return true;
        }

        return state.Settlements.Any(settlement => settlement.IsActive
            && (string.Equals(settlement.FactionId, conflict.FactionA, System.StringComparison.Ordinal)
                || string.Equals(settlement.FactionId, conflict.FactionB, System.StringComparison.Ordinal))
            && LivingWorldTransitVisibility.CanRevealSettlement(state, settlement.Id));
    }

    private static int DaysUntil(int arrivalTick, int currentTick)
    {
        return System.Math.Max(0, (int)System.Math.Ceiling((arrivalTick - currentTick) / 60_000d));
    }

    // Resolve a ledger faction id (a RimWorld faction defName) to its display name so the conflict
    // rows read "The Black Hand vs New Arrivals", not the raw "Pirate vs OutlanderRough" — matching
    // the war-declaration letter, which resolves the same way.
    private static string ResolveFactionName(string factionId)
    {
        if (string.IsNullOrEmpty(factionId))
        {
            return factionId ?? string.Empty;
        }

        var faction = Find.FactionManager?.AllFactionsListForReading
            .FirstOrDefault(candidate => candidate.def?.defName == factionId);
        return faction?.Name ?? factionId;
    }

    private static string ConflictStatusLabel(WorldConflictStatus status)
    {
        return status switch
        {
            WorldConflictStatus.Truce => "LW_ConflictStatus_Truce".Translate(),
            _ => "LW_ConflictStatus_Active".Translate(),
        };
    }

    private static string ConflictIntensityBand(int totalExhaustion)
    {
        if (totalExhaustion >= 40)
        {
            return "LW_ConflictIntensity_Devastating".Translate();
        }

        if (totalExhaustion >= 15)
        {
            return "LW_ConflictIntensity_Heavy".Translate();
        }

        if (totalExhaustion >= 1)
        {
            return "LW_ConflictIntensity_Skirmish".Translate();
        }

        return "LW_ConflictIntensity_None".Translate();
    }

    private static string IntelBand(RaidIntelValueBand band)
    {
        return band switch
        {
            RaidIntelValueBand.Extreme => "LW_IntelBand_Extreme".Translate(),
            RaidIntelValueBand.High => "LW_IntelBand_High".Translate(),
            RaidIntelValueBand.Moderate => "LW_IntelBand_Moderate".Translate(),
            _ => "LW_IntelBand_Low".Translate(),
        };
    }

    // Fallback only: a settlement's raw material stockpile across the tracked resources, used just
    // until the first daily wealth refresh records a priced snapshot (E1/E4b). Shown as a band so
    // the player is not omniscient.
    private static int FactionMaterialStock(WorldState state, EntityId settlementId)
    {
        return state.GetOwnedResourceQuantity(settlementId, "PackagedSurvivalMeal")
            + state.GetOwnedResourceQuantity(settlementId, "Steel")
            + state.GetOwnedResourceQuantity(settlementId, "MedicineIndustrial")
            + state.GetOwnedResourceQuantity(settlementId, "ComponentIndustrial");
    }

    // Bands are calibrated for the priced faction wealth (silver + valued material), which is much
    // larger than a raw quantity count. Exact totals show only under debug.
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

    // Draws a per-faction ledger row with the faction's native icon and colour in front of the
    // text, so the world-war/economy lists read like a real RimWorld faction list instead of a
    // wall of plain labels. Falls back to a plain label when the faction has left the world.
    private static void DrawFactionRow(Rect rect, string factionId, string text, float fill)
    {
        const float IconSize = 22f;
        var faction = Find.FactionManager?.AllFactionsListForReading
            .FirstOrDefault(candidate =>
                candidate.def != null
                && string.Equals(candidate.def.defName, factionId, System.StringComparison.Ordinal));

        // Relative data bar behind the row: a translucent faction-coloured fill scaled by this
        // faction's share of the strongest/richest faction, so the list reads as a comparative
        // bar chart at a glance instead of just words.
        var clampedFill = Mathf.Clamp01(fill);
        if (clampedFill > 0f)
        {
            var barColor = faction != null ? faction.Color : new Color(0.5f, 0.5f, 0.55f);
            barColor.a = 0.3f;
            var previousBarColor = GUI.color;
            GUI.color = barColor;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * clampedFill, rect.height), BaseContent.WhiteTex);
            GUI.color = previousBarColor;
        }

        var labelX = rect.x;
        if (faction?.def?.FactionIcon != null)
        {
            var iconRect = new Rect(rect.x, rect.y + ((rect.height - IconSize) / 2f), IconSize, IconSize);
            var previousColor = GUI.color;
            GUI.color = faction.Color;
            GUI.DrawTexture(iconRect, faction.def.FactionIcon);
            GUI.color = previousColor;
            labelX += IconSize + 6f;
        }

        Widgets.Label(new Rect(labelX, rect.y, rect.width - (labelX - rect.x), rect.height), text);
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

    private static string FormatProductionLine(SettlementKnowledgeSnapshot snapshot)
    {
        return "LW_ProductionLine".Translate(
            snapshot.AdultWorkers.Named("workers"),
            snapshot.FoodPerDay.Named("food"),
            snapshot.SteelPerDay.Named("steel"),
            snapshot.MedicinePerDay.Named("medicine"),
            snapshot.ComponentsPerDay.Named("components"),
            snapshot.Biome.Named("biome"),
            snapshot.Hilliness.Named("hilliness"),
            snapshot.TechLevel.Named("tech")).ToString();
    }

    private static string CargoBand(int quantity)
    {
        if (quantity <= 25)
        {
            return "LW_MarkerCargo_Light".Translate();
        }

        return quantity <= 100
            ? "LW_MarkerCargo_Loaded".Translate()
            : "LW_MarkerCargo_Heavy".Translate();
    }

    private static int PopulationBandMinimum(SettlementPopulationBand band)
    {
        return band switch
        {
            SettlementPopulationBand.Tiny => 1,
            SettlementPopulationBand.Small => 8,
            SettlementPopulationBand.Medium => 20,
            SettlementPopulationBand.Large => 60,
            _ => 0,
        };
    }
}
