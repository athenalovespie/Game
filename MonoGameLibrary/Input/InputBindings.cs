using System;
using Microsoft.Xna.Framework.Input;

namespace MonoGameLibrary.Input;

public class InputBindings
{
    private Keys interact = Keys.E;
    public Keys Down { get; set; } = Keys.S;
    public Keys Left { get; set; } = Keys.A;
    public Keys Right { get; set; } = Keys.D;
    public Keys Up { get; set; } = Keys.W;
    public Keys Inventory { get; set; } = Keys.I;

    public Keys Interact
    {
        get => interact;
        set
        {
            if (interact == value) return;
            interact = value;
            InteractChanged?.Invoke();
        }
    }

    public event Action InteractChanged;
}
