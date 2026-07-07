using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(IncidentWorker_Raid), "TryGenerateRaidInfo")]
public static class LivingWorldRaidPawnGenerationPatch
{
    public static void Postfix(IncidentParms parms, ref List<Pawn> pawns, bool __result)
    {
        if (!__result || pawns == null || pawns.Count == 0)
        {
            return;
        }

        var component = LivingWorldWorldComponent.Instance;
        if (component == null)
        {
            return;
        }

        if (!LivingWorldRaidBindingRuntime.TryGetReservation(parms, out var reservation))
        {
            return;
        }

        RaidPawnBindingService.BindRaidPawns(
            component.State,
            reservation.ArmyId,
            pawns.Select(pawn => pawn.thingIDNumber));
    }
}
