using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Binds RimWorld-generated neutral visitors (visitor groups, trade caravans) to real ledger
/// citizens through a <see cref="MaterializationPurpose.SettlementVisit"/> lease — the settlement-
/// visit consumer for Task 3. This is the mirror of the raid pawn binding: RimWorld still generates
/// the pawns, we only attach a ledger identity so the world's own people are the ones who show up.
/// <para>
/// Only the front half lives here. Fate write-back is already handled by the shared pawn-fate
/// infrastructure: a bound pawn carries <see cref="CompLivingWorldIdentity"/>, so when it leaves,
/// dies, or is captured the exit/kill/capture patches route through
/// <c>LivingWorldPawnSyncService.Apply</c>, which resolves the active lease before the raid link.
/// </para>
/// <para>Fail-safe: any missing settlement, absent citizen, or failed lease is a silent no-op so a
/// visitor incident never throws or strands the ledger.</para>
/// </summary>
public static class LivingWorldVisitorBindingService
{
    // Generous relative to a normal visit (~1 day). If the group somehow overstays, the daily
    // ReleaseExpiredLeases returns the citizen to its settlement rather than leaking the lease.
    private const int VisitLeaseLifetimeTicks = 60_000 * 5;

    public static int BindVisitorPawns(WorldState state, string? factionDefName, IReadOnlyList<Pawn>? pawns)
    {
        if (state == null || string.IsNullOrEmpty(factionDefName) || pawns == null || pawns.Count == 0)
        {
            return 0;
        }

        // Don't materialize identities while the world is still being seeded — settlements and
        // citizens are not stable yet.
        if (state.IsInitialWorldSeedingActive)
        {
            return 0;
        }

        var bindable = pawns
            .Where(pawn => pawn != null
                && !pawn.Dead
                && pawn.RaceProps?.Humanlike == true
                && pawn.GetComp<CompLivingWorldIdentity>()?.HasLedgerId != true)
            .ToList();
        if (bindable.Count == 0)
        {
            return 0;
        }

        // The visitors come from the faction's most-populous active settlement — a stable, deterministic
        // source that returns them there when they leave.
        var source = state.Settlements
            .Where(settlement => settlement.IsActive
                && string.Equals(settlement.FactionId, factionDefName, StringComparison.Ordinal))
            .OrderByDescending(settlement => state.GetSettlementPopulation(settlement.Id).Total)
            .ThenBy(settlement => settlement.Id.Value)
            .FirstOrDefault();
        if (source == null)
        {
            return 0;
        }

        // Lease only as many citizens as the source can actually spare, so CreateLeases (all-or-
        // nothing) never fails and larger groups simply bind the citizens that are available.
        var leasedCitizens = new HashSet<EntityId>(
            state.MaterializationLeases.Where(lease => lease.IsActive).Select(lease => lease.CitizenId));
        var available = state.Citizens.Count(citizen =>
            citizen.Status == CitizenStatus.Alive
            && state.GetOwner(citizen.Id) == source.Id
            && !leasedCitizens.Contains(citizen.Id));
        var count = Math.Min(bindable.Count, available);
        if (count <= 0)
        {
            return 0;
        }

        var result = MaterializationLeaseService.CreateLeases(
            state,
            new MaterializationLeaseRequest(
                source.Id,
                source.Id,
                MaterializationPurpose.SettlementVisit,
                $"visit:{factionDefName}",
                count,
                VisitLeaseLifetimeTicks));
        if (result.Status != MaterializationLeaseStatus.Success)
        {
            return 0;
        }

        var bound = 0;
        for (var i = 0; i < result.Leases.Count && i < bindable.Count; i++)
        {
            var lease = result.Leases[i];
            var pawn = bindable[i];

            var bind = MaterializationLeaseService.BindPawn(state, lease.Id, pawn.thingIDNumber);
            if (bind.Status != MaterializationLeaseBindStatus.Success)
            {
                continue;
            }

            var identity = pawn.GetComp<CompLivingWorldIdentity>();
            if (identity == null)
            {
                identity = new CompLivingWorldIdentity { parent = pawn };
                pawn.AllComps.Add(identity);
            }

            identity.SetLedgerId(lease.CitizenId);
            bound++;
        }

        return bound;
    }
}
