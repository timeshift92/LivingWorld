namespace LivingWorld.Core;

public enum SettlementMapLayoutStatus
{
    Success,
    InvalidRequest,
    UnknownSettlement,
    NoFacilities
}

public sealed record SettlementMapLayoutRequest(
    EntityId SettlementId,
    int CenterX,
    int CenterZ,
    int MaxFacilities);

public sealed record SettlementMapFacilityFeature(
    EntityId FacilityId,
    SettlementFacilityKind Kind,
    int Level,
    int ConditionPercent,
    string PrimaryThingDefName,
    int AnchorX,
    int AnchorZ);

public sealed record SettlementMapRoom(
    EntityId FacilityId,
    SettlementFacilityKind Kind,
    int Level,
    int MinX,
    int MinZ,
    int Width,
    int Height,
    string WallThingDefName,
    string DoorThingDefName,
    string FloorTerrainDefName,
    string WallStuffDefName);

public sealed record SettlementMapStockpileCell(
    int X,
    int Z,
    int Order);

public enum SettlementMapDistrictKind
{
    Housing,
    Storage,
    Industry,
    Medical,
    Agriculture,
    Power,
    Security,
    Commons
}

public sealed record SettlementMapStyle(
    string StyleKey,
    string WallStuffDefName,
    string RoadTerrainDefName,
    string CommonFloorTerrainDefName);

public sealed record SettlementMapDistrict(
    SettlementMapDistrictKind Kind,
    EntityId? FacilityId,
    int CenterX,
    int CenterZ,
    int MinX,
    int MinZ,
    int Width,
    int Height,
    string FloorTerrainDefName,
    string WallStuffDefName,
    int Order);

public sealed record SettlementMapPathCell(
    int X,
    int Z,
    string TerrainDefName,
    int Order);

public enum SettlementMapCityFeatureKind
{
    Bed,
    Defense,
    Power,
    Work,
    StockpileMarker,
    PowerConduit,
    PowerGenerator,
    Light,
    GuardPost,
    Activity
}

public sealed record SettlementMapCityFeature(
    SettlementMapCityFeatureKind Kind,
    string ThingDefName,
    string StuffDefName,
    EntityId? FacilityId,
    int X,
    int Z,
    int Order);

public sealed record SettlementMapLayoutResult(
    SettlementMapLayoutStatus Status,
    string Reason,
    IReadOnlyList<SettlementMapFacilityFeature> Facilities,
    IReadOnlyList<SettlementMapRoom> Rooms,
    IReadOnlyList<SettlementMapStockpileCell> StockpileCells,
    IReadOnlyList<SettlementMapCityFeature> CityFeatures)
{
    public SettlementMapStyle Style { get; init; } =
        new("unknown-tribal-balanced", "WoodLog", "PackedDirt", "PackedDirt");

    public IReadOnlyList<SettlementMapDistrict> Districts { get; init; } =
        Array.Empty<SettlementMapDistrict>();

    public IReadOnlyList<SettlementMapPathCell> PathCells { get; init; } =
        Array.Empty<SettlementMapPathCell>();
}

public static class SettlementMapLayoutService
{
    public static SettlementMapLayoutResult BuildFacilityLayout(
        WorldState state,
        SettlementMapLayoutRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.MaxFacilities <= 0)
        {
            return new SettlementMapLayoutResult(
                SettlementMapLayoutStatus.InvalidRequest,
                "Settlement map layout requires a positive facility cap.",
                Array.Empty<SettlementMapFacilityFeature>(),
                Array.Empty<SettlementMapRoom>(),
                Array.Empty<SettlementMapStockpileCell>(),
                Array.Empty<SettlementMapCityFeature>());
        }

        var settlement = state.GetSettlement(request.SettlementId);
        if (settlement == null || !settlement.IsActive)
        {
            return new SettlementMapLayoutResult(
                SettlementMapLayoutStatus.UnknownSettlement,
                $"Settlement {request.SettlementId} does not exist or is inactive.",
                Array.Empty<SettlementMapFacilityFeature>(),
                Array.Empty<SettlementMapRoom>(),
                Array.Empty<SettlementMapStockpileCell>(),
                Array.Empty<SettlementMapCityFeature>());
        }

