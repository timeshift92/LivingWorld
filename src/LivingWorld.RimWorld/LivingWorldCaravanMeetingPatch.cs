using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Stops caravans being met "from nowhere" in the empty wilderness. Vanilla's caravan-meeting incident
/// conjures a random faction caravan next to the player's caravan wherever it happens to be. We gate it so
/// it only fires when the player's caravan is close to a plausible source of a passing caravan — a non-player
/// settlement — so a met caravan believably came from somewhere nearby instead of materializing in the
/// middle of nowhere. Far from any settlement, no meeting. Fail-open and gated by the arrivals setting.
/// </summary>
[HarmonyPatch(typeof(IncidentWorker_CaravanMeeting), "CanFireNowSub")]
public static class LivingWorldCaravanMeetingGatePatch
{
    private const int MaxMeetingDistanceTiles = 6;

    public static void Postfix(IncidentParms parms, ref bool __result)
    {
        if (!__result)
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
                    && grid.ApproxDistanceInTiles(tile, caravanTile) <= MaxMeetingDistanceTiles;
            });

            if (!nearSource)
            {
                // No plausible nearby source — do not conjure a caravan from the empty wilderness.
                __result = false;
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Caravan-meeting gate skipped safely: {ex.Message}");
        }
    }
}
