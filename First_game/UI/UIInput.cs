using Microsoft.Xna.Framework.Input;
using MonoGameLibrary.Input;

namespace First_game.UI;

// A single frame's input, shared by the active panel.
public readonly struct UIInput
{
    public KeyboardState Keyboard { get; }
    public KeyboardState PreviousKeyboard { get; }
    public MouseInput Mouse { get; }

    public UIInput(KeyboardState keyboard, KeyboardState previousKeyboard, MouseInput mouse)
    {
        Keyboard = keyboard;
        PreviousKeyboard = previousKeyboard;
        Mouse = mouse;
    }

    public bool Pressed(Keys key) => Keyboard.IsKeyDown(key) && PreviousKeyboard.IsKeyUp(key);
}
