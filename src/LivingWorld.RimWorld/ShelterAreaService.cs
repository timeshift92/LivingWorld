using System;
using System.Linq;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Reads and sets a pawn's allowed area for the shelter mechanic, via the vanilla area system. The shelter is
/// the player-painted allowed area labelled "LW Shelter"; if there is none, non-combatants fall back to the
/// Home area (large enough that rescues are rarely blocked). Fail-safe throughout.
/// </summary>
public static class ShelterAreaService
{
    private const string ShelterLabel = "LW Shelter";

    public static Area? ShelterAreaFor(Map map)
    {
        try
        {
            var mgr = map?.areaManager;
            if (mgr == null)
            {
                return null;
            }

            var painted = mgr.AllAreas.FirstOrDefault(a => a != null && a.Label == ShelterLabel);
            return painted ?? mgr.Home;
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Shelter area lookup failed safely: {ex.Message}");
            return null;
        }
    }

    public static bool IsInShelter(Pawn pawn)
    {
        try
        {
            var current = pawn?.playerSettings?.AreaRestrictionInPawnCurrentMap;
            var shelter = pawn?.Map != null ? ShelterAreaFor(pawn.Map) : null;
            return current != null && shelter != null && current == shelter;
        }
        catch
        {
            return false;
        }
    }

    // The pawn's current allowed-area ID, or -1 when unrestricted (no area).
    public static int CurrentAreaId(Pawn pawn)
    {
        try
        {
            return pawn?.playerSettings?.AreaRestrictionInPawnCurrentMap?.ID ?? -1;
        }
        catch
        {
            return -1;
        }
    }

    public static void SetArea(Pawn pawn, Area? area)
    {
        try
        {
            if (pawn?.playerSettings != null)
            {
                pawn.playerSettings.AreaRestrictionInPawnCurrentMap = area;
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Shelter area set failed safely: {ex.Message}");
        }
    }

    public static Area? AreaById(Map map, int id)
    {
        try
        {
            if (id < 0 || map?.areaManager == null)
            {
                return null;
            }

            return map.areaManager.AllAreas.FirstOrDefault(a => a != null && a.ID == id);
        }
        catch
        {
            return null;
        }
    }
}
