using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Gives mechanoid ground raids a believable source instead of "from nowhere". A Postfix on the enemy
/// raid worker: when a raid that actually fired is a mechanoid raid, it is attributed to the nearest
/// mechanoid complex on the world map (rousing one if needed), so the threat has a cause the player can
/// see. Never alters or cancels the raid — purely additive and fail-open.
/// </summary>
[HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "TryExecuteWorker")]
public static class LivingWorldMechRaidPatch
{
    public static void Postfix(IncidentParms parms, bool __result)
    {
        if (!__result || parms?.faction?.def == null)
        {
            return;
        }

        if (parms.faction.def != FactionDefOf.Mechanoid)
        {
            return;
        }

        try
        {
            LivingWorldWorldComponent.Instance?.NotifyMechanoidRaid(parms);
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Mechanoid raid attribution skipped safely: {ex.Message}");
        }
    }
}
