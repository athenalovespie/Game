using System;
using First_game.Input;
using MonoGameLibrary.Input;
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
    private readonly InventoryDragController drag;
    private readonly InputBindings bindings;
    private const string DragHint = "Release over a slot to move. Right-click to cancel.";
    private readonly Vector2 dragHintSize;
    private Vector2 closeHintSize;
    private Vector2 pageCaptionSize;

    public string CloseHint { get; private set; }
    public SpriteFont CaptionFont => slots?.CaptionFont;

    // The door prompt uses the exact footer scale, including narrow-window fitting.
    public float GetHintScale(Viewport viewport) => ItemSlotRenderer.GetCaptionScale(
        closeHintSize, InventoryLayout.ForInventory(viewport).Bounds.Width);
    private int hoveredSlot = -1;
    private int page;
    private string pageCaption;
    private int captionPageCount;

    private int PageCount => Math.Max(1, (int)Math.Ceiling(
        (inventory.Capacity - Hotbar.SlotCount) / (float)InventoryLayout.StorageSlotsPerPage));

    public InventoryPanel(PlayerInventory inventory, Hotbar hotbar,
        Texture2D background, ItemSlotRenderer slots, InventoryDragController drag = null,
        InputBindings bindings = null)
    {
        this.inventory = inventory;
        this.hotbar = hotbar;
        this.background = background;
        this.slots = slots;
        this.drag = drag ?? new InventoryDragController();
        this.bindings = bindings ?? new InputBindings();
        this.bindings.InventoryChanged += RefreshCloseHint;
        dragHintSize = CaptionFont?.MeasureString(DragHint) ?? Vector2.Zero;
        RefreshCloseHint();
        UpdatePageCaption();
    }

    public void Update(GameTime gameTime, UIInput input, Viewport viewport)
    {
        if (captionPageCount != PageCount) UpdatePageCaption();
        int pressedSlot = HotbarInput.GetPressedSlot(input);
        if (pressedSlot >= 0)
            hotbar.SelectSlot(pressedSlot);

        // Legacy upgraded inventories can page their storage rows.
        int previousPage = page;
        if (input.Pressed(Keys.PageDown)) page = Math.Min(PageCount - 1, page + 1);
        if (input.Pressed(Keys.PageUp)) page = Math.Max(0, page - 1);
        bool changedPage = page != previousPage;
        if (changedPage)
        {
            drag.Cancel();
            UpdatePageCaption();
        }

        int visibleSlot = InventoryLayout.ForInventory(viewport).HitTest(input.Mouse.ScreenPosition);
        hoveredSlot = visibleSlot < 0 ? -1 : InventoryLayout.GetInventorySlot(visibleSlot, page);
        if (hoveredSlot >= inventory.Capacity) hoveredSlot = -1;

        if (!changedPage)
        {
            if ((input.Mouse.LeftClicked || (input.Mouse.LeftReleased && drag.IsDragging))
                && hoveredSlot >= 0 && hoveredSlot < Hotbar.SlotCount)
                hotbar.SelectSlot(hoveredSlot);
            drag.Update(input, inventory, hoveredSlot);
        }
    }

    public void OnClosed()
    {
        drag.Cancel();
        // hoveredSlot = -1;
    }

    public void Draw(SpriteBatch batch, Viewport viewport)
    {
        InventoryLayout layout = InventoryLayout.ForInventory(viewport);
        batch.Draw(background, layout.Bounds, Color.White);

        for (int visibleSlot = 0; visibleSlot < InventoryLayout.VisibleSlots; visibleSlot++)
        {
            int inventorySlot = InventoryLayout.GetInventorySlot(visibleSlot, page);
            if (inventorySlot >= inventory.Capacity) continue;
            slots.Draw(batch, layout.GetSlotBounds(visibleSlot), drag.GetDisplayedItem(inventory, inventorySlot),
                visibleSlot < Hotbar.SlotCount ? visibleSlot : -1);
        }

        string hint = drag.IsDragging ? DragHint : CloseHint;
        Vector2 hintSize = drag.IsDragging ? dragHintSize : closeHintSize;
        slots.DrawCaption(batch, hint, hintSize, new Vector2(layout.Bounds.Center.X, layout.Bounds.Bottom + 24),
            layout.Bounds.Width);
        if (PageCount > 1)
            slots.DrawCaption(batch, pageCaption, pageCaptionSize,
                new Vector2(layout.Bounds.Center.X, layout.Bounds.Bottom + 55), layout.Bounds.Width);
        if (hoveredSlot >= 0 && inventory.GetSlot(hoveredSlot) != null)
            slots.DrawCaption(batch, slots.GetItemName(inventory.GetSlot(hoveredSlot)),
                new Vector2(layout.Bounds.Center.X, layout.Bounds.Top - 24), layout.Bounds.Width);

        // Draw last in the screen-space batch so the cursor stack is above the entire panel.
        if (drag.IsDragging)
        {
            Rectangle size = layout.GetSlotBounds(0);
            var cursorBounds = new Rectangle(drag.CursorPosition.X - size.Width / 2,
                drag.CursorPosition.Y - size.Height / 2, size.Width, size.Height);
            slots.Draw(batch, cursorBounds, drag.DraggedItem);
        }
    }

    private void RefreshCloseHint()
    {
        CloseHint = "Drag items to move. Alt+drag: split half. " + bindings.Inventory + ": close.";
        closeHintSize = CaptionFont?.MeasureString(CloseHint) ?? Vector2.Zero;
    }

    private void UpdatePageCaption()
    {
        captionPageCount = PageCount;
        pageCaption = $"Storage page {page + 1}/{captionPageCount} - Page Up / Page Down";
        pageCaptionSize = CaptionFont?.MeasureString(pageCaption) ?? Vector2.Zero;
    }
}
