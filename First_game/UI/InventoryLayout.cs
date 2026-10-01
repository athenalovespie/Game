using System;
using First_game.Inventory;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace First_game.UI;

/// <summary>Artwork coordinates shared by drawing and mouse hit testing.</summary>
public readonly struct InventoryLayout
{
    public const int VisibleSlots = 30;
    public const int StorageSlotsPerPage = VisibleSlots - Hotbar.SlotCount;
    public Rectangle Bounds { get; }
    private readonly bool isHotbar;

    private InventoryLayout(Rectangle bounds, bool isHotbar)
    {
        Bounds = bounds;
        this.isHotbar = isHotbar;
    }

    public static InventoryLayout ForHotbar(Viewport viewport)
    {
        // Limit the bar's height as well as its width, including on ultrawide screens.
        float scale = Math.Min(viewport.Width * 0.78f / 2550f, viewport.Height * 0.14f / 282f);
        int width = Math.Max(1, (int)(2550 * scale));
        int height = Math.Max(1, (int)(282 * scale));
        int margin = Math.Max(8, (int)(viewport.Height * 0.025f));
        return new InventoryLayout(new Rectangle(
            (viewport.Width - width) / 2, viewport.Height - height - margin, width, height), true);
    }

    public static InventoryLayout ForInventory(Viewport viewport)
    {
        float scale = Math.Min(1f, Math.Min(viewport.Width * 0.9f / 2552f, viewport.Height * 0.72f / 883f));
        int width = Math.Max(1, (int)(2552 * scale));
        int height = Math.Max(1, (int)(883 * scale));
        return new InventoryLayout(new Rectangle(
            (viewport.Width - width) / 2, (viewport.Height - height) / 2, width, height), false);
    }

    public Rectangle GetSlotBounds(int visibleSlot)
    {
        int count = isHotbar ? Hotbar.SlotCount : VisibleSlots;
        if ((uint)visibleSlot >= count)
            throw new ArgumentOutOfRangeException(nameof(visibleSlot));

        // Measured against the original PNGs, including their margins and title.
        // Normalize against each image separately: their canvas widths differ by 2 px.
        float sourceWidth = isHotbar ? 2550f : 2552f;
        float sourceHeight = isHotbar ? 282f : 883f;
        float x = 21f + visibleSlot % Hotbar.SlotCount * 252f;
        float y = isHotbar ? 22f : 118f + visibleSlot / Hotbar.SlotCount * 252f;
        return new Rectangle(
            Bounds.X + (int)MathF.Round(x / sourceWidth * Bounds.Width),
            Bounds.Y + (int)MathF.Round(y / sourceHeight * Bounds.Height),
            Math.Max(1, (int)MathF.Round(240f / sourceWidth * Bounds.Width)),
            Math.Max(1, (int)MathF.Round(240f / sourceHeight * Bounds.Height)));
    }

    public int HitTest(Point position)
    {
        int count = isHotbar ? Hotbar.SlotCount : VisibleSlots;
        for (int slot = 0; slot < count; slot++)
            if (GetSlotBounds(slot).Contains(position))
                return slot;
        return -1;
    }

    // Additional pages keep the hotbar row fixed, even for older 36-slot inventories.
    public static int GetInventorySlot(int visibleSlot, int page) =>
        visibleSlot < Hotbar.SlotCount ? visibleSlot : visibleSlot + page * StorageSlotsPerPage;
}
