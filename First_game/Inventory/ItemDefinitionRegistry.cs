using System;
using System.Collections.Generic;

namespace First_game.Inventory;

public sealed class ItemDefinitionRegistry
{
	private readonly Dictionary<string, ItemDefinition> _definitions = new(StringComparer.Ordinal);

	public void Register(ItemDefinition definition)
	{
		if (definition == null)
			throw new ArgumentNullException(nameof(definition));
		if (!_definitions.TryAdd(definition.QualifiedId, definition))
			throw new InvalidOperationException($"Item '{definition.QualifiedId}' is already registered.");
	}

	public ItemDefinition GetRequired(string qualifiedId)
	{
		if (!_definitions.TryGetValue(qualifiedId, out ItemDefinition definition))
			throw new KeyNotFoundException($"Item '{qualifiedId}' is not registered.");
		return definition;
	}

	public bool TryGet(string qualifiedId, out ItemDefinition definition)
	{
		return _definitions.TryGetValue(qualifiedId, out definition);
	}
}