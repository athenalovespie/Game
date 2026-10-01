using System;
using First_game.Input;
using First_game.Inventory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using PlayerInventory = First_game.Inventory.Inventory;

namespace First_game.UI;

/// <summary>Shows every inventory item; its first row is the live hotbar.</summary>
public sealed class InventoryPanel : IMenuPanel
{
    private readonly PlayerInventory inventory;
    private readonly Hotbar hotbar;
    private readonly Texture2D background;
    private readonly ItemSlotRenderer slots;
    private int moveSource = -1;
    private int hoveredSlot = -1;
    private int page;

    private int PageCount => Math.Max(1, (int)Math.Ceiling(
        (inventory.Capacity - Hotbar.SlotCount) / (float)InventoryLayout.StorageSlotsPerPage));

    public InventoryPanel(PlayerInventory inventory, Hotbar hotbar,
        Texture2D background, ItemSlotRenderer slots)
    {
        this.inventory = inventory;
        this.hotbar = hotbar;
        this.background = background;
        this.slots = slots;
    }

    public void Update(GameTime gameTime, UIInput input, Viewport viewport)
    {
        int pressedSlot = HotbarInput.GetPressedSlot(input);
        if (pressedSlot >= 0)
            hotbar.SelectSlot(pressedSlot);

        // The new player inventory fits on one page. Legacy upgraded inventories
        // can use additional storage pages without changing their hotbar row.
        int previousPage = page;
        if (input.Pressed(Keys.PageDown)) page = Math.Min(PageCount - 1, page + 1);
        if (input.Pressed(Keys.PageUp)) page = Math.Max(0, page - 1);
        if (page != previousPage) moveSource = -1;

        int visibleSlot = InventoryLayout.ForInventory(viewport).HitTest(input.Mouse.ScreenPosition);
        hoveredSlot = visibleSlot < 0 ? -1 : InventoryLayout.GetInventorySlot(visibleSlot, page);
        if (hoveredSlot >= inventory.Capacity) hoveredSlot = -1;

        if (input.Mouse.RightClicked)
        {
            moveSource = -1;
            return;
        }

        if (input.Mouse.LeftClicked)
            ClickSlot(hoveredSlot);
    }

    // Click an item, then its destination. Inventory handles moves, merges, and swaps
    // atomically; no stack is removed while the player is choosing a destination.
    public void ClickSlot(int slot)
    {
        if (slot < 0 || slot >= inventory.Capacity)
        {
            moveSource = -1;
            return;
        }

        if (slot < Hotbar.SlotCount)
            hotbar.SelectSlot(slot);

        if (moveSource >= 0)
        {
            inventory.MoveItem(moveSource, slot);
            moveSource = -1;
        }
        else if (inventory.GetSlot(slot) != null)
            moveSource = slot;
    }

    public void OnClosed()
    {
        moveSource = -1;
        hoveredSlot = -1;
    }

    public void Draw(SpriteBatch batch, Viewport viewport)
    {
        InventoryLayout layout = InventoryLayout.ForInventory(viewport);
        batch.Draw(background, layout.Bounds, Color.White);

        for (int visibleSlot = 0; visibleSlot < InventoryLayout.VisibleSlots; visibleSlot++)
        {
            int inventorySlot = InventoryLayout.GetInventorySlot(visibleSlot, page);
            if (inventorySlot >= inventory.Capacity) continue;
            slots.Draw(batch, layout.GetSlotBounds(visibleSlot), inventory.GetSlot(inventorySlot),
                visibleSlot < Hotbar.SlotCount ? visibleSlot : -1,
                selected: inventorySlot == hotbar.SelectedSlot, moving: inventorySlot == moveSource);
        }

        string hint = moveSource >= 0
            ? "Click a destination to move, stack or swap. Right-click to cancel."
            : "Top row: hotbar (1-9, 0). Click an item, then a destination. E: close.";
        slots.DrawCaption(batch, hint, new Vector2(layout.Bounds.Center.X, layout.Bounds.Bottom + 24),
            layout.Bounds.Width);
        if (PageCount > 1)
            slots.DrawCaption(batch, $"Storage page {page + 1}/{PageCount} - Page Up / Page Down",
                new Vector2(layout.Bounds.Center.X, layout.Bounds.Bottom + 55), layout.Bounds.Width);
        if (hoveredSlot >= 0 && inventory.GetSlot(hoveredSlot) != null)
            slots.DrawCaption(batch, slots.GetItemName(inventory.GetSlot(hoveredSlot)),
                new Vector2(layout.Bounds.Center.X, layout.Bounds.Top - 24), layout.Bounds.Width);
    }
}
