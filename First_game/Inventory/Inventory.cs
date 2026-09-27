using System;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace First_game.Inventory;

public sealed class Inventory
{
	public const int DefaultCapacity = 12;
	public const int MaximumCapacity = 36;
	public const int UpgradeIncrement = 12;
	private const int SaveVersion = 1;
	private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
	private readonly ItemDefinitionRegistry _definitions;
	private readonly ItemCategoryBehaviorRegistry _behaviors;
	private ItemInstance[] _slots;

	public Inventory(
		ItemDefinitionRegistry definitions,
		ItemCategoryBehaviorRegistry behaviors,
		int initialCapacity = DefaultCapacity)
	{
		_definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
		_behaviors = behaviors ?? throw new ArgumentNullException(nameof(behaviors));
		ValidateCapacity(initialCapacity);
		_slots = new ItemInstance[initialCapacity];
	}

	public int Capacity => _slots.Length;

	// Adds as many items as fit and returns the number accepted.
	public int AddItem(string qualifiedId, int count = 1, int quality = 0, int? durability = null)
	{
		if (count < 1)
			throw new ArgumentOutOfRangeException(nameof(count));
		ItemDefinition definition = _definitions.GetRequired(qualifiedId);
		var incoming = new ItemStackKey(qualifiedId, quality, durability);
		IItemCategoryBehavior behavior = _behaviors.GetRequired(definition.Category);
		int stackLimit = GetStackLimit(definition, behavior);
		int remaining = count;

		for (int slotIndex = 0; slotIndex < _slots.Length && remaining > 0; slotIndex++)
		{
			ItemInstance slot = _slots[slotIndex];
			if (slot == null || !behavior.CanStack(definition, slot, incoming))
				continue;

			int added = Math.Min(remaining, stackLimit - slot.Count);
			slot.Count += added;
			remaining -= added;
		}

		for (int slotIndex = 0; slotIndex < _slots.Length && remaining > 0; slotIndex++)
		{
			if (_slots[slotIndex] != null)
				continue;

			int added = Math.Min(remaining, stackLimit);
			_slots[slotIndex] = new ItemInstance(qualifiedId, added, quality, durability);
			remaining -= added;
		}

		return count - remaining;
	}

	public int RemoveItem(string qualifiedId, int count, int? quality = null)
	{
		if (count < 1)
			throw new ArgumentOutOfRangeException(nameof(count));
		_definitions.GetRequired(qualifiedId);
		int remaining = count;

		for (int slotIndex = 0; slotIndex < _slots.Length && remaining > 0; slotIndex++)
		{
			ItemInstance slot = _slots[slotIndex];
			if (slot == null
				|| !string.Equals(slot.QualifiedId, qualifiedId, StringComparison.Ordinal)
				|| (quality.HasValue && slot.Quality != quality.Value))
				continue;

			int removed = Math.Min(remaining, slot.Count);
			slot.Count -= removed;
			remaining -= removed;
			if (slot.Count == 0)
				_slots[slotIndex] = null;
		}

		return count - remaining;
	}

	public bool MoveItem(int sourceIndex, int destinationIndex)
	{
		ValidateSlotIndex(sourceIndex);
		ValidateSlotIndex(destinationIndex);
		if (sourceIndex == destinationIndex || _slots[sourceIndex] == null)
			return false;

		ItemInstance source = _slots[sourceIndex];
		ItemInstance destination = _slots[destinationIndex];
		if (destination == null)
		{
			_slots[destinationIndex] = source;
			_slots[sourceIndex] = null;
			return true;
		}

		ItemDefinition definition = _definitions.GetRequired(source.QualifiedId);
		IItemCategoryBehavior behavior = _behaviors.GetRequired(definition.Category);
		if (behavior.CanStack(definition, destination, new ItemStackKey(source.QualifiedId, source.Quality, source.Durability)))
		{
			int space = GetStackLimit(definition, behavior) - destination.Count;
			int moved = Math.Min(space, source.Count);
			if (moved > 0)
			{
				destination.Count += moved;
				source.Count -= moved;
				if (source.Count == 0)
					_slots[sourceIndex] = null;
				return true;
			}
		}

		_slots[sourceIndex] = destination;
		_slots[destinationIndex] = source;
		return true;
	}

