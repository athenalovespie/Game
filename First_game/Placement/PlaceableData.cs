using System;
using System.Collections.Generic;
using First_game.World;
using Microsoft.Xna.Framework;

namespace First_game.Placement;

public enum PlacementAnchor { TopLeft, BottomCenter }

/// <summary>Immutable item metadata. Construct once when registering an item.</summary>
public sealed class PlaceableData
{
    private readonly HashSet<GroundType> allowedGround;
    public PlaceableData(int width, int height, string spriteAsset,
        PlacementAnchor anchor = PlacementAnchor.BottomCenter, GroundType[] allowedGroundTypes = null,
        bool allowOverlap = false, bool consumeOnPlacement = true, string onPlacedEffect = null,
        bool rotatable = false, float maxRangeTiles = 6f, float spriteScale = 1f,
        float baseInsetPixels = 0f, float groundOffsetX = 0f)
    {
        if (width < 1 || height < 1 || width > 256 || height > 256) throw new ArgumentOutOfRangeException(nameof(width));
        if (string.IsNullOrWhiteSpace(spriteAsset)) throw new ArgumentException("A placed sprite asset is required.");
        if (!float.IsFinite(maxRangeTiles) || maxRangeTiles <= 0 || !float.IsFinite(spriteScale) || spriteScale <= 0
            || !float.IsFinite(baseInsetPixels) || !float.IsFinite(groundOffsetX)) throw new ArgumentOutOfRangeException(nameof(maxRangeTiles));
        if (!Enum.IsDefined(anchor)) throw new ArgumentOutOfRangeException(nameof(anchor));
        Width = width; Height = height; SpriteAsset = spriteAsset; Anchor = anchor;
        allowedGround = new HashSet<GroundType>(allowedGroundTypes ?? new[] { GroundType.Grass, GroundType.Dirt, GroundType.Sand });
        AllowOverlap = allowOverlap; ConsumeOnPlacement = consumeOnPlacement; OnPlacedEffect = onPlacedEffect;
        Rotatable = rotatable; MaxRangeTiles = maxRangeTiles; SpriteScale = spriteScale;
        BaseInsetPixels = baseInsetPixels; GroundOffsetX = groundOffsetX;
    }
    public int Width { get; }
    public int Height { get; }
    public string SpriteAsset { get; }
    public PlacementAnchor Anchor { get; }
    public bool AllowOverlap { get; }
    public bool ConsumeOnPlacement { get; }
    public string OnPlacedEffect { get; }
    public bool Rotatable { get; }
    public float MaxRangeTiles { get; }
    public float SpriteScale { get; }
    public float BaseInsetPixels { get; }
    public float GroundOffsetX { get; }
    public bool AllowsGround(GroundType ground) => ground != GroundType.Water && ground != GroundType.Wall && allowedGround.Contains(ground);
    public bool AllowsRotation(int rotation) => rotation >= 0 && rotation < 4 && (Rotatable || rotation == 0);
    public Point Size(int rotation) => (rotation & 1) == 0 ? new(Width, Height) : new(Height, Width);
    public Point OriginFromCursor(Point cursor, int rotation)
    {
        Point size = Size(rotation);
        return Anchor == PlacementAnchor.TopLeft ? cursor : new Point(cursor.X - size.X / 2, cursor.Y - size.Y + 1);
    }
}
