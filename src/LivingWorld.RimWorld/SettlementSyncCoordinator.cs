using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Keeps <see cref="WorldState"/> settlements in sync with real RimWorld settlements that other
/// mods add, remove, or capture at runtime. Hook entry points do targeted single-settlement work;
/// the reconcile passes do a full tile-keyed diff as a safety net. All decisions come from the
/// pure <see cref="SettlementReconciliationService"/>; this class is glue only.
/// </summary>
public sealed class SettlementSyncCoordinator
{
    private readonly LivingWorldWorldComponent component;
    private bool syncing;

    public SettlementSyncCoordinator(LivingWorldWorldComponent component)
    {
        this.component = component ?? throw new ArgumentNullException(nameof(component));
    }

    public void OnSettlementAdded(Settlement settlement)
    {
        if (!Enter(requirePlaying: true))
        {
            return;
        }

        try
        {
            var candidate = ToCandidate(settlement);
            if (candidate == null)
            {
                return; // player settlement or unreadable — VanillaSettlementImporter returned null.
            }

            if (component.State.FindActiveSettlementByTile(candidate.Tile) != null)
            {
                return; // already tracked (e.g. bootstrap imported it).
            }

            component.SeedImportedSettlement(candidate, Settings());
        }
        finally
        {
            Leave();
        }
    }

    public void OnSettlementRemoved(Settlement settlement)
    {
        if (!Enter(requirePlaying: true))
        {
            return;
        }

        try
        {
            var ledger = component.State.FindActiveSettlementByTile(settlement.Tile);
            if (ledger == null)
            {
                return;
            }

            SettlementLifecycleService.DestroySettlement(
                component.State, ledger.Id, CurrentTick(), "settlement removed at runtime");
            component.EnsureRuinSites();
        }
        finally
        {
            Leave();
        }
    }

    public void OnSettlementFactionChanged(Settlement settlement)
    {
        if (!Enter(requirePlaying: true))
        {
            return;
        }

        try
        {
            var ledger = component.State.FindActiveSettlementByTile(settlement.Tile);
            if (ledger == null)
            {
                return; // not tracked yet; the Add hook will import it.
            }

            if (settlement.Faction?.IsPlayer == true)
            {
                // Player captured the base: it leaves the NPC simulation (LW never sims player bases).
                SettlementLifecycleService.AbandonSettlement(
                    component.State, ledger.Id, CurrentTick(), "settlement captured by player");
                return;
            }

            var newFactionId = settlement.Faction?.def?.defName;
            if (string.IsNullOrEmpty(newFactionId))
            {
                return;
            }

            SettlementLifecycleService.ChangeSettlementFaction(
                component.State, ledger.Id, newFactionId!, CurrentTick(), "settlement captured at runtime");
        }
        finally
        {
            Leave();
        }
    }

    public void ReconcileNonDestructive() => Reconcile(includeDestructions: false);

    public void ReconcileWithDestructions() => Reconcile(includeDestructions: true);

    private void Reconcile(bool includeDestructions)
    {
        if (!Enter(requirePlaying: false))
        {
            return;
        }

        try
        {
            var scan = new WorldObjectScanner().Scan();
            var facts = scan.Candidates
                .Select(c => new PhysicalSettlementFact(c.StableKey, c.Name, c.FactionId, c.Tile))
                .ToList();
            var plan = SettlementReconciliationService.ComputePlan(facts, component.State, includeDestructions);

            var candidatesByTile = new Dictionary<int, WorldObjectSettlementCandidate>();
            foreach (var candidate in scan.Candidates)
            {
                candidatesByTile[candidate.Tile] = candidate;
            }

            var settings = Settings();
            foreach (var import in plan.Imports)
            {
                if (candidatesByTile.TryGetValue(import.Tile, out var candidate))
                {
                    component.SeedImportedSettlement(candidate, settings);
                }
            }

            foreach (var change in plan.FactionChanges)
            {
                SettlementLifecycleService.ChangeSettlementFaction(
                    component.State, change.SettlementId, change.NewFactionId, CurrentTick(), "settlement ownership reconciled");
            }

            var destroyedAny = false;
            foreach (var id in plan.Destructions)
            {
                SettlementLifecycleService.DestroySettlement(
                    component.State, id, CurrentTick(), "settlement missing at reconcile");
                destroyedAny = true;
            }

            if (destroyedAny)
            {
                component.EnsureRuinSites();
            }
        }
        finally
        {
            Leave();
        }
    }

    private WorldObjectSettlementCandidate? ToCandidate(Settlement settlement)
    {
        var errors = 0;
        return new VanillaSettlementImporter().ImportCandidate(settlement, ref errors);
    }

    private static LivingWorldSettings Settings() => LivingWorldSettings.Instance ?? new LivingWorldSettings();

    private static int CurrentTick() => Find.TickManager?.TicksGame ?? 0;

    private bool Enter(bool requirePlaying)
    {
        if (syncing)
        {
            return false;
        }

        if (requirePlaying && Current.ProgramState != ProgramState.Playing)
        {
            return false;
        }

        if (!component.IsBootstrapped || component.State == null)
        {
            return false;
        }

        syncing = true;
        return true;
    }

    private void Leave() => syncing = false;
}
