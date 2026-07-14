using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LivingWorld.Core;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// When the player defeats an NPC settlement on its map, RimWorld's
/// <see cref="SettlementDefeatUtility.CheckDefeated"/> destroys it. A Prefix here — before the
/// destruction, while the settlement is still readable — records the defeat as a player intervention
/// in the world's wars: <see cref="PlayerConflictInterventionService.RecordSettlementAttack"/> pressures
/// the victim faction in every war it is fighting, and any player-allied faction at war with it is
/// credited. Settlement maps exist only because the player attacked, so a defeat detected here is
/// always player-caused. Purely additive: never blocks the vanilla defeat.
/// </summary>
[HarmonyPatch(typeof(SettlementDefeatUtility), "CheckDefeated")]
public static class LivingWorldSettlementDefeatPatch
{
    // CheckDefeated can fire more than once for the same base before it is removed, so guard against
    // recording the same defeat twice by the world object's stable ID. A settlement is destroyed on
    // defeat, so its ID never recurs within a session.
    private static readonly HashSet<int> RecordedDefeats = new();

    public static void Prefix(Settlement factionBase)
    {
        if (factionBase?.Faction == null || factionBase.Map == null)
        {
            return;
        }

        // Never treat the player's own colony as a ledger object: when the player's base is overrun,
        // IsDefeated(map, player) is true too, but this hook is only for the player defeating NPCs.
        if (factionBase.Faction.IsPlayer)
        {
            return;
        }

        // Only record an actual defeat (all defenders down), which is what triggers the vanilla removal.
        if (!SettlementDefeatUtility.IsDefeated(factionBase.Map, factionBase.Faction))
        {
            return;
        }

        // Dedup: Add returns false if this base's defeat was already recorded this session.
        if (!RecordedDefeats.Add(factionBase.ID))
        {
            return;
        }

        var component = LivingWorldWorldComponent.Instance;
        if (component == null)
        {
            return;
        }

        var factionDefName = factionBase.Faction.def?.defName;
        if (string.IsNullOrEmpty(factionDefName))
        {
            return;
        }

        var ledgerSettlement = ResolveLedgerSettlement(component.State, factionBase);
        var currentTick = Find.TickManager?.TicksGame ?? 0;
        var observedDefenderLosses = CountObservedDefenderLosses(component.State, factionBase, ledgerSettlement);

        var defeat = ledgerSettlement != null
            ? PlayerSettlementDefeatService.RecordDefeat(
                component.State,
                ledgerSettlement.Id,
                factionDefName!,
                currentTick,
                "player defeated settlement",
                observedDefenderLosses)
            : null;
        var fallbackIntervention = ledgerSettlement == null
            ? PlayerConflictInterventionService.RecordSettlementAttack(
                component.State,
                factionDefName!,
                defenderLosses: observedDefenderLosses,
                capturedSettlementId: null,
                currentTick)
            : null;

        // Slice 2: any player-allied faction at war with the one that just fell is grateful.
        var alliesCredited = AllianceService.CreditAlliesOnPlayerAttack(
            component.State,
            factionDefName!,
            AllianceService.DefaultAllyGratitude,
            currentTick);
        component.EnsureRuinSites();

        if ((LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging)
        {
            Log.Message(
                $"[LivingWorld] player defeated {factionDefName} settlement '{factionBase.LabelCap}'"
                + $" -> destroyed={defeat?.Destroyed ?? false}, refugees={defeat?.RefugeesCreated ?? 0},"
                + $" pressured {defeat?.ConflictsPressured ?? fallbackIntervention?.ConflictsPressured ?? 0} war(s),"
                + $" aggression={defeat?.AggressionRecorded ?? fallbackIntervention?.AggressionRecorded ?? false},"
                + $" allies credited {alliesCredited}.");
        }
    }

    // Ledger settlement slugs embed the RimWorld tile ("worldobject:{defName}:{tile}:{factionId}"),
    // so match the destroyed base by tile, then confirm the faction still agrees. Returns null when unmatched.
    private static WorldSettlement? ResolveLedgerSettlement(WorldState state, Settlement worldObject)
    {
        var ledger = state.FindActiveSettlementByTile(worldObject.Tile);
        if (ledger == null)
        {
            return null;
        }

        var factionId = worldObject.Faction?.def?.defName ?? "UnknownFaction";
        return string.Equals(ledger.FactionId, factionId, StringComparison.Ordinal) ? ledger : null;
    }

    private static int CountObservedDefenderLosses(
        WorldState state,
        Settlement factionBase,
        WorldSettlement? ledgerSettlement)
    {
        if (ledgerSettlement != null)
        {
            var purposeKey = LivingWorldSettlementVisitMapComponent.For(factionBase.Map)?.PurposeKey;
            if (!string.IsNullOrWhiteSpace(purposeKey))
            {
                return state.MaterializationLeases.Count(lease =>
                    lease.SourceOwnerId == ledgerSettlement.Id
                    && string.Equals(lease.PurposeKey, purposeKey, StringComparison.Ordinal)
                    && lease.Lifecycle is MaterializationLeaseLifecycle.Dead
                        or MaterializationLeaseLifecycle.Missing
                        or MaterializationLeaseLifecycle.Prisoner);
            }
        }

        return factionBase.Map.mapPawns.AllPawns.Count(pawn =>
            pawn?.Faction == factionBase.Faction
            && (pawn.Dead || pawn.IsPrisonerOfColony));
    }
}
