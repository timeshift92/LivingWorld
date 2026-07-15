using System;
using System.Linq;
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

        var ownsFactionPopulation = component.State.Settlements.Any(settlement =>
            settlement.IsActive
            && string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal));
        if (ownsFactionPopulation
            && (LivingWorldSettings.Instance ?? new LivingWorldSettings()).travelingRaidsEnabled
            && !ApproachingRaidRuntime.FiringArrival)
        {
            // The vanilla incident has now resolved its real human faction. Convert it to the same
            // prepared, visible travel path as LivingWorld_FactionRaid. Returning false only aborts
            // vanilla's immediate pawn spawn; the committed pending raid remains in the world.
            var launched = component.TryLaunchApproachingRaid(parms, parms.faction);
            __result = false;
            if (!launched)
            {
                Log.Message($"[LivingWorld] Blocked a vanilla {factionId} raid because no conserved expedition could depart.");
            }
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
                    EstimateRequestedCombatants(parms.points),
                    EquipmentPerCombatant: 5));
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Vanilla raid population reservation skipped safely: {ex.GetType().Name}: {ex.Message}");
            if (ownsFactionPopulation)
            {
                __result = false;
            }
            return;
        }

        // Immediate mode is retained only when the user explicitly disables travelling raids.
        if (reservation.Status != RaidPopulationAllocationStatus.Success
            || reservation.Army == null
            || reservation.ReservedCombatants <= 0)
        {
            if (ownsFactionPopulation)
            {
                __result = false;
            }
            return;
        }

        if (!LivingWorldRaidBindingRuntime.TryAddReservation(parms, reservation.Army.Id))
        {
            RaidReconciliationService.ReleaseUndeployedReserves(component.State, reservation.Army.Id);
            if (ownsFactionPopulation)
            {
                __result = false;
            }
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
