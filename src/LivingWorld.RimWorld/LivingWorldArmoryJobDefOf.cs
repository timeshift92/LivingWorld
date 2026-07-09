using RimWorld;
using Verse;

namespace LivingWorld.RimWorld;

/// <summary>Job defs for the armory / mobilization module, populated by RimWorld's DefOf reflection.</summary>
[DefOf]
public static class LivingWorldArmoryJobDefOf
{
    public static JobDef LivingWorld_FetchKit = null!;
    public static JobDef LivingWorld_ReturnKit = null!;
}
