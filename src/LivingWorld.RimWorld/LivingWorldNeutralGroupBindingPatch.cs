using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Binds RimWorld's neutral arrivals (visitor groups and trade caravans both derive from
/// <see cref="IncidentWorker_NeutralGroup"/>) to ledger citizens through a settlement-visit lease.
/// <see cref="IncidentWorker_NeutralGroup.SpawnPawns"/> is the single generation point that returns
/// the freshly-spawned group, so a Postfix here is the neutral-arrival analogue of the raid pawn
/// generation patch. Purely additive: it never blocks or alters the arrival.
/// </summary>
[HarmonyPatch(typeof(IncidentWorker_NeutralGroup), "SpawnPawns")]
public static class LivingWorldNeutralGroupBindingPatch
{
    public static void Postfix(IncidentParms parms, ref List<Pawn> __result)
    {
        if (__result == null || __result.Count == 0)
        {
            return;
        }

        var component = LivingWorldWorldComponent.Instance;
        if (component == null)
        {
            return;
        }

        var factionDefName = parms?.faction?.def?.defName;
        LivingWorldVisitorBindingService.BindVisitorPawns(component.State, factionDefName, __result);
    }
}
