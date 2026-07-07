using System.Runtime.CompilerServices;
using LivingWorld.Core;
using RimWorld;

namespace LivingWorld.RimWorld;

public sealed class LivingWorldRaidReservation
{
    public LivingWorldRaidReservation(EntityId armyId)
    {
        ArmyId = armyId;
    }

    public EntityId ArmyId { get; }
}

public static class LivingWorldRaidBindingRuntime
{
    private static readonly ConditionalWeakTable<IncidentParms, LivingWorldRaidReservation> Reservations = new();

    public static bool TryAddReservation(IncidentParms parms, EntityId armyId)
    {
        if (parms == null)
        {
            return false;
        }

        if (Reservations.TryGetValue(parms, out _))
        {
            return false;
        }

        Reservations.Add(parms, new LivingWorldRaidReservation(armyId));
        return true;
    }

    public static bool TryGetReservation(IncidentParms parms, out LivingWorldRaidReservation reservation)
    {
        if (parms == null)
        {
            reservation = null!;
            return false;
        }

        return Reservations.TryGetValue(parms, out reservation!);
    }
}
