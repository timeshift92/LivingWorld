using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

public sealed record WorldObjectSettlementCandidate(
    string StableKey,
    string Name,
    string FactionId,
    int Tile,
    string DefName,
    bool IsVanillaSettlement);

public sealed record WorldObjectScanSummary(
    int TotalWorldObjects,
    int VanillaSettlementSourceCount,
    int FactionWorldObjectSourceCount,
    int ImportableWorldObjectSourceCount,
    int ScanErrorCount,
    string RejectedWorldObjectTypes);

public sealed record WorldObjectScanResult(
    WorldObjectScanSummary Summary,
    IReadOnlyList<WorldObjectSettlementCandidate> Candidates);

public interface IWorldObjectImporter
{
    bool CanImport(WorldObject obj);

    WorldObjectSettlementCandidate? ImportCandidate(WorldObject obj, ref int scanErrorCount);
}

public sealed class WorldObjectScanner
{
    private readonly IReadOnlyList<IWorldObjectImporter> importers;

    public WorldObjectScanner()
        : this(new IWorldObjectImporter[] { new VanillaSettlementImporter() })
    {
    }

    internal WorldObjectScanner(IReadOnlyList<IWorldObjectImporter> importers)
    {
        this.importers = importers.Count > 0
            ? importers
            : throw new ArgumentException("At least one world object importer is required.", nameof(importers));
    }

    public WorldObjectScanResult Scan()
    {
        var worldObjects = Find.WorldObjects.AllWorldObjects
            .Where(obj => obj != null)
            .ToList();
        var scanErrorCount = 0;
        var vanillaSettlements = worldObjects.Count(obj => obj is Settlement);
        var factionWorldObjects = worldObjects.Count(obj => SafeRead(() => obj.Faction, ref scanErrorCount) != null);
        var candidates = worldObjects
            .Select(obj => ToCandidateOrNull(obj, ref scanErrorCount))
            .Where(candidate => candidate != null)
            .Cast<WorldObjectSettlementCandidate>()
            .GroupBy(candidate => candidate.StableKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        candidates.Sort(CompareCandidates);
        var rejectedWorldObjectTypes = BuildRejectedTypeSummary(worldObjects, candidates, ref scanErrorCount);

        return new WorldObjectScanResult(
            new WorldObjectScanSummary(
                worldObjects.Count,
                vanillaSettlements,
                factionWorldObjects,
                candidates.Count,
                scanErrorCount,
                rejectedWorldObjectTypes),
            candidates);
    }

    private WorldObjectSettlementCandidate? ToCandidateOrNull(WorldObject obj, ref int scanErrorCount)
    {
        foreach (var importer in importers)
        {
            if (!importer.CanImport(obj))
            {
                continue;
            }

            return importer.ImportCandidate(obj, ref scanErrorCount);
        }

        return null;
    }

    private static T? SafeRead<T>(Func<T?> read, ref int scanErrorCount)
        where T : class
    {
        try
        {
            return read();
        }
        catch (Exception ex)
        {
            scanErrorCount++;
            Log.Warning($"[LivingWorld] Failed to read world object data: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static int SafeRead(Func<int> read, ref int scanErrorCount, int fallback)
    {
        try
        {
            return read();
        }
        catch (Exception ex)
        {
            scanErrorCount++;
            Log.Warning($"[LivingWorld] Failed to read world object integer data: {ex.GetType().Name}: {ex.Message}");
            return fallback;
        }
    }

    private static int CompareCandidates(
        WorldObjectSettlementCandidate left,
        WorldObjectSettlementCandidate right)
    {
        var tileComparison = left.Tile.CompareTo(right.Tile);
        return tileComparison != 0
            ? tileComparison
            : string.CompareOrdinal(left.StableKey, right.StableKey);
    }

    private static string BuildRejectedTypeSummary(
        IReadOnlyCollection<WorldObject> worldObjects,
        IReadOnlyCollection<WorldObjectSettlementCandidate> candidates,
        ref int scanErrorCount)
    {
        var candidateKeys = new HashSet<string>(candidates.Select(candidate => candidate.StableKey), StringComparer.Ordinal);
        var rejectedCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var obj in worldObjects)
        {
            if (candidateKeys.Contains(BuildStableKey(obj, ref scanErrorCount)))
            {
                continue;
            }

            var typeName = SafeRead(() => obj.def?.defName, ref scanErrorCount) ?? obj.GetType().Name;
            rejectedCounts.TryGetValue(typeName, out var current);
            rejectedCounts[typeName] = current + 1;
        }

        var rejectedGroups = rejectedCounts.ToList();
        rejectedGroups.Sort(CompareRejectedGroups);

        var rejectedTypes = rejectedGroups
            .Take(6)
            .Select(group => $"{group.Key}:{group.Value}")
            .ToList();

        return rejectedTypes.Count == 0
            ? "none"
            : string.Join(", ", rejectedTypes);
    }

    private static string BuildStableKey(WorldObject obj, ref int scanErrorCount)
    {
        var defName = SafeRead(() => obj.def?.defName, ref scanErrorCount) ?? obj.GetType().Name;
        var tile = SafeRead(() => obj.Tile, ref scanErrorCount, -1);
        var faction = SafeRead(() => obj.Faction, ref scanErrorCount);
        var factionId = faction == null
            ? "UnknownFaction"
            : SafeRead(() => faction.def?.defName, ref scanErrorCount) ?? "UnknownFaction";

        return $"worldobject:{defName}:{tile}:{factionId}";
    }

    private static int CompareRejectedGroups(KeyValuePair<string, int> left, KeyValuePair<string, int> right)
    {
        var countComparison = right.Value.CompareTo(left.Value);
        return countComparison != 0
            ? countComparison
            : string.CompareOrdinal(left.Key, right.Key);
    }
}

public sealed class VanillaSettlementImporter : IWorldObjectImporter
{
    public bool CanImport(WorldObject obj)
    {
        return obj is Settlement;
    }

    public WorldObjectSettlementCandidate? ImportCandidate(WorldObject obj, ref int scanErrorCount)
    {
        if (obj is not Settlement)
        {
            return null;
        }

        var faction = SafeRead(() => obj.Faction, ref scanErrorCount);
        var tile = SafeRead(() => obj.Tile, ref scanErrorCount, -1);
        if (faction == null || tile < 0)
        {
            return null;
        }

        var defName = SafeRead(() => obj.def?.defName, ref scanErrorCount) ?? obj.GetType().Name;
        var factionId = SafeRead(() => faction.def?.defName, ref scanErrorCount) ?? "UnknownFaction";
        var label = SafeRead(() => obj.LabelCap, ref scanErrorCount);
        var name = string.IsNullOrWhiteSpace(label) ? defName : label!;
        var stableKey = $"worldobject:{defName}:{tile}:{factionId}";

        return new(
            stableKey,
            name,
            factionId,
            tile,
            defName,
            true);
    }

    private static T? SafeRead<T>(Func<T?> read, ref int scanErrorCount)
        where T : class
    {
        try
        {
            return read();
        }
        catch (Exception ex)
        {
            scanErrorCount++;
            Log.Warning($"[LivingWorld] Failed to read world object data: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static int SafeRead(Func<int> read, ref int scanErrorCount, int fallback)
    {
        try
        {
            return read();
        }
        catch (Exception ex)
        {
            scanErrorCount++;
            Log.Warning($"[LivingWorld] Failed to read world object integer data: {ex.GetType().Name}: {ex.Message}");
            return fallback;
        }
    }
}
