using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Recipe worker for the armory repair bench. Vanilla has no gear repair, so each completed bill iteration
/// restores the most-damaged weapon or piece of apparel sitting on the colony's armory racks back to full
/// hit points. The steel cost and work time come from the recipe/bill; this worker only applies the repair.
/// Reuses the entire vanilla workbench/bill/hauling pipeline — no custom WorkGiver or JobDriver. Fail-safe:
/// any error just skips the repair, the bill still completes.
/// </summary>
public class RecipeWorker_RepairArmoryGear : RecipeWorker
{
    public override void Notify_IterationCompleted(Pawn billDoer, List<Thing> ingredients)
    {
        try
        {
            var map = billDoer?.Map;
            if (map == null)
            {
                return;
            }

            var damaged = ArmorySources.All(map)
                .SelectMany(source => ArmorySources.StoredItems(source.building))
                .Where(IsRepairable)
                .OrderBy(thing => (float)thing.HitPoints / thing.MaxHitPoints)
                .FirstOrDefault();
            if (damaged == null)
            {
                return;
            }

            damaged.HitPoints = damaged.MaxHitPoints;
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Armory gear repair skipped safely: {ex.Message}");
        }
    }

    private static bool IsRepairable(Thing thing)
    {
        return thing?.def != null
               && (thing.def.IsWeapon || thing is Apparel)
               && thing.def.useHitPoints
               && thing.HitPoints < thing.MaxHitPoints;
    }
}
