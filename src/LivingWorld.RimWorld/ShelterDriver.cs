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

    public void Drive(Map map, ThreatTier tier, bool shelterWorthy)
    {
        var settings = LivingWorldSettings.Instance ?? new LivingWorldSettings();
        if (!settings.armoryMobilizationEnabled || !ModsConfig.OdysseyActive)
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

            // shelterWorthy is decided by the caller: a Serious threat, or a non-animal Raid. A lone dangerous
            // animal (a revenge elephant) grades Raid so the FIGHTERS deal with it, but does NOT send the whole
            // colony to cover — matching the module's stated "proportionate response" intent.
            var wantsShelter = shelterWorthy;
            var shelter = wantsShelter ? ShelterAreaService.ShelterAreaFor(map) : null;

            var diagnostics = settings.mobilizationDiagnostics;

            foreach (var pawn in colonists.ToList())
            {
                if (pawn != null)
                {
                    HandleShelter(pawn, MobilizationCandidates.IsNonCombatant(pawn),
                        MobilizationCandidates.IsBusyUrgent(pawn), wantsShelter, shelter, map, diagnostics);
                }
            }

            // Player animals shelter with the non-combatants (same allowed-area mechanic). A war-trained animal
            // is not a shelter animal (it fights, following its drafted master) — handled with isNonCombatant
            // false so that if we HAD sheltered it and it later became a combatant, its area is still restored.
            var animals = mapPawns?.SpawnedColonyAnimals?.ToList() ?? new List<Pawn>();
            foreach (var animal in animals)
            {
                if (animal != null)
                {
                    HandleShelter(animal, MobilizationCandidates.IsShelterAnimal(animal), isBusyUrgent: false,
                        wantsShelter, shelter, map, diagnostics);
                }
            }

            // Drop tracking for pawns that despawned/left so the map does not grow unbounded.
            var live = new HashSet<int>(colonists.Where(p => p != null).Select(p => p.thingIDNumber));
            live.UnionWith(animals.Where(a => a != null).Select(a => a.thingIDNumber));
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

    private void HandleShelter(Pawn pawn, bool isNonCombatant, bool isBusyUrgent, bool wantsShelter,
        Area? shelter, Map map, bool diagnostics)
    {
        var state = new NonCombatantState
        {
            IsNonCombatant = isNonCombatant,
            IsBusyUrgent = isBusyUrgent,
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
                if (diagnostics)
                {
                    Log.Message($"[LivingWorld] Shelter: {pawn.LabelShort} -> Flee");
                }

                break;

            case ShelterPhase.Restore:
                RestoreArea(pawn, map);
                if (diagnostics)
                {
                    Log.Message($"[LivingWorld] Shelter: {pawn.LabelShort} -> Restore");
                }

                break;
        }
    }

    public bool WeChangedArea(Pawn pawn) => pawn != null && prevAreaByPawn.ContainsKey(pawn.thingIDNumber);

    public int PrevAreaId(Pawn pawn)
        => pawn != null && prevAreaByPawn.TryGetValue(pawn.thingIDNumber, out var id) ? id : -1;

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
