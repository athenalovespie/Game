using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace First_game.Inventory;

public sealed class Inventory
{
	public const int Rows = 3;
	public const int Columns = 9;
	private readonly ItemInstance[,] _slots = new ItemInstance[Rows, Columns];

	public bool TryAdd(Item item, int quantity = 1)
	{
		if (item == null)
			throw new ArgumentNullException(nameof(item));
		if (quantity < 1)
			throw new ArgumentOutOfRangeException(nameof(quantity));

		long availableCapacity = 0;
		for (int row = 0; row < Rows; row++)
		{
			for (int column = 0; column < Columns; column++)
			{
				ItemInstance slot = _slots[row, column];
				if (slot == null)
					availableCapacity += item.MaxStack;
				else if (string.Equals(slot.Item.Id, item.Id, StringComparison.Ordinal))
					availableCapacity += item.MaxStack - slot.Quantity;
			}
		}

		if (availableCapacity < quantity)
			return false;

		int remaining = quantity;
		for (int row = 0; row < Rows && remaining > 0; row++)
		{
			for (int column = 0; column < Columns && remaining > 0; column++)
			{
				ItemInstance slot = _slots[row, column];
				if (slot != null && string.Equals(slot.Item.Id, item.Id, StringComparison.Ordinal))
					remaining = slot.AddQuantity(remaining);
			}
		}

		for (int row = 0; row < Rows && remaining > 0; row++)
		{
			for (int column = 0; column < Columns && remaining > 0; column++)
			{
				if (_slots[row, column] != null)
					continue;

				int stackQuantity = Math.Min(remaining, item.MaxStack);
				_slots[row, column] = new ItemInstance(item, stackQuantity);
				remaining -= stackQuantity;
			}
		}

		return remaining == 0;
	}

	public ItemInstance GetSlot(int row, int column)
	{
		return _slots[row, column];
	}

	public void Draw(
		SpriteBatch spriteBatch,
		Texture2D pixel,
		SpriteFont font,
		int viewportWidth,
		int viewportHeight)
	{
		const int panelWidth = 460;
		const int panelHeight = 210;
		const int slotSize = 40;
		const int slotGap = 5;
		int panelX = (viewportWidth - panelWidth) / 2;
		int panelY = (viewportHeight - panelHeight) / 2;

		spriteBatch.Draw(pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.55f);
		spriteBatch.Draw(pixel, new Rectangle(panelX, panelY, panelWidth, panelHeight), new Color(48, 52, 45));
		spriteBatch.Draw(pixel, new Rectangle(panelX, panelY, panelWidth, 3), new Color(179, 167, 125));
		spriteBatch.DrawString(font, "Inventory", new Vector2(panelX + 24, panelY + 18), Color.White);

		int gridX = panelX + (panelWidth - (9 * slotSize + 8 * slotGap)) / 2;
		int gridY = panelY + 54;
		for (int row = 0; row < Rows; row++)
		{
			for (int column = 0; column < Columns; column++)
			{
				int slotX = gridX + column * (slotSize + slotGap);
				int slotY = gridY + row * (slotSize + slotGap);
				spriteBatch.Draw(pixel, new Rectangle(slotX, slotY, slotSize, slotSize), new Color(20, 23, 20));
				spriteBatch.Draw(pixel, new Rectangle(slotX + 2, slotY + 2, slotSize - 4, slotSize - 4), new Color(77, 79, 64));

				ItemInstance itemInstance = _slots[row, column];
				if (itemInstance == null)
					continue;

				Texture2D icon = itemInstance.Item.Icon;
				int iconSize = 26;
				float scale = Math.Min(iconSize / (float)icon.Width, iconSize / (float)icon.Height);
				int iconWidth = (int)(icon.Width * scale);
				int iconHeight = (int)(icon.Height * scale);
				var iconBounds = new Rectangle(
					slotX + (slotSize - iconWidth) / 2,
					slotY + (slotSize - iconHeight) / 2,
					iconWidth,
					iconHeight);
				spriteBatch.Draw(icon, iconBounds, Color.White);
				if (itemInstance.Quantity > 1)
					spriteBatch.DrawString(font, itemInstance.Quantity.ToString(), new Vector2(slotX + 24, slotY + 22), Color.White);
			}
		}
	}
}