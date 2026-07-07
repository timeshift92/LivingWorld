using System;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Storyteller-scheduled arrival that materializes a ledger drifter as a colony joiner.
/// Cadence is native (vanilla storyteller picks it from the AllyArrival category) and
/// gated by the ledger via <see cref="CanFireNowSub"/>. Fail-open throughout: any failure
/// leaves the game unchanged.
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

        try
        {
            var state = component.State;

            var pooled = state.Drifters
                .OrderBy(drifter => drifter.ArrivalTick)
                .ThenBy(drifter => drifter.Id.Value)
                .FirstOrDefault();

            var tick = Find.TickManager?.TicksGame ?? 0;
            var sex = pooled?.Sex ?? (tick % 2 == 0 ? Sex.Female : Sex.Male);

            var pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.SpaceRefugee,
                faction: Faction.OfPlayer,
                context: PawnGenerationContext.NonPlayer,
                fixedGender: sex == Sex.Female ? Gender.Female : Gender.Male));

            // Spawn first: only mutate the ledger once the pawn is actually placed on the
            // map, so a spawn failure never consumes a drifter with no pawn to show for it.
            GenSpawn.Spawn(pawn, CellFinder.RandomEdgeCell(map), map);

            EntityId ledgerId;
            if (pooled != null)
            {
                ledgerId = state.MaterializeDrifter(pooled.Id, pawn.thingIDNumber, tick).Id;
            }
            else
            {
                ledgerId = state
                    .MaterializeNewArrival(pawn.thingIDNumber, tick, pawn.LabelShortCap, pawn.ageTracker.AgeBiologicalYears, sex)
                    .Id;
            }

            var identityComp = pawn.GetComp<CompLivingWorldIdentity>();
            if (identityComp == null)
            {
                identityComp = new CompLivingWorldIdentity { parent = pawn };
                pawn.AllComps.Add(identityComp);
            }

            identityComp.SetLedgerId(ledgerId);

            SendStandardLetter(
                "LW_DrifterArrivalLetterLabel".Translate(),
                "LW_DrifterArrivalLetterText".Translate(pawn.LabelShortCap.Named("PAWN")),
                LetterDefOf.PositiveEvent,
                parms,
                pawn);
            return true;
        }
        catch (Exception error)
        {
            Log.Warning($"[LivingWorld] Drifter arrival failed, deferring to vanilla: {error}");
            return false;
        }
    }
}
