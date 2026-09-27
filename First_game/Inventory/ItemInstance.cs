using System;
using System.Text.Json.Serialization;

namespace First_game.Inventory;

// Runtime state only; static item metadata is resolved from QualifiedId.
public sealed class ItemInstance
{
	[JsonConstructor]
	public ItemInstance(string qualifiedId, int count, int quality = 0, int? durability = null)
	{
		if (string.IsNullOrWhiteSpace(qualifiedId))
			throw new ArgumentException("An item ID is required.", nameof(qualifiedId));
		if (count < 1)
			throw new ArgumentOutOfRangeException(nameof(count));
		if (quality < 0)
			throw new ArgumentOutOfRangeException(nameof(quality));
		if (durability < 0)
			throw new ArgumentOutOfRangeException(nameof(durability));

		QualifiedId = qualifiedId;
		Count = count;
		Quality = quality;
		Durability = durability;
	}

	public string QualifiedId { get; }
	public int Count { get; internal set; }
	public int Quality { get; }
	public int? Durability { get; private set; }

	public void SetDurability(int? durability)
	{
		if (durability < 0)
			throw new ArgumentOutOfRangeException(nameof(durability));

		Durability = durability;
	}
}