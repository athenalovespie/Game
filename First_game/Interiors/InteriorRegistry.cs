using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using First_game.Doors;
using Microsoft.Xna.Framework;

namespace First_game.Interiors;

/// <summary>Data is read and validated once. Templates contain no per-instance mutable state or GPU assets.</summary>
public sealed class InteriorRegistry
{
    private readonly Dictionary<string, InteriorTemplate> templates = new(StringComparer.Ordinal);
    public void Register(InteriorTemplate template) => templates.Add(template.Id, template);
    public InteriorTemplate GetRequired(string id) => templates.TryGetValue(id, out var template)
        ? template : throw new InvalidDataException("Unknown interior template: " + id);
    public static InteriorRegistry Load(string path)
    {
        var definitions = JsonSerializer.Deserialize<TemplateDefinition[]>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Missing interior templates.");
        var registry = new InteriorRegistry();
        foreach (var d in definitions)
        {
            if (d.ExitTile == null || d.ExitTile.Length != 2) throw new InvalidDataException("ExitTile requires x,y.");
            registry.Register(new InteriorTemplate(d.Id, d.WidthTiles, d.HeightTiles, d.TileSize,
                d.WallThickness, new Point(d.ExitTile[0], d.ExitTile[1])));
        }
        return registry;
    }
    public sealed class TemplateDefinition
    {
        public string Id { get; set; }
        public int WidthTiles { get; set; }
        public int HeightTiles { get; set; }
        public int TileSize { get; set; } = 100;
        public int WallThickness { get; set; } = 16;
        public int[] ExitTile { get; set; }
    }
}

public sealed class InteriorTemplate
{
    public string Id { get; }
    public int WidthTiles { get; }
    public int HeightTiles { get; }
    public int TileSize { get; }
    public int WallThickness { get; }
    public Point ExitTile { get; }
    public InteriorTemplate(string id, int widthTiles, int heightTiles, int tileSize, int wallThickness, Point exitTile)
    {
        if (string.IsNullOrWhiteSpace(id) || widthTiles < 3 || heightTiles < 3 || widthTiles > 256 || heightTiles > 256
            || tileSize < 16 || tileSize > 1024 || wallThickness <= 0 || wallThickness >= tileSize / 2
            || exitTile.X < 0 || exitTile.Y < 0 || exitTile.X >= widthTiles || exitTile.Y >= heightTiles)
            throw new ArgumentException("Invalid interior template.");
        Id = id; WidthTiles = widthTiles; HeightTiles = heightTiles; TileSize = tileSize;
        WallThickness = wallThickness; ExitTile = exitTile;
    }
    public Vector2 TileCenter(Point tile) => new((tile.X + .5f) * TileSize, (tile.Y + .5f) * TileSize);
    public Rectangle TileBounds(Point tile) => new(tile.X * TileSize, tile.Y * TileSize, TileSize, TileSize);
    public ResidentArea CreateArea(string instanceKey) => new("interior:" + instanceKey,
        new Rectangle(0, 0, WidthTiles * TileSize, HeightTiles * TileSize), WallThickness) { DrawRug = false };
}
