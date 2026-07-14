using System.Linq;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>
/// Resolves WHERE the fighters muster and hold. Priority, all fail-safe:
///   1. A player-painted allowed area labelled "LW Muster" (mirrors the "LW Shelter" convention) — the cell in
///      it nearest the colony centre. Deterministic and player-approved: no algorithm out-guesses a player who
///      bothered to paint their real chokepoint.
///   2. An automatic perimeter-facing cell: step from the colony centre toward the threat, staying inside the
///      Home area, and stop at the last standable Home cell — the edge of the base that faces the enemy.
///   3. The raw map centre — today's baseline, so the feature never regresses and never throws.
/// </summary>
public static class MusterAnchorService
{
    private const string MusterAreaLabel = "LW Muster";
    private const int MaxPerimeterSteps = 60;

    public enum Source { Painted, Auto, Center }

    public static IntVec3 Resolve(Map map, IntVec3 hostileCentroid, bool enemyAtBase, out Source source)
    {
        source = Source.Center;
        if (map == null)
        {
            return IntVec3.Invalid;
        }

        try
        {
            var center = ColonyCenter(map);

            var painted = PaintedAnchor(map, center);
            if (painted.IsValid)
            {
                source = Source.Painted;
                return painted;
            }

            // An interior (drop-pod/breach) threat makes a perimeter-facing pick meaningless — hold at the
            // colony centre instead of walking out to a wall the enemy is already behind.
            if (!enemyAtBase && hostileCentroid.IsValid && hostileCentroid != center)
            {
                var perimeter = PerimeterAnchor(map, center, hostileCentroid);
                if (perimeter.IsValid)
                {
                    source = Source.Auto;
                    return perimeter;
                }
            }

            return center;
        }
        catch
        {
            return map.Center;
        }
    }

    // Centroid of the Home area if it has cells, else the raw map centre.
    private static IntVec3 ColonyCenter(Map map)
    {
        var home = map.areaManager?.Home;
        if (home == null || home.TrueCount == 0)
        {
            return map.Center;
        }

        long sx = 0, sz = 0;
        var n = 0;
        foreach (var c in home.ActiveCells)
        {
            sx += c.x;
            sz += c.z;
            n++;
        }

        if (n == 0)
        {
            return map.Center;
        }

        var avg = new IntVec3((int)(sx / n), 0, (int)(sz / n));
        return avg.InBounds(map) ? avg : map.Center;
    }

    private static IntVec3 PaintedAnchor(Map map, IntVec3 center)
    {
        var area = map.areaManager?.AllAreas?.FirstOrDefault(a => a?.Label == MusterAreaLabel);
        if (area == null || area.TrueCount == 0)
        {
            return IntVec3.Invalid;
        }

        var best = IntVec3.Invalid;
        var bestDist = float.MaxValue;
        foreach (var c in area.ActiveCells)
        {
            if (!c.Standable(map))
            {
                continue;
            }

            var d = (c - center).LengthHorizontalSquared;
            if (d < bestDist)
            {
                bestDist = d;
                best = c;
            }
        }

        return best;
    }

    // Walk from the colony centre one cell at a time toward the threat, staying inside the Home area; the last
    // standable Home cell is the base edge that faces the enemy — a natural place to hold.
    private static IntVec3 PerimeterAnchor(Map map, IntVec3 center, IntVec3 hostileCentroid)
    {
        var home = map.areaManager?.Home;
        if (home == null)
        {
            return IntVec3.Invalid;
        }

        var dir = (hostileCentroid - center).ToVector3();
        if (dir.MagnitudeHorizontal() < 0.01f)
        {
            return IntVec3.Invalid;
        }

        dir = dir.normalized;
        var best = center.Standable(map) && home[center] ? center : IntVec3.Invalid;

        for (var step = 1; step <= MaxPerimeterSteps; step++)
        {
            var cell = (center.ToVector3Shifted() + dir * step).ToIntVec3();
            if (!cell.InBounds(map))
            {
                break;
            }

            if (home[cell] && cell.Standable(map))
            {
                best = cell;
            }
            else if (!home[cell])
            {
                // Left the base — the previous kept cell is the perimeter.
                break;
            }
        }

        return best;
    }
}
