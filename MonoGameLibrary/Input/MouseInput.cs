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
    public bool LeftClicked => currentState.LeftButton == ButtonState.Pressed
        && previousState.LeftButton == ButtonState.Released;
    public bool LeftReleased => currentState.LeftButton == ButtonState.Released
        && previousState.LeftButton == ButtonState.Pressed;
    public bool RightClicked => currentState.RightButton == ButtonState.Pressed
        && previousState.RightButton == ButtonState.Released;

    // Call once each frame, including while menus are open.
    public void Update() => Update(Mouse.GetState());

    // Explicit samples also allow deterministic gesture checks without a window.
    public void Update(MouseState state)
    {
        previousState = currentState;
        currentState = state;
    }

    public Vector2 GetWorldPosition(Camera2D camera, Viewport viewport)
    {
        return camera.ScreenToWorld(
            ScreenPosition.ToVector2(), viewport.Width, viewport.Height);
    }
}
