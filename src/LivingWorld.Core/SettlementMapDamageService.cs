namespace LivingWorld.Core;

public enum SettlementMapDamageStatus
{
    Success,
    InvalidRequest,
    UnknownFacility,
    NoDamage
}

public sealed record SettlementMapDamageRequest(
    EntityId FacilityId,
    int TrackedThings,
    int SurvivingThings,
    string Reason);

public sealed record SettlementMapDamageResult(
    SettlementMapDamageStatus Status,
    string Reason,
    int DamagePercent);

public static class SettlementMapDamageService
{
    public static SettlementMapDamageResult ReconcileFacilityDamage(
        WorldState state,
        SettlementMapDamageRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.FacilityId.Kind != EntityKind.SettlementFacility
            || request.TrackedThings <= 0
            || request.SurvivingThings < 0
            || request.SurvivingThings > request.TrackedThings
            || string.IsNullOrWhiteSpace(request.Reason))
        {
            return new SettlementMapDamageResult(
                SettlementMapDamageStatus.InvalidRequest,
                "Settlement map damage requires a facility, tracked count, surviving count and reason.",
                0);
        }

        if (state.GetSettlementFacility(request.FacilityId) == null)
        {
            return new SettlementMapDamageResult(
                SettlementMapDamageStatus.UnknownFacility,
                $"Facility {request.FacilityId} does not exist.",
                0);
        }

        var destroyed = request.TrackedThings - request.SurvivingThings;
        if (destroyed <= 0)
        {
            return new SettlementMapDamageResult(
                SettlementMapDamageStatus.NoDamage,
                $"Facility {request.FacilityId} retained all tracked map structures.",
                0);
        }

        var damagePercent = Math.Max(1, Math.Min(100, destroyed * 100 / request.TrackedThings));
        SettlementFacilityService.DamageFacility(
            state,
            request.FacilityId,
            damagePercent,
            request.Reason);

        return new SettlementMapDamageResult(
            SettlementMapDamageStatus.Success,
            $"Facility {request.FacilityId} took {damagePercent}% map damage.",
            damagePercent);
    }
}
