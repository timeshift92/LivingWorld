using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(IncidentWorker_Raid), "TryGenerateRaidInfo")]
[HarmonyAfter("helldan.finitepopulation", "rimworld.torann.rimwar", "com.Matathias.Empire")]
[HarmonyPriority(Priority.Last)]
public static class LivingWorldRaidPawnGenerationPatch
{
    public static void Postfix(IncidentParms parms, ref List<Pawn> pawns, bool __result)
    {
        var component = LivingWorldWorldComponent.Instance;
        if (component == null)
        {
            return;
        }

        if (!LivingWorldRaidBindingRuntime.TryGetReservation(parms, out var reservation))
        {
            return;
        }

        if (!__result || pawns == null || pawns.Count == 0)
        {
            if (ApproachingRaidRuntime.ArrivingRaid == null)
            {
                RaidReconciliationService.ReleaseUndeployedReserves(component.State, reservation.ArmyId);
            }
            return;
        }

        try
        {
            RaidPawnBindingService.BindRaidPawns(
                component.State,
                reservation.ArmyId,
                pawns.Select(pawn => pawn.thingIDNumber));
            foreach (var pawn in pawns)
            {
                var link = component.State.GetRaidPawnLink(pawn.thingIDNumber);
                if (link == null)
                {
                    continue;
                }

                // Human ThingDefs declare CompLivingWorldIdentity. Modded races without the comp
                // keep the persisted raid-link/thingID fallback; never mutate their comp schema.
                pawn.GetComp<CompLivingWorldIdentity>()?.SetLedgerId(link.CitizenId);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Raid pawn binding skipped safely: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            // Any reservist that did not get a generated pawn stands down and returns to its source.
            RaidReconciliationService.ReleaseUndeployedReserves(component.State, reservation.ArmyId);
        }
    }
}
