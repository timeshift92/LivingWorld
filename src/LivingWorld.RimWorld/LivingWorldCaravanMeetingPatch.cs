using System;
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

            var nearSource = worldObjects.Settlements.Any(settlement =>
            {
                var faction = settlement?.Faction;
                if (faction == null || faction.IsPlayer)
                {
                    return false;
                }

                int tile = settlement!.Tile;
                return tile >= 0
                    && grid.ApproxDistanceInTiles(tile, caravanTile) <= MaxEncounterDistanceTiles;
            });

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
}

[HarmonyPatch(typeof(IncidentWorker_CaravanMeeting), "CanFireNowSub")]
public static class LivingWorldCaravanMeetingGatePatch
{
    public static void Postfix(IncidentParms parms, ref bool __result)
    {
        LivingWorldCaravanEncounterGate.GateByNearbySettlement(parms, ref __result);
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
