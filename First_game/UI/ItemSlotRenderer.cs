using System;
using First_game.Inventory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace First_game.UI;

/// <summary>Draws item icons, counts, key labels, and the supplied selection artwork.</summary>
public sealed class ItemSlotRenderer
{
    private static readonly string[] KeyLabels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };
    private static readonly Color Ink = new(65, 49, 30);
    private readonly ItemDefinitionRegistry definitions;
    private readonly Func<string, Texture2D> loadIcon;
    private readonly SpriteFont font;
    private readonly Texture2D highlight;

    public ItemSlotRenderer(ItemDefinitionRegistry definitions, Func<string, Texture2D> loadIcon,
        SpriteFont font, Texture2D highlight)
    {
        this.definitions = definitions;
        this.loadIcon = loadIcon;
        this.font = font;
        this.highlight = highlight;
    }

    public void Draw(SpriteBatch batch, Rectangle bounds, ItemInstance item, int keySlot = -1,
        bool selected = false, bool moving = false)
    {
        float textScale = Math.Min(0.65f, bounds.Width / 150f);
        float padding = Math.Max(3, bounds.Width * 0.07f);
        if (item != null)
        {
            ItemDefinition definition = definitions.GetRequired(item.QualifiedId);
            if (!string.IsNullOrWhiteSpace(definition.IconAsset))
            {
                Texture2D icon = loadIcon(definition.IconAsset);
                float scale = Math.Min(bounds.Width * 0.65f / icon.Width, bounds.Height * 0.65f / icon.Height);
                batch.Draw(icon, bounds.Center.ToVector2(), null, Color.White, 0,
                    new Vector2(icon.Width, icon.Height) / 2, scale, SpriteEffects.None, 0);
            }
            else
            {
                // Items without an icon still have a visible, identifiable slot.
                Vector2 size = font.MeasureString(definition.Name);
                float scale = Math.Min(textScale, bounds.Width * 0.8f / Math.Max(1, size.X));
                batch.DrawString(font, definition.Name, bounds.Center.ToVector2() - size * scale / 2,
                    Ink, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            }

            if (item.Count > 1)
            {
                Vector2 size = font.MeasureString(item.CountText) * textScale;
                batch.DrawString(font, item.CountText,
                    new Vector2(bounds.Right - padding, bounds.Bottom - padding) - size,
                    Ink, 0, Vector2.Zero, textScale, SpriteEffects.None, 0);
            }
        }

        if (keySlot >= 0)
            batch.DrawString(font, KeyLabels[keySlot],
                new Vector2(bounds.X + padding, bounds.Y + padding), Ink,
                0, Vector2.Zero, textScale, SpriteEffects.None, 0);

        // Draw the transparent frame last so it sits above the bar and item icon.
        if (selected || moving)
            batch.Draw(highlight, bounds, moving ? new Color(180, 220, 255) : Color.White);
    }

    public void DrawCaption(SpriteBatch batch, string text, Vector2 center, float maxWidth)
    {
        Vector2 size = font.MeasureString(text);
        float scale = Math.Min(0.65f, maxWidth / Math.Max(1, size.X));
        Vector2 position = center - size * scale / 2;
        batch.DrawString(font, text, position + Vector2.One, Color.Black,
            0, Vector2.Zero, scale, SpriteEffects.None, 0);
        batch.DrawString(font, text, position, Color.White,
            0, Vector2.Zero, scale, SpriteEffects.None, 0);
    }

    public string GetItemName(ItemInstance item) =>
        item == null ? "Empty slot" : definitions.GetRequired(item.QualifiedId).Name;
}
