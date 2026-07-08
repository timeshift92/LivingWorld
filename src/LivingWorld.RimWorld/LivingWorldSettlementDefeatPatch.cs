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
        // A fallen settlement is a decisive loss for its faction; weight the pressure by the garrison
        // the ledger knows about, capped so a huge settlement never produces absurd war exhaustion.
        var defenderLosses = ledgerSettlement != null
            ? Math.Min(50, Math.Max(1, component.State.GetSettlementPopulation(ledgerSettlement.Id).Total))
            : 8;
        var currentTick = Find.TickManager?.TicksGame ?? 0;

        var intervention = PlayerConflictInterventionService.RecordSettlementAttack(
            component.State,
            factionDefName!,
            defenderLosses,
            ledgerSettlement?.Id,
            currentTick);

        // Slice 2: any player-allied faction at war with the one that just fell is grateful.
        var alliesCredited = AllianceService.CreditAlliesOnPlayerAttack(
            component.State,
            factionDefName!,
            AllianceService.DefaultAllyGratitude,
            currentTick);

        if ((LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging)
        {
            Log.Message(
                $"[LivingWorld] player defeated {factionDefName} settlement '{factionBase.LabelCap}'"
                + $" -> pressured {intervention.ConflictsPressured} war(s), aggression={intervention.AggressionRecorded},"
                + $" allies credited {alliesCredited} (losses {defenderLosses}).");
        }
    }

    // Ledger settlement slugs embed the RimWorld tile ("worldobject:{defName}:{tile}:{factionId}"),
    // so match the destroyed base by its faction plus that tile token. Returns null when unmatched.
    private static WorldSettlement? ResolveLedgerSettlement(WorldState state, Settlement worldObject)
    {
        var factionId = worldObject.Faction?.def?.defName ?? "UnknownFaction";
        var tileToken = $":{worldObject.Tile}:";
        return state.Settlements.FirstOrDefault(candidate =>
            string.Equals(candidate.FactionId, factionId, StringComparison.Ordinal)
            && candidate.Slug.Contains(tileToken));
    }
}
