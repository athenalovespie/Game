using System;
using System.Collections.Generic;
using First_game.Inventory;

namespace First_game.World;

public sealed class WorldSpawnRuleRegistry
{
	private readonly Dictionary<string, WorldSpawnRule> _byId = new(StringComparer.Ordinal);
	private readonly Dictionary<string, List<WorldSpawnRule>> _byNodeType = new(StringComparer.Ordinal);
	private readonly ItemDefinitionRegistry _items;

	public WorldSpawnRuleRegistry(ItemDefinitionRegistry items)
	{
		_items = items ?? throw new ArgumentNullException(nameof(items));
	}

	public void Register(WorldSpawnRule rule)
	{
		if (rule == null)
			throw new ArgumentNullException(nameof(rule));
		ItemDefinition item = _items.GetRequired(rule.QualifiedItemId);
		if (!string.Equals(item.SpawnerRuleId, rule.RuleId, StringComparison.Ordinal))
			throw new InvalidOperationException($"Item '{item.QualifiedId}' does not reference spawn rule '{rule.RuleId}'.");
		if (rule.MaximumCount > item.MaxStackSize)
			throw new InvalidOperationException($"Spawn rule '{rule.RuleId}' can exceed its item's stack limit.");
		if (!_byId.TryAdd(rule.RuleId, rule))
			throw new InvalidOperationException($"Spawn rule '{rule.RuleId}' is already registered.");

		if (!_byNodeType.TryGetValue(rule.NodeType, out List<WorldSpawnRule> rules))
			_byNodeType.Add(rule.NodeType, rules = new List<WorldSpawnRule>(1));
		rules.Add(rule);
	}

	public IReadOnlyList<WorldSpawnRule> GetForNode(string nodeType)
	{
		return _byNodeType.TryGetValue(nodeType, out List<WorldSpawnRule> rules)
			? rules
			: Array.Empty<WorldSpawnRule>();
	}

	public WorldSpawnRule GetRequired(string ruleId)
	{
		if (!_byId.TryGetValue(ruleId, out WorldSpawnRule rule))
			throw new KeyNotFoundException($"Spawn rule '{ruleId}' is not registered.");
		return rule;
	}
}