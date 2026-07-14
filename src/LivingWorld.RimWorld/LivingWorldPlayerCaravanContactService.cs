using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using LivingWorld.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Resolves a physical meeting between the player's caravan and a ledger-backed world group.
/// The marker is only presentation; every accepted action mutates either the real caravan, the
/// ledger, or both. This keeps contacts observable without creating free armies or goods.
/// </summary>
internal static class LivingWorldPlayerCaravanContactService
{
    private const int MaxTradeUnits = 25;
    private const float TradePriceMultiplier = 1.10f;

    public static void Handle(WorldState state, Caravan playerCaravan, WorldObject_LivingWorldArmy marker)
    {
        if (state == null || playerCaravan == null || marker == null)
        {
            return;
        }

        Find.TickManager?.Pause();
        if (marker.Faction?.HostileTo(Faction.OfPlayer) == true)
        {
            ShowHostileContact(state, playerCaravan, marker);
            return;
        }

        if (TryParseMarkerEntity(marker.MarkerKey, "caravan:", EntityKind.Caravan, out var caravanId)
            && state.GetCaravan(caravanId) is { Status: CaravanStatus.Traveling })
        {
            ShowTradeContact(state, playerCaravan, marker, caravanId);
            return;
        }

        ShowPeacefulContact(state, marker);
    }

    private static void ShowHostileContact(WorldState state, Caravan playerCaravan, WorldObject_LivingWorldArmy marker)
    {
        var body = "LW_PlayerCaravanHostileContactText".Translate(
            marker.DetailsText.Named("details"));
        Find.WindowStack?.Add(new Dialog_MessageBox(
            body,
            "LW_PlayerCaravanContactEngage".Translate(),
            () => TryStartLedgerAmbush(state, playerCaravan, marker),
            "LW_PlayerCaravanContactAvoid".Translate(),
            () => Messages.Message(
                "LW_PlayerCaravanContactAvoided".Translate(),
                new LookTargets(playerCaravan),
                MessageTypeDefOf.NeutralEvent),
            title: "LW_PlayerCaravanMarkerContactLabel".Translate()));
    }

    private static void ShowTradeContact(
        WorldState state,
        Caravan playerCaravan,
        WorldObject_LivingWorldArmy marker,
        EntityId ledgerCaravanId)
    {
        var offer = BuildTradeOffer(state, playerCaravan, ledgerCaravanId);
        if (offer == null)
        {
            ShowPeacefulContact(state, marker);
            return;
        }

        var body = "LW_PlayerCaravanTradeContactText".Translate(
            marker.DetailsText.Named("details"),
            offer.ResourceLabel.Named("resource"),
            offer.Quantity.Named("quantity"),
            offer.Price.Named("price"));
        Find.WindowStack?.Add(new Dialog_MessageBox(
            body,
            "LW_PlayerCaravanContactBuy".Translate(),
            () => ExecuteTrade(state, playerCaravan, ledgerCaravanId, offer),
            "LW_PlayerCaravanContactPass".Translate(),
            null,
            title: "LW_PlayerCaravanMarkerContactLabel".Translate()));
    }

    private static void ShowPeacefulContact(WorldState state, WorldObject_LivingWorldArmy marker)
    {
        var body = "LW_PlayerCaravanPeacefulContactText".Translate(
            marker.DetailsText.Named("details"));
        Find.WindowStack?.Add(new Dialog_MessageBox(
            body,
            "LW_PlayerCaravanContactTalk".Translate(),
            () => ResolveConversation(state, marker),
            "LW_PlayerCaravanContactPass".Translate(),
            null,
            title: "LW_PlayerCaravanMarkerContactLabel".Translate()));
    }

