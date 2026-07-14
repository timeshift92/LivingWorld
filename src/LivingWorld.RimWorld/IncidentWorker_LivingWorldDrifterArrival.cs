using System;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Storyteller-scheduled arrival that materializes a ledger drifter as a colony joiner.
/// Cadence is native (vanilla storyteller picks it from the safe Misc category) and gated
/// by the ledger via <see cref="CanFireNowSub"/>. A generated pawn remains provisional until
/// its ledger identity commits; failures preserve the person in the outside-world pool.
/// </summary>
public sealed class IncidentWorker_LivingWorldDrifterArrival : IncidentWorker
{
    protected override bool CanFireNowSub(IncidentParms parms)
    {
        if (!base.CanFireNowSub(parms))
        {
            return false;
        }

        var component = LivingWorldWorldComponent.Instance;
        return component != null && component.WantsDrifterArrival;
    }

    protected override bool TryExecuteWorker(IncidentParms parms)
    {
        var component = LivingWorldWorldComponent.Instance;
        if (component == null)
        {
            return false;
        }

        var map = parms.target as Map ?? Find.AnyPlayerHomeMap;
        if (map == null)
        {
            return false;
        }

        Pawn? pawn = null;
        var ledgerCommitted = false;
        try
        {
            var state = component.State;

            var pooled = state.Drifters
                .Where(drifter => !state.IsDrifterReserved(drifter.Id))
                .OrderBy(drifter => drifter.ArrivalTick)
                .ThenBy(drifter => drifter.Id.Value)
                .FirstOrDefault();

            var tick = Find.TickManager?.TicksGame ?? 0;
            var sex = pooled?.Sex ?? (tick % 2 == 0 ? Sex.Female : Sex.Male);
            if (!TryFindArrivalCell(map, out var spawnCell))
            {
                return false;
            }

            pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.SpaceRefugee,
                faction: Faction.OfPlayer,
                context: PawnGenerationContext.NonPlayer,
                fixedGender: sex == Sex.Female ? Gender.Female : Gender.Male));

            var identityComp = pawn.GetComp<CompLivingWorldIdentity>();
            if (identityComp == null)
            {
                identityComp = new CompLivingWorldIdentity { parent = pawn };
                pawn.AllComps.Add(identityComp);
            }

            // The spawned pawn is provisional until the ledger mutation commits. Any failure below
            // destroys it, so a reserved/missing drifter can never leave a free colonist behind.
            GenSpawn.Spawn(pawn, spawnCell, map);

            // Create an unpooled candidate before the irreversible step. If identity or spawn fails,
            // that person remains in the outside-world pool instead of disappearing. MaterializeDrifter
            // is intentionally the final operation that can throw before commit.
            var candidate = pooled ?? state.CreateDrifter(
                pawn.LabelShortCap,
                pawn.ageTracker.AgeBiologicalYears,
                sex);
            identityComp.SetLedgerId(candidate.Id);
            state.MaterializeDrifter(candidate.Id, pawn.thingIDNumber, tick);
            ledgerCommitted = true;

            try
            {
                SendStandardLetter(
                    "LW_DrifterArrivalLetterLabel".Translate(),
                    "LW_DrifterArrivalLetterText".Translate(pawn.LabelShortCap.Named("PAWN")),
                    LetterDefOf.PositiveEvent,
                    parms,
                    pawn);
            }
            catch (Exception letterError)
            {
                Log.Warning(
                    $"[LivingWorld] Drifter arrived but its letter failed: "
                    + $"{letterError.GetType().Name}: {letterError.Message}");
            }

            return true;
        }
        catch (Exception error)
        {
            if (!ledgerCommitted && pawn is { Destroyed: false })
            {
                try
                {
                    pawn.Destroy(DestroyMode.Vanish);
                }
                catch (Exception cleanupError)
                {
                    Log.Error(
                        $"[LivingWorld] Failed provisional drifter cleanup: "
                        + $"{cleanupError.GetType().Name}: {cleanupError.Message}");
                }
            }

            Log.Warning($"[LivingWorld] Drifter arrival failed, deferring to vanilla: {error}");
            return ledgerCommitted;
        }
    }

    private static bool TryFindArrivalCell(Map map, out IntVec3 cell)
    {
        return CellFinder.TryFindRandomEdgeCellWith(
            candidate => candidate.Standable(map) && !candidate.Fogged(map),
            map,
            CellFinder.EdgeRoadChance_Neutral,
            out cell);
    }
}
