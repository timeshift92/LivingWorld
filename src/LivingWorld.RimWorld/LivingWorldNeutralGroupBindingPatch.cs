using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Binds RimWorld's neutral arrivals (visitor groups and trade caravans both derive from
/// <see cref="IncidentWorker_NeutralGroup"/>) to the persisted reservation created before departure.
/// <see cref="IncidentWorker_NeutralGroup.SpawnPawns"/> is the single generation point that returns
/// the freshly-spawned group, so a Postfix here is the neutral-arrival analogue of the raid pawn
/// generation patch. Generated humans, animals and inventory that exceed that reservation are removed
/// before the incident worker can spawn them.
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

        var reservedGroup = ApproachingGroupRuntime.CurrentGroup;
        if (reservedGroup != null)
        {
            ApproachingGroupRuntime.RecordGeneratedPawns(__result);
            if (!LivingWorldVisitorBindingService.BindReservedVisitorPawns(
                    component.State,
                    reservedGroup,
                    __result))
            {
                __result.Clear();
            }

            ApproachingGroupRuntime.RecordGeneratedPawns(__result);
            return;
        }

        var factionDefName = parms?.faction?.def?.defName;
        var compatibility = LivingWorldVisitorBindingService.BindVisitorPayload(
            component.State,
            factionDefName,
            __result);
        if (!compatibility.IsHandled)
        {
            return;
        }

        if (!compatibility.IsSuccess || compatibility.Manifest == null)
        {
            __result.Clear();
            return;
        }

        var manifestOwner = LivingWorldCompatibilityVisitorComponent.Instance;
        if (manifestOwner == null)
        {
            LivingWorldVisitorBindingService.RollbackFailedArrival(
                component.State,
                compatibility.Manifest,
                __result,
                component.State.CurrentTick,
                "compatibility visitor manifest owner unavailable");
            __result.Clear();
            return;
        }

        manifestOwner.Register(compatibility.Manifest);
    }
}