    private static void TryStartLedgerAmbush(
        WorldState state,
        Caravan playerCaravan,
        WorldObject_LivingWorldArmy marker)
    {
        IncidentParms? preparedParms = null;
        EntityId? preparedArmyId = null;
        try
        {
            if (!TryParseMarkerEntity(marker.MarkerKey, "army:", EntityKind.Army, out var armyId))
            {
                SendContactFailure("LW_PlayerCaravanContactNoLedgerForce".Translate());
                return;
            }

            var army = state.GetArmy(armyId);
            var movement = state.GetArmyMovement(armyId);
            var available = state.Citizens.Count(citizen =>
                citizen.Status == CitizenStatus.Alive
                && state.GetOwner(citizen.Id) == armyId
                && state.RaidPawnLinks.All(link => link.CitizenId != citizen.Id));
            if (army == null || movement?.Status != ArmyMovementStatus.Traveling || available <= 0
                || marker.Faction == null)
            {
                SendContactFailure("LW_PlayerCaravanContactNoLedgerForce".Translate());
                return;
            }

            var incident = DefDatabase<IncidentDef>.GetNamedSilentFail("Ambush");
            if (incident == null)
            {
                SendContactFailure("LW_PlayerCaravanContactFailed".Translate());
                return;
            }

            var parms = StorytellerUtility.DefaultParmsNow(incident.category, playerCaravan);
            preparedParms = parms;
            preparedArmyId = armyId;
            parms.target = playerCaravan;
            parms.faction = marker.Faction;
            parms.forced = true;
            parms.points = Math.Max(35f, Math.Min(available, Math.Max(1, marker.Combatants)) * 100f);
            if (!LivingWorldRaidBindingRuntime.TryAddReservation(parms, armyId))
            {
                SendContactFailure("LW_PlayerCaravanContactFailed".Translate());
                return;
            }

            if (!incident.Worker.TryExecute(parms))
            {
                LivingWorldCaravanAmbushGenerationRuntime.RollBack(state, parms, armyId);
                SendContactFailure("LW_PlayerCaravanContactFailed".Translate());
                return;
            }

            LivingWorldCaravanAmbushGenerationRuntime.Forget(parms);
            state.SetArmyMovementStatus(armyId, ArmyMovementStatus.Disbanded);
        }
        catch (Exception error)
        {
            if (preparedParms != null && preparedArmyId.HasValue)
            {
                LivingWorldCaravanAmbushGenerationRuntime.RollBack(state, preparedParms, preparedArmyId.Value);
            }

            Log.Warning($"[LivingWorld] Player-caravan ambush failed safely: {error.GetType().Name}: {error.Message}");
            SendContactFailure("LW_PlayerCaravanContactFailed".Translate());
        }
    }

    private static TradeOffer? BuildTradeOffer(WorldState state, Caravan playerCaravan, EntityId ledgerCaravanId)
    {
        var availableSilver = CountThing(playerCaravan, ThingDefOf.Silver);
        foreach (var resource in state.ResourcesForOwner(ledgerCaravanId)
                     .Where(resource => resource.Quantity > 0 && resource.ResourceKey != ThingDefOf.Silver.defName)
                     .OrderBy(resource => resource.ResourceKey, StringComparer.Ordinal))
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(resource.ResourceKey);
            if (def == null || def.category != ThingCategory.Item)
            {
                continue;
            }

            var unitPrice = Math.Max(1, (int)Math.Ceiling(def.BaseMarketValue * TradePriceMultiplier));
            var affordable = availableSilver / unitPrice;
            var quantity = Math.Min(Math.Min(resource.Quantity, Math.Max(1, def.stackLimit)), MaxTradeUnits);
            quantity = Math.Min(quantity, affordable);
            if (quantity > 0)
            {
                return new TradeOffer(def, def.LabelCap, quantity, quantity * unitPrice);
            }
        }

