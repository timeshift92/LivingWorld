using System.Collections.Generic;
using System.Linq;

namespace LivingWorld.Core;

/// <summary>
/// Selects not-yet-notified world events against a monotonic event-Id cursor instead of a positional index
/// into the live event journal. The journal is compacted (oldest events archived out once it grows past its
/// cap), so a positional Skip(count) desyncs the moment old events are archived — Skip past a shrunk list drops
/// genuinely-new events. Event Ids come from a monotonic counter, so an Id cursor is stable across compaction:
/// archived-out events all have Id at or below the cursor, new events have Id above it.
/// </summary>
public static class WorldWarNotificationCursor
{
    public static List<WorldEvent> SelectNewer(IEnumerable<WorldEvent> events, long lastNotifiedEventId)
    {
        if (events == null)
        {
            return new List<WorldEvent>();
        }

        return events
            .Where(worldEvent => worldEvent != null && worldEvent.Id.Value > lastNotifiedEventId)
            .OrderBy(worldEvent => worldEvent.Tick)
            .ThenBy(worldEvent => worldEvent.Id.Value)
            .ToList();
    }

    public static long AdvanceCursor(IEnumerable<WorldEvent> events, long lastNotifiedEventId)
    {
        if (events == null)
        {
            return lastNotifiedEventId;
        }

        var cursor = lastNotifiedEventId;
        foreach (var worldEvent in events)
        {
            if (worldEvent != null && worldEvent.Id.Value > cursor)
            {
                cursor = worldEvent.Id.Value;
            }
        }

        return cursor;
    }
}
