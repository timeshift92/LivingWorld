using System;
using HarmonyLib;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

[HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "TryResolveRaidFaction")]
public static class LivingWorldRaidIncidentPatch
{
    private const float PointsPerCombatant = 100f;
    private const float MinimumRaidPoints = 35f;

    public static void Postfix(IncidentParms parms, ref bool __result)
    {
        if (!__result || parms?.faction == null)
        {
            return;
        }

        if (LivingWorldRaidBindingRuntime.TryGetReservation(parms, out _))
        {
            // LivingWorld_FactionRaid owns the primary Living World raid path. This patch is
            // only a legacy vanilla raid fallback and must not reserve a second army.
            return;
        }

        var component = LivingWorldWorldComponent.Instance;
        if (component == null)
        {
            return;
        }

        var factionId = parms.faction.def?.defName;
        if (string.IsNullOrWhiteSpace(factionId))
        {
            return;
        }

        var interception = VanillaRaidInterceptor.TryIntercept(
            component.State,
            new VanillaRaidInterceptionRequest(
                factionId!,
                EstimateRequestedCombatants(parms.points),
                $"Vanilla raid {Find.TickManager?.TicksGame ?? 0}"));

        // Living World never cancels the raid. If it cannot supply combatants it steps aside and
        // lets vanilla generate the raid unchanged (__result stays true).
        if (interception.Action != VanillaRaidInterceptionAction.Intercepted || interception.Army == null)
        {
            return;
        }

        LivingWorldRaidBindingRuntime.TryAddReservation(parms, interception.Army.Id);

        var cappedPoints = Math.Max(MinimumRaidPoints, interception.ReservedCombatants * PointsPerCombatant);
        if (cappedPoints < parms.points)
        {
            parms.points = cappedPoints;
        }
    }

    private static int EstimateRequestedCombatants(float raidPoints)
    {
        if (raidPoints <= 0f)
        {
            return 1;
        }

        return Math.Max(1, (int)Math.Ceiling(raidPoints / PointsPerCombatant));
    }
}
