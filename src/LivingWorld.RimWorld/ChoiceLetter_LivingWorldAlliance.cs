using System.Collections.Generic;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// A proactive alliance offer surfaced as a real accept/decline letter (not a buried tab button): a
/// faction at war sends an envoy proposing an alliance against a common enemy. Accepting forms the
/// alliance and bridges it to REAL RimWorld relations — the ally becomes a true ally, its enemy a true
/// enemy. This is the "make the war arc a felt event, not a number" surfacing.
/// </summary>
public sealed class ChoiceLetter_LivingWorldAlliance : ChoiceLetter
{
    public string allyFactionId = string.Empty;
    public string enemyFactionId = string.Empty;

    public override IEnumerable<DiaOption> Choices
    {
        get
        {
            var accept = new DiaOption("LW_AllianceOfferAccept".Translate())
            {
                action = AcceptAlliance,
                resolveTree = true,
            };
            yield return accept;

            var decline = new DiaOption("LW_AllianceOfferDecline".Translate())
            {
                action = () => Find.LetterStack.RemoveLetter(this),
                resolveTree = true,
            };
            yield return decline;
        }
    }

    private void AcceptAlliance()
    {
        var component = LivingWorldWorldComponent.Instance;
        if (component != null && !string.IsNullOrEmpty(allyFactionId))
        {
            var result = AllianceService.FormAlliance(
                component.State, allyFactionId, Find.TickManager?.TicksGame ?? 0);
            if (result.Status == AllianceFormStatus.Formed)
            {
                // Bridge to real relations: a true ally, and its enemy becomes the player's enemy too.
                LivingWorldFactionRelations.FormRealAlliance(allyFactionId);
                if (!string.IsNullOrEmpty(enemyFactionId))
                {
                    LivingWorldFactionRelations.FormRealEnmity(enemyFactionId);
                }
            }
        }

        Find.LetterStack.RemoveLetter(this);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref allyFactionId, "lwAllyFactionId", string.Empty);
        Scribe_Values.Look(ref enemyFactionId, "lwEnemyFactionId", string.Empty);
    }
}
