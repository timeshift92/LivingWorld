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
    private List<(string AllyFactionId, string EnemyFactionId, string Label)> cachedAllianceOffers = new();
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
            + (cachedActiveWarbandRows.Count * 26f)
            + (cachedFactionStrengthRows.Count * 26f)
            + (cachedWarHistoryRows.Count * 24f)
            + 30f
            + (cachedFactionEconomyRows.Count * 26f)
            + 30f
            + (cachedWatcherRows.Count * 26f)
            + 30f
            + (cachedConflictRows.Count * 24f)
            + (cachedAllianceOffers.Count * 26f)
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

        // Player war participation (Slice 2): propose an alliance against a common enemy. The button is
        // the player's envoy — clicking it sends a diplomatic overture that, if the faction is at war and
        // reconcilable, forms the alliance (an Ally-stance relation). Offered only for neutral factions.
        foreach (var offer in cachedAllianceOffers)
        {
            if (Widgets.ButtonText(new Rect(0f, y, viewRect.width, 24f), offer.Label))
            {
                var result = AllianceService.FormAlliance(
                    component.State, offer.AllyFactionId, Find.TickManager?.TicksGame ?? 0);
                lastActionResult = (result.Status == AllianceFormStatus.Formed
                    ? "LW_AllianceFormed".Translate(ResolveFactionName(offer.AllyFactionId).Named("faction"))
                    : "LW_AllianceFailed".Translate(ResolveFactionName(offer.AllyFactionId).Named("faction"))).ToString();

                // Slice 3: the new ally names the enemy as your war objective.
                if (result.Status == AllianceFormStatus.Formed)
                {
                    Find.LetterStack?.ReceiveLetter(
                        "LW_AllianceObjectiveLetterLabel".Translate(),
                        "LW_AllianceObjectiveLetterText".Translate(
                            ResolveFactionName(offer.AllyFactionId).Named("ally"),
                            ResolveFactionName(offer.EnemyFactionId).Named("enemy")),
                        LetterDefOf.PositiveEvent);
                }

                RefreshCachedRows(state);
            }

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
        var strengthRaw = state.Settlements
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
                return (FactionId: factionId, Text: "LW_FactionStrengthLine".Translate(
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

        var economyRaw = state.Settlements
            .Select(settlement => settlement.FactionId)
            .Distinct(System.StringComparer.Ordinal)
            .OrderBy(factionId => factionId, System.StringComparer.Ordinal)
            .Take(MaxWarRows)
            .Select(factionId =>
            {
                // E4b: read the priced ledger wealth (silver + valued material) the economy sim
                // records each day; fall back to a raw material sum only before the first daily
                // refresh has run.
                var stock = state.GetFactionWealth(factionId)?.TotalWealth
                    ?? state.Settlements
                        .Where(settlement => string.Equals(settlement.FactionId, factionId, System.StringComparison.Ordinal))
                        .Sum(settlement => FactionMaterialStock(state, settlement.Id));
                var wealth = debugExact ? stock.ToString() : WealthBand(stock);
                return (FactionId: factionId, Text: "LW_FactionEconomyLine".Translate(
                    factionId.Named("faction"),
                    wealth.Named("wealth")).ToString(), Value: stock);
            })
            .ToList();
        var maxStock = economyRaw.Count > 0 ? economyRaw.Max(row => row.Value) : 0;
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

        // Alliance offers (Slice 2): for each ongoing war the player is not part of, offer to ally with a
        // neutral participant against the other. Deduped per prospective ally, capped like the war rows.
        cachedAllianceOffers = new List<(string AllyFactionId, string EnemyFactionId, string Label)>();
        var playerId = state.PlayerFactionId;
        if (!string.IsNullOrEmpty(playerId))
        {
            var offered = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            foreach (var conflict in state.Conflicts
                .Where(conflict => conflict.Status == WorldConflictStatus.Active
                    && !conflict.Involves(playerId!))
                .OrderByDescending(conflict => conflict.WarExhaustionA + conflict.WarExhaustionB)
                .ThenBy(conflict => conflict.Id.Value))
            {
                TryAddAllianceOffer(state, playerId!, conflict.FactionA, conflict.FactionB, offered);
                TryAddAllianceOffer(state, playerId!, conflict.FactionB, conflict.FactionA, offered);
                if (cachedAllianceOffers.Count >= MaxWarRows)
                {
                    break;
                }
            }
        }
    }

    // Offer an alliance with a neutral, not-yet-allied faction against its current enemy. Irreconcilable
    // and hostile factions are Hostile stance and excluded; already-allied factions are excluded too.
    private void TryAddAllianceOffer(
        WorldState state,
        string playerId,
        string allyFactionId,
        string enemyFactionId,
        System.Collections.Generic.HashSet<string> offered)
    {
        if (string.Equals(allyFactionId, playerId, System.StringComparison.Ordinal)
            || offered.Contains(allyFactionId)
            || AllianceService.IsAlliedWithPlayer(state, allyFactionId)
            || DiplomacyService.GetStance(state, playerId, allyFactionId) != RelationStance.Neutral)
        {
            return;
        }

        offered.Add(allyFactionId);
        cachedAllianceOffers.Add((
            allyFactionId,
            enemyFactionId,
            "LW_ProposeAllianceButton".Translate(
                ResolveFactionName(allyFactionId).Named("faction"),
                ResolveFactionName(enemyFactionId).Named("enemy")).ToString()));
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
