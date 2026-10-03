using MonoGameLibrary.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace First_game.UI;

public sealed class CraftingPanel : IMenuPanel
{
    private readonly SpriteFont font;

    private readonly InputBindings bindings;
    private string text;
    private Vector2 textSize;

    public CraftingPanel(SpriteFont font, InputBindings bindings = null)
    {
        this.font = font;
        this.bindings = bindings ?? new InputBindings();
        this.bindings.InventoryChanged += RefreshText;
        RefreshText();
    }

    private void RefreshText()
    {
        text = "Crafting\nNo recipes yet.\n\n" + bindings.Inventory + ": Inventory    C / Esc: Close";
        textSize = font?.MeasureString(text) ?? Vector2.Zero;
    }

    public void Update(GameTime gameTime, UIInput input, Viewport viewport)
    {
        // Add recipe selection here, then ask a crafting system to craft.
    }

    public void Draw(SpriteBatch spriteBatch, Viewport viewport)
    {
        Vector2 position = (new Vector2(viewport.Width, viewport.Height)
            - textSize) / 2f;
        spriteBatch.DrawString(font, text, position, Color.White);
    }
}
