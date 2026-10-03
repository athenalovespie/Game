using First_game.Doors;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace First_game.UI;

public sealed class DoorOverlay
{
    private readonly DoorSystem doors;
    private readonly Texture2D pixel;
    private readonly SpriteFont font;
    private readonly InventoryPanel inventoryPanel;
    private string prompt;
    private Vector2 promptSize;

    public DoorOverlay(DoorSystem doors, Texture2D pixel, InventoryPanel inventoryPanel)
    {
        this.doors = doors;
        this.pixel = pixel;
        this.inventoryPanel = inventoryPanel;
        font = inventoryPanel.CaptionFont;
        doors.PromptChanged += SetPrompt;
        SetPrompt(doors.Prompt);
    }

    private void SetPrompt(string value)
    {
        if (ReferenceEquals(prompt, value)) return;
        prompt = value;
        promptSize = value == null ? Vector2.Zero : font.MeasureString(value);
    }

    public void Draw(SpriteBatch batch, Viewport viewport)
    {
        if (prompt != null)
        {
            float scale = inventoryPanel.GetHintScale(viewport);
            Vector2 scaledSize = promptSize * scale;
            Vector2 position = new Vector2((viewport.Width - scaledSize.X) * .5f, 36);
            batch.Draw(pixel, new Rectangle((int)position.X - 12, 28,
                (int)scaledSize.X + 24, (int)scaledSize.Y + 16), Color.Black * .8f);
            batch.DrawString(font, prompt, position + Vector2.One, Color.Black,
                0, Vector2.Zero, scale, SpriteEffects.None, 0);
            batch.DrawString(font, prompt, position, Color.White,
                0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }
        if (doors.FadeOpacity > 0)
            batch.Draw(pixel, viewport.Bounds, Color.Black * doors.FadeOpacity);
    }
}
