using System;

namespace First_game.Inventory;

/// <summary>The first ten inventory slots, plus the player's current selection.</summary>
public sealed class Hotbar : IDisposable
{
    public const int SlotCount = 10;
    private readonly Inventory inventory;

    public event Action SelectionChanged;
    public event Action SelectedItemChanged;
    public ItemDefinition SelectedDefinition => SelectedItem == null ? null : inventory.GetDefinition(SelectedItem.QualifiedId);
    private ItemInstance lastItem;
    private int lastCount;
    public int SelectedSlot { get; private set; }

    // Read the live slot so moving, collecting, or consuming items updates both views.
    // An empty slot is a valid selection and returns null.
    public ItemInstance SelectedItem => inventory.GetSlot(SelectedSlot);

    public Hotbar(Inventory inventory)
    {
        this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        if (inventory.Capacity < SlotCount)
            throw new ArgumentException("The hotbar needs ten inventory slots.", nameof(inventory));
        inventory.Changed += OnInventoryChanged;
        RememberItem();
    }

    public void SelectSlot(int slotIndex)
    {
        if ((uint)slotIndex >= SlotCount)
            throw new ArgumentOutOfRangeException(nameof(slotIndex));
        SelectedSlot = slotIndex;
        RememberItem();
        SelectionChanged?.Invoke();
    }

    private void RememberItem()
    {
        lastItem = SelectedItem;
        lastCount = lastItem?.Count ?? 0;
    }

    private void OnInventoryChanged()
    {
        if (ReferenceEquals(lastItem, SelectedItem) && lastCount == (SelectedItem?.Count ?? 0)) return;
        RememberItem();
        SelectedItemChanged?.Invoke();
    }

    public void Dispose() => inventory.Changed -= OnInventoryChanged;

    // Gameplay systems can supply their use context when tools/food actions are ready.
    // Selecting a slot alone never consumes its item.
    public bool TryUseSelectedItem(IItemUseContext context) =>
        inventory.TryUseItem(SelectedSlot, context);
}
