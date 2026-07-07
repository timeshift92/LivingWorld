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
/// world ledger — not the fragile thingIDNumber — is the intended durable link.
/// <para>
/// This slice attaches the comp at materialization via <c>Pawn.AllComps.Add</c> and
/// serializes the id in <see cref="PostExposeData"/>. Note: durable save/load round-trip
/// additionally requires registering <see cref="CompProperties_LivingWorldIdentity"/> on
/// the pawn ThingDef's &lt;comps&gt; (a def patch) — RimWorld only recreates comps that the
/// ThingDef declares. Until that registration lands, a runtime-attached comp will not be
/// rebuilt on load. Harmless for now (nothing reads <see cref="LedgerId"/> back yet); the
/// durable identity map is a separate slice — see docs/design/storyteller-normalization.md.
/// </para>
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
