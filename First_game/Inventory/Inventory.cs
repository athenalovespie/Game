using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace First_game.Inventory;

public static class Inventory
{
	public static void Draw(
		SpriteBatch spriteBatch,
		Texture2D pixel,
		SpriteFont font,
		Texture2D fishTexture,
		int fishCollected,
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
		for (int row = 0; row < 3; row++)
		{
			for (int column = 0; column < 9; column++)
			{
				int slotX = gridX + column * (slotSize + slotGap);
				int slotY = gridY + row * (slotSize + slotGap);
				spriteBatch.Draw(pixel, new Rectangle(slotX, slotY, slotSize, slotSize), new Color(20, 23, 20));
				spriteBatch.Draw(pixel, new Rectangle(slotX + 2, slotY + 2, slotSize - 4, slotSize - 4), new Color(77, 79, 64));

				if (row == 0 && column == 0 && fishCollected > 0)
				{
					int iconSize = 26;
					float scale = Math.Min(iconSize / (float)fishTexture.Width, iconSize / (float)fishTexture.Height);
					int iconWidth = (int)(fishTexture.Width * scale);
					int iconHeight = (int)(fishTexture.Height * scale);
					var iconBounds = new Rectangle(
						slotX + (slotSize - iconWidth) / 2,
						slotY + (slotSize - iconHeight) / 2,
						iconWidth,
						iconHeight);
					spriteBatch.Draw(fishTexture, iconBounds, Color.White);
					spriteBatch.DrawString(font, fishCollected.ToString(), new Vector2(slotX + 24, slotY + 22), Color.White);
				}
			}
		}
	}
}