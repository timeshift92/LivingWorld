using System;
using System.Collections.Generic;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Game-wide "who is a fighter" roster. A colonist is a fighter by default when their best combat skill meets
/// the threshold; the player overrides that per pawn (add a weak pawn, or pull a skilled crafter out) via the
/// Fighter gizmo. Stored as two id sets so the default tracks skill changes while explicit choices stick.
/// Auto-created by RimWorld (any GameComponent with a (Game) ctor is instantiated) and persisted with the save.
/// Fail-safe: any error reads as "not a fighter".
/// </summary>
public sealed class FightersRoster : GameComponent
{
    private HashSet<int> includedIds = new();
    private HashSet<int> excludedIds = new();

    public FightersRoster(Game game)
    {
    }

    public static FightersRoster? Get() => Current.Game?.GetComponent<FightersRoster>();

    public bool IsFighter(Pawn pawn)
    {
        try
        {
            if (pawn == null)
            {
                return false;
            }

            var id = pawn.thingIDNumber;
            if (includedIds.Contains(id))
            {
                return true;
            }

            if (excludedIds.Contains(id))
            {
                return false;
            }

            return SkillEligible(pawn);
        }
        catch
        {
            return false;
        }
    }

    public void Toggle(Pawn pawn)
    {
        try
        {
            if (pawn == null)
            {
                return;
            }

            var id = pawn.thingIDNumber;
            if (IsFighter(pawn))
            {
                // Was a fighter -> exclude.
                excludedIds.Add(id);
                includedIds.Remove(id);
            }
            else
            {
                // Was not -> include.
                includedIds.Add(id);
                excludedIds.Remove(id);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Fighter roster toggle failed safely: {ex.Message}");
        }
    }

    private static bool SkillEligible(Pawn pawn)
    {
        var shooting = pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0;
        var melee = pawn.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0;
        var threshold = LivingWorldSettings.Instance?.mobilizationSkillThreshold
                        ?? MobilizationTuning.CombatSkillThreshold;
        return LoadoutSelectionService.IsCombatEligible(shooting, melee, threshold);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref includedIds, "livingWorld_fighterIncluded", LookMode.Value);
        Scribe_Collections.Look(ref excludedIds, "livingWorld_fighterExcluded", LookMode.Value);
        includedIds ??= new HashSet<int>();
        excludedIds ??= new HashSet<int>();
    }
}
