namespace LivingWorld.Core;

public enum ResourceCategory
{
    Food,
    Material,
    Medicine,
    Component,
    Luxury,
    Drug,
    Other
}

public sealed record WorldResourceKey(
    string DefName,
    ResourceCategory Category,
    bool IsPerishable,
    float MarketValue,
    float Mass)
{
    public static WorldResourceKey FromDefName(string defName)
    {
        if (string.IsNullOrWhiteSpace(defName))
        {
            throw new ArgumentException("Resource defName cannot be empty.", nameof(defName));
        }

        return new WorldResourceKey(
            defName,
            ResourceCategory.Other,
            false,
            0,
            0);
    }
}
