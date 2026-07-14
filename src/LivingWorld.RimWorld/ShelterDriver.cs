using System;
using System.Collections.Generic;
using System.Linq;
using LivingWorld.Core;
using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Executes the shelter phase machine over non-combatants: while the threat tier is Raid-or-worse, move each
/// non-combatant's allowed area to the shelter (recording their previous area), and restore it when the threat
/// passes. One action per pawn per recheck; the previous-area map is exported for save/load persistence so a
/// reload mid-raid still restores correctly. Fail-safe throughout.
/// </summary>
public sealed class ShelterDriver
{
    // pawn thingIDNumber -> the Area.ID they had before we sheltered them (-1 = was unrestricted).
    private readonly Dictionary<int, int> prevAreaByPawn = new();

    public void Drive(Map map, ThreatTier tier)
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        if (!settings.armoryMobilizationEnabled)
        {
            return;
        }

        try
        {
            if (map == null)
            {
                return;
            }

            // Route through a separate local for the null-conditional chain — conditional-accessing `map`
            // itself would leave Roslyn's nullable flow analysis treating `map` as maybe-null for the rest of
            // this method, even though it is guarded non-null above and declared non-nullable.
            var mapPawns = map.mapPawns;
            var colonists = mapPawns?.FreeColonistsSpawned;
            if (colonists == null)
            {
                return;
            }

            var wantsShelter = tier >= ThreatTier.Raid;
            var shelter = wantsShelter ? ShelterAreaService.ShelterAreaFor(map) : null;

            foreach (var pawn in colonists.ToList())
            {
                if (pawn == null)
                {
                    continue;
                }

                var state = new NonCombatantState
                {
                    IsNonCombatant = MobilizationCandidates.IsNonCombatant(pawn),
                    IsBusyUrgent = MobilizationCandidates.IsBusyUrgent(pawn),
                    TierWantsShelter = wantsShelter,
                    InShelterArea = ShelterAreaService.IsInShelter(pawn),
                    AreaChangedByUs = prevAreaByPawn.ContainsKey(pawn.thingIDNumber),
                };

                switch (ShelterPlan.NextAction(state))
                {
                    case ShelterPhase.Flee:
                        if (!prevAreaByPawn.ContainsKey(pawn.thingIDNumber))
                        {
                            prevAreaByPawn[pawn.thingIDNumber] = ShelterAreaService.CurrentAreaId(pawn);
                        }

                        ShelterAreaService.SetArea(pawn, shelter);
                        if (settings.mobilizationDiagnostics)
                        {
                            Log.Message($"[LivingWorld] Shelter: {pawn.LabelShort} -> Flee");
                        }
                        break;

                    case ShelterPhase.Restore:
                        RestoreArea(pawn, map);
                        if (settings.mobilizationDiagnostics)
                        {
                            Log.Message($"[LivingWorld] Shelter: {pawn.LabelShort} -> Restore");
                        }
                        break;
                }
            }

            // Drop tracking for pawns that despawned/left so the map does not grow unbounded.
            var live = new HashSet<int>(colonists.Where(p => p != null).Select(p => p.thingIDNumber));
            foreach (var goneId in prevAreaByPawn.Keys.Where(id => !live.Contains(id)).ToList())
            {
                prevAreaByPawn.Remove(goneId);
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Shelter drive failed safely: {ex.Message}");
        }
    }

    public bool WeChangedArea(Pawn pawn) => pawn != null && prevAreaByPawn.ContainsKey(pawn.thingIDNumber);

    public Dictionary<int, int> ExportPrevAreas() => new(prevAreaByPawn);

    public void ImportPrevAreas(Dictionary<int, int> data)
    {
        prevAreaByPawn.Clear();
        if (data == null)
        {
            return;
        }

        foreach (var kv in data)
        {
            prevAreaByPawn[kv.Key] = kv.Value;
        }
    }

    private void RestoreArea(Pawn pawn, Map map)
    {
        if (!prevAreaByPawn.TryGetValue(pawn.thingIDNumber, out var prevId))
        {
            return;
        }

        // Resolve the old area by id; if it was deleted (or was "unrestricted"), restore to null (whole map).
        var prevArea = ShelterAreaService.AreaById(map, prevId);
        ShelterAreaService.SetArea(pawn, prevArea);
        prevAreaByPawn.Remove(pawn.thingIDNumber);
    }
}
