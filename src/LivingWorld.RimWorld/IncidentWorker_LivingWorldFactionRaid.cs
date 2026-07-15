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
            && !component.IsRimWarActive
            && ResolveFaction(parms, component.State) != null;
    }

    protected override bool TryExecuteWorker(IncidentParms parms)
    {
        var component = LivingWorldWorldComponent.Instance;
        if (component == null)
        {
            return false;
        }

        if (component.IsRimWarActive)
        {
            return false;
        }

        // First storyteller fire: turn the raid into a warband that marches across the world map and
        // materializes on arrival, so the player sees the threat coming instead of it appearing at the
        // map edge. On arrival the world component re-fires this incident with FiringArrival set, which
        // skips this branch and runs the normal reserve-and-spawn path below. If travel can't be set up
        // (disabled, no map target, or the faction has no settlement to march from) it falls through and
        // the raid fires immediately, so a raid is never lost.
        if (!ApproachingRaidRuntime.FiringArrival)
        {
            var travelFaction = ResolveFaction(parms, component.State);
            if (travelFaction != null && component.TryLaunchApproachingRaid(parms, travelFaction))
            {
                return true;
            }
        }

        var faction = ResolveFaction(parms, component.State);
        if (faction == null)
        {
            return false;
        }

        // Ask Core for a prepared expedition instead of reserving ad-hoc: intel the faction holds
        // about the player (from trade/scouting) drives a plunder intent, otherwise generic
        // hostility. The storyteller's points set the floor; believable intel can scale the raid up.
        var storytellerCombatants = EstimateRequestedCombatants(parms.points);
        if (!RaidIntentService.TryCreateBestIntent(
                component.State,
                new RaidIntentRequest(faction.def.defName, FactionHostility.Hostile),
                out var intent)
            || intent == null)
        {
            return false;
        }

        var scaledIntent = intent with { DesiredCombatants = Math.Max(storytellerCombatants, intent.DesiredCombatants) };

        RaidPreparation preparation;
        try
        {
            preparation = RaidPreparationService.PrepareRaid(
                component.State,
                new RaidPreparationRequest(scaledIntent, "Silver", 0, RaidIntelService.DefaultTradeIntelLifetimeTicks));
        }
        catch (InvalidOperationException)
        {
            // Could not reserve real citizens (no eligible adults) — no raid.
            return false;
        }

        if (!LivingWorldRaidBindingRuntime.TryAddReservation(parms, preparation.ArmyId))
        {
            RaidReconciliationService.ReleaseUndeployedReserves(component.State, preparation.ArmyId);
            component.State.ReleaseRaidPreparation(preparation.Id);
            return false;
        }

        parms.faction = faction;
        // A wealthier faction (developed settlements, facilities, silver) fields better-equipped
        // raiders — more points per reserved combatant — a poorer one weaker ones. Still capped by the
        // storyteller's budget below, so this shifts raid quality within the budget, never past it.
        var wealthMultiplier = FactionRaidStrengthService.WealthRaidMultiplier(component.State, faction.def.defName);
        var cappedPoints = Math.Max(MinimumRaidPoints, preparation.ReservedCombatants * PointsPerCombatant * wealthMultiplier);
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
                Log.Warning($"[LivingWorld] Prepared raid failed after reserving {preparation.ReservedCombatants} citizens; releasing undeployed reserves.");
            }

            RaidReconciliationService.ReleaseUndeployedReserves(component.State, preparation.ArmyId);
            if (executed)
            {
                component.State.LaunchRaidPreparation(preparation.Id);
            }
            else
            {
                component.State.ReleaseRaidPreparation(preparation.Id);
            }
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
            .Where(settlement => settlement.IsActive)
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
