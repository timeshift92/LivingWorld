using System.Linq;
using LivingWorld.Core;
using Verse;

namespace LivingWorld.RimWorld;

public static class LivingWorldPawnIdentityService
{
    public static bool TryGetLedgerId(Pawn pawn, out EntityId id)
    {
        id = default;
        if (pawn == null)
        {
            return false;
        }

        var identityComp = pawn.GetComp<CompLivingWorldIdentity>();
        if (identityComp?.HasLedgerId == true)
        {
            id = identityComp.LedgerId;
            return true;
        }

        var component = LivingWorldWorldComponent.Instance;
        var link = component?.State.GetRaidPawnLink(pawn.thingIDNumber);
        if (link == null)
        {
            return false;
        }

        id = link.CitizenId;
        return true;
    }

    public static bool TryGetRaidPawnThingId(WorldState state, Pawn pawn, out int pawnThingId)
    {
        pawnThingId = 0;
        if (state == null || pawn == null)
        {
            return false;
        }

        if (TryGetLedgerId(pawn, out var ledgerId))
        {
            var linkFromIdentity = state.RaidPawnLinks
                .Where(link => link.CitizenId == ledgerId)
                .OrderBy(link => link.Status == RaidPawnLinkStatus.Active ? 0 : 1)
                .ThenBy(link => link.PawnThingId)
                .FirstOrDefault();

            if (linkFromIdentity != null)
            {
                pawnThingId = linkFromIdentity.PawnThingId;
                return true;
            }
        }

        var fallbackLink = state.GetRaidPawnLink(pawn.thingIDNumber);
        if (fallbackLink == null)
        {
            return false;
        }

        pawnThingId = pawn.thingIDNumber;
        return true;
    }
}
