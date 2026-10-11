using First_game.World;
using Microsoft.Xna.Framework;

namespace First_game.Placement;

/// <summary>Common terrain/occupancy validation for player previews, commits, and world resource spawning.</summary>
public static class PlacementValidity
{
    public static bool CanOccupy(WorldGrid grid, PlaceableData data, Point origin, int rotation = 0)
    {
        if (data == null || !data.AllowsRotation(rotation)) return false;
        Point size = data.Size(rotation);
        if (origin.X < 0 || origin.Y < 0 || origin.X > grid.Columns - size.X || origin.Y > grid.Rows - size.Y) return false;
        for (int y = 0; y < size.Y; y++) for (int x = 0; x < size.X; x++)
        {
            GridCell cell = grid.GetCell(origin + new Point(x, y));
            if (cell.PlacementForbidden || !data.AllowsGround(cell.Ground)
                || (!data.AllowOverlap && (cell.IsOccupied() || cell.BlocksMovement))) return false;
        }
        return true;
    }
}