        var profile = state.GetSettlementProductionProfile(request.SettlementId);
        var style = BuildStyle(profile);
        var techScore = TechScore(profile?.TechLevel);
        var facilities = state.GetSettlementFacilities(request.SettlementId)
            .Where(facility => facility.ConditionPercent > 0)
            .OrderBy(facility => FacilitySortRank(facility.Kind))
            .ThenByDescending(facility => facility.Level)
            .ThenBy(facility => facility.Id.Value)
            .Take(request.MaxFacilities)
            .Select((facility, index) => ToFeature(facility, request.CenterX, request.CenterZ, index))
            .ToList();

        if (facilities.Count == 0)
        {
            return new SettlementMapLayoutResult(
                SettlementMapLayoutStatus.NoFacilities,
                $"Settlement {request.SettlementId} has no active facilities to materialize.",
                Array.Empty<SettlementMapFacilityFeature>(),
                Array.Empty<SettlementMapRoom>(),
                Array.Empty<SettlementMapStockpileCell>(),
                Array.Empty<SettlementMapCityFeature>());
        }

        var rooms = facilities
            .Select(feature => ToRoom(feature, techScore, style))
            .ToList();
        var districts = BuildDistricts(state, request.SettlementId, rooms, request.CenterX, request.CenterZ, style).ToList();
        var pathCells = BuildPathCells(districts, style).ToList();
        var stockpileRoom = rooms.FirstOrDefault(room => room.Kind == SettlementFacilityKind.Storage)
            ?? rooms.First();
        var stockpileCells = ToStockpileCells(stockpileRoom).ToList();
        var cityFeatures = BuildCityFeatures(
            state,
            request.SettlementId,
            rooms,
            districts,
            pathCells,
            stockpileCells,
            techScore).ToList();

