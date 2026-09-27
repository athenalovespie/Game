using System;

namespace First_game.Inventory;

public sealed class StorageChest
{
	public StorageChest(
		ItemDefinitionRegistry definitions,
		ItemCategoryBehaviorRegistry behaviors,
		int capacity = Inventory.MaximumCapacity)
	{
		Contents = new Inventory(definitions, behaviors, capacity);
	}

	public Inventory Contents { get; }
}