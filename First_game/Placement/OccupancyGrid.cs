using System.Collections.Generic;
using First_game.World;
using Microsoft.Xna.Framework;

namespace First_game.Placement;

/// <summary>A separate occupancy layer preserves static objects and overlapping placements.</summary>
public sealed class OccupancyGrid
{
    private readonly WorldGrid grid;
    private readonly Dictionary<Point, List<PlacementObject>> cells = new();
    public OccupancyGrid(WorldGrid grid) { this.grid = grid; }
    public PlacementObject At(Point tile) => cells.TryGetValue(tile, out var objects) ? objects[objects.Count - 1] : null;
    internal void Add(PlacementObject obj)
    {
        Point size = obj.Definition.Placeable.Size(obj.Rotation);
        for (int y = 0; y < size.Y; y++) for (int x = 0; x < size.X; x++)
        {
            Point tile = obj.OriginTile + new Point(x, y);
            if (!cells.TryGetValue(tile, out var objects)) cells.Add(tile, objects = new());
            objects.Add(obj);
            grid.GetCell(tile).PlacementCount++;
        }
    }
    internal void Remove(PlacementObject obj)
    {
        Point size = obj.Definition.Placeable.Size(obj.Rotation);
        for (int y = 0; y < size.Y; y++) for (int x = 0; x < size.X; x++)
        {
            Point tile = obj.OriginTile + new Point(x, y);
            if (!cells.TryGetValue(tile, out var objects) || !objects.Remove(obj)) continue;
            grid.GetCell(tile).PlacementCount--;
            if (objects.Count == 0) cells.Remove(tile);
        }
    }
}
