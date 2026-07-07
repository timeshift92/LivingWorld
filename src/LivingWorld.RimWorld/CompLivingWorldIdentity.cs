using LivingWorld.Core;
using Verse;

namespace LivingWorld.RimWorld;

public sealed class CompProperties_LivingWorldIdentity : CompProperties
{
    public CompProperties_LivingWorldIdentity()
    {
        compClass = typeof(CompLivingWorldIdentity);
    }
}

/// <summary>
/// Carries the ledger identity (<see cref="EntityId"/>) on a materialized pawn so the
/// world ledger — not the fragile thingIDNumber — is the durable link. Serialized with
/// the pawn, so it survives save/load and travel into WorldPawns.
/// </summary>
public sealed class CompLivingWorldIdentity : ThingComp
{
    private int ledgerKind;
    private long ledgerValue;

    public bool HasLedgerId => ledgerValue > 0;

    public EntityId LedgerId
    {
        get => EntityId.Create((EntityKind)ledgerKind, ledgerValue);
        set
        {
            ledgerKind = (int)value.Kind;
            ledgerValue = value.Value;
        }
    }

    public void SetLedgerId(EntityId id) => LedgerId = id;

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref ledgerKind, "livingWorld_ledgerKind", 0);
        Scribe_Values.Look(ref ledgerValue, "livingWorld_ledgerValue", 0L);
    }
}
