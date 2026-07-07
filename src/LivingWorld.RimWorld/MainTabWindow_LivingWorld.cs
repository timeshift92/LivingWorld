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
    private const int MaxEventRows = 20;
    private const int CacheRefreshIntervalTicks = 120;

    private Vector2 scrollPosition;
    private string? lastActionResult;
    private int cachedAtTick = -CacheRefreshIntervalTicks;
    private int cachedSettlementCount = -1;
    private int cachedArmyCount = -1;
    private int cachedOutcomeCount = -1;
    private int cachedEventCount = -1;
    private int cachedCitizenCount = -1;
    private List<string> cachedSettlementRows = new();
    private List<string> cachedArmyRows = new();
    private List<string> cachedOutcomeRows = new();
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
            + (cachedArmyRows.Count * 52f)
            + (cachedOutcomeRows.Count * 30f)
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
            && cachedEventCount == state.Events.Count
            && cachedCitizenCount == state.Citizens.Count
            && currentTick - cachedAtTick < CacheRefreshIntervalTicks)
        {
            return;
        }

        cachedAtTick = currentTick;
        cachedSettlementCount = state.Settlements.Count;
        cachedArmyCount = state.Armies.Count;
        cachedOutcomeCount = state.RaidOutcomes.Count;
        cachedEventCount = state.Events.Count;
        cachedCitizenCount = state.Citizens.Count;
        cachedSettlementRows = state.Settlements
            .OrderBy(settlement => settlement.Id.Value)
            .Take(MaxSettlementRows)
            .Select(settlement =>
            {
                var known = state.GetKnownSettlementInfo(settlement.Id);
                var knowledgeLine = FormatKnowledgeLine(known);
                return "LW_SettlementLine".Translate(
                    settlement.Name.Named("name"),
                    settlement.FactionId.Named("faction"),
                    knowledgeLine.Named("knowledge")).ToString();
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
                    outcome.Prisoner.Named("prisoner")).ToString();
            })
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
    }

    private static string FormatKnowledgeLine(KnownSettlementInfo? known)
    {
        if (known == null)
        {
            return "LW_KnowledgeUnknown".Translate().ToString();
        }

        return "LW_KnowledgeLine".Translate(
            known.SourceKind.Named("source"),
            known.Confidence.Named("confidence"),
            known.Tick.Named("tick"),
            known.PopulationBand.Named("populationBand"),
            known.Food.Named("food"),
            known.Migration.Named("migration")).ToString();
    }
}