        return null;
    }

    private static void ExecuteTrade(
        WorldState state,
        Caravan playerCaravan,
        EntityId ledgerCaravanId,
        TradeOffer offer)
    {
        if (CountThing(playerCaravan, ThingDefOf.Silver) < offer.Price
            || state.GetOwnedResourceQuantity(ledgerCaravanId, offer.ResourceDef.defName) < offer.Quantity)
        {
            SendContactFailure("LW_PlayerCaravanTradeUnavailable".Translate());
            return;
        }

        var consumed = state.ConsumeResource(
            ledgerCaravanId,
            offer.ResourceDef.defName,
            offer.Quantity,
            "sold to player caravan during physical world contact");
        if (consumed != offer.Quantity)
        {
            if (consumed > 0)
            {
                state.AddResource(ledgerCaravanId, offer.ResourceDef.defName, consumed);
            }

            SendContactFailure("LW_PlayerCaravanTradeUnavailable".Translate());
            return;
        }

        if (!TryRemoveThing(playerCaravan, ThingDefOf.Silver, offer.Price))
        {
            state.AddResource(ledgerCaravanId, offer.ResourceDef.defName, offer.Quantity);
            SendContactFailure("LW_PlayerCaravanTradeUnavailable".Translate());
            return;
        }

        try
        {
            var goods = ThingMaker.MakeThing(offer.ResourceDef);
            goods.stackCount = offer.Quantity;
            playerCaravan.AddPawnOrItem(goods, addCarriedPawnToWorldPawnsIfAny: true);
            playerCaravan.RecacheInventory();
            state.AddResource(ledgerCaravanId, ThingDefOf.Silver.defName, offer.Price);
            Messages.Message(
                "LW_PlayerCaravanTradeCompleted".Translate(
                    offer.Quantity.Named("quantity"),
                    offer.ResourceLabel.Named("resource"),
                    offer.Price.Named("price")),
                new LookTargets(playerCaravan),
                MessageTypeDefOf.PositiveEvent);
        }
        catch (Exception error)
        {
            state.AddResource(ledgerCaravanId, offer.ResourceDef.defName, offer.Quantity);
            GiveThing(playerCaravan, ThingDefOf.Silver, offer.Price);
            Log.Warning($"[LivingWorld] Contact trade rolled back safely: {error.GetType().Name}: {error.Message}");
            SendContactFailure("LW_PlayerCaravanContactFailed".Translate());
        }
    }

    private static void ResolveConversation(WorldState state, WorldObject_LivingWorldArmy marker)
    {
        try
        {
            EntityId? settlementId = null;
            if (TryParseMarkerEntity(marker.MarkerKey, "mission:", EntityKind.Mission, out var missionId))
            {
                settlementId = state.GetMission(missionId)?.TargetSettlementId;
            }
            else if (TryParseMarkerEntity(marker.MarkerKey, "caravan:", EntityKind.Caravan, out var caravanId))
            {
                settlementId = state.GetCaravan(caravanId)?.TargetSettlementId;
            }

            if (settlementId.HasValue && state.GetSettlement(settlementId.Value) != null)
            {
                PlayerKnowledgeService.RecordTraderSettlementInfo(
                    state,
                    settlementId.Value,
                    "Information learned by speaking with a physically encountered world group.");
            }

            var factionId = marker.Faction?.def?.defName;
            var playerId = state.PlayerFactionId;
            if (!string.IsNullOrWhiteSpace(factionId) && !string.IsNullOrWhiteSpace(playerId))
            {
                DiplomacyService.AdjustGoodwill(state, playerId!, factionId!, 1);
                LivingWorldFactionRelations.ApplyGoodwill(factionId!, 1);
            }

            Messages.Message(
                "LW_PlayerCaravanConversationCompleted".Translate(),
                new LookTargets((PlanetTile)marker.Tile),
                MessageTypeDefOf.PositiveEvent);
        }
        catch (Exception error)
        {
            Log.Warning($"[LivingWorld] Caravan conversation failed safely: {error.GetType().Name}: {error.Message}");
            SendContactFailure("LW_PlayerCaravanContactFailed".Translate());
        }
    }

    private static int CountThing(Caravan caravan, ThingDef def)
    {
        return caravan.AllThings.Where(thing => thing?.def == def && !thing.Destroyed).Sum(thing => thing.stackCount);
    }

    private static bool TryRemoveThing(Caravan caravan, ThingDef def, int quantity)
    {
        if (quantity <= 0 || CountThing(caravan, def) < quantity)
        {
            return false;
        }

        var remaining = quantity;
        foreach (var thing in caravan.AllThings.Where(thing => thing?.def == def && !thing.Destroyed).ToList())
        {
            var take = Math.Min(remaining, thing.stackCount);
            var removed = take == thing.stackCount ? thing : thing.SplitOff(take);
            removed.Destroy(DestroyMode.Vanish);
            remaining -= take;
            if (remaining == 0)
            {
                break;
            }
        }

        caravan.RecacheInventory();
        return remaining == 0;
    }

    private static void GiveThing(Caravan caravan, ThingDef def, int quantity)
    {
        while (quantity > 0)
        {
            var thing = ThingMaker.MakeThing(def);
            thing.stackCount = Math.Min(quantity, Math.Max(1, def.stackLimit));
            quantity -= thing.stackCount;
            caravan.AddPawnOrItem(thing, addCarriedPawnToWorldPawnsIfAny: true);
        }

        caravan.RecacheInventory();
    }

    private static bool TryParseMarkerEntity(string markerKey, string prefix, EntityKind kind, out EntityId id)
    {
        id = default;
        return markerKey.StartsWith(prefix, StringComparison.Ordinal)
            && long.TryParse(markerKey.Substring(prefix.Length), out var value)
            && value > 0
            && (id = EntityId.Create(kind, value)).Value > 0;
    }

    private static void SendContactFailure(TaggedString text)
    {
        Messages.Message(text, MessageTypeDefOf.RejectInput);
    }

    private sealed record TradeOffer(ThingDef ResourceDef, TaggedString ResourceLabel, int Quantity, int Price);
}

