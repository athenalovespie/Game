using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using First_game.Placement;
using First_game.World;
using Microsoft.Xna.Framework;

namespace First_game.Regrowing;

/// <summary>New plants require only JSON entries and content sprites.</summary>
public sealed class HarvestableCatalog
{
    public PlantEntry[] Plants { get; set; } = Array.Empty<PlantEntry>();
    public static SpawnNodeConfig[] Load(string path, HarvestableRegistry registry, WorldSpawnRuleRegistry rules)
    {
        var catalog = JsonSerializer.Deserialize<HarvestableCatalog>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Missing harvestable catalog.");
        var nodes = new List<SpawnNodeConfig>();
        foreach (var plant in catalog.Plants)
        {
            var data = new HarvestableData(plant.Id,
                new PlaceableData(plant.Width, plant.Height, plant.States[0].SpriteAsset,
                    spriteScale: plant.SpriteScale, baseInsetPixels: plant.BaseInsetPixels),
                plant.States, plant.Yield, plant.RipeState, plant.AfterHarvestState,
                plant.RegrowSeconds, plant.PromptText, plant.RangePixels);
            registry.Register(data);
            foreach (var spawn in plant.Spawns)
            {
                rules.Register(new WorldSpawnRule(spawn.RuleId, spawn.NodeType, plant.Yield.QualifiedItemId,
                    spawn.Chance, plant.Yield.Minimum, plant.Yield.Maximum, spawn.RetrySeconds, plant.Id));
                nodes.Add(new SpawnNodeConfig(spawn.NodeType, spawn.Count,
                    new Vector2(spawn.MinimumX, spawn.MinimumY), new Vector2(spawn.MaximumX, spawn.MaximumY)));
            }
        }
        return nodes.ToArray();
    }
    public sealed class PlantEntry
    {
        public string Id { get; set; }
        public int Width { get; set; } = 1;
        public int Height { get; set; } = 1;
        public float SpriteScale { get; set; } = 1;
        public float BaseInsetPixels { get; set; }
        public HarvestableState[] States { get; set; }
        public HarvestYield Yield { get; set; }
        public string RipeState { get; set; }
        public string AfterHarvestState { get; set; }
        public double RegrowSeconds { get; set; }
        public string PromptText { get; set; }
        public int RangePixels { get; set; } = 140;
        public SpawnEntry[] Spawns { get; set; } = Array.Empty<SpawnEntry>();
    }
    public sealed class SpawnEntry
    {
        public string RuleId { get; set; }
        public string NodeType { get; set; }
        public float Chance { get; set; } = 1;
        public double RetrySeconds { get; set; } = 90;
        public int Count { get; set; } = 1;
        public float MinimumX { get; set; }
        public float MinimumY { get; set; }
        public float MaximumX { get; set; }
        public float MaximumY { get; set; }
    }
}
