using Microsoft.Xna.Framework.Graphics;

namespace First_game.Inventory;

public sealed class FishItem : Item
{
	public FishItem(Texture2D icon) : base("fish", "Fish", 99, icon)
	{
	}
}