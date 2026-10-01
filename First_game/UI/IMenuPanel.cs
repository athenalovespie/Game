using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace First_game.UI;

public interface IMenuPanel
{
    void Update(GameTime gameTime, UIInput input, Viewport viewport);
    void Draw(SpriteBatch spriteBatch, Viewport viewport);

    // Panels may clear temporary interactions when closed or replaced by another menu.
    void OnClosed() { }
}
