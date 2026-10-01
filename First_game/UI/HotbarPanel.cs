using First_game.Input;
using First_game.Inventory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PlayerInventory = First_game.Inventory.Inventory;

namespace First_game.UI;

/// <summary>The persistent screen-space bar at the bottom of the game.</summary>
public sealed class HotbarPanel
{
    private readonly PlayerInventory inventory;
    private readonly Hotbar hotbar;
    private readonly Texture2D background;
    private readonly ItemSlotRenderer slots;

    public HotbarPanel(PlayerInventory inventory, Hotbar hotbar, Texture2D background, ItemSlotRenderer slots)
    {
        this.inventory = inventory;
        this.hotbar = hotbar;
        this.background = background;
        this.slots = slots;
    }

    // Returns true over the HUD so right-clicks cannot collect objects behind it.
    public bool Update(UIInput input, Viewport viewport)
    {
        int pressedSlot = HotbarInput.GetPressedSlot(input);
        if (pressedSlot >= 0)
            hotbar.SelectSlot(pressedSlot);

        InventoryLayout layout = InventoryLayout.ForHotbar(viewport);
        int clickedSlot = layout.HitTest(input.Mouse.ScreenPosition);
        if (input.Mouse.LeftClicked && clickedSlot >= 0)
            hotbar.SelectSlot(clickedSlot);
        return layout.Bounds.Contains(input.Mouse.ScreenPosition);
    }

    public void Draw(SpriteBatch batch, Viewport viewport)
    {
        InventoryLayout layout = InventoryLayout.ForHotbar(viewport);
        batch.Draw(background, layout.Bounds, Color.White);
        for (int slot = 0; slot < Hotbar.SlotCount; slot++)
            slots.Draw(batch, layout.GetSlotBounds(slot), inventory.GetSlot(slot), slot,
                selected: slot == hotbar.SelectedSlot);

        if (hotbar.SelectedItem != null)
            slots.DrawCaption(batch, slots.GetItemName(hotbar.SelectedItem),
                new Vector2(layout.Bounds.Center.X, layout.Bounds.Top - 20), layout.Bounds.Width);
    }
}
