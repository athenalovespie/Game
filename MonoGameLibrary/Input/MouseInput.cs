using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameLibrary.Graphics;

namespace MonoGameLibrary.Input;

public class MouseInput
{
    private MouseState previousState;
    private MouseState currentState;

    public Point ScreenPosition => currentState.Position;
    public bool RightClicked => currentState.RightButton == ButtonState.Pressed
        && previousState.RightButton == ButtonState.Released;

    // Call once each frame, including while menus are open.
    public void Update()
    {
        previousState = currentState;
        currentState = Mouse.GetState();
    }

    public Vector2 GetWorldPosition(Camera2D camera, Viewport viewport)
    {
        return camera.ScreenToWorld(
            ScreenPosition.ToVector2(), viewport.Width, viewport.Height);
    }
}
