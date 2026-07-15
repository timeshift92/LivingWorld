namespace LivingWorld.Core;

public enum VanillaRaidInterceptionAction
{
    /// <summary>Living World supplied real combatants; the raid is now backed by the ledger.</summary>
    Intercepted,

    /// <summary>Living World has nothing to contribute; the vanilla raid is left exactly as-is.</summary>
    PassThrough
}

public sealed record VanillaRaidInterceptionRequest(
    string FactionId,
    int EstimatedCombatants,
    string ArmyName);

public sealed record VanillaRaidInterceptionResult(
    VanillaRaidInterceptionAction Action,
    WorldArmy? Army,
    int ReservedCombatants,
    bool ConsumedOpportunity,
    string Reason);

/// <summary>
/// Decides whether a vanilla enemy raid should be backed by real Living World population.
///
/// Key rule: Living World never cancels a vanilla raid. Either it can supply real combatants
/// (Intercepted) or it steps aside and lets vanilla generate the raid unchanged (PassThrough).
/// A standing raid opportunity from trade intel is consumed as flavour when a raid actually
/// happens, but it does NOT gate whether the raid occurs.
/// </summary>
public static class VanillaRaidInterceptor
{
    public static VanillaRaidInterceptionResult TryIntercept(
        WorldState state,
        VanillaRaidInterceptionRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        // Hand back any reservists stranded by an earlier raid that reserved combatants but never
        // deployed them (e.g. it was aborted after the faction was resolved).
        RaidReconciliationService.ReleaseAllUndeployedReserves(state);

        if (string.IsNullOrWhiteSpace(request.FactionId) || state.Settlements.Count == 0)
        {
            return PassThrough("No Living World faction data; vanilla raid left untouched.");
        }

        var requested = Math.Max(1, request.EstimatedCombatants);
        RaidPopulationAllocationResult reservation;
        try
        {
            reservation = RaidPopulationAllocator.ReserveForRaid(
                state,
                new RaidPopulationAllocationRequest(request.FactionId, request.ArmyName, requested));
        }
        catch (InvalidOperationException ex)
        {
            // Defense in depth: a residual ledger desync (e.g. a raid source that just went inactive) must
            // never crash the storyteller tick — leave the vanilla raid untouched instead.
            return PassThrough(
                $"Living World raid reservation failed safely ({ex.Message}); vanilla raid left untouched.");
        }

        if (reservation.Status != RaidPopulationAllocationStatus.Success || reservation.ReservedCombatants <= 0)
        {
            return PassThrough(
                $"No Living World combatants available for {request.FactionId}; vanilla raid left untouched.");
        }

        // Only spend an opportunity once we know the raid is actually going ahead, so a pass-through
        // never wastes it.
        var consumedOpportunity = RaidOpportunityService.TryConsumeBestOpportunity(state, request.FactionId, out _);

        return new VanillaRaidInterceptionResult(
            VanillaRaidInterceptionAction.Intercepted,
            reservation.Army,
            reservation.ReservedCombatants,
            consumedOpportunity,
            $"Living World supplied {reservation.ReservedCombatants} combatants for {request.FactionId}.");
    }

    private static VanillaRaidInterceptionResult PassThrough(string reason)
    {
        return new VanillaRaidInterceptionResult(
            VanillaRaidInterceptionAction.PassThrough,
            null,
            0,
            false,
            reason);
    }
}
