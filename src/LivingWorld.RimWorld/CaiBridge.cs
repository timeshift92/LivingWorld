using System;
using System.Reflection;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Optional integration with CAI 5000 (Combat Extended-style advanced AI, packageId Krkr.rule56). When CAI is
/// loaded, a mobilized-and-equipped colonist is given CAI's autonomous "hunt down enemies" custom duty, so it
/// fights intelligently on its own with no player micromanagement. Accessed purely by reflection — there is no
/// assembly reference to CombatAI.dll — so the mod builds and runs whether or not CAI is installed. When CAI is
/// absent (or any reflection call fails), <see cref="TryEngage"/> returns false and the driver drafts instead.
///
/// EXPIRY IS A LIVE-TUNING VALUE: DutyExpireTicks is set long enough to outlast a normal engagement and is
/// re-issued only once per alert (the driver tracks who it engaged). If future playtesting shows pawns going
/// passive mid-fight, shorten it and have the driver re-engage on a timer.
/// </summary>
public static class CaiBridge
{
    // ~ half an in-game day. Long enough that a single HuntDownEnemies duty covers a whole raid.
    private const int DutyExpireTicks = 30000;

    private static readonly MethodInfo? HuntMethod;
    private static readonly MethodInfo? StartMethod;

    static CaiBridge()
    {
        try
        {
            var utility = GenTypes.GetTypeInAnyAssembly("CombatAI.CustomDutyUtility");
            if (utility == null)
            {
                return;
            }

            // HuntDownEnemies(IntVec3 fallbackPosition, int expireAfter, int startAfter)
            HuntMethod = utility.GetMethod(
                "HuntDownEnemies",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(IntVec3), typeof(int), typeof(int) },
                modifiers: null);

            // TryStartCustomDuty(Pawn pawn, CustomPawnDuty duty, bool returnCurDutyToQueue)
            StartMethod = utility.GetMethod("TryStartCustomDuty", BindingFlags.Public | BindingFlags.Static);
        }
        catch (Exception ex)
        {
            HuntMethod = null;
            StartMethod = null;
            Log.Warning($"[LivingWorld] CAI bridge resolution failed safely (CAI disabled): {ex.Message}");
        }
    }

    public static bool Available => HuntMethod != null && StartMethod != null;

    public static bool TryEngage(Pawn pawn)
    {
        if (!Available || pawn?.Spawned != true)
        {
            return false;
        }

        try
        {
            // Fall back to the pawn's own position; CAI drives it toward whatever enemies it can sense.
            var duty = HuntMethod!.Invoke(null, new object[] { pawn.Position, DutyExpireTicks, 0 });
            if (duty == null)
            {
                return false;
            }

            var result = StartMethod!.Invoke(null, new object[] { pawn, duty, true });
            return result is true;
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] CAI engage failed safely (will draft instead): {ex.Message}");
            return false;
        }
    }
}
