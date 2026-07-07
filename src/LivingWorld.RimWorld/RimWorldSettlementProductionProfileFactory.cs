using System;
using LivingWorld.Core;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LivingWorld.RimWorld;

internal static class RimWorldSettlementProductionProfileFactory
{
    public static SettlementProductionProfile Create(
        WorldObjectSettlementCandidate candidate,
        EntityId settlementId,
        Faction? faction)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }

        var tile = TryGetTile(candidate.Tile);
        var biome = tile?.PrimaryBiome?.defName ?? candidate.DefName;
        var hilliness = tile?.HillinessLabel.ToString() ?? "UnknownHilliness";
        var averageTemperature = tile == null
            ? 20
            : (int)Math.Round((tile.MinTemperature + tile.MaxTemperature) / 2f);
        var plantDensity = tile?.PlantDensityFactor
            ?? tile?.PrimaryBiome?.plantDensity
            ?? 0.35f;
        var growingDays = EstimateGrowingDays(averageTemperature, plantDensity);
        var rainfall = EstimateRainfall(plantDensity, tile?.PrimaryBiome);
        var techLevel = EstimateTechLevel(faction);

        return SettlementProductionProfile.FromEnvironment(
            settlementId,
            new SettlementProductionEnvironment(
                biome,
                hilliness,
                techLevel,
                growingDays,
                rainfall,
                averageTemperature));
    }

    private static Tile? TryGetTile(int tileId)
    {
        if (tileId < 0 || Find.WorldGrid == null)
        {
            return null;
        }

        try
        {
            PlanetTile planetTile = tileId;
            return Find.WorldGrid[planetTile];
        }
        catch (Exception ex)
        {
            Log.Warning($"[LivingWorld] Failed to read production tile {tileId}: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    private static int EstimateGrowingDays(int averageTemperature, float plantDensity)
    {
        if (plantDensity <= 0.05f || averageTemperature <= -5)
        {
            return 0;
        }

        if (averageTemperature < 5)
        {
            return 20;
        }

        if (averageTemperature <= 30)
        {
            return plantDensity >= 0.45f ? 60 : 45;
        }

        return plantDensity >= 0.35f ? 35 : 15;
    }

    private static int EstimateRainfall(float plantDensity, BiomeDef? biome)
    {
        var biomeModifier = biome?.isExtremeBiome == true ? -150 : 0;
        return Math.Max(0, (int)Math.Round(plantDensity * 1000f) + biomeModifier);
    }

    private static string EstimateTechLevel(Faction? faction)
    {
        var def = faction?.def;
        if (def == null)
        {
            return "UnknownTech";
        }

        var defName = def.defName ?? string.Empty;
        if (defName.IndexOf("Empire", StringComparison.OrdinalIgnoreCase) >= 0
            || defName.IndexOf("Mech", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Spacer";
        }

        if (defName.IndexOf("Tribe", StringComparison.OrdinalIgnoreCase) >= 0
            || defName.IndexOf("Tribal", StringComparison.OrdinalIgnoreCase) >= 0
            || !def.humanlikeFaction)
        {
            return "Neolithic";
        }

        return def.startingResearchTags != null && def.startingResearchTags.Count > 0
            ? "Industrial"
            : "Medieval";
    }
}
