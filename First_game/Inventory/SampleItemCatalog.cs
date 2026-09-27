namespace First_game.Inventory;

public static class SampleItemCatalog
{
	public static ItemDefinitionRegistry CreateDefinitions()
	{
		var definitions = new ItemDefinitionRegistry();
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
			"(O)copper_ore", "Copper Ore", "Ore from a copper vein.", "Images/wood",
			ItemCategory.Resource, maxStackSize: 99, basePrice: 25, isUsable: true,
			spawnerRuleId: "copper_vein_drop", useEffectId: "apply_buff",
			effectAmount: 1f, effectDurationSeconds: 15f, effectTargetId: "mining_luck"));
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