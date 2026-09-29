using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using First_game.Inventory;
using PlayerInventory = First_game.Inventory.Inventory;

namespace First_game.UI;

public sealed class InventoryPanel : IMenuPanel
{
    private readonly PlayerInventory inventory;
    private readonly ItemDefinitionRegistry definitions;
    private readonly Texture2D background;
    private readonly SpriteFont font;
    private readonly Func<string, Texture2D> loadIcon;

    public InventoryPanel(PlayerInventory inventory, ItemDefinitionRegistry definitions,
        Texture2D background, SpriteFont font, Func<string, Texture2D> loadIcon)
    {
        this.inventory = inventory;
        this.definitions = definitions;
        this.background = background;
        this.font = font;
        this.loadIcon = loadIcon;
    }

    public void Update(GameTime gameTime, UIInput input, Viewport viewport)
    {
        // Add slot selection or dragging here. Inventory owns the item data.
    }

    public void Draw(SpriteBatch spriteBatch, Viewport viewport)
    {
        int viewportWidth = viewport.Width;
        int viewportHeight = viewport.Height;
        // This artwork has exactly 12 slots.
        if (inventory.Capacity != 12)
        {
            throw new InvalidOperationException(
                "This inventory layout requires exactly 12 slots.");
        }

        // Fit the panel inside 85% of the screen.
        // Don't enlarge it beyond its original resolution.
        float uiScale = Math.Min(
            1f,
            Math.Min(
                viewportWidth * 0.85f / background.Width,
                viewportHeight * 0.85f / background.Height));

        Vector2 panelPosition = new Vector2(
            (viewportWidth - background.Width * uiScale) / 2f,
            (viewportHeight - background.Height * uiScale) / 2f);

        // The image already contains the title and slot frames.
        spriteBatch.Draw(
            background,
            panelPosition,
            null,
            Color.White,
            0f,
            Vector2.Zero,
            uiScale,
            SpriteEffects.None,
            0f);

        const int columns = 4;

        for (int slotIndex = 0; slotIndex < inventory.Capacity; slotIndex++)
        {
            ItemInstance instance = inventory.GetSlot(slotIndex);

            if (instance == null)
                continue;

            int column = slotIndex % columns;
            int row = slotIndex / columns;

            // Coordinates measured in the original artwork.
            Vector2 localCenter = new Vector2(
                126f + column * 252f,
                206f + row * 252f);

            Vector2 screenCenter =
                panelPosition + localCenter * uiScale;

            ItemDefinition definition =
                definitions.GetRequired(instance.QualifiedId);

            if (!string.IsNullOrWhiteSpace(definition.IconAsset))
            {
                Texture2D icon = loadIcon(definition.IconAsset);

                // Fit the icon inside a 160x160 area,
                // preserving its aspect ratio.
                float iconScale = Math.Min(
                    160f / icon.Width,
                    160f / icon.Height) * uiScale;

                spriteBatch.Draw(
                    icon,
                    screenCenter,
                    null,
                    Color.White,
                    0f,
                    new Vector2(icon.Width / 2f, icon.Height / 2f),
                    iconScale,
                    SpriteEffects.None,
                    0f);
            }

            if (instance.Count > 1)
            {
                string text = instance.CountText;

                // Bottom-right area inside the slot.
                Vector2 countCorner = panelPosition
                    + (localCenter + new Vector2(94f, 94f))
                    * uiScale;

                Vector2 textSize = font.MeasureString(text);
                Vector2 textPosition =
                    countCorner - textSize;
                textPosition = new Vector2(
                MathF.Round(textPosition.X),
                MathF.Round(textPosition.Y));

                spriteBatch.DrawString(
                    font,
                    text,
                    textPosition,
                    new Color(65, 49, 30),
                    0f,
                    Vector2.Zero,
                    uiScale,
                    SpriteEffects.None,
                    0f);
            }
        }
    }


}

