using System;
using System.Text.Json;

namespace First_game.Inventory;

public sealed class Inventory
{
	public const int DefaultCapacity = 12;
	// The player's artwork has three rows of ten. Keep legacy sizes valid for saves/chests.
	public const int PlayerCapacity = 30;
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
	public event Action Changed;
	public ItemDefinition GetDefinition(string id) => _definitions.GetRequired(id);

	// Exact-instance consumption prevents a stale preview from spending a replacement stack.
	public bool TryConsumeSlot(int slotIndex, ItemInstance expected)
	{
		ValidateSlotIndex(slotIndex);
		if (expected == null || !ReferenceEquals(_slots[slotIndex], expected) || expected.Count < 1)
			return false;
		if (--expected.Count == 0) _slots[slotIndex] = null;
		Changed?.Invoke();
		return true;
	}

	public void RestoreFromJson(string json)
	{
		Inventory restored = LoadFromJson(json, _definitions, _behaviors);
		_slots = restored._slots;
		Changed?.Invoke();
	}

	private void NotifyTransfer(Inventory destination)
	{
		Changed?.Invoke();
		if (!ReferenceEquals(this, destination)) destination.Changed?.Invoke();
	}

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
			if (slot == null || !CanStack(definition, behavior, slot, incoming))
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

		if (count != remaining) Changed?.Invoke();
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

		if (count != remaining) Changed?.Invoke();
		return count - remaining;
	}

	public bool MoveItem(int sourceIndex, int destinationIndex) =>
		MoveItem(sourceIndex, this, destinationIndex);

	// Inventories (including chest contents) share the game's definition and behavior registries.
	public bool MoveItem(int sourceIndex, Inventory destinationInventory, int destinationIndex)
	{
		ValidateSlotIndex(sourceIndex);
		ValidateTransferDestination(destinationInventory, destinationIndex);
		if ((ReferenceEquals(this, destinationInventory) && sourceIndex == destinationIndex)
			|| _slots[sourceIndex] == null)
			return false;

		ItemInstance source = _slots[sourceIndex];
		ItemInstance destination = destinationInventory._slots[destinationIndex];
		if (destination == null)
		{
			destinationInventory._slots[destinationIndex] = source;
			_slots[sourceIndex] = null;
			NotifyTransfer(destinationInventory);
			return true;
		}

		int moved = GetMergeCount(source, destination, source.Count);
		if (moved > 0)
		{
			destination.Count += moved;
			source.Count -= moved;
			if (source.Count == 0)
				_slots[sourceIndex] = null;
			NotifyTransfer(destinationInventory);
			return true;
		}

		// Preserve the existing full-stack swap rule, including a compatible full destination.
		_slots[sourceIndex] = destination;
		destinationInventory._slots[destinationIndex] = source;
		NotifyTransfer(destinationInventory);
		return true;
	}

	public bool SplitStack(int sourceIndex, int destinationIndex, int count) =>
		SplitStack(sourceIndex, this, destinationIndex, count);

	// Commit a split atomically. Compatible stacks accept what fits; leftovers stay
	// at the source. Incompatible or full destinations reject the split without changes.
	public bool SplitStack(int sourceIndex, Inventory destinationInventory, int destinationIndex, int count)
	{
		ValidateSlotIndex(sourceIndex);
		ValidateTransferDestination(destinationInventory, destinationIndex);
		ItemInstance source = _slots[sourceIndex];
		if ((ReferenceEquals(this, destinationInventory) && sourceIndex == destinationIndex)
			|| count < 1 || source == null || count >= source.Count)
			return false;

		ItemInstance destination = destinationInventory._slots[destinationIndex];
		if (destination == null)
		{
			destinationInventory._slots[destinationIndex] = new ItemInstance(
				source.QualifiedId, count, source.Quality, source.Durability);
			source.Count -= count;
			NotifyTransfer(destinationInventory);
			return true;
		}

		int moved = GetMergeCount(source, destination, count);
		if (moved == 0)
			return false;

		destination.Count += moved;
		source.Count -= moved;
		NotifyTransfer(destinationInventory);
		return true;
	}

	// One compatibility path for full moves and split moves; category policies own matching.
	private int GetMergeCount(ItemInstance source, ItemInstance destination, int count)
	{
		ItemDefinition definition = _definitions.GetRequired(source.QualifiedId);
		IItemCategoryBehavior behavior = _behaviors.GetRequired(definition.Category);
		if (!CanStack(definition, behavior, destination,
			new ItemStackKey(source.QualifiedId, source.Quality, source.Durability)))
			return 0;
		return Math.Min(count, Math.Max(0, GetStackLimit(definition, behavior) - destination.Count));
	}

	private void ValidateTransferDestination(Inventory destination, int slotIndex)
	{
		if (destination == null)
			throw new ArgumentNullException(nameof(destination));
		destination.ValidateSlotIndex(slotIndex);
		if (!ReferenceEquals(_definitions, destination._definitions)
			|| !ReferenceEquals(_behaviors, destination._behaviors))
			throw new ArgumentException("Transfers require shared item definitions and category behaviors.", nameof(destination));
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
		Changed?.Invoke();
		return true;
	}

	public bool TryUseItem(int slotIndex, IItemUseContext context)
	{
		ValidateSlotIndex(slotIndex);
		ItemInstance instance = _slots[slotIndex];
		if (instance == null)
			return false;

		ItemDefinition definition = _definitions.GetRequired(instance.QualifiedId);
		if (definition.Placeable != null || !definition.IsUsable || definition.ResolvedUseEffect == null)
			return false;

		ItemUseOutcome outcome = definition.ResolvedUseEffect.Execute(definition, instance, context);
		if (outcome == ItemUseOutcome.Consumed)
		{
			instance.Count--;
			if (instance.Count == 0)
				_slots[slotIndex] = null;
		}
		if (outcome != ItemUseOutcome.Failed) Changed?.Invoke();
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
			else if (CanStack(definition, behavior, slot, incoming))
				available += Math.Max(0, stackLimit - slot.Count);
		}
		return available;
	}

	private static bool CanStack(ItemDefinition definition, IItemCategoryBehavior behavior,
		ItemInstance slot, ItemStackKey incoming) => definition.Placeable != null
		? slot.QualifiedId == incoming.QualifiedId && slot.Quality == incoming.Quality && slot.Durability == incoming.Durability
		: behavior.CanStack(definition, slot, incoming);

	private static int GetStackLimit(ItemDefinition definition, IItemCategoryBehavior behavior)
	{
		int stackLimit = definition.Placeable != null ? definition.MaxStackSize : behavior.GetMaxStackSize(definition);
		if (stackLimit < 1 || stackLimit > definition.MaxStackSize)
			throw new InvalidOperationException($"The category returned an invalid stack limit for '{definition.QualifiedId}'.");
		return stackLimit;
	}

	private static void ValidateCapacity(int capacity)
	{
		if (capacity != PlayerCapacity
			&& (capacity < DefaultCapacity || capacity > MaximumCapacity || capacity % UpgradeIncrement != 0))
			throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be 12, 24, 30, or 36 slots.");
	}

	private void ValidateSlotIndex(int slotIndex)
	{
		if ((uint)slotIndex >= (uint)_slots.Length)
			throw new ArgumentOutOfRangeException(nameof(slotIndex));
	}
}
