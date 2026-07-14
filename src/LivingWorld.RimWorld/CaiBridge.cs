using System;
using System.Linq;
using System.Reflection;
using LivingWorld.Core;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Optional integration with CAI 5000 (packageId Krkr.rule56), reached purely by reflection — no assembly
/// reference to CombatAI.dll, so the mod builds and runs with or without CAI. On engage we (a) start a CAI
/// custom duty matched to the threat tier (Serious -> hold a line via DefendPoint; otherwise hunt enemies
/// down), and (b) enable CAI's reactive layer by setting the pawn's ThingComp_CombatAI.aiAutoControl = true —
/// without that flag CAI only follows the top-level objective and never ducks / retreats / evades (the field
/// defaults to false and nothing else sets it). On stand-down we clear the duty and the flag. Any reflection
/// failure returns false / no-ops, and the driver drafts instead.
/// </summary>
public static class CaiBridge
{
    // ~ half an in-game day. Long enough to cover a raid; the driver re-engages if the tier changes.
    private const int DutyExpireTicks = 30000;

    // Radius CAI holds around the defend anchor for a Serious threat.
    private const int DefendRadius = 10;

    private static readonly MethodInfo? HuntMethod;
    private static readonly MethodInfo? DefendMethod;
    private static readonly MethodInfo? StartMethod;
    private static readonly MethodInfo? GetTrackerMethod;
    private static readonly MethodInfo? FinishAllDutiesMethod;
    private static readonly PropertyInfo? CurDutyDefProp;
    private static readonly Type? CompType;
    private static readonly FieldInfo? AutoControlField;

    static CaiBridge()
    {
        try
        {
            var utility = GenTypes.GetTypeInAnyAssembly("CombatAI.CustomDutyUtility");
            if (utility != null)
            {
                // HuntDownEnemies(IntVec3 fallbackPosition, int expireAfter, int startAfter)
                HuntMethod = utility.GetMethod(
                    "HuntDownEnemies", BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(IntVec3), typeof(int), typeof(int) }, null);

                // DefendPoint(IntVec3 dest, int radius, bool endOnTookDamage, int expireAfter, int startAfter)
                DefendMethod = utility.GetMethod(
                    "DefendPoint", BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(IntVec3), typeof(int), typeof(bool), typeof(int), typeof(int) }, null);

                // TryStartCustomDuty(Pawn pawn, CustomPawnDuty duty, bool returnCurDutyToQueue)
                StartMethod = utility.GetMethod("TryStartCustomDuty", BindingFlags.Public | BindingFlags.Static);

                // GetPawnCustomDutyTracker(Pawn pawn)
                GetTrackerMethod = utility.GetMethod("GetPawnCustomDutyTracker", BindingFlags.Public | BindingFlags.Static);
            }

            var trackerType = GenTypes.GetTypeInAnyAssembly("CombatAI.Pawn_CustomDutyTracker");
            if (trackerType != null)
            {
                CurDutyDefProp = trackerType.GetProperty("CurDutyDef", BindingFlags.Public | BindingFlags.Instance);
                FinishAllDutiesMethod = trackerType.GetMethod("FinishAllDuties", BindingFlags.Public | BindingFlags.Instance);
            }

            CompType = GenTypes.GetTypeInAnyAssembly("CombatAI.Comps.ThingComp_CombatAI");
            if (CompType != null)
            {
                AutoControlField = CompType.GetField("aiAutoControl", BindingFlags.Public | BindingFlags.Instance);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] CAI bridge resolution failed safely (CAI disabled): {ex.Message}");
        }
    }

    public static bool Available => HuntMethod != null && StartMethod != null;

    public static bool IsAutoControlled(Pawn pawn)
    {
        try
        {
            if (CompType == null || AutoControlField == null || pawn?.AllComps == null)
            {
                return false;
            }

            var comp = pawn.AllComps.FirstOrDefault(c => CompType.IsInstanceOfType(c));
            return comp != null && AutoControlField.GetValue(comp) is true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryEngage(Pawn pawn, ThreatTier tier, IntVec3 anchor)
    {
        if (!Available || pawn?.Spawned != true)
        {
            return false;
        }

        try
        {
            object? duty;
            if (tier == ThreatTier.Serious && DefendMethod != null)
            {
                // Hold the line at the anchor rather than chasing (don't run into sappers/mechs in the open).
                duty = DefendMethod.Invoke(null, new object[] { anchor, DefendRadius, false, DutyExpireTicks, 0 });
            }
            else
            {
                // Nuisance / Raid: fall back to the pawn's own position; CAI drives it toward sensed enemies.
                duty = HuntMethod!.Invoke(null, new object[] { pawn.Position, DutyExpireTicks, 0 });
            }

            if (duty == null || StartMethod!.Invoke(null, new object[] { pawn, duty, true }) is not true)
            {
                return false;
            }

            // Required: enable CAI's reactive tactical layer for this drafted pawn.
            SetAutoControl(pawn, true);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] CAI engage failed safely (will draft instead): {ex.Message}");
            return false;
        }
    }

    public static void Disengage(Pawn pawn)
    {
        if (pawn == null)
        {
            return;
        }

        try
        {
            SetAutoControl(pawn, false);

            if (GetTrackerMethod != null && FinishAllDutiesMethod != null && CurDutyDefProp != null)
            {
                var tracker = GetTrackerMethod.Invoke(null, new object[] { pawn });
                var curDef = tracker != null ? CurDutyDefProp.GetValue(tracker) : null;
                if (tracker != null && curDef != null)
                {
                    // FinishAllDuties(DutyDef def, Thing focus = null)
                    FinishAllDutiesMethod.Invoke(tracker, new[] { curDef, null });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] CAI disengage failed safely: {ex.Message}");
        }
    }

    private static void SetAutoControl(Pawn pawn, bool value)
    {
        if (CompType == null || AutoControlField == null || pawn?.AllComps == null)
        {
            return;
        }

        var comp = pawn.AllComps.FirstOrDefault(c => CompType.IsInstanceOfType(c));
        if (comp != null)
        {
            AutoControlField.SetValue(comp, value);
        }
    }
}
