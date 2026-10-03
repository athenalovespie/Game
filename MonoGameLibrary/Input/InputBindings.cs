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
    private Keys inventory = Keys.Tab;
    public Keys Inventory
    {
        get => inventory;
        set
        {
            if (inventory == value) return;
            inventory = value;
            InventoryChanged?.Invoke();
        }
    }

    public event Action InventoryChanged;

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
