using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace First_game.UI;

public sealed class PauseMenu : IMenuPanel
{
    private readonly SpriteFont font;

    public PauseMenu(SpriteFont font) => this.font = font;

    public void Update(GameTime gameTime, UIInput input, Viewport viewport)
    {
        // Add settings or other pause-menu controls here.
    }

    public void Draw(SpriteBatch spriteBatch, Viewport viewport)
    {
        const string text = "Paused\n\nP / Esc: Resume";
        Vector2 position = (new Vector2(viewport.Width, viewport.Height)
            - font.MeasureString(text)) / 2f;
        spriteBatch.DrawString(font, text, position, Color.White);
    }
}
