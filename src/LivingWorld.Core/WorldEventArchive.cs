using System.Security.Cryptography;
using System.Text;

namespace LivingWorld.Core;

public static class WorldEventJournalPolicy
{
    public const int DetailedEventLimit = 8_192;
    public const int CompactionBatchSize = 1_024;
    public const int RecentAggregateLimit = 8_192;
}

public sealed record WorldEventKindCount(WorldEventKind Kind, long Count);

public sealed record WorldEventArchiveAggregate(
    int Tick,
    WorldEventKind Kind,
    EntityId? SettlementId,
    long Count);

public sealed record WorldEventArchiveCheckpoint(
    long ArchivedEventCount,
    long FirstArchivedEventId,
    long LastArchivedEventId,
    int FirstArchivedTick,
    int LastArchivedTick,
    string AuditHash,
    IReadOnlyList<WorldEventKindCount> LifetimeKindCounts,
    IReadOnlyList<WorldEventArchiveAggregate> RecentAggregates)
{
    public static WorldEventArchiveCheckpoint Empty { get; } = new(
        0,
        0,
        0,
        0,
        0,
        string.Empty,
        Array.Empty<WorldEventKindCount>(),
        Array.Empty<WorldEventArchiveAggregate>());

    public long RecentGlobalEventCount => RecentAggregates
        .Where(aggregate => !aggregate.SettlementId.HasValue)
        .Sum(aggregate => aggregate.Count);

    public long Count(WorldEventKind kind)
    {
        return LifetimeKindCounts.FirstOrDefault(entry => entry.Kind == kind)?.Count ?? 0;
    }
}

internal static class WorldEventArchiveService
{
    public static WorldEventArchiveCheckpoint Append(
        WorldEventArchiveCheckpoint checkpoint,
        IReadOnlyList<WorldEvent> events)
    {
        if (events.Count == 0)
        {
            return checkpoint;
        }

        var ordered = events.OrderBy(worldEvent => worldEvent.Id.Value).ToList();
        var kindCounts = checkpoint.LifetimeKindCounts.ToDictionary(entry => entry.Kind, entry => entry.Count);
        var aggregates = checkpoint.RecentAggregates.ToDictionary(
            aggregate => (aggregate.Tick, aggregate.Kind, aggregate.SettlementId),
            aggregate => aggregate.Count);

        foreach (var worldEvent in ordered)
        {
            kindCounts.TryGetValue(worldEvent.Kind, out var kindCount);
            kindCounts[worldEvent.Kind] = kindCount + 1;

            AddAggregate(aggregates, worldEvent.Tick, worldEvent.Kind, settlementId: null);
            if (worldEvent.SettlementId.HasValue)
            {
                AddAggregate(aggregates, worldEvent.Tick, worldEvent.Kind, worldEvent.SettlementId);
            }
        }

        var boundedAggregates = BoundAggregates(aggregates);
        var first = ordered[0];
        var last = ordered[ordered.Count - 1];
        return new WorldEventArchiveCheckpoint(
            checkpoint.ArchivedEventCount + ordered.Count,
            checkpoint.ArchivedEventCount == 0 ? first.Id.Value : checkpoint.FirstArchivedEventId,
            last.Id.Value,
            checkpoint.ArchivedEventCount == 0 ? first.Tick : Math.Min(checkpoint.FirstArchivedTick, first.Tick),
            Math.Max(checkpoint.LastArchivedTick, last.Tick),
            ExtendAuditHash(checkpoint.AuditHash, ordered),
            kindCounts
                .OrderBy(pair => pair.Key)
                .Select(pair => new WorldEventKindCount(pair.Key, pair.Value))
                .ToList(),
            boundedAggregates);
    }

