using System;
using HarmonyLib;
using LivingWorld.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "TryResolveRaidFaction")]
[HarmonyAfter("helldan.finitepopulation", "rimworld.torann.rimwar", "com.Matathias.Empire")]
[HarmonyPriority(Priority.Last)]
public static class LivingWorldRaidIncidentPatch
{
    private const float PointsPerCombatant = 100f;
    private const float MinimumRaidPoints = 35f;

    public static void Postfix(IncidentParms parms, ref bool __result)
    {
        if (!__result || parms?.faction == null)
        {
            return;
        }

        if (LivingWorldRaidBindingRuntime.TryGetReservation(parms, out _))
        {
            // LivingWorld_FactionRaid owns the primary Living World raid path. This patch is
            // only a legacy vanilla raid fallback and must not reserve a second army.
            return;
        }

        var component = LivingWorldWorldComponent.Instance;
        if (component == null || ShouldYieldRaidOwnership(component, parms))
        {
            return;
        }

        var factionId = parms.faction.def?.defName;
        if (string.IsNullOrWhiteSpace(factionId))
        {
            return;
        }

        RaidPopulationAllocationResult reservation;
        try
        {
            // Reserve only for this raid. VanillaRaidInterceptor performs a global stale-reserve
            // sweep that cannot distinguish aborted vanilla raids from live world-war armies.
            reservation = RaidPopulationAllocator.ReserveForRaid(
                component.State,
                new RaidPopulationAllocationRequest(
                    factionId!,
                    $"Vanilla raid {Find.TickManager?.TicksGame ?? 0}",
                    EstimateRequestedCombatants(parms.points)));
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Vanilla raid population reservation skipped safely: {ex.GetType().Name}: {ex.Message}");
            return;
        }

        // Living World never cancels the raid. If it cannot supply combatants it steps aside and
        // lets vanilla generate the raid unchanged (__result stays true).
        if (reservation.Status != RaidPopulationAllocationStatus.Success
            || reservation.Army == null
            || reservation.ReservedCombatants <= 0)
        {
            return;
        }

        if (!LivingWorldRaidBindingRuntime.TryAddReservation(parms, reservation.Army.Id))
        {
            RaidReconciliationService.ReleaseUndeployedReserves(component.State, reservation.Army.Id);
            return;
        }

        RaidOpportunityService.TryConsumeBestOpportunity(component.State, factionId!, out _);

        var cappedPoints = Math.Max(MinimumRaidPoints, reservation.ReservedCombatants * PointsPerCombatant);
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

    private static bool ShouldYieldRaidOwnership(LivingWorldWorldComponent component, IncidentParms parms)
    {
        if (component.IsRimWarActive || ModsConfig.IsActive("helldan.economicsdemography"))
        {
            // Both mods reserve/price vanilla raids from their own world state. Running the fallback
            // interception too would withdraw the same conceptual population twice.
            return true;
        }

        return parms.target is Map map
            && map.Parent is Settlement settlement
            && settlement.GetType() != typeof(Settlement);
    }
}
