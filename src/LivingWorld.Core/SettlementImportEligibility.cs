using System;

namespace LivingWorld.Core;

/// <summary>
/// Engine-agnostic snapshot of the faction that owns a world settlement, carrying only the fields the
/// import-eligibility decision needs. The RimWorld importer fills this from <c>Faction</c>/<c>FactionDef</c>;
/// headless tests construct it directly. Keeping the decision here (in Core) makes the "is this a real NPC
/// neighbour we should simulate, or a player-owned/structural settlement we must leave alone" rule testable
/// without the RimWorld runtime.
/// </summary>
public readonly record struct SettlementFactionDescriptor(
    string FactionDefName,
    bool IsPlayer,
    float SettlementGenerationWeight,
    bool CanMakeRandomly);

/// <summary>
/// Decides whether a world settlement should be imported into the Living World ledger as an NPC settlement.
/// Living World simulates the <em>organic</em> outside world (famine, births, wars, capture); settlements the
/// game never spawns naturally — the player's own colony, and player-management / structural factions placed
/// by other mods — must never be swept into that simulation.
/// </summary>
public static class SettlementImportEligibility
{
    /// <summary>
    /// Empire (Matathias.Empire) manages the player's vassal colonies under a dedicated faction with this
    /// defName. It is NOT <c>Faction.OfPlayer</c>, so it slips past a plain <c>IsPlayer</c> filter and would
    /// otherwise be imported as an ordinary NPC settlement — then simulated (famine/births), warred against,
    /// or "captured" out from under the player. Excluded explicitly so the guard is self-documenting and
    /// stable even if the def's generation flags ever change.
    /// </summary>
    public const string EmpirePlayerColonyFactionDefName = "PColony";

    public static bool ShouldImport(SettlementFactionDescriptor faction)
    {
        // The player's own colony is the active map, not a ledger NPC settlement. Never import it, so the
        // world war can never silently target/capture the player's base or collapse the player faction
        // (see docs/design/world-war-open-gaps.md, G1).
        if (faction.IsPlayer)
        {
            return false;
        }

        // Empire's player-owned vassal colonies wear a hidden, non-player faction. Excluded by name so the
        // guard is explicit and cannot regress if the mod ever changes the def's generation flags.
        if (string.Equals(faction.FactionDefName, EmpirePlayerColonyFactionDefName, StringComparison.Ordinal))
        {
            return false;
        }

        // Generalize past the single known mod: a faction with zero settlement-generation weight AND that the
        // world generator will never create randomly is, by RimWorld's own semantics, never a natural NPC
        // neighbour. Any settlements it owns were placed by a mod's own logic (player-empire holders, unique
        // structural factions), so they are not part of the organic world Living World simulates — keep them
        // out. Note the AND: real neighbours are randomly creatable (canMakeRandomly == true), so they are
        // kept regardless of weight, and weighted unique factions (e.g. Royalty's Empire) are kept too.
        if (faction.SettlementGenerationWeight <= 0f && !faction.CanMakeRandomly)
        {
            return false;
        }

        return true;
    }
}