        return new SettlementMapLayoutResult(
            SettlementMapLayoutStatus.Success,
            $"Prepared {facilities.Count} settlement facility feature(s).",
            facilities,
            rooms,
            stockpileCells,
            cityFeatures)
        {
            Style = style,
            Districts = districts,
            PathCells = pathCells,
        };
    }

    private static SettlementMapFacilityFeature ToFeature(
        SettlementFacility facility,
        int centerX,
        int centerZ,
        int index)
    {
        var ring = index / 4 + 1;
        var slot = index % 4;
        var offset = 6 * ring;
        var (dx, dz) = slot switch
        {
            0 => (-offset, -offset),
            1 => (offset, -offset),
            2 => (-offset, offset),
            _ => (offset, offset)
        };

        return new SettlementMapFacilityFeature(
            facility.Id,
            facility.Kind,
            facility.Level,
            Math.Max(0, Math.Min(100, facility.ConditionPercent)),
            PrimaryThingDefName(facility.Kind),
            centerX + dx,
            centerZ + dz);
    }

    private static int FacilitySortRank(SettlementFacilityKind kind)
    {
        return kind switch
        {
            SettlementFacilityKind.Storage => 0,
            SettlementFacilityKind.Workshop => 1,
            SettlementFacilityKind.Clinic => 2,
            SettlementFacilityKind.Farm => 3,
            SettlementFacilityKind.PowerPlant => 4,
            _ => 10
        };
    }

    private static string PrimaryThingDefName(SettlementFacilityKind kind)
    {
        return kind switch
        {
            SettlementFacilityKind.Farm => "HydroponicsBasin",
            SettlementFacilityKind.Workshop => "TableMachining",
            SettlementFacilityKind.Clinic => "HospitalBed",
            SettlementFacilityKind.PowerPlant => "Battery",
            SettlementFacilityKind.Storage => "Shelf",
            _ => "TableShort"
        };
    }

    private static SettlementMapRoom ToRoom(SettlementMapFacilityFeature feature, int techScore, SettlementMapStyle style)
    {
        var size = Math.Max(7, Math.Min(12, 6 + feature.Level));
        if (feature.Kind == SettlementFacilityKind.Storage)
        {
            size += 1;
        }

        var minX = feature.AnchorX - size / 2;
        var minZ = feature.AnchorZ - size / 2;
        return new SettlementMapRoom(
            feature.FacilityId,
            feature.Kind,
            feature.Level,
            minX,
            minZ,
            size,
            size,
            "Wall",
            "Door",
            FloorTerrainDefName(feature.Kind, techScore),
            style.WallStuffDefName);
    }

    private static IEnumerable<SettlementMapDistrict> BuildDistricts(
        WorldState state,
        EntityId settlementId,
        IReadOnlyList<SettlementMapRoom> rooms,
        int centerX,
        int centerZ,
        SettlementMapStyle style)
    {
        var order = 0;
        foreach (var room in rooms.OrderBy(room => room.MinX).ThenBy(room => room.MinZ))
        {
            yield return new SettlementMapDistrict(
                DistrictKindForFacility(room.Kind),
                room.FacilityId,
                room.MinX + room.Width / 2,
                room.MinZ + room.Height / 2,
                room.MinX - 2,
                room.MinZ - 2,
                room.Width + 4,
                room.Height + 4,
                room.FloorTerrainDefName,
                room.WallStuffDefName,
                order++);
        }

        var population = state.GetSettlementPopulation(settlementId);
        var housingSize = Math.Max(9, Math.Min(18, 8 + population.Total / 4));
        yield return new SettlementMapDistrict(
            SettlementMapDistrictKind.Housing,
            null,
            centerX,
            centerZ + 14,
            centerX - housingSize / 2,
            centerZ + 14 - housingSize / 2,
            housingSize,
            housingSize,
            style.CommonFloorTerrainDefName,
            style.WallStuffDefName,
            order++);

        yield return new SettlementMapDistrict(
            SettlementMapDistrictKind.Commons,
            null,
            centerX,
            centerZ,
            centerX - 5,
            centerZ - 5,
            11,
            11,
            style.CommonFloorTerrainDefName,
            style.WallStuffDefName,
            order++);

        var securitySize = Math.Max(11, Math.Min(22, 10 + rooms.Count * 2));
        yield return new SettlementMapDistrict(
            SettlementMapDistrictKind.Security,
            null,
            centerX,
            centerZ - 18,
            centerX - securitySize / 2,
            centerZ - 18 - securitySize / 2,
            securitySize,
            securitySize,
            style.RoadTerrainDefName,
            style.WallStuffDefName,
            order++);
    }

    private static SettlementMapDistrictKind DistrictKindForFacility(SettlementFacilityKind kind)
    {
        return kind switch
        {
            SettlementFacilityKind.Storage => SettlementMapDistrictKind.Storage,
            SettlementFacilityKind.Workshop => SettlementMapDistrictKind.Industry,
            SettlementFacilityKind.Clinic => SettlementMapDistrictKind.Medical,
            SettlementFacilityKind.Farm => SettlementMapDistrictKind.Agriculture,
            SettlementFacilityKind.PowerPlant => SettlementMapDistrictKind.Power,
            _ => SettlementMapDistrictKind.Commons,
        };
    }

    private static IEnumerable<SettlementMapPathCell> BuildPathCells(
        IReadOnlyList<SettlementMapDistrict> districts,
        SettlementMapStyle style)
    {
        var order = 0;
        var commons = districts.FirstOrDefault(district => district.Kind == SettlementMapDistrictKind.Commons)
            ?? districts.OrderBy(district => district.Order).FirstOrDefault();
        if (commons == null)
        {
            yield break;
        }

        var seen = new HashSet<(int X, int Z)>();
        foreach (var district in districts.OrderBy(district => district.Order))
        {
            foreach (var cell in ManhattanPath(commons.CenterX, commons.CenterZ, district.CenterX, district.CenterZ))
            {
                if (!seen.Add(cell))
                {
                    continue;
                }

                yield return new SettlementMapPathCell(cell.X, cell.Z, style.RoadTerrainDefName, order++);
            }
        }
    }

    private static IEnumerable<(int X, int Z)> ManhattanPath(int startX, int startZ, int endX, int endZ)
    {
        var stepX = startX <= endX ? 1 : -1;
        for (var x = startX; x != endX; x += stepX)
        {
            yield return (x, startZ);
        }

        var stepZ = startZ <= endZ ? 1 : -1;
        for (var z = startZ; z != endZ; z += stepZ)
        {
            yield return (endX, z);
        }

        yield return (endX, endZ);
    }

    private static IEnumerable<SettlementMapStockpileCell> ToStockpileCells(SettlementMapRoom room)
    {
        var order = 0;
        for (var z = room.MinZ + 2; z < room.MinZ + room.Height - 1; z++)
        {
            for (var x = room.MinX + 2; x < room.MinX + room.Width - 1; x++)
            {
                yield return new SettlementMapStockpileCell(x, z, order++);
            }
        }
    }

    private static IEnumerable<SettlementMapCityFeature> BuildCityFeatures(
        WorldState state,
        EntityId settlementId,
        IReadOnlyList<SettlementMapRoom> rooms,
        IReadOnlyList<SettlementMapDistrict> districts,
        IReadOnlyList<SettlementMapPathCell> pathCells,
        IReadOnlyList<SettlementMapStockpileCell> stockpileCells,
        int techScore)
    {
        var order = 0;
        var population = state.GetSettlementPopulation(settlementId);
        var housingRoom = rooms.FirstOrDefault(room => room.Kind == SettlementFacilityKind.Clinic)
            ?? rooms.First();
        var bedCount = Math.Max(2, Math.Min(16, (population.Total + 1) / 2));
        var bedDef = techScore >= 3 ? "Bed" : "Bedroll";
        var bedStuff = techScore >= 3 ? "Steel" : "WoodLog";
        foreach (var cell in InteriorGrid(housingRoom, margin: 2).Take(bedCount))
        {
            yield return new SettlementMapCityFeature(
                SettlementMapCityFeatureKind.Bed,
                bedDef,
                bedStuff,
                null,
                cell.X,
                cell.Z,
                order++);
        }

        var defenseRoom = rooms
            .OrderBy(room => room.MinX)
            .ThenBy(room => room.MinZ)
            .First();
        var defenseCount = Math.Max(4, Math.Min(18, population.Adults / 2 + rooms.Count));
        foreach (var cell in DefenseRing(defenseRoom).Take(defenseCount))
        {
            yield return new SettlementMapCityFeature(
                SettlementMapCityFeatureKind.Defense,
                techScore >= 3 ? "Barricade" : "Sandbags",
                techScore >= 3 ? "Steel" : "WoodLog",
                null,
                cell.X,
                cell.Z,
                order++);
        }

        var storageRoom = rooms.FirstOrDefault(room => room.Kind == SettlementFacilityKind.Storage);
        if (storageRoom != null)
        {
            var storageFacilityId = storageRoom.FacilityId;
            foreach (var slot in stockpileCells.Take(4))
            {
                yield return new SettlementMapCityFeature(
                    SettlementMapCityFeatureKind.StockpileMarker,
                    "Shelf",
                    techScore >= 3 ? "Steel" : "WoodLog",
                    storageFacilityId,
                    slot.X,
                    slot.Z,
                    order++);
            }
        }

        var workshopRoom = rooms.FirstOrDefault(room => room.Kind == SettlementFacilityKind.Workshop);
        if (workshopRoom != null)
        {
            var workDef = techScore >= 3 ? "TableMachining" : "FueledSmithy";
            foreach (var cell in InteriorGrid(workshopRoom, margin: 3).Take(2))
            {
                yield return new SettlementMapCityFeature(
                    SettlementMapCityFeatureKind.Work,
                    workDef,
                    techScore >= 3 ? "Steel" : "WoodLog",
                    workshopRoom.FacilityId,
                    cell.X,
                    cell.Z,
                    order++);
            }
        }

        var powerRoom = rooms.FirstOrDefault(room => room.Kind == SettlementFacilityKind.PowerPlant);
        if (powerRoom != null)
        {
            var powerCells = InteriorGrid(powerRoom, margin: 3).Take(3).ToList();
            if (powerCells.Count > 0)
            {
                yield return new SettlementMapCityFeature(
                    SettlementMapCityFeatureKind.PowerGenerator,
                    techScore >= 3 ? "SolarGenerator" : "Battery",
                    "Steel",
                    powerRoom.FacilityId,
                    powerCells[0].X,
                    powerCells[0].Z,
                    order++);

                yield return new SettlementMapCityFeature(
                    SettlementMapCityFeatureKind.Power,
                    "Battery",
                    "Steel",
                    powerRoom.FacilityId,
                    powerCells[0].X + 1,
                    powerCells[0].Z,
                    order++);
            }

            foreach (var cell in powerCells.Skip(1))
            {
                yield return new SettlementMapCityFeature(
                    SettlementMapCityFeatureKind.Power,
                    "Battery",
                    "Steel",
                    powerRoom.FacilityId,
                    cell.X,
                    cell.Z,
                    order++);
            }
        }

        foreach (var pathCell in pathCells.Where((_, index) => index % 3 == 0).Take(24))
        {
            yield return new SettlementMapCityFeature(
                SettlementMapCityFeatureKind.PowerConduit,
                "PowerConduit",
                "Steel",
                null,
                pathCell.X,
                pathCell.Z,
                order++);
        }

        foreach (var district in districts.Where(district => district.Kind is SettlementMapDistrictKind.Commons or SettlementMapDistrictKind.Housing).Take(4))
        {
            yield return new SettlementMapCityFeature(
                SettlementMapCityFeatureKind.Light,
                "StandingLamp",
                "Steel",
                district.FacilityId,
                district.CenterX,
                district.CenterZ,
                order++);
        }

        var securityDistrict = districts.FirstOrDefault(district => district.Kind == SettlementMapDistrictKind.Security);
        if (securityDistrict != null)
        {
            foreach (var cell in DistrictRing(securityDistrict).Take(Math.Max(6, Math.Min(18, population.Adults / 3 + rooms.Count))))
            {
                yield return new SettlementMapCityFeature(
                    SettlementMapCityFeatureKind.GuardPost,
                    techScore >= 3 ? "Barricade" : "Sandbags",
                    techScore >= 3 ? "Steel" : "WoodLog",
                    null,
                    cell.X,
                    cell.Z,
                    order++);
            }
        }

        var commonsDistrict = districts.FirstOrDefault(district => district.Kind == SettlementMapDistrictKind.Commons);
        if (commonsDistrict != null)
        {
            foreach (var cell in DistrictInterior(commonsDistrict, margin: 2).Take(4))
            {
                yield return new SettlementMapCityFeature(
                    SettlementMapCityFeatureKind.Activity,
                    techScore >= 3 ? "TableShort" : "Campfire",
                    techScore >= 3 ? "Steel" : "WoodLog",
                    null,
                    cell.X,
                    cell.Z,
                    order++);
            }
        }
    }

    private static IEnumerable<(int X, int Z)> InteriorGrid(SettlementMapRoom room, int margin)
    {
        var minX = room.MinX + Math.Max(1, margin);
        var maxX = room.MinX + room.Width - Math.Max(1, margin);
        var minZ = room.MinZ + Math.Max(1, margin);
        var maxZ = room.MinZ + room.Height - Math.Max(1, margin);
        for (var z = minZ; z < maxZ; z += 2)
        {
            for (var x = minX; x < maxX; x += 2)
            {
                yield return (x, z);
            }
        }
    }

    private static IEnumerable<(int X, int Z)> DefenseRing(SettlementMapRoom room)
    {
        var minX = room.MinX - 2;
        var maxX = room.MinX + room.Width + 1;
        var minZ = room.MinZ - 2;
        var maxZ = room.MinZ + room.Height + 1;
        for (var x = minX; x <= maxX; x += 2)
        {
            yield return (x, minZ);
            yield return (x, maxZ);
        }

        for (var z = minZ + 2; z <= maxZ - 2; z += 2)
        {
            yield return (minX, z);
            yield return (maxX, z);
        }
    }

    private static IEnumerable<(int X, int Z)> DistrictInterior(SettlementMapDistrict district, int margin)
    {
        var minX = district.MinX + Math.Max(1, margin);
        var maxX = district.MinX + district.Width - Math.Max(1, margin);
        var minZ = district.MinZ + Math.Max(1, margin);
        var maxZ = district.MinZ + district.Height - Math.Max(1, margin);
        for (var z = minZ; z < maxZ; z += 2)
        {
            for (var x = minX; x < maxX; x += 2)
            {
                yield return (x, z);
            }
        }
    }

    private static IEnumerable<(int X, int Z)> DistrictRing(SettlementMapDistrict district)
    {
        var minX = district.MinX - 1;
        var maxX = district.MinX + district.Width;
        var minZ = district.MinZ - 1;
        var maxZ = district.MinZ + district.Height;
        for (var x = minX; x <= maxX; x += 3)
        {
            yield return (x, minZ);
            yield return (x, maxZ);
        }

        for (var z = minZ + 3; z <= maxZ - 3; z += 3)
        {
            yield return (minX, z);
            yield return (maxX, z);
        }
    }

    private static SettlementMapStyle BuildStyle(SettlementProductionProfile? profile)
    {
        var biome = BiomeStyle(profile?.Biome);
        var tech = TechStyle(profile?.TechLevel);
        var archetype = (profile?.Archetype ?? ProductionArchetype.Balanced).ToString().ToLowerInvariant();
        var wallStuff = biome switch
        {
            "arid" => "SandstoneBlocks",
            "desert" => "SandstoneBlocks",
            "cold" => tech == "industrial" ? "Steel" : "GraniteBlocks",
            _ => tech == "industrial" ? "Steel" : "WoodLog",
        };
        var roadTerrain = tech == "industrial" ? "Concrete" : "PackedDirt";
        return new SettlementMapStyle(
            $"{biome}-{tech}-{archetype}",
            wallStuff,
            roadTerrain,
            tech == "industrial" ? "Concrete" : "WoodPlankFloor");
    }

    private static string BiomeStyle(string? biome)
    {
        var value = (biome ?? string.Empty).ToLowerInvariant();
        if (ContainsOrdinal(value, "arid"))
        {
            return "arid";
        }

        if (ContainsOrdinal(value, "desert"))
        {
            return "desert";
        }

        if (ContainsOrdinal(value, "boreal") || ContainsOrdinal(value, "tundra") || ContainsOrdinal(value, "ice"))
        {
            return "cold";
        }

        return "temperate";
    }

    private static string TechStyle(string? techLevel)
    {
        return TechScore(techLevel) >= 3 ? "industrial" : "tribal";
    }

    private static string WallStuffDefName(int techScore)
    {
        return techScore >= 3
            ? "Steel"
            : "WoodLog";
    }

    private static string FloorTerrainDefName(SettlementFacilityKind kind, int techScore)
    {
        if (kind == SettlementFacilityKind.Clinic && techScore >= 3)
        {
            return "SterileTile";
        }

        return techScore >= 3
            ? "Concrete"
            : "WoodPlankFloor";
    }

    private static int TechScore(string? techLevel)
    {
        if (string.IsNullOrWhiteSpace(techLevel))
        {
            return 0;
        }

        var normalized = (techLevel ?? string.Empty).Trim().ToLowerInvariant();
        if (ContainsOrdinal(normalized, "spacer") || ContainsOrdinal(normalized, "ultra"))
        {
            return 4;
        }

        if (ContainsOrdinal(normalized, "industrial"))
        {
            return 3;
        }

        if (ContainsOrdinal(normalized, "medieval"))
        {
            return 2;
        }

        return ContainsOrdinal(normalized, "neolithic")
            ? 1
            : 0;
    }

    private static bool ContainsOrdinal(string value, string token)
    {
        return value.IndexOf(token, StringComparison.Ordinal) >= 0;
    }
}

