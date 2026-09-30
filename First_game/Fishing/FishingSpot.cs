using System.Collections.Generic;
using Microsoft.Xna.Framework;
using First_game.World;

namespace First_game.Fishing;

public sealed class FishingSpot
{
    private readonly WorldGrid grid;
    private readonly HashSet<Point> cells;
    public string ItemId { get; }
    public FishingSettings Settings { get; }

    public FishingSpot(WorldGrid grid, IEnumerable<Point> cells, string itemId, FishingSettings settings)
    {
        settings.Validate();
        this.grid = grid;
        this.cells = new HashSet<Point>(cells);
        ItemId = itemId;
        Settings = settings;
    }
    public bool Contains(Vector2 position) => cells.Contains(grid.WorldToCell(position));
}