	public bool SplitStack(int sourceIndex, int destinationIndex, int count)
	{
		ValidateSlotIndex(sourceIndex);
		ValidateSlotIndex(destinationIndex);
		if (sourceIndex == destinationIndex
			|| count < 1
			|| _slots[sourceIndex] == null
			|| _slots[destinationIndex] != null
			|| count >= _slots[sourceIndex].Count)
			return false;

		ItemInstance source = _slots[sourceIndex];
		source.Count -= count;
		_slots[destinationIndex] = new ItemInstance(
			source.QualifiedId,
			count,
			source.Quality,
			source.Durability);
		return true;
	}

	public int GetItemCount(string qualifiedId, int? quality = null)
	{
		_definitions.GetRequired(qualifiedId);
		int count = 0;
		foreach (ItemInstance slot in _slots)
		{
			if (slot != null
				&& string.Equals(slot.QualifiedId, qualifiedId, StringComparison.Ordinal)
				&& (!quality.HasValue || slot.Quality == quality.Value))
				count += slot.Count;
		}
		return count;
	}

	public bool HasSpaceFor(string qualifiedId, int count = 1, int quality = 0, int? durability = null)
	{
		if (count < 1)
			throw new ArgumentOutOfRangeException(nameof(count));
		ItemDefinition definition = _definitions.GetRequired(qualifiedId);
		var incoming = new ItemStackKey(qualifiedId, quality, durability);
		IItemCategoryBehavior behavior = _behaviors.GetRequired(definition.Category);
		return GetAvailableCapacity(definition, incoming, behavior) >= count;
	}

	public int FindFirstEmptySlot()
	{
		for (int slotIndex = 0; slotIndex < _slots.Length; slotIndex++)
		{
			if (_slots[slotIndex] == null)
				return slotIndex;
		}
		return -1;
	}

	public ItemInstance GetSlot(int slotIndex)
	{
		ValidateSlotIndex(slotIndex);
		return _slots[slotIndex];
	}

	public bool TryUpgrade(int slotsToAdd = UpgradeIncrement)
	{
		if (slotsToAdd != UpgradeIncrement || _slots.Length + slotsToAdd > MaximumCapacity)
			return false;

		Array.Resize(ref _slots, _slots.Length + slotsToAdd);
		return true;
	}

	public bool TryUseItem(int slotIndex, IItemUseContext context)
	{
		ValidateSlotIndex(slotIndex);
		ItemInstance instance = _slots[slotIndex];
		if (instance == null)
			return false;

		ItemDefinition definition = _definitions.GetRequired(instance.QualifiedId);
		if (!definition.IsUsable || definition.ResolvedUseEffect == null)
			return false;

		ItemUseOutcome outcome = definition.ResolvedUseEffect.Execute(definition, instance, context);
		if (outcome == ItemUseOutcome.Consumed)
		{
			instance.Count--;
			if (instance.Count == 0)
				_slots[slotIndex] = null;
		}
		return outcome != ItemUseOutcome.Failed;
	}

	public string SaveToJson()
	{
		var saveData = new InventorySaveData
		{
			Version = SaveVersion,
			Capacity = Capacity,
			Slots = (ItemInstance[])_slots.Clone()
		};
		return JsonSerializer.Serialize(saveData, JsonOptions);
	}

	public static Inventory LoadFromJson(
		string json,
		ItemDefinitionRegistry definitions,
		ItemCategoryBehaviorRegistry behaviors)
	{
		if (string.IsNullOrWhiteSpace(json))
			throw new ArgumentException("Save data is required.", nameof(json));

		InventorySaveData saveData = JsonSerializer.Deserialize<InventorySaveData>(json, JsonOptions);
		if (saveData == null || saveData.Version != SaveVersion)
			throw new InvalidOperationException("Unsupported or invalid inventory save data.");
		ValidateCapacity(saveData.Capacity);
		if (saveData.Slots == null || saveData.Slots.Length != saveData.Capacity)
			throw new InvalidOperationException("Inventory save slot count does not match its capacity.");

		var inventory = new Inventory(definitions, behaviors, saveData.Capacity);
		for (int slotIndex = 0; slotIndex < saveData.Slots.Length; slotIndex++)
		{
			ItemInstance instance = saveData.Slots[slotIndex];
			if (instance == null)
				continue;

			ItemDefinition definition = definitions.GetRequired(instance.QualifiedId);
			int stackLimit = GetStackLimit(definition, behaviors.GetRequired(definition.Category));
			if (instance.Count > stackLimit)
				throw new InvalidOperationException($"Saved stack for '{instance.QualifiedId}' exceeds its maximum size.");
			inventory._slots[slotIndex] = instance;
		}
		return inventory;
	}

