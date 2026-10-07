using System;
using First_game.Placement;
using First_game.World;
using Microsoft.Xna.Framework;

namespace First_game.Interiors;

/// <summary>Cached immutable metadata; offsets are clockwise quarter turns from the unrotated footprint.</summary>
public sealed class EnterableData
{
    public Point EntranceOffset { get; }
    public Point Outward { get; }
    public string InteriorTemplateId { get; }
    public Point InteriorSpawnTile { get; }
    public bool Shared { get; }
    public EnterableData(Point entranceOffset, string interiorTemplateId, Point interiorSpawnTile,
        bool shared = false, Point? outward = null)
    {
        EntranceOffset = entranceOffset; InteriorTemplateId = interiorTemplateId;
        InteriorSpawnTile = interiorSpawnTile; Shared = shared; Outward = outward ?? new Point(0, 1);
        if (string.IsNullOrWhiteSpace(interiorTemplateId) || Math.Abs((long)Outward.X) + Math.Abs((long)Outward.Y) != 1)
            throw new ArgumentException("An interior ID and cardinal outward direction are required.");
    }
    public void ValidateFootprint(int width, int height)
    {
        Point outside = EntranceOffset + Outward;
        if (EntranceOffset.X < 0 || EntranceOffset.Y < 0 || EntranceOffset.X >= width || EntranceOffset.Y >= height
            || (outside.X >= 0 && outside.Y >= 0 && outside.X < width && outside.Y < height))
            throw new ArgumentException("Entrance must lie on the footprint edge and point out of it.");
    }
    public Point Offset(PlaceableData data, int rotation) => rotation switch {
        0 => EntranceOffset,
        1 => new(data.Height - 1 - EntranceOffset.Y, EntranceOffset.X),
        2 => new(data.Width - 1 - EntranceOffset.X, data.Height - 1 - EntranceOffset.Y),
        3 => new(EntranceOffset.Y, data.Width - 1 - EntranceOffset.X),
        _ => throw new ArgumentOutOfRangeException(nameof(rotation)) };
    public Point Direction(int rotation) => rotation switch {
        0 => Outward, 1 => new(-Outward.Y, Outward.X),
        2 => new(-Outward.X, -Outward.Y), 3 => new(Outward.Y, -Outward.X),
        _ => throw new ArgumentOutOfRangeException(nameof(rotation)) };
    public Point OutsideTile(PlaceableData data, Point origin, int rotation) => origin + Offset(data, rotation) + Direction(rotation);
    public Vector2 ReturnGround(WorldGrid grid, PlaceableData data, Point origin, int rotation, Rectangle playerBounds)
    {
        Point direction = Direction(rotation);
        Vector2 ground = grid.CellCenter(OutsideTile(data, origin, rotation));
        // Wide actors must clear the side of a rotated footprint, while remaining on its approach tile.
        ground.X += direction.X * Math.Max(0, playerBounds.Width / 2f - grid.CellSize / 2f + 1);
        if (direction.Y > 0) ground.Y += Math.Max(0, playerBounds.Height - grid.CellSize / 2f + 1);
        return ground;
    }
}
