using System;
using System.Collections.Generic;

namespace First_game.Inventory;

public interface IItemCategoryBehavior
{
	int GetMaxStackSize(ItemDefinition definition);
	bool CanStack(ItemDefinition definition, ItemInstance existing, ItemStackKey incoming);
	IItemUseEffect ResolveDefaultUseEffect(ItemDefinition definition, UseEffectRegistry effects);
}

public class StackableItemBehavior : IItemCategoryBehavior
{
	public virtual int GetMaxStackSize(ItemDefinition definition)
	{
		return definition.MaxStackSize;
	}

	public virtual bool CanStack(ItemDefinition definition, ItemInstance existing, ItemStackKey incoming)
	{
		return string.Equals(existing.QualifiedId, incoming.QualifiedId, StringComparison.Ordinal)
			&& existing.Quality == incoming.Quality
			&& existing.Durability == incoming.Durability;
	}

	public virtual IItemUseEffect ResolveDefaultUseEffect(ItemDefinition definition, UseEffectRegistry effects)
	{
		return null;
	}
}

public sealed class FoodItemBehavior : StackableItemBehavior
{
	public override IItemUseEffect ResolveDefaultUseEffect(ItemDefinition definition, UseEffectRegistry effects)
	{
		return definition.IsEdible ? effects.Resolve("restore_vitals", definition) : null;
	}
}

public class ToolItemBehavior : StackableItemBehavior
{
	public override int GetMaxStackSize(ItemDefinition definition)
	{
		return 1;
	}

	public override bool CanStack(ItemDefinition definition, ItemInstance existing, ItemStackKey incoming)
	{
		return false;
	}

	public override IItemUseEffect ResolveDefaultUseEffect(ItemDefinition definition, UseEffectRegistry effects)
	{
		return definition.IsUsable ? effects.Resolve("tool_action", definition) : null;
	}
}

public sealed class WeaponItemBehavior : ToolItemBehavior
{
	public override IItemUseEffect ResolveDefaultUseEffect(ItemDefinition definition, UseEffectRegistry effects)
	{
		return definition.IsUsable ? effects.Resolve("weapon_action", definition) : null;
	}
}

public sealed class FurnitureItemBehavior : StackableItemBehavior
{
	public override int GetMaxStackSize(ItemDefinition definition) => 1;

	public override IItemUseEffect ResolveDefaultUseEffect(ItemDefinition definition, UseEffectRegistry effects)
	{
		return definition.IsUsable ? effects.Resolve("place_object", definition) : null;
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
		Register(ItemCategory.Furniture, new FurnitureItemBehavior());
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

	public IItemUseEffect ResolveUseEffect(ItemDefinition definition, UseEffectRegistry effects)
	{
		if (definition.UseEffectId != null)
			return effects.Resolve(definition);
		return GetRequired(definition.Category).ResolveDefaultUseEffect(definition, effects);
	}
}