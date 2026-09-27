using Microsoft.Xna.Framework.Graphics;

namespace First_game.Inventory;

public sealed class WoodItem : Item
{
	public WoodItem(Texture2D icon) : base("wood", "Wood", 99, icon)
	{
	}
}