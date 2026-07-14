using System.Runtime.CompilerServices;

namespace LivingWorld.Core;

public enum ArmyReservationPurpose
{
    VanillaIncidentRaid,
    WorldWarMovement
}

public static class ArmyReservationPurposeService
{
    private static readonly ConditionalWeakTable<WorldState, Dictionary<EntityId, ArmyReservationPurpose>> Purposes = new();

    public static void Mark(WorldState state, EntityId armyId, ArmyReservationPurpose purpose)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (state.GetArmy(armyId) == null)
        {
            throw new InvalidOperationException($"Army {armyId} does not exist.");
        }

        var purposes = Purposes.GetOrCreateValue(state);
        purposes[armyId] = purpose;
    }

    public static ArmyReservationPurpose? GetPurpose(WorldState state, EntityId armyId)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (Purposes.TryGetValue(state, out var purposes)
            && purposes.TryGetValue(armyId, out var registeredPurpose))
        {
            return registeredPurpose;
        }

        var army = state.GetArmy(armyId);
        if (army == null)
        {
            return null;
        }

        // Compatibility for saves created before reservation purposes were recorded.
        if (state.GetArmyMovement(armyId) != null
            || army.Name.EndsWith(" warband", StringComparison.OrdinalIgnoreCase))
        {
            return ArmyReservationPurpose.WorldWarMovement;
        }

        if (army.Name.StartsWith("Vanilla raid", StringComparison.OrdinalIgnoreCase))
        {
            return ArmyReservationPurpose.VanillaIncidentRaid;
        }

        // Legacy saves did not record reservation purpose. Limit migration cleanup to an
        // unmistakably orphaned raid-shaped reservation: no movement, preparation, pawn link or
        // supplies. Active prepared raids and all world-war movements remain outside this scope.
        var isLegacyOrphan = army.Name.IndexOf("raid", StringComparison.OrdinalIgnoreCase) >= 0
            && !state.RaidPreparations.Any(preparation => preparation.ArmyId == armyId)
            && !state.RaidPawnLinks.Any(link => link.ArmyId == armyId)
            && state.ResourcesForOwner(armyId).Count == 0;
        return isLegacyOrphan
            ? ArmyReservationPurpose.VanillaIncidentRaid
            : null;
    }

    public static int ReleaseUndeployedReservations(
        WorldState state,
        ArmyReservationPurpose purpose)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var released = 0;
        foreach (var army in state.Armies.OrderBy(army => army.Id.Value).ToList())
        {
            if (GetPurpose(state, army.Id) != purpose)
            {
                continue;
            }

            if (purpose == ArmyReservationPurpose.VanillaIncidentRaid
                && state.GetArmyMovement(army.Id)?.Status == ArmyMovementStatus.Traveling)
            {
                continue;
            }

            released += RaidReconciliationService.ReleaseUndeployedReserves(state, army.Id);
        }

        return released;
    }
}
