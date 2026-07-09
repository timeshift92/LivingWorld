using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LivingWorld.Core;

internal static class WorldTargetPressureService
{
    // A faction will not launch a warband at a settlement already under this much targeting pressure
    // (an in-flight army counts x3, a same-day planned attack x2). Excess would-be attackers abstain and
    // scout instead of dogpiling a single settlement — which also spreads their intel for future rounds.
    // Without this, when early-game intel leaves many factions with only one known target, they all march
    // on it despite the planned-target penalty, because there is no other eligible settlement to shift to.
    public const int MaxConcurrentTargetPressure = 6;

    public static int GetTargetPressure(
        WorldState state,
        EntityId targetSettlementId,
        IReadOnlyCollection<EntityId>? plannedTargets = null)
    {
        var pressure = 0;
        pressure += state.ArmyMovements.Count(movement =>
            movement.Status == ArmyMovementStatus.Traveling
            && movement.TargetSettlementId == targetSettlementId) * 3;
        pressure += state.Caravans.Count(caravan =>
            caravan.Status == CaravanStatus.Traveling
            && caravan.TargetSettlementId == targetSettlementId);
        pressure += state.Missions.Count(mission =>
            mission.Status == WorldMissionStatus.Traveling
            && mission.TargetSettlementId == targetSettlementId);

        if (plannedTargets != null)
        {
            pressure += plannedTargets.Count(target => target == targetSettlementId) * 2;
        }

        return pressure;
    }

    public static ulong StableTargetScore(string purpose, string factionId, EntityId targetId)
    {
        unchecked
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offsetBasis;

            Append(purpose);
            Append("|");
            Append(factionId);
            Append("|");
            Append(targetId.Kind.ToString());
            Append(":");
            Append(targetId.Value.ToString(CultureInfo.InvariantCulture));
            return hash;

            void Append(string value)
            {
                foreach (var character in value ?? string.Empty)
                {
                    hash ^= character;
                    hash *= prime;
                }
            }
        }
    }
}
