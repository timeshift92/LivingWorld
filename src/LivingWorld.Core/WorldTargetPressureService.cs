using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LivingWorld.Core;

internal static class WorldTargetPressureService
{
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
