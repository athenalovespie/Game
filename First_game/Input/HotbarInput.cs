using First_game.UI;
using Microsoft.Xna.Framework.Input;

namespace First_game.Input;

public static class HotbarInput
{
    private static readonly Keys[] NumberKeys =
    {
        Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5,
        Keys.D6, Keys.D7, Keys.D8, Keys.D9, Keys.D0
    };

    private static readonly Keys[] NumpadKeys =
    {
        Keys.NumPad1, Keys.NumPad2, Keys.NumPad3, Keys.NumPad4, Keys.NumPad5,
        Keys.NumPad6, Keys.NumPad7, Keys.NumPad8, Keys.NumPad9, Keys.NumPad0
    };

    // Return a zero-based slot once per press. The 0 key selects slot ten.
    public static int GetPressedSlot(UIInput input)
    {
        for (int slot = 0; slot < NumberKeys.Length; slot++)
            if (input.Pressed(NumberKeys[slot]) || input.Pressed(NumpadKeys[slot]))
                return slot;
        return -1;
    }
}
