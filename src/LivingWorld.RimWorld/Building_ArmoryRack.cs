using System.Collections.Generic;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

public enum ArmoryRackKind
{
    Weapon,
    Armor,
    Apparel,
}

/// <summary>Def-side marker telling an armory rack which kind of gear it holds.</summary>
public sealed class ArmoryRackExtension : DefModExtension
{
    public ArmoryRackKind kind = ArmoryRackKind.Weapon;
}

/// <summary>
/// A storage building that holds one kind of the colony's combat gear (weapons, armour, or clothing) while
/// the colony is stood down. Just a filtered <see cref="Building_Storage"/> plus a kind marker; the loadout
/// adapter reads <see cref="StoredItems"/> to build the pool a mobilizing colonist picks from.
/// </summary>
public sealed class Building_ArmoryRack : Building_Storage
{
    public ArmoryRackKind Kind => def?.GetModExtension<ArmoryRackExtension>()?.kind ?? ArmoryRackKind.Weapon;

    public IEnumerable<Thing> StoredItems
    {
        get
        {
            var group = slotGroup;
            if (group?.HeldThings == null)
            {
                yield break;
            }

            foreach (var thing in group.HeldThings)
            {
                if (thing != null)
                {
                    yield return thing;
                }
            }
        }
    }
}
