using System;

namespace First_game.Inventory;

public sealed class ItemInstance
{
	public ItemInstance(Item item, int quantity = 1)
	{
		Item = item ?? throw new ArgumentNullException(nameof(item));
		if (quantity < 1 || quantity > item.MaxStack)
			throw new ArgumentOutOfRangeException(nameof(quantity));

		Quantity = quantity;
	}

	public Item Item { get; }
	public int Quantity { get; private set; }

	internal int AddQuantity(int quantity)
	{
		int added = Math.Min(quantity, Item.MaxStack - Quantity);
		Quantity += added;
		return quantity - added;
	}
}