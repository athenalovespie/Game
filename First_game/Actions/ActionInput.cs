using Microsoft.Xna.Framework.Input;

namespace First_game.Actions;

/// <summary>Input snapshot; actions never poll devices or update shared input themselves.</summary>
public readonly record struct ActionInput(bool ConfirmPressed, bool ConfirmHeld, bool CancelPressed)
{
    public static ActionInput FromKeyboard(KeyboardState current, KeyboardState previous) => new(
        current.IsKeyDown(Keys.Space) && previous.IsKeyUp(Keys.Space),
        current.IsKeyDown(Keys.Space),
        current.IsKeyDown(Keys.Q) && previous.IsKeyUp(Keys.Q));
}
