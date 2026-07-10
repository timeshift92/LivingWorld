using System;
using System.Collections;
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
            cleaned += CleanMap(map);
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
        cleaned += CleanOrphanedLordOwnedPawns(map);
        cleaned += CleanOrphanedDirectPawnRelations(map);
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

    private static int CleanOrphanedDirectPawnRelations(Map map)
    {
        var pawns = map.mapPawns?.AllPawns;
        if (pawns == null || pawns.Count == 0)
        {
            return 0;
        }

        var cleaned = 0;
        foreach (var pawn in pawns.ToList())
        {
            var directRelations = pawn?.relations?.DirectRelations;
            if (directRelations == null || directRelations.Count == 0)
            {
                continue;
            }

            for (var index = directRelations.Count - 1; index >= 0; index--)
            {
                var relation = directRelations[index];
                if (relation == null)
                {
                    directRelations.RemoveAt(index);
                    cleaned++;
                    continue;
                }

                var otherPawnField = AccessTools.Field(relation.GetType(), "otherPawn");
                if (otherPawnField?.GetValue(relation) is not Pawn otherPawn)
                {
                    continue;
                }

                if (IsPawnSavedAnywhere(otherPawn))
                {
                    continue;
                }

                directRelations.RemoveAt(index);
                cleaned++;
            }
        }

        return cleaned;
    }

    private static int CleanOrphanedLordOwnedPawns(Map map)
    {
        var lords = map.lordManager?.lords;
        if (lords == null || lords.Count == 0)
        {
            return 0;
        }

        var cleaned = 0;
        foreach (var lord in lords.ToList())
        {
            if (lord == null)
            {
                continue;
            }

            var ownedPawnsField = AccessTools.Field(lord.GetType(), "ownedPawns");
            if (ownedPawnsField?.GetValue(lord) is not IList ownedPawns)
            {
                continue;
            }

            for (var index = ownedPawns.Count - 1; index >= 0; index--)
            {
                if (ownedPawns[index] is not Pawn pawn)
                {
                    ownedPawns.RemoveAt(index);
                    cleaned++;
                    continue;
                }

                if (IsPawnDeepSavedByMap(map, pawn))
                {
                    continue;
                }

                ownedPawns.RemoveAt(index);
                cleaned++;
            }
        }

        return cleaned;
    }

    private static bool IsDormancyWakeUpSignal(Thing thing)
    {
        return thing?.def?.defName == DormancyWakeUpDefName;
    }

    private static bool HasOrphanedLordReference(Thing thing, Map map)
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

    private static bool IsPawnDeepSavedByMap(Map map, Pawn pawn)
    {
        if (pawn.Destroyed || pawn.Discarded || pawn.Map != map)
        {
            return false;
        }

        return map.mapPawns?.AllPawns?.Contains(pawn) == true;
    }

    private static bool IsPawnSavedAnywhere(Pawn pawn)
    {
        if (pawn.Destroyed || pawn.Discarded)
        {
            return false;
        }

        if (Find.Maps?.Any(map => IsPawnDeepSavedByMap(map, pawn)) == true)
        {
            return true;
        }

        if (Find.WorldPawns?.Contains(pawn) == true)
        {
            return true;
        }

        return Find.WorldObjects?.Caravans?.Any(caravan =>
            caravan?.PawnsListForReading?.Contains(pawn) == true) == true;
    }
}
