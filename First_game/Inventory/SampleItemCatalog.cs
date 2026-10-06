namespace First_game.Inventory;

public static class SampleItemCatalog
{
	public static ItemDefinitionRegistry CreateDefinitions()
	{
		var definitions = new ItemDefinitionRegistry();
		definitions.Register(new ItemDefinition(
			First_game.Harvesting.HarvestCatalog.BasicAxeId, "Basic Axe",
			"Select in the hotbar, then left-click a nearby tree.", "Images/Axe_Item",
			ItemCategory.Tool, maxStackSize: 1, basePrice: 50));
		definitions.Register(new ItemDefinition(
			"(O)wood", "Wood", "A piece of building material.", "Images/wood",
			ItemCategory.Resource, maxStackSize: 99, basePrice: 2,
			spawnerRuleId: "wood_pile_drop"));
		definitions.Register(new ItemDefinition(
			"(O)fish", "Fish", "A freshly caught fish.", "Images/Fish",
			ItemCategory.Food, maxStackSize: 99, basePrice: 30, isEdible: true, isUsable: true,
			spawnerRuleId: "fish_spot_drop", useEffectId: "restore_vitals",
			effectAmount: 5f, effectSecondaryAmount: 5f));
		definitions.Register(new ItemDefinition(
			"(O)blackberry", "Blackberry", "A dark, juicy berry.", "Images/Blackberry",
			ItemCategory.Food, maxStackSize: 99, basePrice: 20, isEdible: true, isUsable: true,
			spawnerRuleId: "blackberry_bush_drop", useEffectId: "restore_vitals",
			effectAmount: 3f, effectSecondaryAmount: 5f));
		definitions.Register(new ItemDefinition(
			"(C)parsnip", "Parsnip", "A root vegetable.", null,
			ItemCategory.Crop, maxStackSize: 10, basePrice: 35));
		definitions.Register(new ItemDefinition(
			"(T)iron_pickaxe", "Iron Pickaxe", "A durable mining tool.", null,
			ItemCategory.Tool, maxStackSize: 1, basePrice: 500, isUsable: true,
			useEffectId: "tool_action", effectTargetId: "mine"));
		return definitions;
	}

	public static ItemCategoryBehaviorRegistry CreateBehaviors()
	{
		return new ItemCategoryBehaviorRegistry();
	}
}
