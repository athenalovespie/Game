using System;
using First_game.Inventory;
using First_game.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PlayerInventory = First_game.Inventory.Inventory;

namespace First_game.Input;

/// <summary>
/// One reusable gesture state for inventory and chest panels. A drag is a visual
/// preview until mouse-up; Inventory owns the only real stacks and commits the move.
/// </summary>
public sealed class InventoryDragController
{
    private ItemInstance sourceItem;
    private ItemInstance sourcePreview;
    private int originalCount;
    private int? originalDurability;

    public PlayerInventory SourceInventory { get; private set; }
    public int SourceSlot { get; private set; } = -1;
    public ItemInstance DraggedItem { get; private set; }
    public bool IsDragging => SourceInventory != null;
    public bool IsSplit { get; private set; }
    public Point CursorPosition { get; private set; }

    // Call once per frame with the slot hit by the mouse, or null/-1 outside slots.
    // A chest panel can supply chest.Contents as either the source or destination.
    public void Update(UIInput input, PlayerInventory hoveredInventory, int hoveredSlot)
    {
        CursorPosition = input.Mouse.ScreenPosition;
        if (input.Mouse.RightClicked || (IsDragging && !SourceIsCurrent()))
        {
            Cancel();
            return;
        }

        if (input.Mouse.LeftClicked)
            BeginDrag(hoveredInventory, hoveredSlot,
                input.Keyboard.IsKeyDown(Keys.LeftAlt) || input.Keyboard.IsKeyDown(Keys.RightAlt));

        if (input.Mouse.LeftReleased)
            Drop(hoveredInventory, hoveredSlot);
    }

    public bool BeginDrag(PlayerInventory inventory, int slot, bool split)
    {
        if (IsDragging || !IsValidSlot(inventory, slot))
            return false;

        ItemInstance item = inventory.GetSlot(slot);
        // Round halves down; single items cannot be split.
        if (item == null || (split && item.Count < 2))
            return false;

        sourceItem = item;
        originalCount = item.Count;
        originalDurability = item.Durability;
        SourceInventory = inventory;
        SourceSlot = slot;
        IsSplit = split;

        // Preview instances are created once on mouse-down, never during mouse movement.
        // They are never inserted into Inventory, so cancellation needs no rollback.
        DraggedItem = split
            ? new ItemInstance(item.QualifiedId, item.Count / 2, item.Quality, item.Durability)
            : item;
        sourcePreview = split
            ? new ItemInstance(item.QualifiedId, item.Count - DraggedItem.Count, item.Quality, item.Durability)
            : null;
        _ = DraggedItem.CountText;
        if (sourcePreview != null) _ = sourcePreview.CountText;
        return true;
    }

    public bool Drop(PlayerInventory destination, int slot)
    {
        try
        {
            if (!IsDragging || !SourceIsCurrent() || !IsValidSlot(destination, slot)
                || (ReferenceEquals(SourceInventory, destination) && SourceSlot == slot))
                return false;

            return IsSplit
                ? SourceInventory.SplitStack(SourceSlot, destination, slot, DraggedItem.Count)
                : SourceInventory.MoveItem(SourceSlot, destination, slot);
        }
        finally
        {
            // TODO: World-drop behavior can replace invalid-target snap-back here.
            // Clearing the preview restores the source, including rejected split halves.
            Cancel();
        }
    }

    public ItemInstance GetDisplayedItem(PlayerInventory inventory, int slot) =>
        IsDragging && ReferenceEquals(inventory, SourceInventory) && slot == SourceSlot && SourceIsCurrent()
            ? sourcePreview
            : inventory.GetSlot(slot);

    public void Cancel()
    {
        SourceInventory = null;
        SourceSlot = -1;
        sourceItem = null;
        sourcePreview = null;
        DraggedItem = null;
        IsSplit = false;
        originalCount = 0;
        originalDurability = null;
    }

    // If another system changed/replaced the source, discard the stale preview.
    // Never resurrect an old stack or transfer a replacement item.
    private bool SourceIsCurrent() =>
        ReferenceEquals(SourceInventory.GetSlot(SourceSlot), sourceItem)
        && sourceItem.Count == originalCount && sourceItem.Durability == originalDurability;

    private static bool IsValidSlot(PlayerInventory inventory, int slot) =>
        inventory != null && (uint)slot < (uint)inventory.Capacity;
}
