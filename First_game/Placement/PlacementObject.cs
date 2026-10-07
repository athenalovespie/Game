using System;
using First_game.Inventory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace First_game.Placement;

public sealed class PlacementObject
{
    internal PlacementObject(ItemDefinition definition, Point origin, int rotation, int quality, int? durability)
    {
        Definition = definition; OriginTile = origin; Rotation = rotation; Quality = quality; Durability = durability;
    }
    public ItemDefinition Definition { get; }
    public Point OriginTile { get; }
    public int Rotation { get; }
    public int Quality { get; }
    public int? Durability { get; }
    internal Action<SpriteBatch> DrawAction { get; set; }
}