public enum AnimalMapFateKind
{
    Returned,
    Dead,
    Missing,
    TakenByPlayer
}

public enum AnimalMapFateSyncStatus
{
    Success,
    InvalidRequest,
    UnknownCohort
}

public sealed record AnimalMapFateSyncRequest(
    EntityId CohortId,
    string AnimalKind,
    AnimalCohortType Type,
    int Count,
    AnimalMapFateKind Fate,
    int Tick,
    string Reason);

public sealed record AnimalMapFateSyncResult(
    AnimalMapFateSyncStatus Status,
    string Reason,
    int ReturnedCount);

public static class AnimalMapFateSyncService
{
    public static AnimalMapFateSyncResult Resolve(WorldState state, AnimalMapFateSyncRequest request)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (request.CohortId.Kind != EntityKind.Animal
            || request.Count <= 0
            || request.Tick < 0
            || string.IsNullOrWhiteSpace(request.AnimalKind)
            || string.IsNullOrWhiteSpace(request.Reason))
        {
            return new AnimalMapFateSyncResult(
                AnimalMapFateSyncStatus.InvalidRequest,
                "Animal map fate sync requires a cohort, positive count, tick, animal kind and reason.",
                0);
        }

        var cohort = state.GetAnimalCohort(request.CohortId);
        if (cohort == null)
        {
            return new AnimalMapFateSyncResult(
                AnimalMapFateSyncStatus.UnknownCohort,
                $"Animal cohort {request.CohortId} does not exist.",
                0);
        }

        if (request.Fate == AnimalMapFateKind.Returned)
        {
            var returned = AnimalMapMaterializationService.ReturnToCohorts(
                state,
                new[]
                {
                    new MaterializedAnimalStack(
                        request.CohortId,
                        request.AnimalKind,
                        request.Type,
                        request.Count)
                },
                request.Tick,
                request.Reason);

            return new AnimalMapFateSyncResult(
                AnimalMapFateSyncStatus.Success,
                $"Returned {returned} animal(s) to cohort {request.CohortId}.",
                returned);
        }

        state.RecordEvent(
            WorldEventKind.AnimalCohortDeclined,
            request.CohortId,
            $"Animal cohort {request.CohortId} lost {request.Count} {request.AnimalKind} on settlement map: {request.Reason}.");
        return new AnimalMapFateSyncResult(
            AnimalMapFateSyncStatus.Success,
            $"Recorded {request.Count} animal(s) as {request.Fate}.",
            0);
    }
}
