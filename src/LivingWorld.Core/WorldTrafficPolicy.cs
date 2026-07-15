using System;
using System.Collections.Generic;
using System.Linq;

namespace LivingWorld.Core;

/// <summary>
/// Bounds visible strategic traffic. The limits are simulation rules rather than render filters:
/// every marker still represents a real reserved citizen or cargo, but a fresh world cannot launch
/// one identical mission per faction on the same day and flood the globe.
/// </summary>
public static class WorldTrafficPolicy
{
    public const int MaxConcurrentScouts = 3;
    public const int MaxConcurrentDiplomats = 2;
    public const int MaxConcurrentCaravans = 4;

    public static bool CanDispatchMission(WorldState state, WorldMissionKind kind, string factionId)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var traveling = state.Missions
            .Where(mission => mission.Status == WorldMissionStatus.Traveling && mission.Kind == kind)
            .ToList();
        return traveling.Count < MissionLimit(kind)
            && traveling.All(mission => !string.Equals(mission.FactionId, factionId, StringComparison.Ordinal));
    }

    public static bool CanDispatchCaravan(WorldState state, string factionId)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var traveling = state.Caravans.Where(caravan => caravan.Status == CaravanStatus.Traveling).ToList();
        return traveling.Count < MaxConcurrentCaravans
            && traveling.All(caravan => !string.Equals(caravan.FactionId, factionId, StringComparison.Ordinal));
    }

    /// <summary>
    /// Repairs legacy saves that already contain a mission wave above the current limits. Excess
    /// missions fail conservatively, return their exact reserved crew, and are removed after their
    /// disruption event has been recorded.
    /// </summary>
    public static int ReconcileExcessMissions(WorldState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var excess = new HashSet<EntityId>();
        foreach (var kind in new[] { WorldMissionKind.Scout, WorldMissionKind.Diplomat })
        {
            var active = state.Missions
                .Where(mission => mission.Status == WorldMissionStatus.Traveling && mission.Kind == kind)
                .OrderBy(mission => mission.DepartTick)
                .ThenBy(mission => mission.Id.Value)
                .ToList();

            foreach (var factionGroup in active.GroupBy(mission => mission.FactionId, StringComparer.Ordinal))
            {
                foreach (var duplicate in factionGroup.Skip(1))
                {
                    excess.Add(duplicate.Id);
                }
            }

            foreach (var overflow in active
                .Where(mission => !excess.Contains(mission.Id))
                .Skip(MissionLimit(kind)))
            {
                excess.Add(overflow.Id);
            }
        }

        foreach (var missionId in excess.OrderBy(id => id.Value))
        {
            state.FailMission(missionId, "world traffic capacity reconciliation", returnCrew: true);
            state.RemoveMissionForLedger(missionId);
        }

        return excess.Count;
    }

    /// <summary>
    /// Repairs legacy caravan waves above the global and per-faction limits. Excess caravans are
    /// recalled through the normal terminal path so their exact crew and cargo return atomically.
    /// </summary>
    public static int ReconcileExcessCaravans(WorldState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var active = state.Caravans
            .Where(caravan => caravan.Status == CaravanStatus.Traveling)
            .OrderBy(caravan => caravan.DepartTick)
            .ThenBy(caravan => caravan.Id.Value)
            .ToList();
        var excess = new HashSet<EntityId>();
        foreach (var factionGroup in active.GroupBy(caravan => caravan.FactionId, StringComparer.Ordinal))
        {
            foreach (var duplicate in factionGroup.Skip(1))
            {
                excess.Add(duplicate.Id);
            }
        }

        foreach (var overflow in active
            .Where(caravan => !excess.Contains(caravan.Id))
            .Skip(MaxConcurrentCaravans))
        {
            excess.Add(overflow.Id);
        }

        foreach (var caravanId in excess.OrderBy(id => id.Value))
        {
            state.MarkCaravanRecalled(caravanId, "world traffic capacity reconciliation");
        }

        return excess.Count;
    }

    public static ulong StableDailyFactionOrder(string factionId, int tick)
    {
        var day = Math.Max(0, tick) / 60_000;
        var text = day.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + (factionId ?? string.Empty);
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offset;
        foreach (var character in text)
        {
            hash ^= character;
            hash *= prime;
        }

        return hash;
    }

    private static int MissionLimit(WorldMissionKind kind)
    {
        return kind == WorldMissionKind.Scout ? MaxConcurrentScouts : MaxConcurrentDiplomats;
    }
}
