using System;
using HarmonyLib;
using Verse;

namespace LivingWorld.RimWorld;

// The orphaned-reference cleaner runs post-materialize and on world-component ticks, so a
// DirectPawnRelation can be orphaned in the window between the last cleaner pass and an
// autosave (or a manual save). GameDataSaveLoader.SaveGame is the single choke point every
// save path — Autosaver.DoAutosave included — funnels through, so a Prefix here flushes the
// orphaned references from every map and from world pawns right before the scribe walks the
// object graph. Without it the DebugLoadIDsSavingErrorsChecker logs the not-deep-saved warning.
[HarmonyPatch(typeof(GameDataSaveLoader), nameof(GameDataSaveLoader.SaveGame))]
public static class LivingWorldSaveHygienePatch
{
    public static void Prefix()
    {
        try
        {
            LivingWorldOrphanedLordReferenceCleaner.CleanAllMaps();
        }
        catch (Exception ex)
        {
            // Fail-safe: a cleanup hiccup must never block the save itself.
            Log.Warning($"[LivingWorld] Pre-save orphaned-reference cleanup failed safely: {ex.Message}");
        }
    }
}
