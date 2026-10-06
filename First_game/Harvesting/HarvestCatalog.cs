using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace First_game.Harvesting;

/// <summary>Initial balance data. Additional tiers/species need definitions, not new mechanics.</summary>
public static class HarvestCatalog
{
    public const string BasicAxeId = "(T)basic_axe";
    public static ToolDefinition BasicAxe { get; } = new(
        BasicAxeId, "axe", damage: 1, range: 180f,
        swingSeconds: .6f, impactProgress: .5f,
        leftAnimation: "ChopLeft", rightAnimation: "ChopRight");
    public static ResourceDefinition Tree { get; } = new("tree", "axe", maxHealth: 3);
    public static ResourceDefinition Pine { get; } = new("pine", "axe", maxHealth: 3);
    public static IReadOnlyDictionary<string, ToolDefinition> Tools { get; } =
        new ReadOnlyDictionary<string, ToolDefinition>(new Dictionary<string, ToolDefinition>
        {
            [BasicAxeId] = BasicAxe
        });
}
