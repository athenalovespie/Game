using System;

namespace First_game.Inventory;

public static class InventoryDemo
{
	public static Inventory Run()
	{
		ItemDefinitionRegistry definitions = SampleItemCatalog.CreateDefinitions();
		ItemCategoryBehaviorRegistry behaviors = SampleItemCatalog.CreateBehaviors();
		var inventory = new Inventory(definitions, behaviors);

		if (inventory.AddItem("(O)wood", 150) != 150
			|| inventory.AddItem("(C)parsnip", 23) != 23)
			throw new InvalidOperationException("The sample items did not fit in the inventory.");

		inventory.MoveItem(2, 5);
		string saveJson = inventory.SaveToJson();
		return Inventory.LoadFromJson(saveJson, definitions, behaviors);
	}
}