using System;
using First_game.World;
using Microsoft.Xna.Framework;

namespace First_game.Interiors;

public static class ReturnPosition
{
    public static Rectangle BoundsAtGround(Rectangle shape, Vector2 ground) => new(
        (int)ground.X - shape.Width / 2, (int)ground.Y - shape.Height, shape.Width, shape.Height);
    public static bool IsFree(WorldGrid grid, Vector2 ground, Rectangle shape)
    {
        Rectangle bounds = BoundsAtGround(shape, ground);
        return grid.IsValidCell(grid.WorldToCell(new Vector2(bounds.Left, bounds.Top)))
            && grid.IsValidCell(grid.WorldToCell(new Vector2(Math.Max(bounds.Left, bounds.Right - 1), Math.Max(bounds.Top, bounds.Bottom - 1))))
            && !grid.IntersectsBlockedCell(bounds);
    }
    // Rare exit-time search, deterministic nearest tile by squared world distance; no frame-time scanning.
    public static bool TryFind(WorldGrid grid, Vector2 preferred, Rectangle shape, out Vector2 result)
    {
        result = preferred;
        if (IsFree(grid, preferred, shape)) return true;
        float best = float.PositiveInfinity;
        for (int y = 0; y < grid.Rows; y++) for (int x = 0; x < grid.Columns; x++)
        {
            Vector2 candidate = grid.CellCenter(new Point(x, y));
            float distance = Vector2.DistanceSquared(candidate, preferred);
            if (distance >= best || !IsFree(grid, candidate, shape)) continue;
            best = distance; result = candidate;
        }
        return float.IsFinite(best);
    }
}