	public void Draw(
		SpriteBatch spriteBatch,
		Texture2D pixel,
		SpriteFont font,
		Func<string, Texture2D> loadIcon,
		int viewportWidth,
		int viewportHeight)
	{
		const int columns = 6;
		const int slotSize = 40;
		const int slotGap = 5;
		int rows = (Capacity + columns - 1) / columns;
		int panelWidth = columns * slotSize + (columns - 1) * slotGap + 40;
		int panelHeight = rows * (slotSize + slotGap) + 74;
		int panelX = (viewportWidth - panelWidth) / 2;
		int panelY = (viewportHeight - panelHeight) / 2;

		spriteBatch.Draw(pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.55f);
		spriteBatch.Draw(pixel, new Rectangle(panelX, panelY, panelWidth, panelHeight), new Color(48, 52, 45));
		spriteBatch.Draw(pixel, new Rectangle(panelX, panelY, panelWidth, 3), new Color(179, 167, 125));
		spriteBatch.DrawString(font, "Inventory", new Vector2(panelX + 24, panelY + 18), Color.White);

		int gridX = panelX + 20;
		int gridY = panelY + 54;
		for (int slotIndex = 0; slotIndex < Capacity; slotIndex++)
		{
			int slotX = gridX + slotIndex % columns * (slotSize + slotGap);
			int slotY = gridY + slotIndex / columns * (slotSize + slotGap);
			spriteBatch.Draw(pixel, new Rectangle(slotX, slotY, slotSize, slotSize), new Color(20, 23, 20));
			spriteBatch.Draw(pixel, new Rectangle(slotX + 2, slotY + 2, slotSize - 4, slotSize - 4), new Color(77, 79, 64));

			ItemInstance instance = _slots[slotIndex];
			if (instance == null)
				continue;

			ItemDefinition definition = _definitions.GetRequired(instance.QualifiedId);
			if (definition.IconAsset != null)
			{
				Texture2D icon = loadIcon(definition.IconAsset);
				int iconSize = 26;
				float scale = Math.Min(iconSize / (float)icon.Width, iconSize / (float)icon.Height);
				int iconWidth = (int)(icon.Width * scale);
				int iconHeight = (int)(icon.Height * scale);
				var iconBounds = new Rectangle(
					slotX + (slotSize - iconWidth) / 2,
					slotY + (slotSize - iconHeight) / 2,
					iconWidth,
					iconHeight);
				spriteBatch.Draw(icon, iconBounds, Color.White);
			}
			if (instance.Count > 1)
				spriteBatch.DrawString(font, instance.CountText, new Vector2(slotX + 24, slotY + 22), Color.White);
		}
	}

	private long GetAvailableCapacity(
		ItemDefinition definition,
		ItemStackKey incoming,
		IItemCategoryBehavior behavior)
	{
		int stackLimit = GetStackLimit(definition, behavior);
		long available = 0;
		foreach (ItemInstance slot in _slots)
		{
			if (slot == null)
				available += stackLimit;
			else if (behavior.CanStack(definition, slot, incoming))
				available += Math.Max(0, stackLimit - slot.Count);
		}
		return available;
	}

	private static int GetStackLimit(ItemDefinition definition, IItemCategoryBehavior behavior)
	{
		int stackLimit = behavior.GetMaxStackSize(definition);
		if (stackLimit < 1 || stackLimit > definition.MaxStackSize)
			throw new InvalidOperationException($"The category returned an invalid stack limit for '{definition.QualifiedId}'.");
		return stackLimit;
	}

	private static void ValidateCapacity(int capacity)
	{
		if (capacity < DefaultCapacity || capacity > MaximumCapacity || capacity % UpgradeIncrement != 0)
			throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be 12, 24, or 36 slots.");
	}

	private void ValidateSlotIndex(int slotIndex)
	{
		if ((uint)slotIndex >= (uint)_slots.Length)
			throw new ArgumentOutOfRangeException(nameof(slotIndex));
	}
}