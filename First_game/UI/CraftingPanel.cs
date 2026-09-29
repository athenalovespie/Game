using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace First_game.UI;

public sealed class CraftingPanel : IMenuPanel
{
    private readonly SpriteFont font;

    public CraftingPanel(SpriteFont font) => this.font = font;

    public void Update(GameTime gameTime, UIInput input, Viewport viewport)
    {
        // Add recipe selection here, then ask a crafting system to craft.
    }

    public void Draw(SpriteBatch spriteBatch, Viewport viewport)
    {
        const string text = "Crafting\nNo recipes yet.\n\nE: Inventory    C / Esc: Close";
        Vector2 position = (new Vector2(viewport.Width, viewport.Height)
            - font.MeasureString(text)) / 2f;
        spriteBatch.DrawString(font, text, position, Color.White);
    }
}