internal static class LivingWorldCaravanAmbushGenerationRuntime
{
    private sealed class GeneratedPawnSet
    {
        public GeneratedPawnSet(IReadOnlyList<Pawn> pawns) => Pawns = pawns;

        public IReadOnlyList<Pawn> Pawns { get; }
    }

    private static readonly ConditionalWeakTable<IncidentParms, GeneratedPawnSet> Generated = new();

    public static void Remember(IncidentParms parms, IReadOnlyList<Pawn> pawns)
    {
        Generated.Remove(parms);
        Generated.Add(parms, new GeneratedPawnSet(pawns));
    }

    public static void Forget(IncidentParms parms)
    {
        Generated.Remove(parms);
    }

    public static void RollBack(WorldState state, IncidentParms parms, EntityId armyId)
    {
        if (Generated.TryGetValue(parms, out var generated))
        {
            foreach (var pawn in generated.Pawns)
            {
                var link = state.GetRaidPawnLink(pawn.thingIDNumber);
                if (link?.Status == RaidPawnLinkStatus.Active)
                {
                    RaidPawnBindingService.MarkPawnReturned(state, pawn.thingIDNumber, "caravan ambush setup failed");
                }

                if (!pawn.Destroyed)
                {
                    pawn.Destroy(DestroyMode.Vanish);
                }
            }
        }

        Generated.Remove(parms);
        RaidReconciliationService.ReleaseUndeployedReserves(state, armyId);
    }
}

/// <summary>
/// Replaces vanilla's point-budget pawn list only when a player caravan deliberately engages a
/// ledger army. One pawn is generated per real citizen, then linked before the ambush map is made.
/// </summary>
[HarmonyPatch(typeof(IncidentWorker_Ambush_EnemyFaction), "GeneratePawns")]
public static class LivingWorldCaravanAmbushPawnGenerationPatch
{
    public static bool Prefix(IncidentParms parms, ref List<Pawn> __result)
    {
        if (parms == null)
        {
            return true;
        }

        if (!LivingWorldRaidBindingRuntime.TryGetReservation(parms, out var reservation))
        {
            return true;
        }

        var component = LivingWorldWorldComponent.Instance;
        var faction = parms?.faction;
        if (component == null || faction == null)
        {
            return true;
        }

        var generated = new List<Pawn>();
        foreach (var citizen in component.State.Citizens
                     .Where(citizen => citizen.Status == CitizenStatus.Alive
                         && component.State.GetOwner(citizen.Id) == reservation.ArmyId
                         && component.State.RaidPawnLinks.All(link => link.CitizenId != citizen.Id))
                     .OrderBy(citizen => citizen.Id.Value))
        {
            Pawn? pawn = null;
            try
            {
                var kind = faction.def?.basicMemberKind ?? faction.RandomPawnKind();
                if (kind == null)
                {
                    continue;
                }

                pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                    kind,
                    faction,
                    PawnGenerationContext.NonPlayer,
                    forceGenerateNewPawn: true,
                    canGeneratePawnRelations: false,
                    fixedBiologicalAge: Math.Max(1, citizen.Age),
                    fixedChronologicalAge: Math.Max(1, citizen.Age),
                    fixedGender: citizen.Sex == Sex.Female ? Gender.Female : Gender.Male,
                    developmentalStages: DevelopmentalStage.Adult));
                if (!string.IsNullOrWhiteSpace(citizen.Name))
                {
                    pawn.Name = new NameSingle(citizen.Name);
                }

                component.State.LinkRaidPawn(pawn.thingIDNumber, citizen.Id, reservation.ArmyId);
                pawn.GetComp<CompLivingWorldIdentity>()?.SetLedgerId(citizen.Id);
                generated.Add(pawn);
            }
            catch (Exception error)
            {
                pawn?.Destroy(DestroyMode.Vanish);
                Log.Warning($"[LivingWorld] Could not materialize caravan ambusher {citizen.Id}: {error.Message}");
            }
        }

        RaidReconciliationService.ReleaseUndeployedReserves(component.State, reservation.ArmyId);
        LivingWorldCaravanAmbushGenerationRuntime.Remember(parms!, generated);
        __result = generated;
        return false;
    }
}