    public static WorldEventArchiveCheckpoint Normalize(WorldEventArchiveCheckpoint? checkpoint)
    {
        if (checkpoint == null || checkpoint.ArchivedEventCount <= 0)
        {
            return WorldEventArchiveCheckpoint.Empty;
        }

        var counts = checkpoint.LifetimeKindCounts
            .Where(entry => entry.Count > 0)
            .GroupBy(entry => entry.Kind)
            .Select(group => new WorldEventKindCount(group.Key, group.Sum(entry => entry.Count)))
            .OrderBy(entry => entry.Kind)
            .ToList();
        var aggregates = checkpoint.RecentAggregates
            .Where(entry => entry.Count > 0)
            .GroupBy(entry => (Math.Max(0, entry.Tick), entry.Kind, entry.SettlementId))
            .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Count));

        return new WorldEventArchiveCheckpoint(
            Math.Max(0, checkpoint.ArchivedEventCount),
            Math.Max(0, checkpoint.FirstArchivedEventId),
            Math.Max(0, checkpoint.LastArchivedEventId),
            Math.Max(0, checkpoint.FirstArchivedTick),
            Math.Max(0, checkpoint.LastArchivedTick),
            checkpoint.AuditHash ?? string.Empty,
            counts,
            BoundAggregates(aggregates));
    }

    private static void AddAggregate(
        IDictionary<(int Tick, WorldEventKind Kind, EntityId? SettlementId), long> aggregates,
        int tick,
        WorldEventKind kind,
        EntityId? settlementId)
    {
        var key = (tick, kind, settlementId);
        aggregates.TryGetValue(key, out var count);
        aggregates[key] = count + 1;
    }

    private static IReadOnlyList<WorldEventArchiveAggregate> BoundAggregates(
        IDictionary<(int Tick, WorldEventKind Kind, EntityId? SettlementId), long> aggregates)
    {
        if (aggregates.Count > WorldEventJournalPolicy.RecentAggregateLimit)
        {
            foreach (var key in aggregates.Keys
                .Where(key => key.SettlementId.HasValue)
                .OrderBy(key => key.Tick)
                .ThenBy(key => key.Kind)
                .ThenBy(key => key.SettlementId!.Value.Kind)
                .ThenBy(key => key.SettlementId!.Value.Value)
                .ToList())
            {
                aggregates.Remove(key);
                if (aggregates.Count <= WorldEventJournalPolicy.RecentAggregateLimit)
                {
                    break;
                }
            }
        }

        while (aggregates.Count > WorldEventJournalPolicy.RecentAggregateLimit)
        {
            var oldestTick = aggregates.Keys.Min(key => key.Tick);
            foreach (var key in aggregates.Keys.Where(key => key.Tick == oldestTick).ToList())
            {
                aggregates.Remove(key);
            }
        }

        return aggregates
            .OrderBy(pair => pair.Key.Tick)
            .ThenBy(pair => pair.Key.Kind)
            .ThenBy(pair => pair.Key.SettlementId?.Kind)
            .ThenBy(pair => pair.Key.SettlementId?.Value)
            .Select(pair => new WorldEventArchiveAggregate(
                pair.Key.Tick,
                pair.Key.Kind,
                pair.Key.SettlementId,
                pair.Value))
            .ToList();
    }

    private static string ExtendAuditHash(string previousHash, IReadOnlyList<WorldEvent> events)
    {
        using var sha256 = SHA256.Create();
        using var cryptoStream = new CryptoStream(Stream.Null, sha256, CryptoStreamMode.Write);
        using (var writer = new BinaryWriter(cryptoStream, new UTF8Encoding(false), leaveOpen: true))
        {
            writer.Write(previousHash ?? string.Empty);
            foreach (var worldEvent in events)
            {
                writer.Write(worldEvent.Id.Value);
                writer.Write((int)worldEvent.Kind);
                writer.Write(worldEvent.Tick);
                WriteEntityId(writer, worldEvent.SubjectId);
                WriteEntityId(writer, worldEvent.SettlementId);
                writer.Write(worldEvent.Summary ?? string.Empty);
            }
        }

        cryptoStream.FlushFinalBlock();
        return Convert.ToBase64String(sha256.Hash ?? Array.Empty<byte>());
    }

    private static void WriteEntityId(BinaryWriter writer, EntityId? entityId)
    {
        writer.Write(entityId.HasValue);
        if (!entityId.HasValue)
        {
            return;
        }

        writer.Write((int)entityId.Value.Kind);
        writer.Write(entityId.Value.Value);
    }
}
