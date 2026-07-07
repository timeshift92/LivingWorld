namespace LivingWorld.Core;

/// <summary>
/// Returns reserved-but-undeployed raid combatants to their settlements.
///
/// A raid reserves combatants (transferring citizens to a <see cref="WorldArmy"/>) before the
/// actual pawns are generated. If fewer pawns spawn than were reserved — or the raid is aborted
/// before any pawn is generated — the surplus citizens would otherwise be stranded in the army
/// forever, silently draining the settlement. This service hands those idle reservists back.
/// </summary>
public static class RaidReconciliationService
{
    public static int ReleaseUndeployedReserves(WorldState state, EntityId armyId)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var army = state.GetArmy(armyId);
        return army == null
            ? 0
            : ReleaseForArmy(state, army);
    }

    public static int ReleaseAllUndeployedReserves(WorldState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var released = 0;
        foreach (var army in state.Armies.OrderBy(army => army.Id.Value).ToList())
        {
            released += ReleaseForArmy(state, army);
        }

        return released;
    }

    private static int ReleaseForArmy(WorldState state, WorldArmy army)
    {
        // A citizen counts as "deployed" only if it is bound to a raid pawn link (active or already
        // resolved as dead/returned/prisoner/missing). Everything else the army still owns is a
        // reservist that never actually left for the raid.
        var deployedCitizenIds = new HashSet<EntityId>(
            state.RaidPawnLinks
                .Where(link => link.ArmyId == army.Id)
                .Select(link => link.CitizenId));

        var undeployed = state.Citizens
            .Where(citizen =>
                citizen.Status == CitizenStatus.Alive
                && state.GetOwner(citizen.Id) == army.Id
                && !deployedCitizenIds.Contains(citizen.Id))
            .OrderBy(citizen => citizen.Id.Value)
            .ToList();

        var released = 0;
        foreach (var citizen in undeployed)
        {
            var transfer = state.TransferAsset(
                citizen.Id,
                army.Id,
                army.SourceSettlementId,
                "reservist stood down without deploying");
            if (transfer.Status == OwnershipTransferStatus.Success)
            {
                released++;
            }
        }

        return released;
    }
}
