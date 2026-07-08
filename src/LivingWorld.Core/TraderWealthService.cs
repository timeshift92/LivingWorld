using System;

namespace LivingWorld.Core;

/// <summary>
/// Ties a faction's ledger economy to the real quality of its traders: a faction wealthier than the
/// world average sends traders carrying extra silver (more the player can sell to). Reuses the same
/// self-calibrating wealth signal as raid strength, so facilities/wealth/development matter for trade
/// as well as combat instead of being display-only analytics. A poor or average faction adds nothing
/// (no penalty — just no bonus).
/// </summary>
public static class TraderWealthService
{
    public const int MaxBonusSilver = 800;

    public static int BonusSilver(WorldState state, string factionId)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        var multiplier = FactionRaidStrengthService.WealthRaidMultiplier(state, factionId);
        if (multiplier <= 1f)
        {
            return 0;
        }

        // How far above the world average this faction sits, as 0..1 of the multiplier's headroom.
        var headroom = FactionRaidStrengthService.MaxWealthMultiplier - 1f;
        if (headroom <= 0f)
        {
            return 0;
        }

        var excess = Math.Min(1f, (multiplier - 1f) / headroom);
        return (int)(MaxBonusSilver * excess);
    }
}
