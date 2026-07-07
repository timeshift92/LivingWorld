using System;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Ledger-backed human raid incident. This is the primary Living World raid path:
/// reserve real citizens first, then let vanilla raid generation handle pawns/lords/jobs.
/// The existing generation patch binds spawned pawns because the reservation is attached
/// to the same IncidentParms.
/// </summary>
public sealed class IncidentWorker_LivingWorldFactionRaid : IncidentWorker_RaidEnemy
{
    private const float PointsPerCombatant = 100f;
    private const float MinimumRaidPoints = 35f;

    protected override bool CanFireNowSub(IncidentParms parms)
    {
        if (!base.CanFireNowSub(parms))
        {
            return false;
        }

        var component = LivingWorldWorldComponent.Instance;
        return component != null
            && component.IsBootstrapped
            && ResolveFaction(parms, component.State) != null;
    }

    protected override bool TryExecuteWorker(IncidentParms parms)
    {
        var component = LivingWorldWorldComponent.Instance;
        if (component == null)
        {
            return false;
        }

        var faction = ResolveFaction(parms, component.State);
        if (faction == null)
        {
            return false;
        }

        var requestedCombatants = EstimateRequestedCombatants(parms.points);
        var allocation = RaidPopulationAllocator.ReserveForRaid(
            component.State,
            new RaidPopulationAllocationRequest(
                faction.def.defName,
                $"Living World raid {Find.TickManager?.TicksGame ?? 0}",
                requestedCombatants));
        if (allocation.Status != RaidPopulationAllocationStatus.Success || allocation.Army == null)
        {
            return false;
        }

        if (!LivingWorldRaidBindingRuntime.TryAddReservation(parms, allocation.Army.Id))
        {
            RaidReconciliationService.ReleaseUndeployedReserves(component.State, allocation.Army.Id);
            return false;
        }

        parms.faction = faction;
        var cappedPoints = Math.Max(MinimumRaidPoints, allocation.ReservedCombatants * PointsPerCombatant);
        if (cappedPoints < parms.points)
        {
            parms.points = cappedPoints;
        }

        var executed = false;
        try
        {
            executed = base.TryExecuteWorker(parms);
            return executed;
        }
        finally
        {
            if (!executed)
            {
                Log.Warning($"[LivingWorld] Ledger raid failed after reserving {allocation.ReservedCombatants} citizens; releasing undeployed reserves.");
            }

            RaidReconciliationService.ReleaseUndeployedReserves(component.State, allocation.Army.Id);
        }
    }

    private static Faction? ResolveFaction(IncidentParms parms, WorldState state)
    {
        if (parms.faction != null && IsEligibleFaction(parms.faction) && HasRaidReadyPopulation(state, parms.faction))
        {
            return parms.faction;
        }

        return Find.FactionManager.AllFactionsListForReading
            .Where(IsEligibleFaction)
            .Where(faction => HasRaidReadyPopulation(state, faction))
            .OrderBy(faction => faction.def.defName, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool IsEligibleFaction(Faction faction)
    {
        return faction != null
            && faction.def?.humanlikeFaction == true
            && faction != Faction.OfPlayer
            && faction.HostileTo(Faction.OfPlayer);
    }

    private static bool HasRaidReadyPopulation(WorldState state, Faction faction)
    {
        return state.Settlements
            .Where(settlement => string.Equals(settlement.FactionId, faction.def.defName, StringComparison.Ordinal))
            .Any(settlement => state.GetSettlementPopulation(settlement.Id).Adults > 0);
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
