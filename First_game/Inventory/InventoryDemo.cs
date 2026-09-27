using System;

namespace First_game.Inventory;

public static class InventoryDemo
{
	public static Inventory Run()
	{
		ItemDefinitionRegistry definitions = SampleItemCatalog.CreateDefinitions();
		ItemCategoryBehaviorRegistry behaviors = SampleItemCatalog.CreateBehaviors();
		definitions.ResolveUseEffects(UseEffectRegistry.CreateBuiltIns(), behaviors);
		var inventory = new Inventory(definitions, behaviors);

		if (inventory.AddItem("(O)wood", 150) != 150
			|| inventory.AddItem("(C)parsnip", 23) != 23)
			throw new InvalidOperationException("The sample items did not fit in the inventory.");

		inventory.MoveItem(2, 5);
		if (inventory.AddItem("(O)copper_ore") != 1 || !inventory.TryUseItem(2, new DemoUseContext()))
			throw new InvalidOperationException("The copper buff effect did not execute.");
		if (inventory.AddItem("(O)copper_ore") != 1)
			throw new InvalidOperationException("The copper item did not fit after use.");

		string saveJson = inventory.SaveToJson();
		Inventory restored = Inventory.LoadFromJson(saveJson, definitions, behaviors);
		if (restored.GetItemCount("(O)wood") != 150
			|| restored.GetItemCount("(C)parsnip") != 23
			|| restored.GetItemCount("(O)copper_ore") != 1)
			throw new InvalidOperationException("The inventory save/load round trip failed.");
		return restored;
	}

	private sealed class DemoUseContext : IItemUseContext
	{
		public bool RestoreVitals(float health, float stamina) => true;
		public bool ApplyBuff(string buffId, float durationSeconds, float magnitude) =>
			buffId == "mining_luck" && durationSeconds > 0f && magnitude > 0f;
		public bool PlaceWorldObject(string objectId) => true;
		public bool PerformToolAction(string actionId, ItemInstance tool) => true;
	}
}