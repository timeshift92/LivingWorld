using System;
using System.Collections.Generic;
using System.Linq;

namespace LivingWorld.Core;

/// <summary>
/// The world-war action a faction chooses to spend its power on this step. Mirrors the six
/// verbs of a living faction — attack, drop-raid, expand, trade, scout, negotiate — plus doing
/// nothing while it accumulates.
/// </summary>
public enum WarAction
{
    None,
    Warband,
    Settler,
    Caravan,
    ScoutingParty,
    Diplomat,
    Develop,
}

/// <summary>A faction's intended action for a day, with a target where the action needs one.</summary>
public sealed record FactionActionPlan(string FactionId, WarAction Action, EntityId? TargetSettlementId);

/// <summary>
/// Decides, per faction, which world-war action its <see cref="FactionBehavior"/> and accumulated
/// power call for. This is the "brain": a pure, deterministic planner that produces intentions —
/// it does not itself move armies or found settlements (the daily tick executes the plan by
/// reusing the existing army/founding/trade/intel services). Passive archetypes and factions below
/// the action-power floor plan <see cref="WarAction.None"/>.
/// </summary>
public static class FactionActionPlanner
{
    public const int MinimumActionPower = 300;

    public static FactionActionPlan Plan(WorldState state, string factionId, int tick)
    {
        return Plan(state, factionId, tick, Array.Empty<EntityId>());
    }

    private static FactionActionPlan Plan(
        WorldState state,
        string factionId,
        int tick,
        IReadOnlyCollection<EntityId> plannedTargets)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var profile = FactionBehaviorService.GetProfile(state.GetFactionBehavior(factionId));
        if (!profile.ParticipatesInWorldWar || FactionPower(state, factionId) < MinimumActionPower)
        {
            return new FactionActionPlan(factionId, WarAction.None, null);
        }

        var developmentTarget = FindDevelopmentTarget(state, factionId, plannedTargets);
        var target = FindEnemyTarget(state, factionId, tick, plannedTargets);

        var action = profile.Behavior switch
        {
            FactionBehavior.Warmonger => target.HasValue ? WarAction.Warband : WarAction.ScoutingParty,
            FactionBehavior.Aggressive => target.HasValue ? WarAction.Warband : WarAction.ScoutingParty,
            FactionBehavior.Expansionist => developmentTarget.HasValue ? WarAction.Develop : WarAction.Settler,
            FactionBehavior.Merchant => developmentTarget.HasValue ? WarAction.Develop : WarAction.Caravan,
            FactionBehavior.Cautious => developmentTarget.HasValue ? WarAction.Develop : WarAction.ScoutingParty,
            FactionBehavior.Random => DeterministicRandomAction(tick, target.HasValue),
            _ => WarAction.None,
        };

        // Only a warband spends itself against a specific enemy settlement; the rest act at home.
        var planTarget = action == WarAction.Warband
            ? target
            : action == WarAction.Develop
                ? developmentTarget
                : null;
        return new FactionActionPlan(factionId, action, planTarget);
    }

    public static WarAction ChooseAction(WorldState state, string factionId, int tick)
    {
        return Plan(state, factionId, tick).Action;
    }

    public static IReadOnlyList<FactionActionPlan> PlanDay(WorldState state, int tick)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var plannedTargets = new List<EntityId>();
        var plans = new List<FactionActionPlan>();
        foreach (var factionId in state.Settlements
            .Where(settlement => settlement.IsActive)
            .Select(settlement => settlement.FactionId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(factionId => factionId, StringComparer.Ordinal))
        {
            var plan = Plan(state, factionId, tick, plannedTargets);
            if (plan.Action == WarAction.None)
            {
                continue;
            }

            plans.Add(plan);
            if (plan.TargetSettlementId.HasValue)
            {
                plannedTargets.Add(plan.TargetSettlementId.Value);
            }
        }

        return plans;
    }

    private static int FactionPower(WorldState state, string factionId)
    {
        return state.GetFactionDerivedAggregate(factionId).Power.CombatPower;
    }

    private static EntityId? FindEnemyTarget(
        WorldState state,
        string factionId,
        int tick,
        IReadOnlyCollection<EntityId> plannedTargets)
    {
        var enemy = state.Settlements
            .Where(settlement => settlement.IsActive)
            .Where(settlement => !string.Equals(settlement.FactionId, factionId, StringComparison.Ordinal))
            .Where(settlement => !state.IsPlayerFaction(settlement.FactionId))
            .Where(settlement => !ConflictService.IsTruceActive(state, factionId, settlement.FactionId, tick))
            .Where(settlement => DiplomacyService.GetStance(state, factionId, settlement.FactionId) != RelationStance.Ally)
            .Where(settlement => state.HasFactionSettlementIntel(factionId, settlement.Id))
            // Do not pile onto a settlement already saturated with attackers: excess factions fall through
            // to scouting (see Plan), which prevents the whole world marching on one target and spreads
            // their intel so later rounds diversify.
            .Where(settlement => WorldTargetPressureService.GetTargetPressure(state, settlement.Id, plannedTargets)
                < WorldTargetPressureService.MaxConcurrentTargetPressure)
            .OrderBy(settlement => WorldTargetPressureService.GetTargetPressure(state, settlement.Id, plannedTargets))
            .ThenBy(settlement => WorldTargetPressureService.StableTargetScore("enemy", factionId, settlement.Id))
            .ThenBy(settlement => settlement.Id.Value)
            .FirstOrDefault();

        return enemy?.Id;
    }

    private static EntityId? FindDevelopmentTarget(
        WorldState state,
        string factionId,
        IReadOnlyCollection<EntityId> plannedTargets)
    {
        var settlement = state.Settlements
            .Where(candidate => candidate.IsActive)
            .Where(candidate => string.Equals(candidate.FactionId, factionId, StringComparison.Ordinal))
            .Where(candidate => state.GetOwnedResourceQuantity(candidate.Id, "Silver") >= 100)
            .Where(candidate =>
            {
                var population = state.GetSettlementPopulation(candidate.Id).Total;
                var housing = state.GetSettlementCapability(candidate.Id)?.HousingCapacity ?? 0;
                return population > 0 && housing <= population;
            })
            .OrderBy(candidate => WorldTargetPressureService.GetTargetPressure(state, candidate.Id, plannedTargets))
            .ThenBy(candidate => WorldTargetPressureService.StableTargetScore("develop", factionId, candidate.Id))
            .ThenBy(candidate => candidate.Id.Value)
            .FirstOrDefault();
        return settlement?.Id;
    }

    private static WarAction DeterministicRandomAction(int tick, bool hasTarget)
    {
        // Deterministic (no RNG): cycle by simulated day so a Random faction varies over time
        // without breaking reproducibility.
        var options = hasTarget
            ? new[] { WarAction.Warband, WarAction.Settler, WarAction.Caravan, WarAction.ScoutingParty, WarAction.Diplomat }
            : new[] { WarAction.Settler, WarAction.Caravan, WarAction.ScoutingParty, WarAction.ScoutingParty, WarAction.Diplomat };

        var day = Math.Max(0, tick) / 60_000;
        return options[day % options.Length];
    }
}
