using System;
using System.Collections.Generic;

namespace First_game.Inventory;

public enum ItemUseOutcome
{
	Failed,
	Used,
	Consumed
}

public interface IItemUseContext
{
	ItemUseOutcome UseFood(ItemDefinition definition, ItemInstance instance);
	ItemUseOutcome UseTool(ItemDefinition definition, ItemInstance instance);
	ItemUseOutcome UseWeapon(ItemDefinition definition, ItemInstance instance);
}

public interface IItemCategoryBehavior
{
	int GetMaxStackSize(ItemDefinition definition);
	bool CanStack(ItemDefinition definition, ItemInstance existing, ItemInstance incoming);
	ItemUseOutcome TryUse(ItemDefinition definition, ItemInstance instance, IItemUseContext context);
}

public class StackableItemBehavior : IItemCategoryBehavior
{
	public virtual int GetMaxStackSize(ItemDefinition definition)
	{
		return definition.MaxStackSize;
	}

	public virtual bool CanStack(ItemDefinition definition, ItemInstance existing, ItemInstance incoming)
	{
		return string.Equals(existing.QualifiedId, incoming.QualifiedId, StringComparison.Ordinal)
			&& existing.Quality == incoming.Quality
			&& existing.Durability == incoming.Durability;
	}

	public virtual ItemUseOutcome TryUse(ItemDefinition definition, ItemInstance instance, IItemUseContext context)
	{
		return ItemUseOutcome.Failed;
	}
}

public sealed class FoodItemBehavior : StackableItemBehavior
{
	public override ItemUseOutcome TryUse(ItemDefinition definition, ItemInstance instance, IItemUseContext context)
	{
		if (!definition.IsEdible || context == null)
			return ItemUseOutcome.Failed;
		return context.UseFood(definition, instance);
	}
}

public sealed class ToolItemBehavior : StackableItemBehavior
{
	public override int GetMaxStackSize(ItemDefinition definition)
	{
		return 1;
	}

	public override bool CanStack(ItemDefinition definition, ItemInstance existing, ItemInstance incoming)
	{
		return false;
	}

	public override ItemUseOutcome TryUse(ItemDefinition definition, ItemInstance instance, IItemUseContext context)
	{
		return context == null ? ItemUseOutcome.Failed : context.UseTool(definition, instance);
	}
}

public sealed class WeaponItemBehavior : StackableItemBehavior
{
	public override int GetMaxStackSize(ItemDefinition definition)
	{
		return 1;
	}

	public override bool CanStack(ItemDefinition definition, ItemInstance existing, ItemInstance incoming)
	{
		return false;
	}

	public override ItemUseOutcome TryUse(ItemDefinition definition, ItemInstance instance, IItemUseContext context)
	{
		return context == null ? ItemUseOutcome.Failed : context.UseWeapon(definition, instance);
	}
}

// Category policies keep stacking and item-use rules out of Inventory.
public sealed class ItemCategoryBehaviorRegistry
{
	private readonly Dictionary<ItemCategory, IItemCategoryBehavior> _behaviors = new();

	public ItemCategoryBehaviorRegistry()
	{
		Register(ItemCategory.Resource, new StackableItemBehavior());
		Register(ItemCategory.Crop, new StackableItemBehavior());
		Register(ItemCategory.Food, new FoodItemBehavior());
		Register(ItemCategory.Tool, new ToolItemBehavior());
		Register(ItemCategory.Weapon, new WeaponItemBehavior());
		Register(ItemCategory.Clothing, new StackableItemBehavior());
		Register(ItemCategory.Furniture, new StackableItemBehavior());
	}

	public void Register(ItemCategory category, IItemCategoryBehavior behavior)
	{
		_behaviors[category] = behavior ?? throw new ArgumentNullException(nameof(behavior));
	}

	public IItemCategoryBehavior GetRequired(ItemCategory category)
	{
		if (!_behaviors.TryGetValue(category, out IItemCategoryBehavior behavior))
			throw new KeyNotFoundException($"No behavior is registered for category '{category}'.");
		return behavior;
	}
}