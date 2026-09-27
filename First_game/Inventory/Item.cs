using System;

namespace First_game.Inventory;

public enum ItemCategory
{
	Resource,
	Crop,
	Food,
	Tool,
	Weapon,
	Clothing,
	Furniture
}

public sealed class ItemDefinition
{
	public ItemDefinition(
		string qualifiedId,
		string name,
		string description,
		string iconAsset,
		ItemCategory category,
		int maxStackSize,
		int basePrice,
		bool isEdible = false,
		bool isUsable = false,
		string spawnerRuleId = null,
		string useEffectId = null,
		float effectAmount = 0f,
		float effectSecondaryAmount = 0f,
		float effectDurationSeconds = 0f,
		string effectTargetId = null)
	{
		if (string.IsNullOrWhiteSpace(qualifiedId)
			|| qualifiedId[0] != '('
			|| qualifiedId.IndexOf(')') <= 1
			|| qualifiedId.IndexOf(')') == qualifiedId.Length - 1)
			throw new ArgumentException("Use a qualified item ID such as '(O)wood'.", nameof(qualifiedId));
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("An item name is required.", nameof(name));
		if (maxStackSize < 1)
			throw new ArgumentOutOfRangeException(nameof(maxStackSize));
		if (basePrice < 0)
			throw new ArgumentOutOfRangeException(nameof(basePrice));

		QualifiedId = qualifiedId;
		Name = name;
		Description = description ?? string.Empty;
		IconAsset = string.IsNullOrWhiteSpace(iconAsset) ? null : iconAsset;
		Category = category;
		MaxStackSize = maxStackSize;
		BasePrice = basePrice;
		IsEdible = isEdible;
		IsUsable = isUsable;
		SpawnerRuleId = string.IsNullOrWhiteSpace(spawnerRuleId) ? null : spawnerRuleId;
		UseEffectId = string.IsNullOrWhiteSpace(useEffectId) ? null : useEffectId;
		EffectAmount = effectAmount;
		EffectSecondaryAmount = effectSecondaryAmount;
		EffectDurationSeconds = effectDurationSeconds;
		EffectTargetId = string.IsNullOrWhiteSpace(effectTargetId) ? null : effectTargetId;
	}

	public string QualifiedId { get; }
	public string Name { get; }
	public string Description { get; }
	public string IconAsset { get; }
	public ItemCategory Category { get; }
	public int MaxStackSize { get; }
	public int BasePrice { get; }
	public bool IsEdible { get; }
	public bool IsUsable { get; }
	public string SpawnerRuleId { get; }
	public string UseEffectId { get; }
	public float EffectAmount { get; }
	public float EffectSecondaryAmount { get; }
	public float EffectDurationSeconds { get; }
	public string EffectTargetId { get; }
	public IItemUseEffect ResolvedUseEffect { get; private set; }

	internal void BindUseEffect(IItemUseEffect effect)
	{
		ResolvedUseEffect = effect;
	}
}