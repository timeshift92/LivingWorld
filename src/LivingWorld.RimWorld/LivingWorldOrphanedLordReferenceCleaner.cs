using System;
using System.Linq;
using HarmonyLib;
using Verse;

namespace LivingWorld.RimWorld;

internal static class LivingWorldOrphanedLordReferenceCleaner
{
    private const string DormancyWakeUpDefName = "SignalAction_DormancyWakeUp";

    public static int CleanAllMaps()
    {
        var maps = Find.Maps;
        if (maps == null || maps.Count == 0)
        {
            return 0;
        }

        var cleaned = 0;
        foreach (var map in maps)
        {
            try
            {
                cleaned += CleanMap(map);
            }
            catch (Exception ex)
            {
                Log.Warning($"[LivingWorld] Orphan dormancy signal repair skipped safely for map {map?.uniqueID}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        return cleaned;
    }

    public static int CleanMap(Map map)
    {
        if (map?.listerThings == null)
        {
            return 0;
        }

        var cleaned = 0;
        foreach (var thing in map.listerThings.AllThings
            .Where(IsDormancyWakeUpSignal)
            .ToList())
        {
            if (!HasOrphanedLordReference(thing, map))
            {
                continue;
            }

            try
            {
                thing.Destroy(DestroyMode.Vanish);
                cleaned++;
            }
            catch (Exception ex)
            {
                Log.Warning($"[LivingWorld] Could not remove orphaned dormancy wake-up signal {thing.ThingID}: {ex.Message}");
            }
        }

        if (cleaned > 0 && (LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging)
        {
            Log.Message($"[LivingWorld] Removed {cleaned} orphaned dormancy wake-up signal(s) with unsaved Lord references from map {map.uniqueID}.");
        }

        return cleaned;
    }

    private static bool IsDormancyWakeUpSignal(Thing thing)
    {
        return thing?.def?.defName == DormancyWakeUpDefName;
    }

    private static bool HasOrphanedLordReference(Thing thing, Map map)
    {
        try
        {
            var lordField = AccessTools.Field(thing.GetType(), "lord");
            if (lordField == null)
            {
                return false;
            }

            var lord = lordField.GetValue(thing);
            if (lord == null)
            {
                return false;
            }

            var savedLords = map.lordManager?.lords;
            return savedLords == null || !savedLords.Contains(lord);
        }
        catch (Exception ex)
        {
            if ((LivingWorldSettings.Instance ?? new LivingWorldSettings()).debugLogging)
            {
                Log.Warning($"[LivingWorld] Could not inspect dormancy wake-up signal {thing.ThingID}: {ex.Message}");
            }

            return false;
        }
    }
}
