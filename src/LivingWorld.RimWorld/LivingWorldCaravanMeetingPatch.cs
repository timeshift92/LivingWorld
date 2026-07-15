using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Stops caravan-trip encounters happening "from nowhere" in the empty wilderness. Vanilla can meet a
/// caravan, spring a faction ambush, or field a demand party right next to the player's caravan wherever it
/// happens to be. We gate these so they only fire when the player's caravan is close to a plausible source
/// — a non-player settlement — so the other party believably came from somewhere nearby instead of the void.
/// Far from any settlement, no encounter. Fail-open and gated by the arrivals setting.
/// </summary>
internal static class LivingWorldCaravanEncounterGate
{
    private const int MaxEncounterDistanceTiles = 6;
    [ThreadStatic]
    private static int currentTargetTile;

    public static void Enter(IncidentParms parms)
    {
        currentTargetTile = parms?.target is Caravan caravan ? caravan.Tile : -1;
    }

    public static void Exit()
    {
        currentTargetTile = -1;
    }

    public static void ResolveMeetingFaction(ref Faction faction, ref bool result)
    {
        if (!result || currentTargetTile < 0)
        {
            return;
        }

        var source = FindSourceSettlement(currentTargetTile, faction);
        if (source?.Faction == null)
        {
            faction = null!;
            result = false;
            return;
        }

        faction = source.Faction;
    }

    // Sets result to false when the encounter has no plausible nearby source. Leaves it untouched when we
    // can't reason about it (not a caravan target, no world data, feature off).
    public static void GateByNearbySettlement(IncidentParms parms, ref bool result)
    {
        if (!result)
        {
            return;
        }

        try
        {
            var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
            if (!settings.arrivalsTravelEnabled)
            {
                return;
            }

            if (parms?.target is not Caravan caravan)
            {
                return;
            }

            int caravanTile = caravan.Tile;
            var grid = Find.WorldGrid;
            var worldObjects = Find.WorldObjects;
            if (caravanTile < 0 || grid == null || worldObjects == null)
            {
                return;
            }

            var nearSource = FindSourceSettlement(caravanTile, parms.faction) != null;

            if (!nearSource)
            {
                result = false;
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Caravan-encounter gate skipped safely: {ex.Message}");
        }
    }

    public static bool IsLedgerBacked(Faction? faction)
    {
        var factionId = faction?.def?.defName;
        var component = LivingWorldWorldComponent.Instance;
        return component != null
            && !string.IsNullOrWhiteSpace(factionId)
            && component.State.Settlements.Any(settlement => settlement.IsActive
                && string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal));
    }

    private static Settlement? FindSourceSettlement(int caravanTile, Faction? preferredFaction)
    {
        var grid = Find.WorldGrid;
        var component = LivingWorldWorldComponent.Instance;
        var settlements = Find.WorldObjects?.Settlements;
        if (grid == null || component == null || settlements == null || caravanTile < 0)
        {
            return null;
        }

        return settlements
            .Where(settlement => settlement?.Faction != null && !settlement.Faction.IsPlayer)
            .Where(settlement => preferredFaction == null || settlement!.Faction == preferredFaction)
            .Select(settlement => new
            {
                Settlement = settlement!,
                Distance = grid.ApproxDistanceInTiles(settlement!.Tile, caravanTile),
            })
            .Where(candidate => candidate.Distance <= MaxEncounterDistanceTiles)
            .Where(candidate =>
            {
                var factionId = candidate.Settlement.Faction?.def?.defName;
                return !string.IsNullOrWhiteSpace(factionId)
                    && component.State.Settlements.Any(ledger => ledger.IsActive
                        && string.Equals(ledger.FactionId, factionId, StringComparison.Ordinal)
                        && LivingWorld.Core.SettlementSlug.ParseTile(ledger.Slug) == candidate.Settlement.Tile);
            })
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Settlement.ID)
            .Select(candidate => candidate.Settlement)
            .FirstOrDefault();
    }
}

[HarmonyPatch(typeof(IncidentWorker_CaravanMeeting), "CanFireNowSub")]
public static class LivingWorldCaravanMeetingGatePatch
{
    public static void Prefix(IncidentParms parms)
    {
        LivingWorldCaravanEncounterGate.Enter(parms);
    }

    public static void Postfix(IncidentParms parms, ref bool __result)
    {
        try
        {
            LivingWorldCaravanEncounterGate.GateByNearbySettlement(parms, ref __result);
        }
        finally
        {
            LivingWorldCaravanEncounterGate.Exit();
        }
    }
}

[HarmonyPatch(typeof(IncidentWorker_CaravanMeeting), "TryFindFaction")]
public static class LivingWorldCaravanMeetingFactionPatch
{
    public static void Postfix(ref Faction __0, ref bool __result)
    {
        LivingWorldCaravanEncounterGate.ResolveMeetingFaction(ref __0, ref __result);
    }
}

[HarmonyPatch(typeof(IncidentWorker_CaravanMeeting), "TryExecuteWorker")]
public static class LivingWorldCaravanMeetingExecutionPatch
{
    public static void Prefix(IncidentParms parms)
    {
        LivingWorldCaravanEncounterGate.Enter(parms);
    }

    public static Exception? Finalizer(Exception? __exception)
    {
        LivingWorldCaravanEncounterGate.Exit();
        return __exception;
    }
}

[HarmonyPatch(typeof(IncidentWorker_CaravanMeeting), "GenerateCaravanPawns")]
public static class LivingWorldCaravanMeetingPawnPatch
{
    public static void Postfix(Faction __0, ref List<Pawn> __result)
    {
        if (__result == null || __result.Count == 0 || !LivingWorldCaravanEncounterGate.IsLedgerBacked(__0))
        {
            return;
        }

        var component = LivingWorldWorldComponent.Instance;
        var humans = __result.Count(pawn => pawn?.RaceProps?.Humanlike == true);
        var bound = component == null
            ? 0
            : LivingWorldVisitorBindingService.BindVisitorPawns(component.State, __0.def.defName, __result);
        if (bound == humans && humans > 0)
        {
            return;
        }

        foreach (var pawn in __result.Where(pawn => pawn != null && !pawn.Destroyed).ToList())
        {
            pawn.Destroy();
        }
        __result.Clear();
    }
}

[HarmonyPatch(typeof(IncidentWorker_Ambush_EnemyFaction), "CanFireNowSub")]
public static class LivingWorldCaravanAmbushGatePatch
{
    public static void Postfix(IncidentParms parms, ref bool __result)
    {
        LivingWorldCaravanEncounterGate.GateByNearbySettlement(parms, ref __result);
    }
}

[HarmonyPatch(typeof(IncidentWorker_CaravanDemand), "CanFireNowSub")]
public static class LivingWorldCaravanDemandGatePatch
{
    public static void Postfix(IncidentParms parms, ref bool __result)
    {
        LivingWorldCaravanEncounterGate.GateByNearbySettlement(parms, ref __result);
    }
}
