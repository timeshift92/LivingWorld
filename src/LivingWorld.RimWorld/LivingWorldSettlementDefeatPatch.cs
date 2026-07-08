using System;
using System.Linq;
using HarmonyLib;
using LivingWorld.Core;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// When the player defeats an NPC settlement on its map, RimWorld's
/// <see cref="SettlementDefeatUtility.CheckDefeated"/> destroys it. A Prefix here — before the
/// destruction, while the settlement is still readable — records the defeat as a decisive player
/// attack in the conflict ledger, so the player becomes a belligerent in the world's wars (Slice 1
/// of player war participation). Settlement maps exist only because the player attacked, so a defeat
/// detected here is always player-caused. Purely additive: it never blocks the vanilla defeat.
/// </summary>
[HarmonyPatch(typeof(SettlementDefeatUtility), "CheckDefeated")]
public static class LivingWorldSettlementDefeatPatch
{
    public static void Prefix(Settlement factionBase)
    {
        if (factionBase?.Faction == null || factionBase.Map == null)
        {
            return;
        }

        // CheckDefeated is called speculatively each relevant tick; only record when the settlement is
        // actually defeated (all defenders down), which is what triggers the vanilla destruction.
        if (!SettlementDefeatUtility.IsDefeated(factionBase.Map, factionBase.Faction))
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
        // A fallen settlement is a decisive loss for its faction; weight the exhaustion by the garrison
        // the ledger knows about, capped so a huge settlement never produces absurd war exhaustion.
        var defenderLosses = ledgerSettlement != null
            ? Math.Min(50, Math.Max(1, component.State.GetSettlementPopulation(ledgerSettlement.Id).Total))
            : 8;
        var currentTick = Find.TickManager?.TicksGame ?? 0;

        var conflict = PlayerBelligerenceService.RecordPlayerAttack(
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

        if (conflict != null && (LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging)
        {
            Log.Message(
                $"[LivingWorld] player defeated {factionDefName} settlement '{factionBase.LabelCap}'"
                + $" -> conflict {conflict.Id} (defender losses {defenderLosses}, allies credited {alliesCredited}).");
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
