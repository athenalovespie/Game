using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoGameLibrary.Graphics;

namespace First_game.World;

public class PlacedObject
{
    public Sprite Sprite { get; }
    public Point StartingCell { get; }

    private readonly Point[] footprint;

    public PlacedObject(Sprite sprite, Point startingCell, Point[] footprint)
    {
        Sprite = sprite;
        StartingCell = startingCell;
        // Preserve the placement even if the caller later edits its array.
        this.footprint = (Point[])footprint.Clone();
    }

    public IEnumerable<Point> GetOccupiedCells()
    {
        foreach (Point offset in footprint)
        {
            yield return new Point(
                StartingCell.X + offset.X,
                StartingCell.Y + offset.Y);
        }
    }
}
