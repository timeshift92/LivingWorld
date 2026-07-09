using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace LivingWorld.RimWorld;

/// <summary>
/// Walk to an armory rack and arm up: resolve this colonist's skill-appropriate kit from the racks and
/// equip it. Fail-safe — the equip step is try/caught, so a bad state just ends the job and the pawn
/// carries on with normal behaviour.
/// </summary>
public sealed class JobDriver_FetchKit : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return true;
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedOrNull(TargetIndex.A);
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

        var equip = ToilMaker.MakeToil();
        equip.initAction = () =>
        {
            try
            {
                var (weapon, armor) = LoadoutAdapter.ResolveKit(pawn, pawn.Map);
                LoadoutAdapter.EquipKit(pawn, weapon, armor);
            }
            catch (Exception ex)
            {
                Log.Warning($"[LivingWorld] FetchKit toil failed safely: {ex.Message}");
            }
        };
        equip.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return equip;
    }
}

/// <summary>
/// Walk to an armory rack and stand down: drop the weapon and combat armour back so the colonist returns to
/// civvies and work. Fail-safe.
/// </summary>
public sealed class JobDriver_ReturnKit : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return true;
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedOrNull(TargetIndex.A);
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

        var stow = ToilMaker.MakeToil();
        stow.initAction = () =>
        {
            try
            {
                LoadoutAdapter.ReturnKit(pawn, pawn.Map);
            }
            catch (Exception ex)
            {
                Log.Warning($"[LivingWorld] ReturnKit toil failed safely: {ex.Message}");
            }
        };
        stow.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return stow;
    }
}
