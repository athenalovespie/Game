using System;
using First_game.Inventory;
using Microsoft.Xna.Framework;
using PlayerInventory = First_game.Inventory.Inventory;

namespace First_game.Placement;

/// <summary>One reusable preview state. Selection events own mode changes; Update only moves the ghost.</summary>
public sealed class PlacementController : IDisposable
{
    private readonly PlacementWorld world;
    private readonly Hotbar hotbar;
    private readonly PlayerInventory inventory;
    private ItemInstance expected;
    private long revision = -1;
    private Vector2 lastGround;
    private Rectangle lastBounds;
    private bool dirty = true;
    public PlacementController(PlacementWorld world, Hotbar hotbar, PlayerInventory inventory)
    {
        this.world = world; this.hotbar = hotbar; this.inventory = inventory;
        hotbar.SelectionChanged += Select;
        hotbar.SelectedItemChanged += RefreshItem;
        Select();
    }
    public ItemDefinition Definition { get; private set; }
    public Point OriginTile { get; private set; }
    public int Rotation { get; private set; }
    public bool IsActive => Definition != null;
    public bool IsVisible { get; private set; }
    public bool IsValid { get; private set; }
    public int ValidationCount { get; private set; }
    private void Select()
    {
        expected = hotbar.SelectedItem;
        Definition = hotbar.SelectedDefinition;
        if (Definition?.Placeable == null) Definition = null;
        Rotation = 0; dirty = true; IsVisible = false;
    }
    private void RefreshItem()
    {
        if (ReferenceEquals(expected, hotbar.SelectedItem)) return;
        Select();
    }
    public void Cancel() { Definition = null; IsVisible = false; IsValid = false; }
    public void Rotate()
    {
        if (Definition?.Placeable.Rotatable != true) return;
        Rotation = (Rotation + 1) % 4; dirty = true;
    }
    public void Update(Vector2 mouseWorld, bool blocked)
    {
        IsVisible = IsActive && !blocked;
        if (!IsVisible) return;
        Point origin = Definition.Placeable.OriginFromCursor(world.Grid.WorldToCell(mouseWorld), Rotation);
        if (!dirty && origin == OriginTile && revision == world.Grid.Revision
            && lastGround == world.PlayerGround && lastBounds == world.PlayerBounds) return;
        OriginTile = origin; revision = world.Grid.Revision;
        lastGround = world.PlayerGround; lastBounds = world.PlayerBounds; dirty = false;
        IsValid = world.IsPlacementValid(Definition, OriginTile, Rotation); ValidationCount++;
    }
    public bool TryPrimaryInteract(Vector2 mouseWorld)
    {
        if (!IsActive) return false;
        if (!IsVisible) return true;
        Update(mouseWorld, blocked: false);
        world.TryPlace(Definition, OriginTile, Rotation, inventory, hotbar.SelectedSlot, expected);
        dirty = true;
        if (IsActive) Update(mouseWorld, blocked: false);
        return true; // Invalid previews also own the click.
    }
    public void Dispose()
    {
        hotbar.SelectionChanged -= Select; hotbar.SelectedItemChanged -= RefreshItem;
    }
}
