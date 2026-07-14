using System.Linq;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Bridges the ledger's player-war-participation model to <b>real</b> RimWorld faction relations, so
/// alliances and victories actually change the player's standing (real allies help in fights, trade,
/// and stop raiding) instead of only moving ledger numbers. Fail-open: unknown, defeated, non-humanlike
/// or permanent-enemy factions are skipped and nothing throws. Also stands down entirely when Rim War
/// (Torann.RimWar) is active — it hooks <see cref="Faction.TryAffectGoodwillWith"/> and would silently
/// neutralize these changes — deferring real faction diplomacy to it (see <see cref="DeferToRimWar"/>).
/// </summary>
public static class LivingWorldFactionRelations
{
    // Goodwill that makes a neutral faction a real RimWorld ally (vanilla ally threshold is 75).
    public const int AllyGoodwillTarget = 75;

    // Goodwill that makes a faction a real RimWorld enemy (well below the hostility threshold).
    public const int EnemyGoodwillTarget = -80;

    /// <summary>Raise the player's real goodwill with the faction up to the alliance threshold.</summary>
    public static bool FormRealAlliance(string factionDefName)
    {
        if (DeferToRimWar("alliance", factionDefName))
        {
            return false;
        }

        var player = Faction.OfPlayer;
        var other = ResolveReconcilableFaction(factionDefName);
        if (player == null || other == null)
        {
            return false;
        }

        var gap = AllyGoodwillTarget - other.GoodwillWith(player);
        if (gap > 0)
        {
            other.TryAffectGoodwillWith(player, gap, canSendMessage: false, canSendHostilityLetter: false, reason: null);
        }

        return true;
    }

    /// <summary>
    /// Drive the player's real goodwill with the faction down to the hostility threshold — the cost of
    /// picking a side: the ally's enemy becomes the player's enemy too (and starts raiding).
    /// </summary>
    public static bool FormRealEnmity(string factionDefName)
    {
        if (DeferToRimWar("enmity", factionDefName))
        {
            return false;
        }

        var player = Faction.OfPlayer;
        var other = ResolveReconcilableFaction(factionDefName);
        if (player == null || other == null)
        {
            return false;
        }

        var gap = EnemyGoodwillTarget - other.GoodwillWith(player);
        if (gap < 0)
        {
            // canSendHostilityLetter so the player is told they are now at war with this faction.
            other.TryAffectGoodwillWith(player, gap, canSendMessage: false, canSendHostilityLetter: true, reason: null);
        }

        return true;
    }

    /// <summary>Apply a real goodwill delta with the faction (e.g. a victory windfall).</summary>
    public static bool ApplyGoodwill(string factionDefName, int delta)
    {
        if (DeferToRimWar("victory", factionDefName))
        {
            return false;
        }

        var player = Faction.OfPlayer;
        var other = ResolveReconcilableFaction(factionDefName);
        if (player == null || other == null || delta == 0)
        {
            return false;
        }

        other.TryAffectGoodwillWith(player, delta, canSendMessage: false, canSendHostilityLetter: false, reason: null);
        return true;
    }

    /// <summary>
    /// True when the caller should stand down instead of writing real goodwill. Rim War (Torann.RimWar)
    /// installs a Harmony prefix on <see cref="Faction.TryAffectGoodwillWith"/> that takes goodwillChange
    /// by ref and can silently dampen, zero, or block it to keep its own diplomacy in control — so a
    /// forced alliance may never reach +75 nor a war declaration fall to -80, with no warning. When Rim
    /// War is active Living World defers real faction diplomacy to it (the same mutual exclusion it applies
    /// to its own world-war loop) rather than pushing a mutation Rim War would quietly neutralize.
    /// </summary>
    private static bool DeferToRimWar(string action, string factionDefName)
    {
        if (!ModsConfig.IsActive("Torann.RimWar"))
        {
            return false;
        }

        if (LivingWorldSettings.Instance?.debugLogging == true)
        {
            Log.Message(
                $"[LivingWorld] Rim War active — skipping real {action} goodwill for '{factionDefName}'; "
                + "Rim War manages faction diplomacy.");
        }

        return true;
    }

    private static Faction? ResolveReconcilableFaction(string factionDefName)
    {
        if (string.IsNullOrEmpty(factionDefName))
        {
            return null;
        }

        var other = Find.FactionManager?.AllFactionsListForReading
            .FirstOrDefault(faction => faction?.def?.defName == factionDefName);
        if (other == null
            || other.IsPlayer
            || other.defeated
            || other.def == null
            || !other.def.humanlikeFaction
            || other.def.permanentEnemy)
        {
            return null;
        }

        return other;
    }
}
