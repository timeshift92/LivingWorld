using System.Linq;
using LivingWorld.Core;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Declares the identity comp on every loaded humanlike race before any game is loaded. Runtime-only
/// additions to Pawn.AllComps are not reconstructed by ThingWithComps during load; putting the comp on
/// the ThingDef makes its serialized ledger id durable for vanilla humans and modded humanlikes alike.
/// </summary>
[StaticConstructorOnStartup]
public static class LivingWorldIdentityDefInjector
{
    static LivingWorldIdentityDefInjector()
    {
        foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading
            .Where(def => def?.race?.Humanlike == true)
            .OrderBy(def => def.defName))
        {
            def.comps ??= new System.Collections.Generic.List<CompProperties>();
            if (def.comps.Any(properties =>
                properties is CompProperties_LivingWorldIdentity
                || properties?.compClass == typeof(CompLivingWorldIdentity)))
            {
                continue;
            }

            def.comps.Add(new CompProperties_LivingWorldIdentity());
        }
    }
}

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
/// LivingWorld_PawnIdentity.xml registers <see cref="CompProperties_LivingWorldIdentity"/>
/// on the vanilla Human ThingDef, while <see cref="LivingWorldIdentityDefInjector"/> declares it on
/// every loaded humanlike race. Newly loaded and generated pawns therefore get a ThingDef-declared
/// comp that RimWorld can restore with the pawn. Materialization still creates it defensively if a
/// late-mutating mod bypasses the declaration.
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
