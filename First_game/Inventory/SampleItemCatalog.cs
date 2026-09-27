namespace First_game.Inventory;

public static class SampleItemCatalog
{
	public static ItemDefinitionRegistry CreateDefinitions()
	{
		var definitions = new ItemDefinitionRegistry();
		definitions.Register(new ItemDefinition(
			"(O)wood", "Wood", "A piece of building material.", "Images/wood",
			ItemCategory.Resource, maxStackSize: 99, basePrice: 2));
		definitions.Register(new ItemDefinition(
			"(O)fish", "Fish", "A freshly caught fish.", "Images/Fish",
			ItemCategory.Food, maxStackSize: 99, basePrice: 30, isEdible: true, isUsable: true));
		definitions.Register(new ItemDefinition(
			"(C)parsnip", "Parsnip", "A root vegetable.", null,
			ItemCategory.Crop, maxStackSize: 10, basePrice: 35));
		definitions.Register(new ItemDefinition(
			"(T)iron_pickaxe", "Iron Pickaxe", "A durable mining tool.", null,
			ItemCategory.Tool, maxStackSize: 1, basePrice: 500, isUsable: true));
		return definitions;
	}

	public static ItemCategoryBehaviorRegistry CreateBehaviors()
	{
		return new ItemCategoryBehaviorRegistry();
	}
}