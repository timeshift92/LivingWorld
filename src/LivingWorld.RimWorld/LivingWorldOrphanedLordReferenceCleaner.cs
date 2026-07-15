using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Verse;

namespace LivingWorld.RimWorld;

internal static class LivingWorldOrphanedLordReferenceCleaner
{
    private const string DormancyWakeUpDefName = "SignalAction_DormancyWakeUp";

    public static int CleanAllMaps()
    {
        var cleaned = 0;

        var maps = Find.Maps;
        if (maps != null)
        {
            foreach (var map in maps)
            {
                cleaned += CleanMap(map);
            }
        }

        // World pawns (colonists away in a caravan, world settlement pawns) also hold
        // reciprocal DirectPawnRelations whose otherPawn can dangle. CleanMap only scans
        // map pawns, so scan the world-pawn pool once here — before a save flushes them all.
        // Not gated on maps existing: world pawns must be swept regardless.
        cleaned += CleanOrphanedDirectPawnRelationsForWorldPawns();

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
        cleaned += CleanDesyncedPawnLordBacklinks(map);
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
        return CleanOrphanedDirectPawnRelations(map.mapPawns?.AllPawns);
    }

    private static int CleanOrphanedDirectPawnRelationsForWorldPawns()
    {
        // AllPawnsAliveOrDead covers pawns that are saved but not on any map — the exact
        // holders of orphaned relations that map-only scans miss.
        return CleanOrphanedDirectPawnRelations(Find.WorldPawns?.AllPawnsAliveOrDead);
    }

    private static int CleanOrphanedDirectPawnRelations(IEnumerable<Pawn>? pawns)
    {
        if (pawns == null)
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

                // Prune only pawns saved nowhere (destroyed/discarded) — the ones that would
                // dangle a save reference. A pawn that is merely off this map but alive and
                // saved elsewhere (caravan, world pawn, another map, a settlement visit) is a
                // legitimate lord member: removing it out-of-band here bypasses vanilla's
                // Lord.Notify_PawnLost/duty teardown and desyncs the lord, which later logs
                // "Lord lost pawn X it didn't have. Condition=LeftVoluntarily".
                if (IsPawnSavedAnywhere(pawn))
                {
                    continue;
                }

                ownedPawns.RemoveAt(index);
                cleaned++;
            }
        }

        return cleaned;
    }

    // Heal the pawn side of a broken lord backlink. Pawn.lord is a direct field; vanilla only clears it
    // inside Lord.RemovePawn, which runs solely when the lord's ownedPawns still contains the pawn. If the
    // pawn was pruned from ownedPawns out-of-band (our historic over-aggressive cleaner) or the lord self-
    // disposed, Pawn.lord dangles: GetLord() keeps returning a dead lord, so ThinkNode_JoinVoluntarilyJoinable
    // spams "Lord lost pawn X it didn't have. Condition=LeftVoluntarily" every tick, and the pawn saves an
    // un-deep-saved "lord" reference. Clear the pawn side exactly as Lord.RemovePawn does (lord + duty).
    private static int CleanDesyncedPawnLordBacklinks(Map map)
    {
        var pawns = map.mapPawns?.AllPawns;
        if (pawns == null || pawns.Count == 0)
        {
            return 0;
        }

        var lords = map.lordManager?.lords;
        var cleaned = 0;
        foreach (var pawn in pawns.ToList())
        {
            if (pawn == null)
            {
                continue;
            }

            var lord = pawn.lord;
            if (lord == null)
            {
                continue;
            }

            // A healthy backlink: the lord is still tracked by the manager AND actually owns the pawn.
            var live = lords?.Contains(lord) == true;
            var owns = lord.ownedPawns?.Contains(pawn) == true;
            if (live && owns)
            {
                continue;
            }

            pawn.lord = null;
            if (pawn.mindState != null)
            {
                pawn.mindState.duty = null;
            }

            cleaned++;
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
