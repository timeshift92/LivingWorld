using System;
using HarmonyLib;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "TryResolveRaidFaction")]
public static class LivingWorldRaidIncidentPatch
{
    private const float PointsPerCombatant = 100f;
    private const float MinimumRaidPoints = 35f;

    public static void Postfix(IncidentParms parms, ref bool __result)
    {
        if (!__result)
        {
            return;
        }

        if (parms == null || parms.faction == null)
        {
            return;
        }

        if (LivingWorldRaidBindingRuntime.TryGetReservation(parms, out _))
        {
            return;
        }

        var component = LivingWorldWorldComponent.Instance;
        if (component == null || component.State.Settlements.Count == 0)
        {
            return;
        }

        var factionId = parms.faction.def?.defName;
        if (string.IsNullOrWhiteSpace(factionId))
        {
            return;
        }

        if (!RaidOpportunityService.TryConsumeBestOpportunity(component.State, factionId!, out var opportunity))
        {
            __result = false;
            return;
        }

        var requestedCombatants = EstimateRequestedCombatants(parms.points);
        requestedCombatants = Math.Min(requestedCombatants, opportunity!.CombatantDemand);
        var result = RaidPopulationAllocator.ReserveForRaid(
            component.State,
            new RaidPopulationAllocationRequest(
                factionId!,
                $"Vanilla raid {Find.TickManager?.TicksGame ?? 0}",
                requestedCombatants));

        if (result.Status != RaidPopulationAllocationStatus.Success || result.ReservedCombatants <= 0)
        {
            __result = false;
            return;
        }

        LivingWorldRaidBindingRuntime.TryAddReservation(parms, result.Army!.Id);

        var cappedPoints = Math.Max(MinimumRaidPoints, result.ReservedCombatants * PointsPerCombatant);
        if (cappedPoints < parms.points)
        {
            parms.points = cappedPoints;
        }
    }

    private static int EstimateRequestedCombatants(float raidPoints)
    {
        if (raidPoints <= 0f)
        {
            return 1;
        }

        return Math.Max(1, (int)Math.Ceiling(raidPoints / PointsPerCombatant));
    }
}
